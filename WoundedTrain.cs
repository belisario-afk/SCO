using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("WoundedTrain", "Jess", "2.1.0")]
    [Description("Creates a human train that you can sit on and steer.")]
    public class WoundedTrain : RustPlugin
    {
        #region Fields
        
        // --- Permissions ---
        private const string PermUse = "woundedtrain.use";
        private const string PermFinale = "woundedtrain.finale";
        private const string PermAdmin = "woundedtrain.admin";
        
        // --- Configuration ---
        private Configuration config;
        
        // --- Data ---
        private Dictionary<ulong, TrainData> activeTrains = new Dictionary<ulong, TrainData>();
        
        // --- Prefabs ---
        private const string ScientistPrefab = "assets/prefabs/npc/scientist/scientist.prefab";
        private const string GhostPrefab = "assets/prefabs/visualization/sphere.prefab"; 
        private const string ChairPrefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";
        
        #endregion
        
        #region Configuration
        
        private class Configuration
        {
            public int TrainLength { get; set; } = 30;
            public int PullerCount { get; set; } = 2;
            public float MoveSpeed { get; set; } = 30f;
            public float ReverseSpeed { get; set; } = 15f;
            public float TurnSpeed { get; set; } = 15f;
            public float TurnMultiplier { get; set; } = 5f;
            public float UpdateInterval { get; set; } = 0.1f;
            public float NPCSpacing { get; set; } = 1.5f;
            public float GroundOffset { get; set; } = 0.5f;
            public float RaycastHeight { get; set; } = 50f;
            public float RaycastDistance { get; set; } = 100f;
            public float FinaleExplosionForce { get; set; } = 800f;
            public float FinaleSpreadForce { get; set; } = 100f;
            public int CommandCooldown { get; set; } = 5;
            public float GestureInterval { get; set; } = 10f;
            public bool AllowReverse { get; set; } = true;
            public bool AutoCleanupOnDismount { get; set; } = true;
            public Messages Messages { get; set; } = new Messages();
        }
        
        private class Messages
        {
            public string NoPermission { get; set; } = "You don't have permission to use this command.";
            public string TrainReady { get; set; } = "<color=#ff0000><b>[Human Train]</b></color> The Train is ready! Mount the chair to steer!";
            public string TrainCleaned { get; set; } = "<color=#00ff00>[Human Train]</color> Train has been cleaned up.";
            public string FinaleActivated { get; set; } = "<color=orange>GRAND FINALE!</color>";
            public string NoActiveTrain { get; set; } = "You don't have an active train.";
            public string CommandCooldown { get; set; } = "Please wait {0} seconds before using this command again.";
            public string TrainCreationFailed { get; set; } = "<color=red>Failed to create train. Check server logs.</color>";
        }
        
        private class TrainData
        {
            public BasePlayer Owner { get; set; }
            public List<BasePlayer> NPCs { get; set; } = new List<BasePlayer>();
            public List<BasePlayer> PullerNPCs { get; set; } = new List<BasePlayer>();
            public BaseEntity GhostEngine { get; set; }
            public BaseMountable CaptainChair { get; set; }
            public Timer ControlTimer { get; set; }
            public Timer GestureTimer { get; set; }
            public bool IsMovingForward { get; set; }
            public bool IsMovingBackward { get; set; }
            public bool IsTurningLeft { get; set; }
            public bool IsTurningRight { get; set; }
            public float LastCommandTime { get; set; }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LogWarning($"Error reading config, using default values");
                LoadDefaultConfig();
            }
            SaveConfig();
        }
        
        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }
        
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        
        #endregion
        
        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermFinale, this);
            permission.RegisterPermission(PermAdmin, this);
            
            // Register commands
            AddCovalenceCommand("humantrain", nameof(CmdHumanTrain));
            AddCovalenceCommand("finale", nameof(CmdFinale));
            AddCovalenceCommand("cleantrain", nameof(CmdCleanTrain));
        }

        private void Unload()
        {
            // Cleanup all active trains
            foreach (var train in activeTrains.Values.ToList())
            {
                CleanupTrain(train);
            }
            activeTrains.Clear();
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            // Check if entity is part of any train
            foreach (var train in activeTrains.Values)
            {
                if (entity is BasePlayer player && (train.NPCs.Contains(player) || train.PullerNPCs.Contains(player)))
                    return true;
                if (entity == train.CaptainChair)
                    return true;
                if (entity == train.GhostEngine)
                    return true;
            }
            return null;
        }

        object OnWoundedRecover(BasePlayer player)
        {
            // Prevent NPCs from recovering
            foreach (var train in activeTrains.Values)
            {
                if (train.NPCs.Contains(player) || train.PullerNPCs.Contains(player))
                    return true;
            }
            return null;
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null)
                return;
            
            // Find train where this player is the captain
            TrainData train = null;
            foreach (var t in activeTrains.Values)
            {
                if (t.CaptainChair != null && player.GetMounted() == t.CaptainChair)
                {
                    train = t;
                    break;
                }
            }
            
            if (train == null)
                return;

            train.IsMovingForward = input.IsDown(BUTTON.FORWARD);
            train.IsMovingBackward = config.AllowReverse && input.IsDown(BUTTON.BACKWARD);
            train.IsTurningLeft = input.IsDown(BUTTON.LEFT);
            train.IsTurningRight = input.IsDown(BUTTON.RIGHT);
        }
        
        void OnPlayerDismounted(BasePlayer player, BaseMountable entity)
        {
            if (!config.AutoCleanupOnDismount || player == null)
                return;
            
            // Check if player dismounted from a train chair
            TrainData train = null;
            foreach (var t in activeTrains.Values)
            {
                if (t.CaptainChair == entity)
                {
                    train = t;
                    break;
                }
            }
            
            if (train != null)
            {
                SendReply(player, config.Messages.TrainCleaned);
                timer.Once(0.5f, () => CleanupTrain(train));
            }
        }
        
        #endregion
        
        #region Commands

        [ChatCommand("humantrain")]
        private void CmdHumanTrain(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PermUse))
            {
                SendReply(player, config.Messages.NoPermission);
                return;
            }
            
            // Check cooldown
            if (activeTrains.ContainsKey(player.userID))
            {
                var train = activeTrains[player.userID];
                float timeSinceLastCommand = Time.realtimeSinceStartup - train.LastCommandTime;
                if (timeSinceLastCommand < config.CommandCooldown)
                {
                    int remainingTime = Mathf.CeilToInt(config.CommandCooldown - timeSinceLastCommand);
                    SendReply(player, string.Format(config.Messages.CommandCooldown, remainingTime));
                    return;
                }
            }

            // Cleanup existing train if any
            if (activeTrains.ContainsKey(player.userID))
            {
                CleanupTrain(activeTrains[player.userID]);
            }
            
            Puts($"Player {player.displayName} is creating a Human Train...");

            if (!CreateTrain(player))
            {
                SendReply(player, config.Messages.TrainCreationFailed);
                return;
            }

            SendReply(player, config.Messages.TrainReady);
        }

        [ChatCommand("finale")]
        private void CmdFinale(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PermFinale))
            {
                SendReply(player, config.Messages.NoPermission);
                return;
            }
            
            if (!activeTrains.ContainsKey(player.userID))
            {
                SendReply(player, config.Messages.NoActiveTrain);
                return;
            }

            var train = activeTrains[player.userID];
            ExecuteFinale(train);
            SendReply(player, config.Messages.FinaleActivated);
        }
        
        [ChatCommand("cleantrain")]
        private void CmdCleanTrain(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PermUse))
            {
                SendReply(player, config.Messages.NoPermission);
                return;
            }
            
            if (!activeTrains.ContainsKey(player.userID))
            {
                SendReply(player, config.Messages.NoActiveTrain);
                return;
            }

            CleanupTrain(activeTrains[player.userID]);
            SendReply(player, config.Messages.TrainCleaned);
        }
        
        #endregion
        
        #region Helper Methods
        
        private bool HasPermission(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }
        
        private bool CreateTrain(BasePlayer player)
        {
            var train = new TrainData
            {
                Owner = player,
                LastCommandTime = Time.realtimeSinceStartup
            };

            // 1. Spawn the Ghost Engine
            Vector3 spawnPos = player.transform.position + (player.transform.forward * 3f);
            spawnPos.y = GetGroundY(spawnPos) + config.GroundOffset;
            
            train.GhostEngine = GameManager.server.CreateEntity(GhostPrefab, spawnPos);
            if (train.GhostEngine == null)
            {
                LogError("Could not spawn Ghost Engine!");
                return false;
            }
            train.GhostEngine.Spawn();
            Puts("Ghost Engine spawned successfully.");

            // 2. Spawn the Puller NPCs (wounded, in front of the chair)
            for (int i = 0; i < config.PullerCount; i++)
            {
                Vector3 offset = new Vector3(0, 0, 2f + (i * config.NPCSpacing));
                SpawnPullerNPC(train, offset);
            }

            // 3. Spawn the Captain's Chair (in the middle)
            if (!SpawnChair(train))
            {
                CleanupTrain(train);
                return false;
            }

            // 4. Spawn the Sitting NPCs behind the chair
            for (int i = 0; i < config.TrainLength; i++)
            {
                Vector3 offset = new Vector3(0, 0, -2f - (i * config.NPCSpacing)); 
                SpawnSittingNPC(train, offset);
            }

            // 5. Start Physics Loop
            train.ControlTimer = timer.Repeat(config.UpdateInterval, -1, () =>
            {
                UpdateTrainPhysics(train);
            });
            
            // 6. Start Gesture Loop
            train.GestureTimer = timer.Repeat(config.GestureInterval, -1, () =>
            {
                PerformRandomGestures(train);
            });
            
            activeTrains[player.userID] = train;
            return true;
        }
        
        private void UpdateTrainPhysics(TrainData train)
        {
            if (train.GhostEngine == null || train.GhostEngine.IsDestroyed)
            {
                CleanupTrain(train);
                return;
            }

            // Handle forward/backward movement
            if (train.IsMovingForward)
            {
                Vector3 currentPos = train.GhostEngine.transform.position;
                Vector3 moveDir = train.GhostEngine.transform.forward * config.MoveSpeed * config.UpdateInterval;
                Vector3 newPos = currentPos + moveDir;
                newPos.y = GetGroundY(newPos) + config.GroundOffset;
                train.GhostEngine.transform.position = newPos;
                train.GhostEngine.SendNetworkUpdate();
            }
            else if (train.IsMovingBackward)
            {
                Vector3 currentPos = train.GhostEngine.transform.position;
                Vector3 moveDir = train.GhostEngine.transform.forward * config.ReverseSpeed * config.UpdateInterval;
                Vector3 newPos = currentPos - moveDir;
                newPos.y = GetGroundY(newPos) + config.GroundOffset;
                train.GhostEngine.transform.position = newPos;
                train.GhostEngine.SendNetworkUpdate();
            }

            // Handle turning
            if (train.IsTurningLeft)
            {
                train.GhostEngine.transform.Rotate(Vector3.up, -config.TurnSpeed * config.TurnMultiplier * config.UpdateInterval);
                train.GhostEngine.SendNetworkUpdate();
            }
            else if (train.IsTurningRight)
            {
                train.GhostEngine.transform.Rotate(Vector3.up, config.TurnSpeed * config.TurnMultiplier * config.UpdateInterval);
                train.GhostEngine.SendNetworkUpdate();
            }
        }
        
        private void ExecuteFinale(TrainData train)
        {
            if (train.ControlTimer != null)
            {
                train.ControlTimer.Destroy();
                train.ControlTimer = null;
            }
            
            if (train.GestureTimer != null)
            {
                train.GestureTimer.Destroy();
                train.GestureTimer = null;
            }
            
            if (train.GhostEngine != null && !train.GhostEngine.IsDestroyed)
            {
                train.GhostEngine.Kill();
                train.GhostEngine = null;
            }

            // Launch sitting NPCs
            foreach (var npc in train.NPCs)
            {
                if (npc == null || npc.IsDestroyed)
                    continue;

                npc.SetParent(null, true, true);

                Rigidbody rb = npc.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false; 
                    rb.useGravity = true;
                    Vector3 randomSpread = UnityEngine.Random.onUnitSphere * config.FinaleSpreadForce;
                    rb.AddForce(Vector3.up * config.FinaleExplosionForce + randomSpread, ForceMode.Impulse); 
                }
                
                Effect.server.Run("assets/bundled/prefabs/fx/gestures/guitarpluck.prefab", npc.transform.position);
            }
            
            // Launch puller NPCs
            foreach (var npc in train.PullerNPCs)
            {
                if (npc == null || npc.IsDestroyed)
                    continue;

                npc.SetParent(null, true, true);

                Rigidbody rb = npc.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false; 
                    rb.useGravity = true;
                    Vector3 randomSpread = UnityEngine.Random.onUnitSphere * config.FinaleSpreadForce;
                    rb.AddForce(Vector3.up * config.FinaleExplosionForce + randomSpread, ForceMode.Impulse); 
                }
                
                Effect.server.Run("assets/bundled/prefabs/fx/gestures/guitarpluck.prefab", npc.transform.position);
            }

            timer.Once(5f, () => CleanupTrain(train));
        }

        private bool SpawnChair(TrainData train)
        {
            train.CaptainChair = GameManager.server.CreateEntity(ChairPrefab, train.GhostEngine.transform.position) as BaseMountable;
            if (train.CaptainChair == null)
            {
                LogError("Could not spawn Captain's Chair!");
                return false;
            }
            
            train.CaptainChair.SetParent(train.GhostEngine);
            train.CaptainChair.transform.localPosition = new Vector3(0, 0, 0); 
            train.CaptainChair.Spawn();
            Puts("Captain's Chair spawned successfully.");
            return true;
        }

        private void SpawnNPC(TrainData train, Vector3 localPos)
        {
            var npc = GameManager.server.CreateEntity(ScientistPrefab, train.GhostEngine.transform.position) as BasePlayer;
            if (npc == null)
            {
                LogWarning($"Could not spawn NPC! Check prefab path: {ScientistPrefab}");
                return;
            }

            npc.Spawn();
            
            // Force wounded state
            npc.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, true);
            npc.health = 5f; 
            
            npc.SetParent(train.GhostEngine);
            npc.transform.localPosition = localPos;
            npc.transform.localRotation = Quaternion.Euler(0, 0, 0); 
            
            train.NPCs.Add(npc);
        }
        
        private void SpawnPullerNPC(TrainData train, Vector3 localPos)
        {
            var npc = GameManager.server.CreateEntity(ScientistPrefab, train.GhostEngine.transform.position) as BasePlayer;
            if (npc == null)
            {
                LogWarning($"Could not spawn Puller NPC! Check prefab path: {ScientistPrefab}");
                return;
            }

            npc.Spawn();
            
            // Force wounded state (pulling the sled)
            npc.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, true);
            npc.health = 5f; 
            
            npc.SetParent(train.GhostEngine);
            npc.transform.localPosition = localPos;
            npc.transform.localRotation = Quaternion.Euler(0, 180, 0); // Face forward
            
            train.PullerNPCs.Add(npc);
        }
        
        private void SpawnSittingNPC(TrainData train, Vector3 localPos)
        {
            var npc = GameManager.server.CreateEntity(ScientistPrefab, train.GhostEngine.transform.position) as BasePlayer;
            if (npc == null)
            {
                LogWarning($"Could not spawn Sitting NPC! Check prefab path: {ScientistPrefab}");
                return;
            }

            npc.Spawn();
            
            // Make them sit - set Relaxed flag
            npc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
            npc.health = 100f;
            
            npc.SetParent(train.GhostEngine);
            npc.transform.localPosition = localPos;
            npc.transform.localRotation = Quaternion.Euler(0, 0, 0);
            
            train.NPCs.Add(npc);
            
            // Trigger initial sitting gesture
            timer.Once(0.5f, () =>
            {
                if (npc != null && !npc.IsDestroyed)
                {
                    npc.Server_StartGesture(GestureCollection.StringToGestureId("wave"));
                }
            });
        }
        
        private void PerformRandomGestures(TrainData train)
        {
            if (train == null || train.NPCs == null)
                return;
            
            // List of gesture IDs available in Rust
            uint[] gestures = new uint[]
            {
                GestureCollection.StringToGestureId("wave"),
                GestureCollection.StringToGestureId("shrug"),
                GestureCollection.StringToGestureId("victory"),
                GestureCollection.StringToGestureId("thumbsup"),
                GestureCollection.StringToGestureId("chicken"),
                GestureCollection.StringToGestureId("hurry"),
                GestureCollection.StringToGestureId("whoa")
            };
            
            foreach (var npc in train.NPCs)
            {
                if (npc == null || npc.IsDestroyed)
                    continue;
                
                // Random chance to perform gesture
                if (UnityEngine.Random.Range(0f, 1f) > 0.3f)
                {
                    uint randomGesture = gestures[UnityEngine.Random.Range(0, gestures.Length)];
                    npc.Server_StartGesture(randomGesture);
                }
            }
        }

        private float GetGroundY(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + config.RaycastHeight, pos.z), Vector3.down, out hit, 
                config.RaycastDistance, LayerMask.GetMask("Terrain", "World", "Construction")))
            {
                return hit.point.y;
            }
            return pos.y; 
        }

        private void CleanupTrain(TrainData train)
        {
            if (train == null)
                return;
            
            if (train.ControlTimer != null)
            {
                train.ControlTimer.Destroy();
                train.ControlTimer = null;
            }
            
            if (train.GestureTimer != null)
            {
                train.GestureTimer.Destroy();
                train.GestureTimer = null;
            }

            foreach (var npc in train.NPCs)
            {
                if (npc != null && !npc.IsDestroyed)
                    npc.Kill();
            }
            train.NPCs.Clear();
            
            foreach (var npc in train.PullerNPCs)
            {
                if (npc != null && !npc.IsDestroyed)
                    npc.Kill();
            }
            train.PullerNPCs.Clear();

            if (train.CaptainChair != null && !train.CaptainChair.IsDestroyed)
            {
                train.CaptainChair.Kill();
                train.CaptainChair = null;
            }

            if (train.GhostEngine != null && !train.GhostEngine.IsDestroyed)
            {
                train.GhostEngine.Kill();
                train.GhostEngine = null;
            }
            
            if (train.Owner != null && activeTrains.ContainsKey(train.Owner.userID))
            {
                activeTrains.Remove(train.Owner.userID);
            }
        }
        
        private void LogError(string message)
        {
            Puts($"ERROR: {message}");
        }
        
        private void LogWarning(string message)
        {
            Puts($"WARNING: {message}");
        }
        
        #endregion
    }
}