# Changelog

All notable changes to the WoundedTrain plugin will be documented in this file.

## [2.0.0] - 2024-12-09

### Fixed
- **Critical**: Corrected NPC prefab path from incorrect `horse.corpse.prefab` to proper `scientist.prefab`
- **Memory Leak**: Fixed captainChair not being properly destroyed in cleanup
- **Security**: Added permission check for finale command
- **Timer Management**: Improved timer cleanup to prevent leaks
- **Null Safety**: Added comprehensive null checks throughout the code

### Added
- **Multi-Player Support**: Each player can now have their own independent train
- **Configuration System**: Full JSON configuration file with customizable settings
- **Reverse Movement**: Players can now move backward using S key (configurable)
- **Command Cooldown**: Configurable cooldown system to prevent command spam
- **Auto Cleanup**: Optional automatic cleanup when player dismounts
- **New Command**: `/cleantrain` - Manually cleanup your train
- **Localization**: All messages are now customizable through config
- **Error Logging**: Improved logging with LogError and LogWarning methods
- **OnPlayerDismounted Hook**: Detect when players dismount for auto-cleanup

### Changed
- **Code Structure**: Complete refactoring with #region organization
- **Version**: Updated to 2.0.0
- **Permissions**: Added `woundedtrain.finale` and `woundedtrain.admin` permissions
- **Data Management**: Changed from global variables to per-player TrainData dictionary
- **Command Registration**: Using AddCovalenceCommand for better compatibility
- **Messages**: Updated branding from "Killa Dome" to "Human Train"

### Improved
- **Error Handling**: Better error messages and graceful failure handling
- **Code Quality**: Cleaner, more maintainable code structure
- **Documentation**: Added comprehensive inline documentation
- **Physics Updates**: Separated physics update logic into dedicated method
- **Cleanup Logic**: More thorough cleanup preventing orphaned entities

## [1.2.0] - Previous

### Features
- Basic human train functionality
- Captain's chair for steering
- Forward movement and turning
- Finale command for physics explosion
- Basic permission system

### Known Issues (Fixed in 2.0.0)
- Incorrect prefab path for NPCs
- Memory leaks with chair cleanup
- No permission check on finale
- Single train limitation
- No configuration file
- Hardcoded settings
