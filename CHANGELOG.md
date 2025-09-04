# Changelog

All notable changes to Clockify CLI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.6] - 2025-09-04

### New Features

- **Breaks Management System**: Complete separation of break tracking from regular work reporting
  - Added new `breaks-report` command to view break-related time entries
  - Break entries are excluded from regular reports (`week-view`) and exports (`upload-to-tempo`)
  - Supports both "Breaks" project name filtering and "BREAK" type entry filtering
  - Detailed and summary views for break reporting with daily totals
- **Enhanced Edit Timer Command**: Replaced yes/no prompts with menu-based editing system
  - Shows menu with options: "Change project/task", "Change times", "Change description", "Done"
  - Allows multiple edits in a single session with continuous menu loop
  - "Done" is the default option when no changes have been made
  - Improved user experience with clear feedback on what changes will be applied
- **Back Navigation in Task Selection**: Added "← Back to project selection" option in task selection menus
  - Users can now go back to project selection if they choose the wrong project during task selection
  - Back option appears at the bottom of task lists when multiple projects are available
  - Implemented in Start Timer, Add Manual Timer, and Edit Timer commands
- **Type-Aware Time Entry Creation**: StartCommand now explicitly creates "REGULAR" type time entries
  - Ensures proper distinction between regular work time and break time
  - Maintains compatibility with existing time tracking workflows

### Improvements

- Enhanced user experience with seamless navigation between project and task selection
- No more need to cancel entire operation when wrong project is selected
- Continuous editing workflow allows multiple changes without restarting the edit process
- Clear visual feedback shows which changes will be applied before confirmation

## [1.5] - 2025-08-28

### Improvements

- **Consistent Task Selection Flow**: All commands now use the same 2-layer task selection (project first, then tasks)
- Updated Add Manual Entry command to match the Start command's selection flow
- Improved consistency across all task selection interfaces

### Bug Fixes

- Task selection inconsistency between Start and Add Manual Entry commands
- Users now have the same selection experience across all commands

## [1.4] - 2025-08-26

### Added

- **Smart Timer Replacement**: Major improvement to the timer start flow to prevent data loss
- Better user feedback during timer replacement process
- Comprehensive test coverage for all timer replacement scenarios

### Changed

- Timer start flow now collects all new timer details before stopping existing timer
- Original timer is only stopped at the very last moment, just before starting new timer
- Clear messaging throughout the process ("Collecting new timer details first...")

### Fixed

- **Critical**: Prevents accidental loss of running timer when new timer setup fails or is cancelled
- Users now receive confirmation when original timer is preserved during cancellation

### Technical Details

- Modified `StartCommand.cs` to use deferred timer stopping approach
- Added state tracking for timer replacement workflow  
- Enhanced error handling and user feedback messages
- Updated and expanded test suite with new scenarios

## [1.0.0] - Initial Release

### Initial Features

- Command-line interface for Clockify time tracking
- Integration with Jira and Tempo for seamless workflow
- Timer management (start, stop, status, discard, delete, edit)
- Time entry management (add, view, upload to Tempo)
- Task management (add from Jira, archive completed)
- Configuration management for API keys
- Cross-platform support (Windows, macOS, Linux)
- Desktop notifications for timer monitoring
- Flexible time reporting with customizable options
- Interactive command selection with auto-completion support

### Core Capabilities

- **Time Tracking**: Start/stop timers with customizable start times
- **Integration**: Sync between Clockify, Jira, and Tempo
- **Task Management**: Add Jira issues as Clockify tasks
- **Reporting**: Week view with detailed breakdown options
- **Monitoring**: Timer monitoring with desktop notifications
- **Configuration**: Secure API key management
- **User Experience**: Intuitive CLI with help and auto-completion

---

## How to Read This Changelog

- **Added** for new features
- **Changed** for changes in existing functionality  
- **Deprecated** for soon-to-be removed features
- **Removed** for now removed features
- **Fixed** for any bug fixes
- **Security** for vulnerability fixes

## Contributing to the Changelog

When contributing to this project, please update this changelog with your changes following the format above.
