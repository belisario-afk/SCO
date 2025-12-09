# WoundedRide Plugin

## Overview
WoundedRide is a Rust Oxide plugin that creates a winner/loser riding mechanic. When called by external plugins (such as game mode plugins), it puts losing team players into wounded state and allows winning team players to ride them for a configurable duration (default 30 seconds).

## Version
**3.0.0** - Complete rewrite focused on player-to-player riding mechanics

## Features
- **Plugin API Integration**: Called by external plugins to trigger riding sessions
- **Team-Based Mechanics**: Winners ride losers
- **Wounded State Management**: Automatic wounded state for losers
- **Timed Sessions**: Configurable duration (default 30 seconds)
- **Auto-Healing**: Optional automatic healing after session ends
- **Recovery Prevention**: Wounded players cannot recover during active sessions

## API Methods

### For Plugin Developers

This plugin is designed to be called by other plugins (e.g., game mode plugins, arena plugins). It exposes the following API methods:

#### StartRideSession
```csharp
string StartRideSession(List<BasePlayer> winners, List<BasePlayer> losers)
```

**Parameters:**
- `winners`: List of winning team players who can ride
- `losers`: List of losing team players who become wounded

**Returns:** Session ID (string) for tracking

**Example Usage:**
```csharp
[PluginReference]
private Plugin WoundedRide;

// After determining winners and losers
string sessionId = WoundedRide?.Call<string>("StartRideSession", winnersList, losersList);
```

#### EndRideSession
```csharp
void EndRideSession(string sessionId)
```

**Parameters:**
- `sessionId`: The session ID returned from StartRideSession

**Example Usage:**
```csharp
WoundedRide?.Call("EndRideSession", sessionId);
```

## Configuration

The plugin creates a `WoundedRide.json` configuration file:

```json
{
  "RideDuration": 30.0,
  "AllowDismount": false,
  "AutoHealAfter": true,
  "Messages": {
    "YouAreWounded": "<color=red>You lost! You are now wounded for {0} seconds.</color>",
    "YouCanRide": "<color=green>You won! Ride the wounded losers for {0} seconds!</color>",
    "RideEnded": "<color=yellow>Ride session ended.</color>",
    "SessionStarted": "<color=orange>Ride session started! Duration: {0} seconds</color>"
  }
}
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `RideDuration` | float | 30.0 | Duration in seconds for the riding session |
| `AllowDismount` | bool | false | Whether riders can dismount before session ends |
| `AutoHealAfter` | bool | true | Automatically heal wounded players when session ends |

### Message Customization

All player-facing messages support color tags and can be customized in the configuration. Messages that include `{0}` will have the duration automatically inserted.

## How It Works

1. **External Plugin Calls API**: A game mode or arena plugin determines winners/losers and calls `StartRideSession`
2. **Losers Wounded**: All losing players are put into wounded state
3. **Winners Notified**: Winning players are notified they can ride the wounded players
4. **Riding**: Winners can approach and mount wounded losers (using Rust's native mounting system)
5. **Timer**: Session runs for configured duration
6. **Auto-End**: After timer expires, wounded players are restored and optionally healed
7. **Manual End**: External plugin can call `EndRideSession` to end early

## Integration Example

Here's how another plugin would integrate with WoundedRide:

```csharp
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("GameMode", "YourName", "1.0.0")]
    public class GameMode : RustPlugin
    {
        [PluginReference]
        private Plugin WoundedRide;
        
        private void OnRoundEnd()
        {
            // Determine winners and losers
            List<BasePlayer> winners = GetWinningTeam();
            List<BasePlayer> losers = GetLosingTeam();
            
            // Start ride session
            if (WoundedRide != null)
            {
                string sessionId = WoundedRide.Call<string>("StartRideSession", winners, losers);
                Puts($"Started ride session: {sessionId}");
            }
        }
    }
}
```

## Hooks

The plugin uses the following Oxide hooks:

- `OnPlayerRecover`: Prevents wounded players from recovering during active sessions
- `CanDismountEntity`: Optionally prevents dismounting during sessions

## Console Output

The plugin provides detailed console logging:

```
===============================================
WoundedRide v3.0.0 - INIT STARTED
===============================================
Plugin ready to receive API calls from other plugins
===============================================

[API] StartRideSession called
  Session ID: abc123...
  Winners: 5
  Losers: 5
  Duration: 30 seconds
===============================================

[DEBUG] Player JohnDoe set to wounded state
[DEBUG] Player JaneSmith set to wounded state
...

===============================================
[DEBUG] Ending ride session
  Wounded players: 5
  Riders: 5
===============================================
```

## Installation

1. Place `WoundedRide.cs` in `oxide/plugins/` directory
2. Plugin will auto-generate `WoundedRide.json` configuration file
3. Configure as needed
4. Reload or restart server
5. Integrate with your game mode/arena plugin using the API methods

## Requirements

- Rust game server
- Oxide/uMod installed
- Another plugin that calls the WoundedRide API (e.g., game mode plugin)

## Support

For issues or questions:
- Check console logs for detailed debug output
- Verify your external plugin is correctly calling the API methods
- Ensure player lists passed to API contain valid, connected players

## Changelog

### Version 3.0.0
- Complete rewrite from WoundedTrain NPC system
- Focus on player-to-player riding mechanics
- Plugin API for external integration
- Timed riding sessions
- Automatic wounded state management
- Configurable duration and behavior
- Auto-healing option
- Recovery prevention during sessions

## Credits

Author: Jess  
Version: 3.0.0  
Description: Winners ride wounded losers - triggered by external plugins
