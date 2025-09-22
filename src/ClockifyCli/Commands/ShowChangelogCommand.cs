using Spectre.Console;
using Spectre.Console.Cli;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ClockifyCli.Commands;

public class ShowChangelogCommand : BaseCommand
{
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public ShowChangelogCommand(IAnsiConsole console)
    {
        this.console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        await ShowChangelog(console);
        return 0;
    }

    private async Task ShowChangelog(IAnsiConsole console)
    {
        console.MarkupLine("[bold]ClockifyCli Changelog[/]");
        console.WriteLine();

        try
        {
            // Get the current version from assembly
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            var currentVersion = version != null ? $"{version.Major}.{version.Minor}" : "1.12";

            console.MarkupLine($"[dim]Current version: {currentVersion}[/]");
            console.WriteLine();

            // Find and read the CHANGELOG.md file
            var changelogPath = await FindChangelogFile();
            if (string.IsNullOrEmpty(changelogPath))
            {
                console.MarkupLine("[red]CHANGELOG.md not found![/]");
                console.MarkupLine("[dim]The changelog file should be located in the application directory.[/]");
                return;
            }

            var content = await File.ReadAllTextAsync(changelogPath);

            // Extract the section for the current version (using same pattern as PowerShell script)
            var pattern = $@"## \[{Regex.Escape(currentVersion)}\].*?(?=\n## |\n$|\Z)";
            var match = Regex.Match(content, pattern, RegexOptions.Singleline);

            if (!match.Success)
            {
                console.MarkupLine($"[yellow]No changelog section found for version {currentVersion}[/]");
                console.WriteLine();
                
                // Show available versions
                var availableSections = Regex.Matches(content, @"## \[([^\]]+)\]");
                if (availableSections.Count > 0)
                {
                    console.MarkupLine("[dim]Available versions:[/]");
                    foreach (Match section in availableSections)
                    {
                        console.MarkupLine($"[dim]  - {section.Groups[1].Value}[/]");
                    }
                }
                return;
            }

            // Extract and display the changelog content
            var versionChangelog = match.Value;
            var lines = versionChangelog.Split('\n');
            
            // Skip the version header line and get the date if present
            var dateMatch = Regex.Match(lines.ElementAtOrDefault(0) ?? "", @"## \[[\d.]+\] - (\d{4}-\d{2}-\d{2})");
            if (dateMatch.Success)
            {
                console.MarkupLine($"[bold]Release Date:[/] [green]{dateMatch.Groups[1].Value}[/]");
                console.WriteLine();
            }
            
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
        catch (Exception ex)
        {
            console.MarkupLine("[red]Error reading changelog:[/]");
            console.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
        }
    }

    private Task<string?> FindChangelogFile()
    {
        // Try different possible locations for CHANGELOG.md
        var possiblePaths = new[]
        {
            "CHANGELOG.md",
            Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "CHANGELOG.md"),
            Path.Combine(Directory.GetCurrentDirectory(), "CHANGELOG.md"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "CHANGELOG.md"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return Task.FromResult<string?>(path);
            }
        }

        return Task.FromResult<string?>(null);
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