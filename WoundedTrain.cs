using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("WoundedTrain", "Jess", "1.2.0")]
    [Description("Creates a human train that you can sit on and steer.")]
    public class WoundedTrain : RustPlugin
    {
        // --- Permissions & Config ---
        private const string PermUse = "woundedtrain.use";
        private const int TrainLength = 30; 
        private const float MoveSpeed = 30f; 
        private const float TurnSpeed = 15f; 
        
        // --- Data ---
        private List<BasePlayer> activeTrainNPCs = new List<BasePlayer>();
        private BaseEntity ghostEngine = null;
        private BaseMountable captainChair = null;
        private Timer controlTimer;
        
        // Input tracking
        private bool isMovingForward = false;
        private bool isTurningLeft = false;
        private bool isTurningRight = false;
        
        // --- Prefabs ---
        // UPDATED: Using the path you suggested
        private const string ScientistPrefab = "assets/content/vehicles/horse/horse.corpse.prefab";
        private const string GhostPrefab = "assets/prefabs/visualization/sphere.prefab"; 
        private const string ChairPrefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";

        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
        }

        private void Unload()
        {
            CleanupTrain();
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer player && activeTrainNPCs.Contains(player)) return true;
            if (entity == captainChair) return true; 
            return null;
        }

        object OnWoundedRecover(BasePlayer player)
        {
            if (activeTrainNPCs.Contains(player)) return true; 
            return null;
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (captainChair == null || !player.GetMounted() || player.GetMounted() != captainChair)
                return;

            isMovingForward = input.IsDown(BUTTON.FORWARD);
            isTurningLeft = input.IsDown(BUTTON.LEFT);
            isTurningRight = input.IsDown(BUTTON.RIGHT);
        }

        [ChatCommand("humantrain")]
        private void CmdHumanTrain(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUse))
            {
                SendReply(player, "No permission.");
                return;
            }

            CleanupTrain();
            Puts("Starting Human Train creation..."); // Debug Log

            // 1. Spawn the "Ghost Engine"
            Vector3 spawnPos = player.transform.position + (player.transform.forward * 3f);
            spawnPos.y = GetGroundY(spawnPos) + 0.5f; // Lift it slightly so it doesn't clip underground
            
            ghostEngine = GameManager.server.CreateEntity(GhostPrefab, spawnPos);
            if (ghostEngine == null)
            {
                Puts("ERROR: Could not spawn Ghost Engine!");
                return;
            }
            ghostEngine.Spawn();
            Puts("Ghost Engine Spawned.");

            // 2. Spawn the Captain's Chair
            SpawnChair(ghostEngine);

            // 3. Spawn the Line of Crawlers
            for (int i = 0; i < TrainLength; i++)
            {
                Vector3 offset = new Vector3(0, 0, -2f - (i * 1.5f)); 
                SpawnCrawler(ghostEngine, offset);
            }

            SendReply(player, "<color=#ff0000><b>[Killa Dome]</b></color> The Train is ready! Mount the chair to steer!");

            // 4. Start Physics Loop
            controlTimer = timer.Repeat(0.1f, -1, () =>
            {
                if (ghostEngine == null || ghostEngine.IsDestroyed) 
                {
                    CleanupTrain();
                    return;
                }

                if (isMovingForward)
                {
                    Vector3 currentPos = ghostEngine.transform.position;
                    Vector3 moveDir = ghostEngine.transform.forward * MoveSpeed * 0.1f;
                    Vector3 newPos = currentPos + moveDir;
                    newPos.y = GetGroundY(newPos) + 0.5f; // Keep it above ground
                    ghostEngine.transform.position = newPos;
                    ghostEngine.SendNetworkUpdate();
                }

                if (isTurningLeft)
                {
                    ghostEngine.transform.Rotate(Vector3.up, -TurnSpeed * 5f);
                    ghostEngine.SendNetworkUpdate();
                }
                else if (isTurningRight)
                {
                    ghostEngine.transform.Rotate(Vector3.up, TurnSpeed * 5f);
                    ghostEngine.SendNetworkUpdate();
                }
            });
        }

        [ChatCommand("finale")]
        private void CmdFinale(BasePlayer player, string command, string[] args)
        {
            if (activeTrainNPCs.Count == 0) return;

            if (controlTimer != null) controlTimer.Destroy();
            if (ghostEngine != null) ghostEngine.Kill(); 

            foreach (var npc in activeTrainNPCs)
            {
                if (npc == null) continue;

                npc.SetParent(null, true, true);

                Rigidbody rb = npc.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false; 
                    rb.useGravity = true;
                    Vector3 randomSpread = UnityEngine.Random.onUnitSphere * 100f;
                    rb.AddForce(Vector3.up * 800f + randomSpread, ForceMode.Impulse); 
                }
                Effect.server.Run("assets/bundled/prefabs/fx/gestures/guitarpluck.prefab", npc.transform.position);
            }

            SendReply(player, "<color=orange>GRAND FINALE!</color>");
            timer.Once(5f, () => CleanupTrain());
        }

        private void SpawnChair(BaseEntity parent)
        {
            captainChair = GameManager.server.CreateEntity(ChairPrefab, parent.transform.position) as BaseMountable;
            if (captainChair == null)
            {
                Puts("ERROR: Could not spawn Chair!");
                return;
            }
            
            captainChair.SetParent(parent);
            captainChair.transform.localPosition = new Vector3(0, 0, 0); 
            captainChair.Spawn();
            Puts("Chair Spawned.");
        }

        private void SpawnCrawler(BaseEntity parent, Vector3 localPos)
        {
            var npc = GameManager.server.CreateEntity(ScientistPrefab, parent.transform.position) as BasePlayer;
            if (npc == null)
            {
                Puts($"ERROR: Could not spawn Scientist! Check Prefab Path: {ScientistPrefab}");
                return;
            }

            npc.Spawn();
            
            // Force Wounded
            npc.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, true);
            npc.health = 5f; 
            
            npc.SetParent(parent);
            npc.transform.localPosition = localPos;
            npc.transform.localRotation = Quaternion.Euler(0, 0, 0); 
            
            activeTrainNPCs.Add(npc);
        }

        private float GetGroundY(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 50f, pos.z), Vector3.down, out hit, 100f, LayerMask.GetMask("Terrain", "World", "Construction")))
            {
                return hit.point.y;
            }
            return pos.y; 
        }

        private void CleanupTrain()
        {
            if (controlTimer != null) controlTimer.Destroy();

            foreach (var npc in activeTrainNPCs)
            {
                if (npc != null && !npc.IsDestroyed) npc.Kill();
            }
            activeTrainNPCs.Clear();

            if (ghostEngine != null && !ghostEngine.IsDestroyed)
            {
                ghostEngine.Kill();
            }
        }
    }
}