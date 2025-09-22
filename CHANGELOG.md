# Changelog

All notable changes to Clockify CLI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.12] - 2025-09-22

### Bug Fixes

- **Project Filtering**: Projects marked as archived in Clockify are now filtered out from project selection lists
  - Added `archived=false` parameter to GetProjects API call for server-side filtering
  - Improves user experience by showing only active projects in selection prompts
- **Markup Escaping**: Fixed crash when project or task names contain markup-like characters (brackets)
  - Added proper escaping for project and task names in EditTimerCommand success messages
  - Added proper escaping for project names in StartCommand and AddManualTimerCommand error messages
  - Prevents `InvalidOperationException` when displaying projects/tasks with special characters
- **Running Timer Context**: Fixed time parsing context when editing start times on running timers
  - IntelligentTimeParser now uses current time as context for running timers instead of timer's start time
  - Prevents negative duration scenarios when editing running timer start times
  - Example: Entering "9:00" at 5PM now correctly interprets as 9:00 AM (past) instead of 9:00 PM (future)

### Enhancements

- **IntelligentTimeParser Rules**: Updated and clarified rule documentation for better consistency
  - Rule 5: Clarified past time preference with 8-hour and negative duration exceptions
  - Improved rule descriptions to match actual implementation behavior
  - Added comprehensive test coverage for edge cases and rule constraints

## [1.11] - 2025-09-08

### User Experience

- **Improved Time Display**: Removed seconds from all user-facing time displays for cleaner, more readable output
  - Start timer confirmation now shows time as "HH:mm" instead of "HH:mm:ss"
  - Timer status display shows start time as "HH:mm" instead of "HH:mm:ss"  
  - Manual timer confirmation shows times as "HH:mm" instead of "HH:mm:ss"
  - Success messages show simplified time format without seconds

## [1.10] - 2025-01-16

### Major Improvements

- **Eliminated Ambiguous Time Prompts**: Completely removed unnecessary user interruptions for time disambiguation
  - Removed `IsAmbiguousTime()` and `GetAmbiguousTimeOptions()` methods from IntelligentTimeParser
  - Removed `CheckAndConfirmAmbiguousTime()` methods from all command classes
  - Times like "1:00" after "11:30 AM" start are now automatically interpreted as "1:00 PM" without user confirmation
  - Times like "9:51" when editing with "10:00 AM" end are automatically interpreted as "9:51 AM" without user confirmation

### User Experience

- **Streamlined Time Entry**: No more ambiguous time confirmation dialogs interrupt the workflow
  - Context-aware parsing uses start/end relationships and 8-hour constraint to make intelligent choices
  - Eliminated confusing format selection (24-hour vs AM/PM) in favor of automatic disambiguation
  - Improved overall usability by removing unnecessary user decisions

### Technical

- **Code Simplification**: Removed approximately 500 lines of complex ambiguity logic
  - Simplified command classes by removing ambiguous time handling
  - Reduced test complexity by removing 50+ ambiguous time test cases
  - Maintained all core intelligent parsing functionality while eliminating edge case complexity
  - All remaining tests pass with improved reliability

## [1.9] - 2025-01-16

### Major Improvements

- **Simplified Intelligent Time Parser**: Streamlined time parsing from 8 rules to 7 rules for better usability
  - Removed working hours restrictions (former Rule 6) that caused confusion and false positives
  - Enhanced Rule 7: User confirmation for ambiguous times now focuses on genuine ambiguity cases
  - Improved time interpretation logic to be more intuitive and context-aware
  - Fixed critical AM/PM choice logic bug in ambiguous time confirmation dialogs

### Bug Fixes

- **Time Parsing Logic**: Fixed incorrect AM/PM selection logic in `CheckAndConfirmAmbiguousTime` method
  - Corrected user choice interpretation where AM/PM selection was inverted
  - Fixed issue in both AddManualTimerCommand and StartCommand for consistent behavior
  - Enhanced validation logic to use intelligent parser's `GetActualStartDateTime` method

### Technical

- **Test Suite Optimization**: Improved test reliability and coverage
  - Fixed console input mocking for Rule 7 ambiguous time confirmation scenarios
  - Enhanced test scenarios to properly simulate user interactions with Spectre.Console prompts
  - Streamlined edge case handling for better maintainability
  - All 401 tests now pass with robust coverage of simplified intelligent time parsing

## [1.8] - 2025-09-04

### Improvements

- **Comprehensive Ambiguous Time Testing**: Enhanced test coverage for intelligent time parsing and user prompting
  - Added comprehensive test coverage for all three time input commands: AddManualTimerCommand, EditTimerCommand, and StartCommand
  - All commands now consistently include ambiguous time detection and user confirmation prompting
  - Verified CheckAndConfirmAmbiguousTime method implementation across all time input scenarios
  - Added edge case testing for time interpretation in various contexts (morning, afternoon, work hours)
  - Consolidated and cleaned up duplicate test files for better maintainability
  - Ensured all 403 tests pass with robust coverage of intelligent time parsing functionality
  - Enhanced test scenarios covering both ambiguous ("4:37", "9:15") and non-ambiguous ("14:30", "23:59") time inputs

### Technical

- **Test Architecture Improvements**: Merged duplicate test files and organized ambiguous time tests into logical sections
  - Removed redundant EditTimerCommandAmbiguityTests.cs file and integrated tests into main test files
  - Added consistent reflection tests to verify CheckAndConfirmAmbiguousTime method existence across all commands
  - Enhanced test assertions to match actual IntelligentTimeParser behavior and context-aware time interpretation
  - All time input commands now have identical testing patterns for consistent quality assurance

## [1.7] - 2025-09-04

### New Features

- **Interactive Time Confirmation**: Smart ambiguity detection with user confirmation for time input
  - Automatically detects when time input could be interpreted as either AM or PM (e.g., "9:30", "2:15")
  - Shows clear interpretation in both 24-hour and 12-hour formats: "I interpreted this as: 21:30 (9:30 PM)"
  - Asks for user confirmation when ambiguous times are entered
  - Provides AM/PM selection menu if interpretation is incorrect
  - Non-ambiguous times (08:00, 10:00, 14:30, 2:30 PM) skip confirmation for seamless workflow
  - Enhances user experience by making time interpretation transparent and correctable

## [1.6] - 2025-09-04

### New Features

- **Intelligent Time Input System**: Natural time entry without requiring seconds and with smart AM/PM detection
  - Supports multiple formats: `9:30`, `2:30 PM`, `2:30p`, `14:30`, `1:15a`, `1:15p`
  - Context-aware AM/PM interpretation - automatically determines AM/PM based on reasonable work hours and context
  - No seconds required - enter times as HH:mm format
  - Smart validation with helpful error messages for invalid inputs
  - Works across all time entry scenarios: manual time entry, timer editing, and "start earlier" functionality
  - Examples: Start at 8:00 AM, enter "10:00" → correctly interprets as 10:00 AM; Start at 8:00 AM, enter "6:00" → correctly interprets as 6:00 PM
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
