---
title: Home
description: "Streamline your time tracking workflow today! 🚀"
---
## 📖 About

Clockify CLI is a powerful command-line tool that bridges the gap between **Clockify**, **Jira**, and **Tempo**, enabling seamless time tracking workflow automation. Whether you're logging time, syncing data between platforms, or managing tasks, this tool simplifies your daily time tracking routine.

### 🎯 Key Features

- **� Intelligent Time Input** with natural formats and smart AM/PM detection (`9:30`, `2:30 PM`, `2:30p`, `14:30`, `1:15a`)
- **�🔄 Sync time entries** from Clockify to Tempo automatically
- **📋 Manage tasks** directly from Jira issues in Clockify
- **⏱️ Start/Stop timers** with an intuitive command-line interface and customizable start times
- **🗑️ Discard/Delete timers** with safety confirmations and time restrictions
- **✏️ Edit existing timers** with precise start/end time adjustments and menu-based editing
- **📊 View time reports** for current week and specific periods with optional detailed breakdown and configurable week start day
- **☕ Break management** with separate tracking and reporting for break time vs work time
- **🔔 Monitor timers** with desktop notifications
- **🗂️ Archive Tasks For Completed Jiras** automatically
- **⚙️ Easy configuration** management for API keys
- **🌐 Cross-platform** support (Windows, macOS, Linux)

> 📝 **What's New?** Check out the [CHANGELOG.md](https://github.com/BlythMeister/ClockifyCli/blob/master/CHANGELOG.md) for detailed release notes and version history.

## 🚀 Installation

### Prerequisites

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Install as Global Tool

`dotnet tool install --global ClockifyCli`

### Update to Latest Version

`dotnet tool update --global ClockifyCli`

### Uninstall

`dotnet tool uninstall --global ClockifyCli`

### PowerShell Auto-Completion (Windows)

To enable tab completion for commands and options in PowerShell, add the following to your PowerShell profile:

#### Temporary Installation (Current Session Only)

```powershell
# Enable tab completion for the current PowerShell session
Register-ArgumentCompleter -Native -CommandName clockify-cli -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $command = "$wordToComplete"
    if ($command.Length -eq 0) {
        $command = ""
    }
    
    # Get available commands and options
    $commands = @('add', 'add-task-from-jira', 'archive-tasks-for-completed-jiras', 'breaks-report', 'config', 'delete', 'discard', 'edit', 'full-view', 'show-changelog', 'start', 'status', 'stop', 'timer-monitor', 'upload-to-tempo', 'week-view')
    $options = @('--help', '--version')
    
    # Filter suggestions based on what user has typed
    $suggestions = ($commands + $options) | Where-Object { $_ -like "$wordToComplete*" }
    
    foreach ($suggestion in $suggestions) {
        [System.Management.Automation.CompletionResult]::new($suggestion, $suggestion, 'ParameterValue', $suggestion)
    }
}
```

#### Permanent Installation (Add to PowerShell Profile)

```powershell
# Add to your PowerShell profile for persistent auto-completion
# First, check if you have a PowerShell profile
Test-Path $PROFILE

# If it returns False, create the profile
if (!(Test-Path $PROFILE)) {
    New-Item -Type File -Path $PROFILE -Force
}

# Open your profile in notepad (or your preferred editor)
notepad $PROFILE

# Add the Register-ArgumentCompleter block from above to your profile
# Save the file and restart PowerShell or run: . $PROFILE
```

> **Note**: Auto-completion helps you quickly discover available commands and options by pressing `Tab` while typing commands.

## ⚙️ Initial Setup

Before using the CLI, you need to configure your API credentials for the services you want to integrate.

### 1. Set up Configuration

`clockify-cli config set`

This interactive command will prompt you for:

