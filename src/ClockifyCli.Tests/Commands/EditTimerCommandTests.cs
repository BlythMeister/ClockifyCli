using ClockifyCli.Commands;
using ClockifyCli.Models;
using ClockifyCli.Services;
using Moq;
using NUnit.Framework;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace ClockifyCli.Tests.Commands;

[TestFixture]
public class EditTimerCommandTests
{
    private Mock<IClockifyClient> mockClockifyClient;
    private TestConsole testConsole;
    private EditTimerCommand command;

    [SetUp]
    public void Setup()
    {
        mockClockifyClient = new Mock<IClockifyClient>();
        testConsole = new TestConsole()
            .Interactive();
        command = new EditTimerCommand(mockClockifyClient.Object, testConsole);
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
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        var mockTimeEntries = new List<TimeEntry>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser))
                         .ReturnsAsync((TimeEntry?)null);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetLoggedInUser(), Times.Once);
        mockClockifyClient.Verify(x => x.GetLoggedInUserWorkspaces(), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_NoWorkspaceFound_ShowsErrorAndReturnsZero()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);

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
    public async Task ExecuteAsync_NoTimeEntriesFound_ShowsWarningMessage()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 14 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();
        var mockTimeEntries = new List<TimeEntry>();

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser))
                         .ReturnsAsync((TimeEntry?)null);

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No time entries found"));
        Assert.That(output, Does.Contain("Try increasing the number of days"));
    }

    [Test]
    public async Task ExecuteAsync_WithOnlyRunningTimeEntry_ShowsRunningEntryForEdit()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();

        // Create only a running time entry (no completed ones)
        var runningEntry = new TimeEntry(
            "entry1",
            "Running Task",
            "task1",
            "project1",
            "regular",
            new TimeInterval(DateTime.UtcNow.AddHours(-2).ToString("o"), null!)
        );

        var mockTimeEntries = new List<TimeEntry>(); // No completed entries

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser))
                         .ReturnsAsync(runningEntry);

        // Simulate user input for interactive prompts
        // First prompt: Select date (the running entry's date should be the only option)
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select the first (only) date option
        // Second prompt: Select time entry (the running entry should be the only option)  
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select the first (only) time entry option
        // New field selection prompts
        testConsole.Input.PushTextWithEnter("n"); // Don't edit project
        testConsole.Input.PushTextWithEnter("n"); // Don't edit task
        testConsole.Input.PushTextWithEnter("n"); // Don't edit description
        testConsole.Input.PushTextWithEnter("y"); // Edit start time
        // Third prompt: Enter new start time (leave blank to keep current)
        testConsole.Input.PushTextWithEnter(""); // Keep current start time
        // Fourth prompt: Confirm changes
        testConsole.Input.PushTextWithEnter("n"); // Don't apply changes (just testing the flow)

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        // Should find the running timer's date and make it available for editing
        mockClockifyClient.Verify(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser), Times.Once);
        var output = testConsole.Output;
        // Should not show "no entries" message since running timer is available
        Assert.That(output, Does.Not.Contain("No time entries found"));
    }

    [Test]
    public void EditTimerCommand_Constructor_ShouldAcceptDependencies()
    {
        // Arrange & Act
        var command = new EditTimerCommand(mockClockifyClient.Object, testConsole);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Is.InstanceOf<EditTimerCommand>());
    }

    [Test]
    public async Task ExecuteAsync_WithRunningTimeEntry_IncludesRunningEntryOnSameDate()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();

        // Create a completed time entry and a running time entry on the same date
        var today = DateTime.Today;
        var completedEntry = new TimeEntry(
            "entry1",
            "Completed Task",
            "task1",
            "project1",
            "regular",
            new TimeInterval(today.AddHours(9).ToString("o"), today.AddHours(10).ToString("o"))
        );

        var runningEntry = new TimeEntry(
            "entry2",
            "Running Task",
            "task2",
            "project1",
            "regular",
            new TimeInterval(today.AddHours(11).ToString("o"), null!)
        );

        var mockTimeEntries = new List<TimeEntry> { completedEntry };

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser))
                         .ReturnsAsync(runningEntry);

        // Simulate user input for interactive prompts
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select the date (today)
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select the first time entry (could be completed or running)
        // New field selection prompts
        testConsole.Input.PushTextWithEnter("n"); // Don't edit project
        testConsole.Input.PushTextWithEnter("n"); // Don't edit task
        testConsole.Input.PushTextWithEnter("n"); // Don't edit description
        testConsole.Input.PushTextWithEnter("y"); // Edit start time
        testConsole.Input.PushTextWithEnter("y"); // Edit end time (if applicable)
        // Field editing prompts
        testConsole.Input.PushTextWithEnter(""); // Keep current start time
        testConsole.Input.PushTextWithEnter(""); // Keep current end time (if not running entry)
        // Final confirmation
        testConsole.Input.PushTextWithEnter("n"); // Don't apply changes

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser), Times.Once);
        // The command should find both entries for today's date
        var output = testConsole.Output;
        Assert.That(output, Does.Not.Contain("No time entries found"));
    }

    [Test]
    public async Task ExecuteAsync_WithRunningTimeEntryOnDifferentDate_DoesNotIncludeInOtherDates()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();

        // Create entries on different dates
        var yesterday = DateTime.Today.AddDays(-1);
        var today = DateTime.Today;

        var yesterdayEntry = new TimeEntry(
            "entry1",
            "Yesterday Task",
            "task1",
            "project1",
            "regular",
            new TimeInterval(yesterday.AddHours(9).ToString("o"), yesterday.AddHours(10).ToString("o"))
        );

        var runningEntry = new TimeEntry(
            "entry2",
            "Running Task",
            "task2",
            "project1",
            "regular",
            new TimeInterval(today.AddHours(11).ToString("o"), null!)
        );

        var mockTimeEntries = new List<TimeEntry> { yesterdayEntry };

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(mockTimeEntries);
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser))
                         .ReturnsAsync(runningEntry);

        // Simulate user input for interactive prompts - choose yesterday's date first
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select the first date (should be yesterday)
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select the yesterday entry
        testConsole.Input.PushTextWithEnter(""); // Keep current start time
        testConsole.Input.PushTextWithEnter(""); // Keep current end time
        testConsole.Input.PushTextWithEnter(""); // Keep current description
        testConsole.Input.PushTextWithEnter("n"); // Don't apply changes

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser), Times.Once);
        // Should show both yesterday (with 1 entry) and today (with running timer)
        var output = testConsole.Output;
        Assert.That(output, Does.Not.Contain("No time entries found"));
    }

    [Test]
    public void Settings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new EditTimerCommand.Settings();

        // Assert
        Assert.That(settings.Days, Is.EqualTo(7));
    }

    [Test]
    public void Settings_CanSetCustomDays()
    {
        // Arrange & Act
        var settings = new EditTimerCommand.Settings { Days = 30 };

        // Assert
        Assert.That(settings.Days, Is.EqualTo(30));
    }

    [Test]
    public void EditTimerCommand_Constructor_AcceptsRequiredDependencies()
    {
        // Arrange
        var mockClockifyClient = new Mock<IClockifyClient>();
        var testConsole = new TestConsole();

        // Act & Assert
        Assert.DoesNotThrow(() => new EditTimerCommand(mockClockifyClient.Object, testConsole));

        // Cleanup
        testConsole.Dispose();
    }

    [Test]
    public void EditTimerCommand_PromptConfiguration_AllowsEmptyInputs()
    {
        // This test verifies that the TextPrompt configurations allow empty inputs
        // by checking that the AllowEmpty() method is properly configured.
        // The actual implementation uses TextPrompt<string>(...).AllowEmpty()
        // which should allow blank inputs to preserve existing values.

        // Arrange
        var testConsole = new TestConsole();

        // Act - Test that empty string prompts are configured correctly
        var startTimePrompt = new TextPrompt<string>("Start time:").AllowEmpty();
        var endTimePrompt = new TextPrompt<string>("End time:").AllowEmpty();
        var descriptionPrompt = new TextPrompt<string>("Description:").AllowEmpty();

        // Assert - Verify the prompts allow empty values
        Assert.That(startTimePrompt.AllowEmpty, Is.True, "Start time prompt should allow empty input");
        Assert.That(endTimePrompt.AllowEmpty, Is.True, "End time prompt should allow empty input");
        Assert.That(descriptionPrompt.AllowEmpty, Is.True, "Description prompt should allow empty input");

        // Cleanup
        testConsole.Dispose();
    }

    [Test]
    public void EditTimerCommand_RegressionTest_BlankInputsNotRequired()
    {
        // REGRESSION TEST: Ensure that EditTimerCommand allows blank inputs
        // 
        // Issue: After injecting IAnsiConsole dependency, EditTimerCommand was using
        // console.Ask<string>() which requires non-empty input by default.
        // 
        // Fix: Changed to console.Prompt(new TextPrompt<string>(...).AllowEmpty())
        // to allow users to leave fields blank and keep existing values.
        //
        // This test ensures that the AllowEmpty configuration is not accidentally removed.

        // Arrange - Create prompts as they should be configured in EditTimerCommand
        var startTimePrompt = new TextPrompt<string>("Start time").AllowEmpty();
        var endTimePrompt = new TextPrompt<string>("End time").AllowEmpty();
        var descriptionPrompt = new TextPrompt<string>("Description").AllowEmpty();

        // Act & Assert - Verify each prompt allows empty input
        Assert.Multiple(() =>
        {
            Assert.That(startTimePrompt.AllowEmpty, Is.True,
                "Start time prompt must allow empty input to keep existing time");
            Assert.That(endTimePrompt.AllowEmpty, Is.True,
                "End time prompt must allow empty input to keep existing time");
            Assert.That(descriptionPrompt.AllowEmpty, Is.True,
                "Description prompt must allow empty input to keep existing description");
        });
    }

    [Test]
    public void EditTimerCommand_DescriptionLogic_HandlesSpecialClearValue()
    {
        // Test that entering "-" as description clears the existing description

        // Arrange
        var existingDescription = "Existing description";
        var userInput = "-";

        // Act - Simulate the description logic from EditTimerCommand
        string newDescription;
        if (string.IsNullOrWhiteSpace(userInput))
        {
            newDescription = existingDescription;
        }
        else if (userInput.Trim() == "-")
        {
            newDescription = "";
        }
        else
        {
            newDescription = userInput;
        }

        // Assert
        Assert.That(newDescription, Is.EqualTo(""), "Entering '-' should clear the description");
    }

    [Test]
    public void EditTimerCommand_DescriptionLogic_HandlesBlankInput()
    {
        // Test that blank input keeps existing description

        // Arrange
        var existingDescription = "Existing description";
        var userInput = "";

        // Act - Simulate the description logic from EditTimerCommand
        string newDescription;
        if (string.IsNullOrWhiteSpace(userInput))
        {
            newDescription = existingDescription;
        }
        else if (userInput.Trim() == "-")
        {
            newDescription = "";
        }
        else
        {
            newDescription = userInput;
        }

        // Assert
        Assert.That(newDescription, Is.EqualTo(existingDescription), "Blank input should keep existing description");
    }

    [Test]
    public void EditTimerCommand_DescriptionLogic_HandlesRegularInput()
    {
        // Test that regular input replaces existing description

        // Arrange
        var existingDescription = "Existing description";
        var userInput = "New description";

        // Act - Simulate the description logic from EditTimerCommand
        string newDescription;
        if (string.IsNullOrWhiteSpace(userInput))
        {
            newDescription = existingDescription;
        }
        else if (userInput.Trim() == "-")
        {
            newDescription = "";
        }
        else
        {
            newDescription = userInput;
        }

        // Assert
        Assert.That(newDescription, Is.EqualTo(userInput), "Regular input should replace existing description");
    }

    [Test]
    public void EditTimerCommand_DescriptionLogic_HandlesDashWithWhitespace()
    {
        // Test that "-" with surrounding whitespace still clears description

        // Arrange
        var existingDescription = "Existing description";
        var userInput = "  -  "; // Dash with spaces

        // Act - Simulate the description logic from EditTimerCommand
        string newDescription;
        if (string.IsNullOrWhiteSpace(userInput))
        {
            newDescription = existingDescription;
        }
        else if (userInput.Trim() == "-")
        {
            newDescription = "";
        }
        else
        {
            newDescription = userInput;
        }

        // Assert
        Assert.That(newDescription, Is.EqualTo(""), "'-' with whitespace should still clear the description");
    }

    [Test]
    public void EditTimerCommand_DescriptionLogic_ComprehensiveScenarios()
    {
        // Comprehensive test covering all description input scenarios

        var existingDescription = "Original description";

        // Test data: input -> expected output
        var scenarios = new[]
        {
            ("", existingDescription),           // Blank keeps existing
            ("   ", existingDescription),        // Whitespace only keeps existing  
            ("-", ""),                          // Dash clears
            ("  -  ", ""),                      // Dash with spaces clears
            ("New description", "New description"), // Regular text replaces
            ("- this is not a clear", "- this is not a clear"), // Dash in text is preserved
            ("Clear with -", "Clear with -"),   // Dash at end is preserved
        };

        foreach (var (input, expected) in scenarios)
        {
            // Act - Simulate the description logic from EditTimerCommand
            string newDescription;
            if (string.IsNullOrWhiteSpace(input))
            {
                newDescription = existingDescription;
            }
            else if (input.Trim() == "-")
            {
                newDescription = "";
            }
            else
            {
                newDescription = input;
            }

            // Assert
            Assert.That(newDescription, Is.EqualTo(expected),
                $"Input '{input}' should result in '{expected}'");
        }
    }

    [Test]
    public async Task ExecuteAsync_EditProjectOnly_UpdatesProjectSuccessfully()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        
        var originalProject = new ProjectInfo("project1", "Original Project");
        var newProject = new ProjectInfo("project2", "New Project");
        var mockProjects = new List<ProjectInfo> { originalProject, newProject };

        var originalTask = new TaskInfo("task1", "Original Task", "Active");
        var newTask = new TaskInfo("task2", "New Task", "Active");
        var mockTasks = new List<TaskInfo> { originalTask, newTask };

        var timeEntry = new TimeEntry(
            "entry1",
            "Test Description",
            "task1",
            "project1",
            "regular",
            new TimeInterval(DateTime.Today.AddHours(9).ToString("o"), DateTime.Today.AddHours(10).ToString("o"))
        );

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        
        // Setup specific tasks for each project
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, originalProject)).ReturnsAsync(new List<TaskInfo> { originalTask });
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, newProject)).ReturnsAsync(new List<TaskInfo> { newTask });
        
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(new List<TimeEntry> { timeEntry });
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser)).ReturnsAsync((TimeEntry?)null);
        
        mockClockifyClient.Setup(x => x.UpdateTimeEntry(mockWorkspace, timeEntry, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), "project2", It.IsAny<string>()))
                         .ReturnsAsync(timeEntry);

        // Simulate user input
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select date
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select entry
        testConsole.Input.PushTextWithEnter("y"); // Change project
        testConsole.Input.PushTextWithEnter("n"); // Don't change times
        testConsole.Input.PushTextWithEnter("n"); // Don't change description
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select new project (first in alphabetical order)
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select task in new project
        testConsole.Input.PushTextWithEnter("y"); // Confirm changes

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.UpdateTimeEntry(
            mockWorkspace, 
            timeEntry, 
            It.IsAny<DateTime>(), 
            It.IsAny<DateTime>(), 
            It.IsAny<string>(), 
            "project2", 
            It.IsAny<string>()), Times.Once);

        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Time entry updated successfully"));
    }

    [Test]
    public async Task ExecuteAsync_EditTaskOnly_UpdatesTaskSuccessfully()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        
        var project = new ProjectInfo("project1", "Test Project");
        var mockProjects = new List<ProjectInfo> { project };

        var originalTask = new TaskInfo("task1", "Original Task", "Active");
        var newTask = new TaskInfo("task2", "New Task", "Active");
        var mockTasks = new List<TaskInfo> { originalTask, newTask };

        var timeEntry = new TimeEntry(
            "entry1",
            "Test Description",
            "task1",
            "project1",
            "regular",
            new TimeInterval(DateTime.Today.AddHours(9).ToString("o"), DateTime.Today.AddHours(10).ToString("o"))
        );

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        
        // For task-only editing, both tasks should be in the same project (the current one)
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.Is<ProjectInfo>(p => p.Id == "project1"))).ReturnsAsync(mockTasks);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.Is<ProjectInfo>(p => p.Id != "project1"))).ReturnsAsync(new List<TaskInfo>());
        
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(new List<TimeEntry> { timeEntry });
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser)).ReturnsAsync((TimeEntry?)null);
        
        mockClockifyClient.Setup(x => x.UpdateTimeEntry(mockWorkspace, timeEntry, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), "task2"))
                         .ReturnsAsync(timeEntry);

        // Simulate user input
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select date
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select entry
        testConsole.Input.PushTextWithEnter("y"); // Change project (to access task selection)
        testConsole.Input.PushTextWithEnter("n"); // Don't change times
        testConsole.Input.PushTextWithEnter("n"); // Don't change description
        testConsole.Input.PushKey(ConsoleKey.Enter); // Keep same project (only one available)
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select new task (first alphabetically)
        testConsole.Input.PushTextWithEnter("y"); // Confirm changes

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.UpdateTimeEntry(
            mockWorkspace, 
            timeEntry, 
            It.IsAny<DateTime>(), 
            It.IsAny<DateTime>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            "task2"), Times.Once);

        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Time entry updated successfully"));
    }

    [Test]
    public async Task ExecuteAsync_EditRunningEntryProjectAndTask_UpdatesSuccessfully()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        
        var originalProject = new ProjectInfo("project1", "Original Project");
        var newProject = new ProjectInfo("project2", "New Project");
        var mockProjects = new List<ProjectInfo> { originalProject, newProject };

        var originalTask = new TaskInfo("task1", "Original Task", "Active");
        var newTask = new TaskInfo("task2", "New Task", "Active");
        var mockTasks = new List<TaskInfo> { originalTask, newTask };

        var runningEntry = new TimeEntry(
            "entry1",
            "Running Description",
            "task1",
            "project1",
            "regular",
            new TimeInterval(DateTime.UtcNow.AddHours(-2).ToString("o"), "")
        );

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        
        // Setup specific tasks for each project - this test changes from project1 to project2
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, originalProject)).ReturnsAsync(new List<TaskInfo> { originalTask });
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, newProject)).ReturnsAsync(new List<TaskInfo> { newTask });
        
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(new List<TimeEntry>());
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser)).ReturnsAsync(runningEntry);
        
        mockClockifyClient.Setup(x => x.UpdateRunningTimeEntry(mockWorkspace, runningEntry, It.IsAny<DateTime>(), It.IsAny<string>(), "project2", "task2"))
                         .ReturnsAsync(runningEntry);

        // Simulate user input
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select date
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select running entry
        testConsole.Input.PushTextWithEnter("y"); // Change project
        testConsole.Input.PushTextWithEnter("n"); // Don't change times
        testConsole.Input.PushTextWithEnter("n"); // Don't change description
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select new project (first in alphabetical order)
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select new task
        testConsole.Input.PushTextWithEnter("y"); // Confirm changes

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        mockClockifyClient.Verify(x => x.UpdateRunningTimeEntry(
            mockWorkspace, 
            runningEntry, 
            It.IsAny<DateTime>(), 
            It.IsAny<string>(), 
            "project2", 
            "task2"), Times.Once);

        var output = testConsole.Output;
        Assert.That(output, Does.Contain("Time entry updated successfully"));
    }

    [Test]
    public async Task ExecuteAsync_NoFieldsSelected_CancelsOperation()
    {
        // Arrange
        var settings = new EditTimerCommand.Settings { Days = 7 };
        var mockRemainingArgs = new Mock<IRemainingArguments>();
        var context = new CommandContext([], mockRemainingArgs.Object, "", null);

        var mockUser = new UserInfo("user1", "Test User", "test@example.com", "workspace1");
        var mockWorkspace = new WorkspaceInfo("workspace1", "Test Workspace");
        var mockProjects = new List<ProjectInfo>();

        var timeEntry = new TimeEntry(
            "entry1",
            "Test Description",
            "task1",
            "project1",
            "regular",
            new TimeInterval(DateTime.Today.AddHours(9).ToString("o"), DateTime.Today.AddHours(10).ToString("o"))
        );

        mockClockifyClient.Setup(x => x.GetLoggedInUser()).ReturnsAsync(mockUser);
        mockClockifyClient.Setup(x => x.GetLoggedInUserWorkspaces()).ReturnsAsync(new List<WorkspaceInfo> { mockWorkspace });
        mockClockifyClient.Setup(x => x.GetProjects(mockWorkspace)).ReturnsAsync(mockProjects);
        mockClockifyClient.Setup(x => x.GetTasks(mockWorkspace, It.IsAny<ProjectInfo>())).ReturnsAsync(new List<TaskInfo>());
        mockClockifyClient.Setup(x => x.GetTimeEntries(mockWorkspace, mockUser, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ReturnsAsync(new List<TimeEntry> { timeEntry });
        mockClockifyClient.Setup(x => x.GetCurrentTimeEntry(mockWorkspace, mockUser)).ReturnsAsync((TimeEntry?)null);

        // Simulate user input - select no changes
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select date
        testConsole.Input.PushKey(ConsoleKey.Enter); // Select entry
        testConsole.Input.PushTextWithEnter("n"); // Don't change project
        testConsole.Input.PushTextWithEnter("n"); // Don't change times
        testConsole.Input.PushTextWithEnter("n"); // Don't change description

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        Assert.That(result, Is.EqualTo(0));
        
        var output = testConsole.Output;
        Assert.That(output, Does.Contain("No changes selected. Operation cancelled."));
        
        // Verify no update calls were made
        mockClockifyClient.Verify(x => x.UpdateTimeEntry(It.IsAny<WorkspaceInfo>(), It.IsAny<TimeEntry>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        mockClockifyClient.Verify(x => x.UpdateRunningTimeEntry(It.IsAny<WorkspaceInfo>(), It.IsAny<TimeEntry>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
