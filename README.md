# 🕒 Clockify CLI

[![AppVeyor branch](https://img.shields.io/appveyor/ci/blythmeister/clockifycli)](https://ci.appveyor.com/project/BlythMeister/ClockifyCli)
[![NuGet](https://img.shields.io/nuget/v/ClockifyCli?style=flat-square)](https://www.nuget.org/packages/ClockifyCli)
[![Downloads](https://img.shields.io/nuget/dt/ClockifyCli?style=flat-square)](https://www.nuget.org/packages/ClockifyCli)
[![License](https://img.shields.io/github/license/BlythMeister/ClockifyCli?style=flat-square)](https://github.com/BlythMeister/ClockifyCli/blob/main/LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/8.0)

## 📖 About

Clockify CLI is a powerful command-line tool that bridges the gap between **Clockify**, **Jira**, and **Tempo**, enabling seamless time tracking workflow automation. Whether you're logging time, syncing data between platforms, or managing tasks, this tool simplifies your daily time tracking routine.

### 🎯 Key Features

- **🔄 Sync time entries** from Clockify to Tempo automatically
- **📋 Manage tasks** directly from Jira issues in Clockify
- **⏱️ Start/Stop timers** with an intuitive command-line interface
- **📊 View time reports** for current week and specific periods
- **🔔 Monitor timers** with desktop notifications
- **🗂️ Archive completed** Jira tasks automatically
- **⚙️ Easy configuration** management for API keys
- **🌐 Cross-platform** support (Windows, macOS, Linux)

## 🚀 Installation

### Prerequisites

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Install as Global Tool
dotnet tool install --global ClockifyCli
### Update to Latest Version
dotnet tool update --global ClockifyCli
### Uninstall
dotnet tool uninstall --global ClockifyCli
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

#### Start a Timer
`clockify-cli start` - Interactive selection of available tasks

#### Check Current Status
`clockify-cli status` - See what timer is currently running

#### Stop Current Timer
`clockify-cli stop` - Stop the currently running timer

#### View This Week's Time Entries
`clockify-cli week-view` - Display current week's logged time

`clockify-cli week-view --include-current` - Include currently running timer in the view

### Task Management

#### Add New Task from Jira
`clockify-cli add-task` - Interactive selection to add Jira issues as Clockify tasks

#### Archive Completed Tasks
`clockify-cli archive-completed-jiras` - Archive Clockify tasks that are marked as Done in Jira

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
| `start` | Start a new timer by selecting from available tasks | `clockify-cli start` |
| `stop` | Stop the currently running timer | `clockify-cli stop` |
| `status` | Display current in-progress time entry | `clockify-cli status` |
| `week-view` | Display current week's time entries | `clockify-cli week-view --include-current` |

### Task Management Commands

| Command | Description | Examples |
|---------|-------------|----------|
| `add-task` | Add a new task from a Jira issue | `clockify-cli add-task` |
| `archive-completed-jiras` | Archive tasks with completed Jira status | `clockify-cli archive-completed-jiras` |

### Integration Commands

| Command | Description | Examples |
|---------|-------------|----------|
| `upload-to-tempo` | Upload time entries from Clockify to Tempo | `clockify-cli upload-to-tempo --days 7` |
| `timer-monitor` | Monitor timer status with notifications | `clockify-cli timer-monitor --silent` |

### Utility Commands

| Command | Description | Examples |
|---------|-------------|----------|
| `full-view` | Open Clockify web app in browser | `clockify-cli full-view` |
| `config set` | Configure API keys and credentials | `clockify-cli config set` |
| `config view` | View current configuration | `clockify-cli config view` |

## 🤝 Contributing

We welcome contributions! Here's how you can help improve Clockify CLI:

### Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:git clone https://github.com/yourusername/ClockifyCli.git
cd ClockifyCli
3. **Set up development environment**:# Restore dependencies
dotnet restore

### Build the project
dotnet build

### Run tests (if available)
   dotnet test

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

**Configuration Problems**

#### Check your configuration
`clockify-cli config view`

#### Reconfigure if needed
`clockify-cli config set`

**API Connection Issues**
- Verify your API keys are correct and have proper permissions
- Check your internet connection
- Ensure the services (Clockify/Jira/Tempo) are accessible

**Timer Issues**

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
