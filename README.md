# WoundedTrain - Rust Oxide Plugin

A Rust Oxide uMod plugin that creates a controllable "human train" made of wounded NPCs. Players can mount a captain's chair and steer the train around the map.

## Version
2.0.0

## Features

- **Controllable Human Train**: Create a train of wounded NPCs that follows your commands
- **Multiple Players**: Each player can have their own independent train
- **Reverse Movement**: Move forward and backward (configurable)
- **Grand Finale**: Launch all NPCs into the air with physics effects
- **Auto Cleanup**: Automatically cleanup trains when players dismount (configurable)
- **Customizable Configuration**: All settings can be configured via JSON config file
- **Localization Support**: All messages can be customized
- **Permission System**: Three permission levels for different commands
- **Command Cooldowns**: Prevent spam with configurable cooldowns

## Installation

1. Download `WoundedTrain.cs`
2. Place it in your `oxide/plugins` directory
3. The plugin will generate a configuration file on first load: `oxide/config/WoundedTrain.json`

## Permissions

- `woundedtrain.use` - Allows players to create and clean trains
- `woundedtrain.finale` - Allows players to use the finale command
- `woundedtrain.admin` - Reserved for future admin features

### Grant Permissions
```
oxide.grant user <username> woundedtrain.use
oxide.grant group <groupname> woundedtrain.use
```

## Commands

### Player Commands

- `/humantrain` - Create a human train
  - Requires: `woundedtrain.use` permission
  - Creates a train of wounded NPCs that you can control
  - Mount the captain's chair to steer
  - Use W/A/S/D keys to move and turn

- `/finale` - Execute the grand finale
  - Requires: `woundedtrain.finale` permission
  - Launches all NPCs from your train into the air
  - Creates a spectacular physics effect
  - Automatically cleans up after 5 seconds

- `/cleantrain` - Clean up your train
  - Requires: `woundedtrain.use` permission
  - Removes your active train and all NPCs

## Controls

When mounted on the captain's chair:
- **W** - Move forward
- **S** - Move backward (if enabled in config)
- **A** - Turn left
- **D** - Turn right

## Configuration

The plugin generates a configuration file at `oxide/config/WoundedTrain.json` with the following options:

```json
{
  "TrainLength": 30,
  "MoveSpeed": 30.0,
  "ReverseSpeed": 15.0,
  "TurnSpeed": 15.0,
  "TurnMultiplier": 5.0,
  "UpdateInterval": 0.1,
  "NPCSpacing": 1.5,
  "GroundOffset": 0.5,
  "RaycastHeight": 50.0,
  "RaycastDistance": 100.0,
  "FinaleExplosionForce": 800.0,
  "FinaleSpreadForce": 100.0,
  "CommandCooldown": 5,
  "AllowReverse": true,
  "AutoCleanupOnDismount": true,
  "Messages": {
    "NoPermission": "You don't have permission to use this command.",
    "TrainReady": "<color=#ff0000><b>[Human Train]</b></color> The Train is ready! Mount the chair to steer!",
    "TrainCleaned": "<color=#00ff00>[Human Train]</color> Train has been cleaned up.",
    "FinaleActivated": "<color=orange>GRAND FINALE!</color>",
    "NoActiveTrain": "You don't have an active train.",
    "CommandCooldown": "Please wait {0} seconds before using this command again.",
    "TrainCreationFailed": "<color=red>Failed to create train. Check server logs.</color>"
  }
}
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `TrainLength` | int | 30 | Number of NPCs in the train |
| `MoveSpeed` | float | 30.0 | Forward movement speed |
| `ReverseSpeed` | float | 15.0 | Backward movement speed |
| `TurnSpeed` | float | 15.0 | Turning speed |
| `TurnMultiplier` | float | 5.0 | Multiplier for turn speed calculation |
| `UpdateInterval` | float | 0.1 | Physics update interval in seconds |
| `NPCSpacing` | float | 1.5 | Distance between NPCs |
| `GroundOffset` | float | 0.5 | Height offset from ground |
| `RaycastHeight` | float | 50.0 | Starting height for ground detection raycast |
| `RaycastDistance` | float | 100.0 | Maximum distance for ground detection raycast |
| `FinaleExplosionForce` | float | 800.0 | Upward force for finale |
| `FinaleSpreadForce` | float | 100.0 | Horizontal spread force for finale |
| `CommandCooldown` | int | 5 | Cooldown between commands in seconds |
| `AllowReverse` | bool | true | Enable backward movement |
| `AutoCleanupOnDismount` | bool | true | Cleanup train when player dismounts |

## Improvements from v1.2.0

### Version 2.0.0 Changes

1. **Fixed Critical Issues**
   - Corrected NPC prefab path (was using horse corpse, now uses scientist)
   - Fixed memory leaks with proper captainChair cleanup
   - Added permission check for finale command

2. **Multi-Player Support**
   - Each player can now have their own independent train
   - Trains are tracked per-player using dictionary
   - No interference between multiple active trains

3. **Configuration System**
   - Full JSON configuration file support
   - All settings are now customizable
   - Messages support for localization

4. **New Features**
   - Reverse movement support (S key)
   - Command cooldown system
   - Auto-cleanup on dismount
   - New `/cleantrain` command
   - Better error handling and logging

5. **Code Quality**
   - Complete refactoring with regions
   - Better null checks and error handling
   - Improved code structure and readability
   - Comprehensive logging system

6. **Bug Fixes**
   - Fixed timer leaks
   - Fixed cleanup issues
   - Fixed permission system
   - Improved physics updates

## Technical Details

### Prefabs Used
- **Scientist NPC**: `assets/prefabs/npc/scientist/scientist.prefab`
- **Ghost Engine**: `assets/prefabs/visualization/sphere.prefab`
- **Captain's Chair**: `assets/prefabs/deployable/chair/chair.deployed.prefab`

### How It Works
1. Creates an invisible ghost engine (sphere) as the anchor point
2. Spawns a captain's chair parented to the ghost engine
3. Spawns wounded NPCs in a line, parented to the ghost engine
4. Player mounts the chair and controls the ghost engine's position and rotation
5. All parented entities move together, creating the "train" effect

### Protection
- All train entities (NPCs, chair, ghost engine) are protected from damage
- NPCs cannot recover from wounded state while part of the train
- Automatic cleanup on plugin unload

## Known Issues

- NPCs may clip through terrain on steep hills
- Large trains (50+ NPCs) may cause performance issues
- The ghost engine (sphere) is visible but mostly harmless

## Future Enhancements

Potential features for future versions:
- Customizable NPC types (scientists, zombies, etc.)
- Train skins/decorations
- Speed boost pickups
- Multiplayer train races
- Train collision detection
- Admin commands for managing all trains
- Particle effects for train movement

## Credits

- **Original Author**: Jess
- **Version 2.0 Refactor**: Enhanced with improved features and bug fixes

## Support

For issues, suggestions, or contributions, please visit the GitHub repository.

## License

This plugin is provided as-is for use with Rust Oxide servers.
