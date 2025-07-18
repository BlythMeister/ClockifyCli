# Clockify CLI

A powerful command-line interface for managing time entries between Clockify, Jira, and Tempo. Built with .NET 8 and featuring a beautiful, interactive terminal experience powered by Spectre.Console.

## ✨ Features

- 🔄 **Upload time entries** from Clockify to Tempo with smart deduplication
- 📝 **Add new tasks** to Clockify directly from Jira issues
- 📊 **List archivable tasks** based on Jira status
- 🔐 **Secure configuration** with encrypted credential storage
- 🎨 **Beautiful CLI** with colors, progress bars, and interactive prompts
- 🛡️ **Safe operations** with confirmation prompts and validation

## 🚀 Quick Start

### Prerequisites

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Access to:
  - Clockify workspace
  - Jira instance (with API access)
  - Tempo (for time tracking)

### Installation

1. Clone the repository:
```bash
git clone https://github.com/yourusername/ClockifyCli.git
cd ClockifyCli
```

2. Build the project:
```bash
dotnet build src/ClockifyCli/ClockifyCli.csproj -c Release
```

3. Run the application:
```bash
dotnet run --project src/ClockifyCli/ClockifyCli.csproj
```

### First-Time Setup

Before using any commands, you need to configure your API credentials:

```bash
# Interactive setup - you'll be prompted for each credential
clockify-cli config set

# View current configuration
clockify-cli config view
```

You'll need to provide:
- **Clockify API Key** - From Clockify → Profile Settings → API
- **Jira Username** - Your Jira email address
- **Jira API Token** - From Atlassian → Account Settings → Security → API tokens
- **Tempo API Key** - From Tempo → Settings → API Integration

## 📋 Commands

### Configuration Management

#### `config set`
Interactive setup of API keys and credentials (required first step).

```bash
clockify-cli config set
```

#### `config view`
Display current configuration with masked sensitive values.

```bash
clockify-cli config view
```

### Time Management

#### `upload-to-tempo`
Upload time entries from Clockify to Tempo with smart deduplication.

```bash
# Upload last 14 days (default)
clockify-cli upload-to-tempo

# Upload specific number of days
clockify-cli upload-to-tempo --days 7

# Upload with orphaned entry cleanup (use with caution)
clockify-cli upload-to-tempo --days 30 --cleanup-orphaned
```

**Options:**
- `-d, --days <number>` - Number of days to upload (default: 14)
- `--cleanup-orphaned` - Remove orphaned entries without Clockify IDs

### Task Management

#### `add-task`
Add a new task to Clockify from a Jira issue with interactive project selection.

```bash
clockify-cli add-task
```

This command will:
1. Show available Clockify projects
2. Prompt for Jira issue reference or URL
3. Fetch issue details from Jira
4. Create the task with format: `{IssueKey} [{Summary}]`

#### `archive-list`
List tasks that can be archived based on their Jira status.

```bash
clockify-cli archive-list
```

Shows a table of tasks where the corresponding Jira issue is marked as "Done".

### Help

Get help for any command:

```bash
clockify-cli --help
clockify-cli upload-to-tempo --help
clockify-cli config --help
```

## 🔧 Configuration

### Secure Storage

All credentials are stored securely using AES-256 encryption in:
- **Windows**: `%APPDATA%\ClockifyCli\clockify-config.dat`
- **macOS**: `~/.config/ClockifyCli/clockify-config.dat`
- **Linux**: `~/.config/ClockifyCli/clockify-config.dat`

### API Keys Setup

#### Clockify API Key
1. Go to Clockify → Profile Settings → API
2. Copy your API key

#### Jira API Token
1. Go to [Atlassian Account Settings](https://id.atlassian.com/manage-profile/security/api-tokens)
2. Create a new API token
3. Use your Jira email as username

#### Tempo API Key
1. Go to Tempo → Settings → API Integration
2. Generate a new API token

## 🏗️ Architecture

### Project Structure

```
ClockifyCli/
├── Commands/           # CLI command implementations
│   ├── BaseCommand.cs
│   ├── ConfigCommand.cs
│   ├── UploadToTempoCommand.cs
│   ├── AddTaskCommand.cs
│   └── ArchiveListCommand.cs
├── Models/            # Data models for APIs
│   ├── Clockify/      # Clockify API models
│   ├── Jira/          # Jira API models
│   └── Tempo/         # Tempo API models
├── Services/          # API clients and business logic
│   ├── ClockifyClient.cs
│   ├── JiraClient.cs
│   ├── TempoClient.cs
│   └── ConfigurationService.cs
└── Program.cs         # Application entry point
```

### Dependencies

- **.NET 8.0** - Runtime platform
- **Spectre.Console** - Rich terminal UI framework
- **Spectre.Console.Cli** - Command-line interface framework
- **Newtonsoft.Json** - JSON serialization

## 🔐 Security Features

- **Encrypted Configuration**: All API keys stored with AES-256 encryption
- **Masked Display**: Sensitive values are masked in output
- **Secure Prompts**: API keys hidden during input
- **User-Scoped**: Configuration encrypted per user account
- **No Hardcoded Values**: All credentials configurable

## 🎨 User Experience

- **Rich Terminal UI**: Colors, tables, and progress indicators
- **Interactive Prompts**: Select projects, confirm actions
- **Progress Feedback**: Real-time status during operations
- **Error Handling**: Clear error messages and recovery guidance
- **Validation**: Input validation and helpful error messages

## 🔄 Workflow Integration

### Typical Workflow

1. **Setup** (one-time):
   ```bash
   clockify-cli config set
   ```

2. **Daily/Weekly Upload**:
   ```bash
   clockify-cli upload-to-tempo --days 7
   ```

3. **Adding New Tasks**:
   ```bash
   clockify-cli add-task
   ```

4. **Cleanup** (periodic):
   ```bash
   clockify-cli archive-list
   ```

### CI/CD Integration

The CLI can be integrated into automation workflows:

```bash
# Upload time entries in a scheduled job
clockify-cli upload-to-tempo --days 1
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Setup

```bash
# Clone and setup
git clone https://github.com/yourusername/ClockifyCli.git
cd ClockifyCli

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests (if available)
dotnet test

# Run locally
dotnet run --project src/ClockifyCli/ClockifyCli.csproj -- --help
```

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🐛 Troubleshooting

### Common Issues

#### "Configuration is incomplete"
Run `clockify-cli config set` to set up all required API credentials.

#### "No workspace found"
Ensure your Clockify API key is valid and you have access to at least one workspace.

#### Connection errors
- Check your internet connection
- Verify API keys are correct and not expired
- Ensure API endpoints are accessible

#### Permission errors
- Verify your Jira user has necessary permissions
- Check Tempo API key permissions
- Ensure Clockify workspace access

### Debug Mode

For detailed error information, check the console output or enable verbose logging in your development environment.

## 📞 Support

- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/yourusername/ClockifyCli/issues)
- 💡 **Feature Requests**: [GitHub Discussions](https://github.com/yourusername/ClockifyCli/discussions)
- 📖 **Documentation**: [Wiki](https://github.com/yourusername/ClockifyCli/wiki)

---

Made with ❤️ using .NET 8 and Spectre.Console