- **Clockify API Key** - Get from [Clockify → Profile Settings → API](https://app.clockify.me/user/preferences#api)
- **Jira Username** - Your Jira account email
- **Jira API Token** - Generate from [Atlassian Account Settings → Security → API tokens](https://id.atlassian.com/manage-profile/security/api-tokens)
- **Tempo API Key** - Get from [Tempo → Settings → API Integration](https://tempo.io/server-docs/timesheets/api/rest-api/)

### 2. Verify Configuration

`clockify-cli config view`

This shows your current configuration status and masks sensitive values for security.

## 📚 Usage Examples

### Basic Time Tracking

#### Add Manual Time Entry

`clockify-cli add` - Add a completed time entry with both start and end times

This command is useful for logging time you've already spent working on a task. It allows you to:

- Select the task from your available Clockify tasks
- Enter a description for the work performed
- Specify both start and end times for the time entry
- Confirm the details before adding to Clockify

#### Check Current Status

`clockify-cli status` - See what timer is currently running

#### Delete Completed Timer

`clockify-cli delete` - Delete completed timers from this week (newest first)

#### Discard Current Timer

`clockify-cli discard` - Permanently delete the currently running timer (cannot be undone)

#### Edit Existing Timer

`clockify-cli edit` - Edit start/end times of existing time entries

`clockify-cli edit --days 3` - Look for entries from the last 3 days

#### Start a Timer

`clockify-cli start` - Interactive selection of available tasks with option to start now or at an earlier time

#### Stop Current Timer

`clockify-cli stop` - Stop the currently running timer

#### View This Week's Time Entries

`clockify-cli week-view` - Display current week's logged time

`clockify-cli week-view --include-current` - Include currently running timer in the view

`clockify-cli week-view --detailed` - Show detailed view with start time, end time, and duration

`clockify-cli week-view --week-start Sunday` - Start the week on Sunday instead of Monday

`clockify-cli week-view --include-current --detailed --week-start Wednesday` - Detailed view with custom week start day

#### View Break Time Entries

`clockify-cli breaks-report` - Display break time entries from the last 14 days

`clockify-cli breaks-report --detailed` - Show detailed view with start time, end time, and duration for breaks

`clockify-cli breaks-report --days 7` - Show break entries from the last 7 days

`clockify-cli breaks-report --include-current --detailed --days 30` - Comprehensive break report with running break

### Task Management

#### Add Task From Jira

`clockify-cli add-task-from-jira` - Interactive selection to add Jira issues as Clockify tasks

#### Archive Tasks For Completed Jiras

`clockify-cli archive-tasks-for-completed-jiras` - Archive Clockify tasks that are marked as Done in Jira

### Data Synchronization

#### Upload Time to Tempo

`clockify-cli upload-to-tempo` - Upload recent time entries to Tempo

`clockify-cli upload-to-tempo --days 7` - Upload last 7 days

`clockify-cli upload-to-tempo --days 30 --cleanup-orphaned` - Upload last 30 days and cleanup orphaned entries

### Break Management

The CLI automatically separates break time from work time to provide accurate reporting and ensure break entries don't get exported to work tracking systems.

#### How Break Detection Works

1. **Project-based**: Entries from projects named "Breaks" (case-insensitive)
2. **Type-based**: Entries with Type = "BREAK" (regardless of project)

#### Break vs Work Time

- **Work Reports** (`week-view`): Shows only regular work time, excludes all break entries
- **Break Reports** (`breaks-report`): Shows only break-related time entries  
- **Tempo Exports** (`upload-to-tempo`): Automatically excludes break time from exports to maintain clean work logs

#### Examples

```bash
# View work time only (excludes breaks)
clockify-cli week-view

# View break time only  
clockify-cli breaks-report

# Export work time to Jira (breaks are automatically filtered out)
clockify-cli upload-to-tempo
```

### Monitoring & Automation

#### Timer Monitoring

`clockify-cli timer-monitor` - Check timer status and show notifications

`clockify-cli timer-monitor --silent` - Silent mode (no console output)

`clockify-cli timer-monitor --always-notify` - Always show notifications regardless of timer state

#### Schedule Monitoring (Windows)

`clockify-cli config schedule-monitor` - Set up scheduled task to monitor every 30 minutes

`clockify-cli config schedule-monitor --interval 60` - Custom interval (60 minutes)

`clockify-cli config schedule-monitor --remove` - Remove scheduled task

### Utility Commands

#### Open Clockify Web App

`clockify-cli full-view` - Open Clockify in your default browser

#### Help and Information

`clockify-cli --help` - Show available commands

`clockify-cli start --help` - Show help for specific command

## 🔧 Configuration Management

### Configuration Commands

| Command | Description |
|---------|-------------|
| `config set` | Interactive setup of API keys and credentials |
| `config view` | Display current configuration status |
| `config schedule-monitor` | Set up automated timer monitoring |

### Configuration File Location

The configuration is stored securely in your user profile:

- **Windows**: `%USERPROFILE%\.clockify-cli\config.json`
- **macOS/Linux**: `~/.clockify-cli/config.json`

## ⏰ Intelligent Time Input

ClockifyCli features an intelligent time input system that makes entering times natural and intuitive. No more struggling with seconds or manually specifying AM/PM for obvious times!

### Supported Time Formats

| Format | Example | Description |
|--------|---------|-------------|
| **24-hour** | `14:30`, `09:15` | Military time format (leading zeros optional) |
| **12-hour with PM/AM** | `2:30 PM`, `9:15 AM` | Traditional format with full AM/PM |
| **12-hour with p/a** | `2:30p`, `9:15a`, `1:15p` | Shortened format with single letter |
| **Ambiguous times** | `9:30`, `10:00` | Smart detection based on context |

### Smart AM/PM Detection

When you enter ambiguous times (like `9:30`), the system intelligently determines AM or PM based on:

- **Current time context**: For start times relative to when you're entering
- **Work session logic**: For end times relative to start times
- **Business hours assumptions**: Reasonable work patterns (9 AM - 6 PM typical)

### Examples in Action

| Scenario | Input | Interpretation | Why |
|----------|-------|----------------|-----|
| Adding manual entry at 2 PM | Start: `8:00`, End: `10:00` | 8:00 AM - 10:00 AM | Short morning session |
| Adding manual entry at 2 PM | Start: `8:00`, End: `6:00` | 8:00 AM - 6:00 PM | Full work day |
| Starting timer at 2 PM | Earlier time: `9:30` | 9:30 AM (today) | Before current time |
| Starting timer at 8 AM | Earlier time: `10:00` | 10:00 PM (yesterday) | After current time → previous day |

### Where It Works

The intelligent time input works across all time entry scenarios:

- ✅ **Manual time entry** (`add` command)
- ✅ **Timer editing** (`edit` command)
- ✅ **Start earlier** (`start` command with "Earlier time" option)

## 📋 Command Reference

### Time Tracking Commands

| Command | Description | Examples |
|---------|-------------|----------|
| `add` | Add a completed time entry with both start and end times | `clockify-cli add` |
| `breaks-report` | Display break time entries and separate break tracking | `clockify-cli breaks-report --detailed --days 7` |
| `delete` | Delete completed timers from this week | `clockify-cli delete` |
| `discard` | Permanently delete the currently running timer | `clockify-cli discard` |
| `edit` | Edit start/end times of existing time entries | `clockify-cli edit --days 7` |
| `start` | Start a new timer by selecting from available tasks with customizable start time | `clockify-cli start` |
| `status` | Display current in-progress time entry | `clockify-cli status` |
| `stop` | Stop the currently running timer | `clockify-cli stop` |
| `week-view` | Display current week's time entries | `clockify-cli week-view --include-current --detailed --week-start Sunday` |

### Task Management Commands

| Command | Description | Examples |
|---------|-------------|----------|
| `add-task-from-jira` | Add Task From Jira | `clockify-cli add-task-from-jira` |
| `archive-tasks-for-completed-jiras` | Archive Tasks For Completed Jiras | `clockify-cli archive-tasks-for-completed-jiras` |

### Integration Commands

| Command | Description | Examples |
|---------|-------------|----------|
| `timer-monitor` | Monitor timer status with notifications | `clockify-cli timer-monitor --silent` |
| `upload-to-tempo` | Upload time entries from Clockify to Tempo | `clockify-cli upload-to-tempo --days 7` |

### CLI Utility Commands

| Command | Description | Examples |
|---------|-------------|----------|
| `config set` | Configure API keys and credentials | `clockify-cli config set` |
| `config view` | View current configuration | `clockify-cli config view` |
| `full-view` | Open Clockify web app in browser | `clockify-cli full-view` |

## 🤝 Contributing

We welcome contributions! Here's how you can help improve Clockify CLI:

### Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally `git clone https://github.com/yourusername/ClockifyCli.git` & `cd ClockifyCli`
3. **Set up development environment**: Restore dependencies with `dotnet restore`

### Build the project

`dotnet build`

### Run tests (Coming soon)

`dotnet test`

### Development Guidelines

- **Code Style**: Follow C# conventions and use consistent formatting
- **Dependencies**: The project uses minimal dependencies (Spectre.Console, Newtonsoft.Json)
- **Target Framework**: .NET 8.0
- **Testing**: Add tests for new features and bug fixes

### Making Changes

1. **Create a feature branch** `git checkout -b feature/your-feature-name`
2. **Make your changes** and ensure they follow the project's coding standards
3. **Test your changes** `dotnet build` and `dotnet run -- --help`
4. **Commit your changes** `git commit -m "Add: Description of your changes"`
5. **Push and create a Pull Request** `git push origin feature/your-feature-name`

### Types of Contributions Welcome

- 🐛 **Bug fixes**
- ✨ **New features**
- 📚 **Documentation improvements**
- 🎨 **UI/UX enhancements**
- ⚡ **Performance optimizations**
- 🧪 **Test coverage improvements**

## 🆘 Support

### Getting Help

- **📖 Documentation**: Check this README and command help (`--help`)
- **🐛 Bug Reports**: [Create an issue](https://github.com/BlythMeister/ClockifyCli/issues/new) on GitHub
- **💡 Feature Requests**: [Open a feature request](https://github.com/BlythMeister/ClockifyCli/issues/new) on GitHub
- **💬 Discussions**: Use [GitHub Discussions](https://github.com/BlythMeister/ClockifyCli/discussions) for questions

### Troubleshooting

#### Common Issues

### Configuration Problems

#### Check your configuration

`clockify-cli config view`

#### Reconfigure if needed

`clockify-cli config set`

### API Connection Issues

- Verify your API keys are correct and have proper permissions
- Check your internet connection
- Ensure the services (Clockify/Jira/Tempo) are accessible

### Timer Issues

#### Check current timer status

`clockify-cli status`

#### Stop any stuck timers

`clockify-cli stop`

### Reporting Issues

When reporting issues, please include:

1. **Command used** and any relevant flags
2. **Error message** (full output)
3. **Operating system** and .NET version
4. **Steps to reproduce** the issue

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/BlythMeister/ClockifyCli/blob/master/LICENSE) file for details.

## 🙏 Acknowledgments

- Built with [Spectre.Console](https://spectreconsole.net/) for beautiful CLI interfaces
- Integrates with [Clockify API](https://clockify.me/developers-api)
- Supports [Jira REST API](https://developer.atlassian.com/cloud/jira/platform/rest/v2/)
- Works with [Tempo API](https://tempo.io/server-docs/timesheets/api/rest-api/)

---

**Made with ❤️ by [Chris Blyth](https://github.com/BlythMeister) using Copilot, .NET 8 and Spectre.Console**

*Streamline your time tracking workflow today!* 🚀
