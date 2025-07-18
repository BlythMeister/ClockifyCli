using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClockifyCli.Commands;

public class FullViewCommand : BaseCommand
{
    private const string ClockifyUrl = "https://app.clockify.me/tracker";

    public override Task<int> ExecuteAsync(CommandContext context)
    {
        try
        {
            AnsiConsole.MarkupLine("[bold]Opening Clockify web app...[/]");
            AnsiConsole.MarkupLine($"[dim]URL: {ClockifyUrl}[/]");

            OpenUrl(ClockifyUrl);

            AnsiConsole.MarkupLine("[green]✓ Clockify web app opened in your default browser[/]");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to open browser: {ex.Message}[/]");
            AnsiConsole.MarkupLine($"[yellow]You can manually open: {ClockifyUrl}[/]");
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