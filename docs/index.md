---
title: Home
description: "Streamline your time tracking workflow today! 🚀"
---

## 📖 About

Clockify CLI is a powerful command-line tool that bridges the gap between **Clockify**, **Jira**, and **Tempo**, enabling seamless time tracking workflow automation. Whether you're logging time, syncing data between platforms, or managing tasks, this tool simplifies your daily time tracking routine.

### 🎯 Key Features

- **🔄 Sync time entries** from Clockify to Tempo automatically
- **📋 Manage tasks** directly from Jira issues in Clockify
- **⏱️ Start/Stop timers** with an intuitive command-line interface and customizable start times
- **🗑️ Discard/Delete timers** with safety confirmations and time restrictions
- **✏️ Edit existing timers** with precise start/end time adjustments
- **📊 View time reports** for current week and specific periods with optional detailed breakdown and configurable week start day
- **🔔 Monitor timers** with desktop notifications
- **🗂️ Archive Tasks For Completed Jiras** automatically
- **⚙️ Easy configuration** management for API keys
- **🌐 Cross-platform** support (Windows, macOS, Linux)

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
    $commands = @('add', 'add-task-from-jira', 'archive-tasks-for-completed-jiras', 'config', 'delete', 'discard', 'edit', 'full-view', 'start', 'status', 'stop', 'timer-monitor', 'upload-to-tempo', 'week-view')
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

## 📋 Command Reference

### Time Tracking Commands

| Command | Description | Examples |
|---------|-------------|----------|
| `add` | Add a completed time entry with both start and end times | `clockify-cli add` |
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

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with [Spectre.Console](https://spectreconsole.net/) for beautiful CLI interfaces
- Integrates with [Clockify API](https://clockify.me/developers-api)
- Supports [Jira REST API](https://developer.atlassian.com/cloud/jira/platform/rest/v2/)
- Works with [Tempo API](https://tempo.io/server-docs/timesheets/api/rest-api/)

---

**Made with ❤️ by [Chris Blyth](https://github.com/BlythMeister) using Copilot, .NET 8 and Spectre.Console**

*Streamline your time tracking workflow today!* 🚀
