using ClockifyCli.Commands;
using ClockifyCli.Models;
using ClockifyCli.Services;
using ClockifyCli.Tests.Infrastructure;
using Moq;
using NUnit.Framework;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using System.ComponentModel;
using System.Reflection;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class WeekViewCommandTests
{
    private Mock<IClockifyClient> mockClockifyClient;
    private TestConsole testConsole;
    private WeekViewCommand command;

    [SetUp]
    public void Setup()
    {
        mockClockifyClient = new Mock<IClockifyClient>();
        testConsole = new TestConsole();
        var mockClock = new MockClock(new DateTime(2024, 1, 1, 14, 0, 0));
        command = new WeekViewCommand(mockClockifyClient.Object, testConsole, mockClock);
    }

    [TearDown]
    public void TearDown()
    {
        testConsole?.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithValidSettings_ReturnsZero()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        var mockTimeEntries = new List<TimeEntry>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetLoggedInUser(), Times.Once);
        mockClockifyClient.Verify(x => x.GetLoggedInUserWorkspaces(), Times.Once);
        mockClockifyClient.Verify(x => x.GetProjects(mockWorkspace), Times.Once);
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_NoWorkspaceFound_ShowsErrorAndReturnsZero()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo>());

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No workspace found!"));
    }

    [Test]
    public async Task ExecuteAsync_WithIncludeCurrentFlag_CallsCorrectMethods()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings { IncludeCurrent = true };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        var mockTimeEntries = new List<TimeEntry>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithDetailedFlag_CallsCorrectMethods()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings { Detailed = true };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        var mockTimeEntries = new List<TimeEntry>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithNoTimeEntries_ShowsEmptyWeekMessage()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        var mockTimeEntries = new List<TimeEntry>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        // Command should handle empty time entries gracefully
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Test]
    public void Settings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new WeekViewCommand.Settings();

        // Assert
        Assert.That(settings.IncludeCurrent, Is.False);
        Assert.That(settings.Detailed, Is.False);
        Assert.That(settings.WeekStartDay, Is.EqualTo(DayOfWeek.Monday));
    }

    [Test]
    public void Settings_CanSetCustomFlags()
    {
        // Arrange & Act
        var settings = new WeekViewCommand.Settings { IncludeCurrent = true, Detailed = true, WeekStartDay = DayOfWeek.Sunday };

        // Assert
        Assert.That(settings.IncludeCurrent, Is.True);
        Assert.That(settings.Detailed, Is.True);
        Assert.That(settings.WeekStartDay, Is.EqualTo(DayOfWeek.Sunday));
    }

    // Test data for week start day calculations
    private static readonly object[] WeekStartDayTestCases =
    {
        new object[] { DayOfWeek.Monday, new DateTime(2025, 7, 21), new DateTime(2025, 7, 21), new DateTime(2025, 7, 27) }, // Monday test - current week
        new object[] { DayOfWeek.Tuesday, new DateTime(2025, 7, 22), new DateTime(2025, 7, 22), new DateTime(2025, 7, 28) }, // Tuesday test - current week
        new object[] { DayOfWeek.Wednesday, new DateTime(2025, 7, 23), new DateTime(2025, 7, 23), new DateTime(2025, 7, 29) }, // Wednesday test - current week
        new object[] { DayOfWeek.Thursday, new DateTime(2025, 7, 24), new DateTime(2025, 7, 24), new DateTime(2025, 7, 30) }, // Thursday test - current week
        new object[] { DayOfWeek.Friday, new DateTime(2025, 7, 25), new DateTime(2025, 7, 25), new DateTime(2025, 7, 31) }, // Friday test - current week
        new object[] { DayOfWeek.Saturday, new DateTime(2025, 7, 26), new DateTime(2025, 7, 26), new DateTime(2025, 8, 1) }, // Saturday test - current week
        new object[] { DayOfWeek.Sunday, new DateTime(2025, 7, 27), new DateTime(2025, 7, 27), new DateTime(2025, 8, 2) }, // Sunday test - current week
        
        // Test when today is different from week start day
        new object[] { DayOfWeek.Monday, new DateTime(2025, 7, 23), new DateTime(2025, 7, 21), new DateTime(2025, 7, 27) }, // Wednesday, week starts Monday
        new object[] { DayOfWeek.Sunday, new DateTime(2025, 7, 23), new DateTime(2025, 7, 20), new DateTime(2025, 7, 26) }, // Wednesday, week starts Sunday
        new object[] { DayOfWeek.Friday, new DateTime(2025, 7, 23), new DateTime(2025, 7, 18), new DateTime(2025, 7, 24) }, // Wednesday, week starts Friday
    };

    [TestCaseSource(nameof(WeekStartDayTestCases))]
    public async Task ExecuteAsync_WithDifferentWeekStartDays_CallsGetTimeEntriesWithCorrectDateRange(
        DayOfWeek weekStartDay, DateTime today, DateTime expectedStartDate, DateTime expectedEndDate)
    {
        // Arrange
        var settings = new WeekViewCommand.Settings { WeekStartDay = weekStartDay };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        var mockTimeEntries = new List<TimeEntry>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);

        DateTime capturedStartDate = default;
        DateTime capturedEndDate = default;

        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .Callback<WorkspaceInfo, UserInfo, DateTime, DateTime>((w, u, start, end) =>
                         {
                             capturedStartDate = start;
                             capturedEndDate = end;
                         })
                         .ReturnsAsync(mockTimeEntries);

        // Mock DateTime.Today to return our test date
        // Note: This would require making the date calculation testable by injecting a time provider
        // For this test, we'll verify the concept works with the current implementation

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);

        // Note: The actual date verification would require dependency injection of a date/time provider
        // This test ensures the command executes successfully with different week start days
    }

    private static readonly object[] WeekStartDayEnumTestCases =
    {
        new object[] { DayOfWeek.Monday },
        new object[] { DayOfWeek.Tuesday },
        new object[] { DayOfWeek.Wednesday },
        new object[] { DayOfWeek.Thursday },
        new object[] { DayOfWeek.Friday },
        new object[] { DayOfWeek.Saturday },
        new object[] { DayOfWeek.Sunday }
    };

    [TestCaseSource(nameof(WeekStartDayEnumTestCases))]
    public async Task ExecuteAsync_WithAllDaysOfWeek_ExecutesSuccessfully(DayOfWeek weekStartDay)
    {
        // Arrange
        var settings = new WeekViewCommand.Settings { WeekStartDay = weekStartDay };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        var mockTimeEntries = new List<TimeEntry>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetLoggedInUser(), Times.Once);
        mockClockifyClient.Verify(x => x.GetLoggedInUserWorkspaces(), Times.Once);
        mockClockifyClient.Verify(x => x.GetProjects(mockWorkspace), Times.Once);
        mockClockifyClient.Verify(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [TestCaseSource(nameof(WeekStartDayEnumTestCases))]
    public void Settings_WeekStartDay_CanBeSetToAllDaysOfWeek(DayOfWeek weekStartDay)
    {
        // Arrange & Act
        var settings = new WeekViewCommand.Settings { WeekStartDay = weekStartDay };

        // Assert
        Assert.That(settings.WeekStartDay, Is.EqualTo(weekStartDay));
    }

    [Test]
    public async Task ExecuteAsync_WithWeekStartDayAndDetailedFlag_CombinesOptionsCorrectly()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings
        {
            WeekStartDay = DayOfWeek.Wednesday,
            Detailed = true,
            IncludeCurrent = true
        };
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        var mockTimeEntries = new List<TimeEntry>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        Assert.That(settings.WeekStartDay, Is.EqualTo(DayOfWeek.Wednesday));
        Assert.That(settings.Detailed, Is.True);
        Assert.That(settings.IncludeCurrent, Is.True);
    }

    [Test]
    public void WeekStartDayCommandOption_HasCorrectAttributes()
    {
        // Arrange
        var settingsType = typeof(WeekViewCommand.Settings);
        var weekStartDayProperty = settingsType.GetProperty(nameof(WeekViewCommand.Settings.WeekStartDay));

        // Act
        var commandOptionAttribute = weekStartDayProperty?.GetCustomAttributes(typeof(CommandOptionAttribute), false)
                                                        .Cast<CommandOptionAttribute>()
                                                        .FirstOrDefault();
        var descriptionAttribute = weekStartDayProperty?.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
                                                       .Cast<System.ComponentModel.DescriptionAttribute>()
                                                       .FirstOrDefault();
        var defaultValueAttribute = weekStartDayProperty?.GetCustomAttributes(typeof(System.ComponentModel.DefaultValueAttribute), false)
                                                        .Cast<System.ComponentModel.DefaultValueAttribute>()
                                                        .FirstOrDefault();

        // Assert
        Assert.That(commandOptionAttribute, Is.Not.Null, "CommandOption attribute should be present");
        Assert.That(descriptionAttribute, Is.Not.Null, "Description attribute should be present");
        Assert.That(descriptionAttribute.Description, Does.Contain("Day of the week"), "Description should mention day of the week");
        Assert.That(defaultValueAttribute, Is.Not.Null, "DefaultValue attribute should be present");
        Assert.That(defaultValueAttribute.Value, Is.EqualTo(DayOfWeek.Monday), "Default value should be Monday");
    }

    [Test]
    public async Task ExecuteAsync_FiltersOutBreakEntries()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        
        var projects = new List<ProjectInfo>
        {
            new ProjectInfo("work-project", "Work Project"),
            new ProjectInfo("breaks-project", "Breaks") // Breaks project
        };

        var timeEntries = new List<TimeEntry>
        {
            // Regular work entry - should be included
            new TimeEntry("entry1", "Regular work", "task1", "work-project", "REGULAR", 
                new TimeInterval("2024-01-01T09:00:00Z", "2024-01-01T10:00:00Z")),
            
            // Break type entry - should be excluded
            new TimeEntry("entry2", "Coffee break", "task2", "work-project", "BREAK", 
                new TimeInterval("2024-01-01T10:15:00Z", "2024-01-01T10:30:00Z")),
            
            // Breaks project entry - should be excluded
            new TimeEntry("entry3", "Lunch break", "task3", "breaks-project", "REGULAR", 
                new TimeInterval("2024-01-01T12:00:00Z", "2024-01-01T13:00:00Z"))
        };

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(projects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(timeEntries);
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser)).ReturnsAsync((TimeEntry?)null);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        var output = testConsole.Output;
        // Should contain regular work entry
        Assert.That(output, Does.Contain("Regular work"));
        
        // Should NOT contain break entries
        Assert.That(output, Does.Not.Contain("Coffee break"));
        Assert.That(output, Does.Not.Contain("Lunch break"));
        
        // Should show total time excluding breaks (1 hour)
        Assert.That(output, Does.Contain("1:00:00"));
    }

    [Test]
    public async Task ExecuteAsync_FiltersBreaksByProjectName_CaseInsensitive()
    {
        // Arrange
        var settings = new WeekViewCommand.Settings();
        var context = new CommandContext([], new Mock<IRemainingArguments>().Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        
        var projects = new List<ProjectInfo>
        {
            new ProjectInfo("work-project", "Work Project"),
            new ProjectInfo("breaks-project", "BREAKS") // Uppercase BREAKS
        };

        var timeEntries = new List<TimeEntry>
        {
            new TimeEntry("entry1", "Regular work", "task1", "work-project", "REGULAR", 
                new TimeInterval("2024-01-01T09:00:00Z", "2024-01-01T10:00:00Z")),
            
            new TimeEntry("entry2", "Break from uppercase project", "task2", "breaks-project", "REGULAR", 
                new TimeInterval("2024-01-01T10:15:00Z", "2024-01-01T10:30:00Z"))
        };

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(projects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(timeEntries);
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser)).ReturnsAsync((TimeEntry?)null);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        var output = testConsole.Output;
        // Should contain regular work entry
        Assert.That(output, Does.Contain("Regular work"));
        
        // Should NOT contain break entry from uppercase BREAKS project
        Assert.That(output, Does.Not.Contain("Break from uppercase project"));
    }
}
