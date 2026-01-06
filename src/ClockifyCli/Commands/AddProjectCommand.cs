using ClockifyCli.Models;
using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace ClockifyCli.Commands;

public class AddProjectCommand : BaseCommand<AddProjectCommand.Settings>
{
    private readonly IClockifyClient clockifyClient;
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public AddProjectCommand(IClockifyClient clockifyClient, IAnsiConsole console)
    {
        this.clockifyClient = clockifyClient ?? throw new ArgumentNullException(nameof(clockifyClient));
        this.console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await AddProject(clockifyClient, console, settings);
        return 0;
    }

    private async Task AddProject(IClockifyClient clockifyClient, IAnsiConsole console, Settings settings)
    {
        console.MarkupLine("[bold]Add New Project[/]");
        console.WriteLine();

        var workspace = (await clockifyClient.GetLoggedInUserWorkspaces()).FirstOrDefault();
        if (workspace == null)
        {
            console.MarkupLine("[red]No workspace found![/]");
            return;
        }

        // Get project name from parameter or user input
        var projectName = !string.IsNullOrWhiteSpace(settings.Name)
            ? settings.Name
            : console.Ask<string>("Enter [blue]project name[/]:");

        // Check if project already exists
        List<ProjectInfo> existingProjects = new();
        await console.Status()
                     .StartAsync("Checking existing projects...", async ctx =>
                     {
                         ctx.Status("Getting projects from Clockify...");
                         existingProjects = await clockifyClient.GetProjects(workspace);
                     });

        // Check for duplicate project names (case-insensitive)
        if (existingProjects.Any(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase)))
        {
            console.MarkupLine($"[red]Project '{Markup.Escape(projectName)}' already exists![/]");
            return;
        }

        // Show confirmation
        console.MarkupLine($"Will create project '[green]{Markup.Escape(projectName)}[/]' in workspace '[dim]{Markup.Escape(workspace.Name)}[/]'");

        if (console.Confirm("Confirm?"))
        {
            // Add the project
            await console.Status()
                         .StartAsync("Creating project...", async ctx =>
                         {
                             await clockifyClient.AddProject(workspace, projectName);
                         });

            console.MarkupLine("[green]Project created successfully![/]");
        }
        else
        {
            console.MarkupLine("[yellow]Operation cancelled.[/]");
        }
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-n|--name <NAME>")]
        [Description("Project name (optional, will prompt if not provided)")]
        public string? Name { get; set; }
    }
}
