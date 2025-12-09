using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("WoundedRide", "Jess", "3.0.0")]
    [Description("Winners ride wounded losers for 30 seconds - triggered by external plugins")]
    public class WoundedRide : RustPlugin
    {
        #region Fields
        
        private Configuration config;
        private Dictionary<string, RideSession> activeSessions = new Dictionary<string, RideSession>();
        
        #endregion
        
        #region Configuration
        
        private class Configuration
        {
            public float RideDuration { get; set; } = 30f;
            public bool AllowDismount { get; set; } = false;
            public bool AutoHealAfter { get; set; } = true;
            public Messages Messages { get; set; } = new Messages();
        }
        
        private class Messages
        {
            public string YouAreWounded { get; set; } = "<color=red>You lost! You are now wounded for {0} seconds.</color>";
            public string YouCanRide { get; set; } = "<color=green>You won! Ride the wounded losers for {0} seconds!</color>";
            public string RideEnded { get; set; } = "<color=yellow>Ride session ended.</color>";
            public string SessionStarted { get; set; } = "<color=orange>Ride session started! Duration: {0} seconds</color>";
        }
        
        private class RideSession
        {
            public List<BasePlayer> WoundedPlayers { get; set; } = new List<BasePlayer>();
            public List<BasePlayer> RiderPlayers { get; set; } = new List<BasePlayer>();
            public Timer SessionTimer { get; set; }
            public float StartTime { get; set; }
            public float Duration { get; set; }
        }
        
        #endregion
        
        #region Oxide Hooks
        
        private void Init()
        {
            Puts("===============================================");
            Puts("WoundedRide v3.0.0 - INIT STARTED");
            Puts("===============================================");
            Puts("Plugin ready to receive API calls from other plugins");
            Puts("===============================================");
        }
        
        private void Loaded()
        {
            Puts("===============================================");
            Puts("WoundedRide v3.0.0 - LOADED");
            Puts("===============================================");
            Puts("API Methods Available:");
            Puts("  - StartRideSession(List<BasePlayer> winners, List<BasePlayer> losers)");
            Puts("  - EndRideSession(string sessionId)");
            Puts("===============================================");
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
            catch (Exception ex)
            {
                Puts($"Error loading config: {ex.Message}");
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
        
        private void Unload()
        {
            // Clean up all active sessions
            foreach (var session in activeSessions.Values.ToList())
            {
                EndSession(session);
            }
            activeSessions.Clear();
        }
        
        #endregion
        
        #region API Methods (Called by other plugins)
        
        /// <summary>
        /// Start a ride session where winners can ride wounded losers
        /// Called by external plugins (e.g., game mode plugins)
        /// </summary>
        /// <param name="winners">List of winning team players</param>
        /// <param name="losers">List of losing team players</param>
        /// <returns>Session ID for tracking</returns>
        private string StartRideSession(List<BasePlayer> winners, List<BasePlayer> losers)
        {
            if (winners == null || losers == null || winners.Count == 0 || losers.Count == 0)
            {
                Puts("[API] StartRideSession called with invalid parameters");
                return null;
            }
            
            string sessionId = Guid.NewGuid().ToString();
            
            Puts("===============================================");
            Puts($"[API] StartRideSession called");
            Puts($"  Session ID: {sessionId}");
            Puts($"  Winners: {winners.Count}");
            Puts($"  Losers: {losers.Count}");
            Puts($"  Duration: {config.RideDuration} seconds");
            Puts("===============================================");
            
            var session = new RideSession
            {
                WoundedPlayers = new List<BasePlayer>(losers),
                RiderPlayers = new List<BasePlayer>(winners),
                StartTime = Time.realtimeSinceStartup,
                Duration = config.RideDuration
            };
            
            // Put losers into wounded state
            foreach (var loser in losers)
            {
                if (loser != null && !loser.IsWounded())
                {
                    MakeWounded(loser);
                    SendReply(loser, string.Format(config.Messages.YouAreWounded, config.RideDuration));
                }
            }
            
            // Notify winners
            foreach (var winner in winners)
            {
                if (winner != null)
                {
                    SendReply(winner, string.Format(config.Messages.YouCanRide, config.RideDuration));
                }
            }
            
            // Broadcast session start
            Server.Broadcast(string.Format(config.Messages.SessionStarted, config.RideDuration));
            
            // Set timer to end session
            session.SessionTimer = timer.Once(config.RideDuration, () =>
            {
                EndSession(session);
                activeSessions.Remove(sessionId);
            });
            
            activeSessions[sessionId] = session;
            
            return sessionId;
        }
        
        /// <summary>
        /// End a ride session early
        /// Called by external plugins
        /// </summary>
        /// <param name="sessionId">Session ID returned from StartRideSession</param>
        private void EndRideSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return;
            
            if (activeSessions.ContainsKey(sessionId))
            {
                Puts($"[API] EndRideSession called for session: {sessionId}");
                EndSession(activeSessions[sessionId]);
                activeSessions.Remove(sessionId);
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private void MakeWounded(BasePlayer player)
        {
            if (player == null || player.IsWounded())
                return;
                
            // Set player to wounded state
            player.health = 2f; // Low health to maintain wounded state
            player.metabolism.bleeding.value = 0f; // Stop bleeding
            player.BecomeWounded();
            
            Puts($"[DEBUG] Player {player.displayName} set to wounded state");
        }
        
        private void EndSession(RideSession session)
        {
            if (session == null)
                return;
                
            Puts("===============================================");
            Puts("[DEBUG] Ending ride session");
            Puts($"  Wounded players: {session.WoundedPlayers.Count}");
            Puts($"  Riders: {session.RiderPlayers.Count}");
            Puts("===============================================");
            
            // Cancel timer if still active
            session.SessionTimer?.Destroy();
            
            // Restore wounded players
            foreach (var player in session.WoundedPlayers)
            {
                if (player != null && player.IsWounded())
                {
                    player.StopWounded();
                    
                    if (config.AutoHealAfter)
                    {
                        player.health = player.MaxHealth();
                    }
                    
                    SendReply(player, config.Messages.RideEnded);
                }
            }
            
            // Notify riders
            foreach (var player in session.RiderPlayers)
            {
                if (player != null)
                {
                    SendReply(player, config.Messages.RideEnded);
                }
            }
            
            Server.Broadcast(config.Messages.RideEnded);
        }
        
        #endregion
        
        #region Hooks
        
        // Prevent wounded players from recovering during session
        private object OnPlayerRecover(BasePlayer player)
        {
            foreach (var session in activeSessions.Values)
            {
                if (session.WoundedPlayers.Contains(player))
                {
                    Puts($"[DEBUG] Preventing {player.displayName} from recovering during session");
                    return false; // Prevent recovery
                }
            }
            return null;
        }
        
        // Optional: Prevent dismount if configured
        private object CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            if (config.AllowDismount)
                return null;
                
            foreach (var session in activeSessions.Values)
            {
                // Check if player is riding a wounded player
                if (session.RiderPlayers.Contains(player))
                {
                    // Check if they're mounted on a wounded player
                    // (This would need additional logic to track mounting relationships)
                    return null; // Allow for now
                }
            }
            return null;
        }
        
        #endregion
    }
}
