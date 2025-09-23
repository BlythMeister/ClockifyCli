using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using ClockifyCli.Services;

namespace ClockifyCli.Commands;

public class ShowChangelogCommand : BaseCommand<ShowChangelogCommand.Settings>
{
    private readonly IAnsiConsole console;
    private readonly IChangelogReader changelogReader;

    // Constructor for dependency injection (now required)
    public ShowChangelogCommand(IAnsiConsole console, IChangelogReader changelogReader)
    {
        this.console = console ?? throw new ArgumentNullException(nameof(console));
        this.changelogReader = changelogReader ?? throw new ArgumentNullException(nameof(changelogReader));
    }

    public class Settings : CommandSettings
    {
        [Description("Specific version to show changelog for (e.g., 1.11). If not specified, shows interactive version selection.")]
        [CommandOption("-v|--version")]
        public string? Version { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await ShowChangelog(console, settings.Version);
        return 0;
    }

    private async Task ShowChangelog(IAnsiConsole console, string? requestedVersion)
    {
        console.MarkupLine("[bold]ClockifyCli Changelog[/]");
        console.WriteLine();

        try
        {
            // Read the CHANGELOG.md from embedded resources
            var content = await changelogReader.ReadChangelogAsync();
            if (string.IsNullOrEmpty(content))
            {
                console.MarkupLine("[red]CHANGELOG.md not found in embedded resources![/]");
                console.MarkupLine("[dim]The changelog should be embedded in the application.[/]");
                return;
            }

            // Parse all available versions with their dates
            var versionInfos = ParseAvailableVersions(content);
            if (!versionInfos.Any())
            {
                console.MarkupLine("[yellow]No version sections found in changelog![/]");
                return;
            }

            string selectedVersion;

            if (!string.IsNullOrEmpty(requestedVersion))
            {
                // Use the specified version
                selectedVersion = requestedVersion;
                if (!versionInfos.Any(v => v.Version.Equals(selectedVersion, StringComparison.OrdinalIgnoreCase)))
                {
                    console.MarkupLine($"[yellow]Version '{requestedVersion}' not found in changelog![/]");
                    console.WriteLine();
                    ShowAvailableVersions(console, versionInfos);
                    return;
                }
            }
            else
            {
                // Get current version and show interactive selection
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                var currentVersion = version != null ? $"{version.Major}.{version.Minor}" : null;

                if (currentVersion != null)
                {
                    console.MarkupLine($"[dim]Current version: {currentVersion}[/]");
                    console.WriteLine();
                }

                // Interactive version selection
                var versionChoices = versionInfos.Select(v => new VersionChoice(v.Version, v.Date, v.Version.Equals(currentVersion))).ToList();
                
                var selectedVersionChoice = console.Prompt(
                    new SelectionPrompt<VersionChoice>()
                        .Title("Select a [green]version[/] to view its changelog:")
                        .PageSize(15)
                        .AddChoices(versionChoices)
                        .UseConverter(choice => 
                        {
                            var indicator = choice.IsCurrent ? " [green](current)[/]" : "";
                            var dateDisplay = !string.IsNullOrEmpty(choice.Date) ? $" [dim]({choice.Date})[/]" : "";
                            return $"[bold]{choice.Version}[/]{dateDisplay}{indicator}";
                        }));

                selectedVersion = selectedVersionChoice.Version;
            }

            // Extract and display the changelog content for the selected version
            DisplayVersionChangelog(console, content, selectedVersion);
        }
        catch (Exception ex)
        {
            console.MarkupLine("[red]Error reading changelog:[/]");
            console.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
        }
    }

    private List<VersionInfo> ParseAvailableVersions(string content)
    {
        var versionInfos = new List<VersionInfo>();
        var versionPattern = @"## \[([^\]]+)\](?:\s*-\s*(\d{4}-\d{2}-\d{2}|\w+))?";
        var matches = Regex.Matches(content, versionPattern);

        foreach (Match match in matches)
        {
            var version = match.Groups[1].Value;
            var date = match.Groups[2].Success ? match.Groups[2].Value : "";
            versionInfos.Add(new VersionInfo(version, date));
        }

        return versionInfos;
    }

