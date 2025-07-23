using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClockifyCli.Commands;

public class FullViewCommand : BaseCommand
{
    private const string ClockifyUrl = "https://app.clockify.me/tracker";
    private readonly IAnsiConsole console;

    // Constructor for dependency injection (now required)
    public FullViewCommand(IAnsiConsole console)
    {
        this.console = console;
    }

    public override Task<int> ExecuteAsync(CommandContext context)
    {
        try
        {
            console.MarkupLine("[bold]Opening Clockify web app...[/]");
            console.MarkupLine($"[dim]URL: {ClockifyUrl}[/]");

            OpenUrl(ClockifyUrl);

            console.MarkupLine("[green]:check_mark: Clockify web app opened in your default browser[/]");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]:cross_mark: Failed to open browser: {ex.Message}[/]");
            console.MarkupLine($"[yellow]You can manually open: {ClockifyUrl}[/]");
            return Task.FromResult(1);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = url,
                    UseShellExecute = false
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = url,
                    UseShellExecute = false
                });
            }
            else
            {
                throw new PlatformNotSupportedException("Opening URLs is not supported on this platform");
            }
        }
        catch (Exception)
        {
            // If the above fails, try a more general approach
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}
