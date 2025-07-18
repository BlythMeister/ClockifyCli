# 🕒 Clockify CLI

[![AppVeyor branch](https://img.shields.io/appveyor/ci/blythmeister/clockifycli)](https://ci.appveyor.com/project/BlythMeister/ClockifyCli)
[![NuGet](https://img.shields.io/nuget/v/ClockifyCli?style=flat-square)](https://www.nuget.org/packages/ClockifyCli)
[![Downloads](https://img.shields.io/nuget/dt/ClockifyCli?style=flat-square)](https://www.nuget.org/packages/ClockifyCli)
[![License](https://img.shields.io/github/license/BlythMeister/ClockifyCli?style=flat-square)](https://github.com/BlythMeister/ClockifyCli/blob/main/LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/8.0)

A powerful cross-platform command-line tool for seamless time tracking integration between **Clockify**, **Jira**, and **Tempo**. Built with .NET 8 and featuring a beautiful, interactive terminal experience powered by Spectre.Console.

## ✨ Key Features

🔄 **Smart Time Sync** - Upload time entries from Clockify to Tempo with intelligent deduplication  
📝 **Jira Integration** - Create Clockify tasks directly from Jira issues  
📊 **Auto Archiving** - Archive completed tasks based on Jira status  
⏱️ **Real-time Status** - View current timer with live duration updates  
▶️ **Interactive Timer** - Start/stop timers with guided task selection  
📅 **Weekly Reports** - Comprehensive time tracking with totals and averages  
🔐 **Secure Storage** - AES-256 encrypted API key management  
🎨 **Rich UI** - Beautiful terminal interface with colors and progress indicators  
🛡️ **Safe Operations** - Confirmation prompts and validation for all destructive actions  
🚀 **Cross-platform** - Works on Windows, macOS, and Linux

## 🚀 Quick Start

### Installation

#### Option 1: .NET Global Tool (Recommended)dotnet tool install --global ClockifyCli
#### Option 2: From Sourcegit clone https://github.com/BlythMeister/ClockifyCli.git
cd ClockifyCli
dotnet build -c Release
dotnet tool install --global --add-source ./src/ClockifyCli/nupkg ClockifyCli
### First Run Setup

1. **Configure your API credentials** (required first step):clockify-cli config set
2. **Verify your configuration**:clockify-cli config view
3. **Start tracking time**:clockify-cli start
### Prerequisites

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Access to:
  - Clockify workspace (free account works)
  - Jira instance with API access
  - Tempo (for time logging integration)

## 📋 Complete Command Reference

### ⚙️ Configuration Management

#### `config set`
Interactive setup wizard for API credentials.clockify-cli config set**Features:**
- Secure prompts for API keys (masked input)
- Validation and testing of credentials
- Incremental updates (modify only specific keys)
- Helpful setup guidance and links

#### `config view`
Display current configuration with masked sensitive values.clockify-cli config view**Shows:**
- Configuration completeness status
- Masked API keys for security
- Configuration file location
- Missing credential warnings

#### `config schedule-monitor`
Set up automated scheduled task for timer monitoring (Windows only).# Interactive setup with interval selection
clockify-cli config schedule-monitor

# Create task with specific interval (in minutes)
clockify-cli config schedule-monitor --interval 60

# Remove existing scheduled task
clockify-cli config schedule-monitor --remove**Options:**
- `-i, --interval <minutes>` - Set monitoring interval (15, 30, 60, 120, 240)
- `-r, --remove` - Remove existing scheduled task

**Features:**
- Automatically checks if ClockifyCli is installed as global tool
- Creates Windows scheduled task with proper permissions
- Interactive interval selection with common presets
- Safe task replacement (confirms before overwriting)
- Admin privilege handling for task creation
- Provides guidance for macOS/Linux alternatives (cron jobs)

**Prerequisites:**
- Windows operating system
- ClockifyCli installed as global .NET tool
- Administrator privileges for task creation

### ⏱️ Time Management

#### `start`
Start a new time entry with interactive task selection.clockify-cli start**Features:**
- Browse all available tasks across projects
- Project-only time tracking option
- Optional description entry
- Running timer detection and prevention
- Confirmation before starting
- Sorted project and task display

#### `stop`
Stop the currently running time entry.clockify-cli stop**Features:**
- Shows current timer details before stopping
- Elapsed time calculation
- Confirmation prompt
- Final duration display
- Graceful handling when no timer is running

#### `status`
View detailed information about the current running timer.clockify-cli status**Displays:**
- Project and task information
- Description and start time
- Real-time elapsed duration
- Beautiful panel-based layout
- Helpful guidance when no timer is running

#### `timer-monitor`
Monitor timer status and show Windows notifications (ideal for scheduled tasks).
# Basic monitoring with notification
clockify-cli timer-monitor

# Silent mode (no console output) - perfect for scheduled tasks
clockify-cli timer-monitor --silent

# Always show notifications (even when timer is running)
clockify-cli timer-monitor --always-notify

# Silent mode with status notifications
clockify-cli timer-monitor --silent --always-notify
**Options:**
- `-s, --silent` - Suppress console output (useful for scheduled tasks)
- `--always-notify` - Show notification even when timer is running

**Features:**
- Windows balloon notifications when no timer is running
- Optional status notifications for running timers
- Silent mode perfect for automated scheduled tasks
- Cross-platform compatible (notifications only on Windows)
- Exit codes for automation (0=timer running, 2=no timer, 1=error)

**Scheduled Task Setup:**
Create a Windows scheduled task to run every hour:Program: clockify-cli.exe
Arguments: timer-monitor --silent
#### `week-view`
Comprehensive weekly time tracking overview.# View completed entries only
clockify-cli week-view

# Include currently running timer with live duration
clockify-cli week-view --include-current**Features:**
- Current week view (Monday-Sunday)
- Daily breakdowns with project, task, and description
- Running vs completed entry status indicators
- Real-time duration for active timers
- Daily totals and week total
- Average hours per working day
- Optional inclusion of in-progress work

### 🔄 Integration & Automation

#### `upload-to-tempo`
Intelligent time entry synchronization with Tempo.# Upload last 14 days (default)
clockify-cli upload-to-tempo

# Upload specific timeframe
clockify-cli upload-to-tempo --days 7

# Advanced: cleanup orphaned entries
clockify-cli upload-to-tempo --days 30 --cleanup-orphaned**Options:**
- `-d, --days <number>` - Number of days to upload (default: 14)
- `--cleanup-orphaned` - Remove entries without Clockify IDs (use with caution)

**Safety Features:**
- Smart deduplication prevents duplicate entries
- Running timer detection with warning
- Progress tracking and detailed error reporting
- Rollback capabilities for failed operations
- Confirmation prompts for destructive actions

### 📊 Task Management

#### `add-task`
Create Clockify tasks from Jira issues seamlessly.clockify-cli add-task**Workflow:**
1. Select target Clockify project
2. Enter Jira issue reference or URL
3. Automatic Jira issue lookup and validation
4. Task creation with format: `{IssueKey} [{Summary}]`
5. Confirmation and success feedback

#### `archive-completed-jiras`
Automatically archive tasks based on Jira completion status.clockify-cli archive-completed-jiras**Process:**
1. Scan all Clockify projects and tasks
2. Cross-reference with Jira issue status
3. Identify tasks with "Done" Jira status
4. Display archivable tasks in organized table
5. Batch archive with progress tracking
6. Detailed success/failure reporting

**Features:**
- Interactive confirmation before archiving
- Real-time progress tracking
- Individual task status reporting
- Safe operation with detailed error handling

### 📚 Help & Documentation

Get comprehensive help for any command:clockify-cli --help                      # General help
clockify-cli [command] --help            # Command-specific help
clockify-cli config --help               # Configuration help
clockify-cli config schedule-monitor --help # Scheduled task setup help
clockify-cli week-view --help            # Week view options
## 🔧 Configuration & Setup

### 🔐 API Keys Required

#### Clockify API Key
1. Navigate to [Clockify Profile Settings](https://clockify.me/user/settings) → API
2. Copy your personal API key
3. Provides access to workspaces, projects, tasks, and time entries

#### Jira API Token
1. Visit [Atlassian Account Security](https://id.atlassian.com/manage-profile/security/api-tokens)
2. Create a new API token
3. Use your Jira email address as the username
4. Enables Jira issue lookup and status checking

#### Tempo API Key
1. Go to Tempo → Settings → API Integration
2. Generate a new API token
3. Required for time entry synchronization

### 🔒 Secure Storage

All credentials are encrypted using **AES-256** encryption and stored locally:
- **Windows**: `%APPDATA%\ClockifyCli\clockify-config.dat`
- **macOS**: `~/.config/ClockifyCli/clockify-config.dat`
- **Linux**: `~/.config/ClockifyCli/clockify-config.dat`

**Security Features:**
- User-scoped encryption with unique keys
- No credentials stored in plain text
- Secure prompts with masked input
- Local storage only (no cloud sync)

## 🏗️ Architecture & Technical Details

### 🛠️ Built With

- **.NET 8.0** - Modern cross-platform runtime
- **Spectre.Console** - Rich terminal UI framework
- **Spectre.Console.Cli** - Command-line interface framework
- **Newtonsoft.Json** - Robust JSON serialization
- **System.Security.Cryptography** - AES-256 encryption

## 🎯 Usage Scenarios & Workflows

### 📈 Daily Workflow# Morning: Check yesterday's work
clockify-cli week-view

# Start new task
clockify-cli start

# Check current status (anytime)
clockify-cli status --include-current

# Monitor timer status (can be automated)
clockify-cli timer-monitor

# End of day: Stop timer and upload
clockify-cli stop
clockify-cli upload-to-tempo --days 1
### 🗂️ Project Management# Add new tasks from Jira
clockify-cli add-task

# Weekly cleanup
clockify-cli archive-completed-jiras

# Weekly report
clockify-cli week-view --include-current
### 🤖 CI/CD Integration# Automated daily sync (cron job)
clockify-cli upload-to-tempo --days 1

# Weekly cleanup automation
clockify-cli archive-completed-jiras

# Set up automated hourly timer reminder (Windows)
clockify-cli config schedule-monitor --interval 60

# Set up automated timer reminder (macOS/Linux with cron)
# Add to crontab: */60 * * * * clockify-cli timer-monitor --silent
## 🛠️ Development & Contributing

### 🔧 Development Setup
# Clone and setup
git clone https://github.com/BlythMeister/ClockifyCli.git
cd ClockifyCli

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run locally
dotnet run --project src/ClockifyCli/ClockifyCli.csproj -- --help

# Install as global tool for testing
dotnet pack -c Release
dotnet tool install --global --add-source ./src/ClockifyCli/nupkg ClockifyCli
### 🤝 Contributing

We welcome contributions! Please follow these steps:

1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/amazing-feature`
3. **Make your changes** with tests
4. **Commit your changes**: `git commit -m 'Add amazing feature'`
5. **Push to the branch**: `git push origin feature/amazing-feature`
6. **Open a Pull Request**

### 📋 Contribution Guidelines

- Follow existing code style and patterns
- Add tests for new functionality
- Update documentation as needed
- Ensure cross-platform compatibility
- Test on multiple operating systems

## 🐛 Troubleshooting

### Common Issues

#### Configuration Problems
| Issue | Solution |
|-------|----------|
| "Configuration is incomplete" | Run `clockify-cli config set` to configure missing API keys |
| "No workspace found" | Verify Clockify API key and workspace access |
| "Invalid credentials" | Check API key validity and permissions |

#### Connection Issues
| Issue | Solution |
|-------|----------|
| Network timeouts | Check internet connection and firewall settings |
| API rate limits | Wait and retry, or reduce frequency of operations |
| SSL/TLS errors | Update .NET runtime or check system certificates |

#### Permission Issues
| Issue | Solution |
|-------|----------|
| Jira access denied | Verify user permissions and API token scope |
| Tempo sync failures | Check Tempo API key permissions |
| Clockify write errors | Ensure workspace admin or project permissions |

### 📊 Debug Mode

For detailed troubleshooting:# Enable verbose logging (if implemented)
clockify-cli --verbose [command]

# Check configuration status
clockify-cli config view
## 📄 License & Legal

This project is licensed under the **MIT License** - see the [LICENSE](https://github.com/BlythMeister/ClockifyCli/blob/main/LICENSE) file for details.

### Third-Party Acknowledgments
- [Spectre.Console](https://spectreconsole.net/) - Terminal UI framework
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON serialization
- [.NET Foundation](https://dotnetfoundation.org/) - Runtime platform

## 📞 Support & Community

- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/BlythMeister/ClockifyCli/issues)
- 💡 **Feature Requests**: [GitHub Discussions](https://github.com/BlythMeister/ClockifyCli/discussions)
- 📖 **Documentation**: [GitHub Wiki](https://github.com/BlythMeister/ClockifyCli/wiki)
- 📧 **Direct Contact**: [Repository Owner](https://github.com/BlythMeister)

### 🌟 Show Your Support

If this tool helps your workflow, please consider:
- ⭐ Starring the repository
- 🐛 Reporting issues and bugs
- 💡 Suggesting new features
- 🤝 Contributing code improvements
- 📢 Sharing with your team

---

**Made with ❤️ by [Chris Blyth](https://github.com/BlythMeister) using Copilot, .NET 8 and Spectre.Console**

*Streamline your time tracking workflow today!* 🚀
