using System.Diagnostics;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace ClockifyCli.Services;

public static class ScheduledTaskService
{
    public static bool IsToolInstalledAsGlobalTool()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "tool list -g",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains("clockifycli") || output.Contains("ClockifyCli");
        }
        catch
        {
            return false;
        }
    }

    public static string? GetGlobalToolPath()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "clockify-cli",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return process.ExitCode == 0 ? output.Split('\n')[0].Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> CreateScheduledTask(string taskName, int intervalMinutes)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AnsiConsole.MarkupLine("[red]Scheduled tasks are only supported on Windows[/]");
            return false;
        }

        var toolPath = GetGlobalToolPath();
        if (string.IsNullOrEmpty(toolPath))
        {
            AnsiConsole.MarkupLine("[red]Could not find clockify-cli executable path[/]");
            return false;
        }

        try
        {
            // Create the scheduled task using schtasks command
            var createTaskCommand = $"schtasks /create /tn \"{taskName}\" /tr \"\\\"{toolPath}\\\" timer-monitor --silent\" /sc minute /mo {intervalMinutes} /ru SYSTEM /f";

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {createTaskCommand}",
                UseShellExecute = true,
                Verb = "runas", // Request admin privileges
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to create scheduled task: {ex.Message}[/]");
            return false;
        }
    }

    public static async Task<bool> DeleteScheduledTask(string taskName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            var deleteTaskCommand = $@"schtasks /delete /tn ""{taskName}"" /f";

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {deleteTaskCommand}",
                UseShellExecute = true,
                Verb = "runas", // Request admin privileges
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool TaskExists(string taskName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            var queryTaskCommand = $@"schtasks /query /tn ""{taskName}""";

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {queryTaskCommand}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static List<string> GetValidIntervals()
    {
        return new List<string> { "15", "30", "60", "120", "240" };
    }

    public static string GetIntervalDescription(string interval)
    {
        return interval switch
        {
            "15" => "Every 15 minutes",
            "30" => "Every 30 minutes",
            "60" => "Every hour",
            "120" => "Every 2 hours",
            "240" => "Every 4 hours",
            _ => $"Every {interval} minutes"
        };
    }
}