    private void ShowAvailableVersions(IAnsiConsole console, List<VersionInfo> versionInfos)
    {
        console.MarkupLine("[dim]Available versions:[/]");
        foreach (var versionInfo in versionInfos)
        {
            var dateDisplay = !string.IsNullOrEmpty(versionInfo.Date) ? $" ({versionInfo.Date})" : "";
            console.MarkupLine($"[dim]  - {versionInfo.Version}{dateDisplay}[/]");
        }
    }

    private void DisplayVersionChangelog(IAnsiConsole console, string content, string selectedVersion)
    {
        // Extract the section for the selected version
        var pattern = $@"## \[{Regex.Escape(selectedVersion)}\].*?(?=\n## |\n$|\Z)";
        var match = Regex.Match(content, pattern, RegexOptions.Singleline);

        if (!match.Success)
        {
            console.MarkupLine($"[yellow]No changelog section found for version {selectedVersion}[/]");
            return;
        }

        // Extract and display the changelog content
        var versionChangelog = match.Value;
        var lines = versionChangelog.Split('\n');
        
        // Show version header
        console.MarkupLine($"[bold]Version {selectedVersion}[/]");
        
        // Skip the version header line and get the date if present
        var dateMatch = Regex.Match(lines.ElementAtOrDefault(0) ?? "", @"## \[[\d.]+\] - (\d{4}-\d{2}-\d{2}|\w+)");
        if (dateMatch.Success)
        {
            console.MarkupLine($"[bold]Release Date:[/] [green]{dateMatch.Groups[1].Value}[/]");
        }
        console.WriteLine();
        
        // Process the content lines (skip version header)
        var contentLines = lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

        if (!contentLines.Any())
        {
            console.MarkupLine("[yellow]No changelog content found for this version.[/]");
            return;
        }

        foreach (var line in contentLines)
        {
            // Format the line with appropriate styling
            var formattedLine = FormatChangelogLine(line);
            if (!string.IsNullOrEmpty(formattedLine))
            {
                console.MarkupLine(formattedLine);
            }
        }
    }

    private static string FormatChangelogLine(string line)
    {
        // Format different types of changelog lines with appropriate styling
        var trimmedLine = line.Trim();
        
        if (string.IsNullOrEmpty(trimmedLine))
        {
            return string.Empty;
        }

        // Section headers (### New Features, ### Bug Fixes, etc.)
        if (trimmedLine.StartsWith("### "))
        {
            return $"\n[bold yellow]{Markup.Escape(trimmedLine)}[/]";
        }

        // Main bullets with bold formatting (- **Something**: Description)
        if (Regex.IsMatch(trimmedLine, @"^- \*\*.*?\*\*:"))
        {
            var match = Regex.Match(trimmedLine, @"^- (\*\*.*?\*\*:)(.*)");
            if (match.Success)
            {
                var boldPart = match.Groups[1].Value; // "**Something**:"
                var description = match.Groups[2].Value.Trim(); // Rest of the description
                
                return $"[green]•[/] [bold cyan]{Markup.Escape(boldPart)}[/] {Markup.Escape(description)}";
            }
        }

        // Sub-bullets with indentation (  - Description)
        if (trimmedLine.StartsWith("  - "))
        {
            var content = trimmedLine.Substring(4); // Remove "  - "
            return $"  [dim]•[/] [dim]{Markup.Escape(content)}[/]";
        }

        // Regular bullets (- Description)
        if (trimmedLine.StartsWith("- "))
        {
            var content = trimmedLine.Substring(2); // Remove "- "
            return $"[green]•[/] {Markup.Escape(content)}";
        }

        // Default formatting for other lines (just escape markup)
        return Markup.Escape(trimmedLine);
    }
}

public record VersionInfo(string Version, string Date);

public record VersionChoice(string Version, string Date, bool IsCurrent);