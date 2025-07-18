using System.Runtime.InteropServices;

namespace ClockifyCli.Services;

public static class NotificationService
{
    public static void ShowTimerReminderNotification()
    {
        // Only show notifications on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            // Use PowerShell to show Windows balloon notification
            var script = @"
Add-Type -AssemblyName System.Windows.Forms

$notifyIcon = New-Object System.Windows.Forms.NotifyIcon
$notifyIcon.Icon = [System.Drawing.SystemIcons]::Information
$notifyIcon.Visible = $true
$notifyIcon.ShowBalloonTip(5000, 'Clockify Timer Reminder', 'No timer is currently running - Don''t forget to start tracking your time!', [System.Windows.Forms.ToolTipIcon]::Warning)

Start-Sleep -Seconds 6
$notifyIcon.Dispose()
";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };

            using var process = System.Diagnostics.Process.Start(startInfo);

            // Don't wait for completion to avoid blocking
        }
        catch (Exception ex)
        {
            // If notification fails, we don't want to crash the application
            Console.WriteLine($"Failed to show notification: {ex.Message}");
        }
    }

    public static void ShowTimerRunningNotification(string projectName, string taskName, TimeSpan elapsed)
    {
        // Only show notifications on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            var formattedElapsed = FormatElapsedTime(elapsed);
            var message = $"Timer is running!\\nProject: {projectName}\\nTask: {taskName}\\nElapsed: {formattedElapsed}";

            var script = @$"
Add-Type -AssemblyName System.Windows.Forms

$notifyIcon = New-Object System.Windows.Forms.NotifyIcon
$notifyIcon.Icon = [System.Drawing.SystemIcons]::Information
$notifyIcon.Visible = $true
$notifyIcon.ShowBalloonTip(5000, 'Clockify Timer Status', '{message}', [System.Windows.Forms.ToolTipIcon]::Info)

Start-Sleep -Seconds 6
$notifyIcon.Dispose()
";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };

            using var process = System.Diagnostics.Process.Start(startInfo);

            // Don't wait for completion to avoid blocking
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to show notification: {ex.Message}");
        }
    }

    private static string FormatElapsedTime(TimeSpan elapsed)
    {
        var totalHours = (int)elapsed.TotalHours;
        var minutes = elapsed.Minutes;

        if (totalHours > 0)
        {
            return $"{totalHours}h {minutes}m";
        }
        else
        {
            return $"{minutes}m";
        }
    }
}
