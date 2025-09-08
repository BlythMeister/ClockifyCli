## Development Instructions for ClockifyCli

This document outlines the essential rules and procedures for making changes to the ClockifyCli project.

### General Development Rules

#### Code Quality Standards
- **Clear, concise, and maintainable code** - Follow best practices and design patterns
- **Meaningful naming** - Use descriptive variable and method names
- **Documentation** - Comment complex logic and decisions
- **Exception handling** - Handle exceptions gracefully with meaningful error messages
- **Security** - Follow security best practices
- **Performance** - Consider performance implications and optimize where necessary
- **Compatibility** - Ensure changes don't introduce breaking changes without confirmation

#### Before Writing Any Code
1. **Ask clarifying questions** to understand requirements fully
2. **Consider edge cases** and potential pitfalls
3. **Think about overall architecture** and how changes fit into the larger system
4. **Consider performance implications**
5. **Create a detailed task list** and get approval
6. **Ensure branch is based on develop** (if applicable)
7. **Check for conflicting changes** on the current branch

#### Testing Requirements
- **Write unit tests** for new code to ensure correctness and reliability
- **Create test project** if one doesn't exist
- **No warnings or errors** in submitted code
- **Build verification** before committing

### Version Management

#### Files That Contain Version Numbers
When bumping versions, update ALL of the following files:

1. **`src/ClockifyCli/ClockifyCli.csproj`**
   - `<Version>X.XX</Version>`

2. **`appveyor.yml`**
   - `version: X.XX.{build}`

3. **`src/ClockifyCli/Program.cs`**
   - `config.SetApplicationVersion("X.XX");`

4. **`CHANGELOG.md`**
   - Add new version entry at the top

#### Version Number Format
- Use semantic versioning: `MAJOR.MINOR` (e.g., `1.11`)
- Increment MINOR for new features and improvements
- Increment MAJOR for breaking changes

### Commit and Release Process

#### Branch Management
- **Base changes on master** when applicable
- **Check for existing changes** before starting work

#### Commit Message Format
Use conventional commit format:
```
<type>: <description>

<optional body>

Signed-off-by: Copilot
```

Types:
- `feat`: New features
- `fix`: Bug fixes
- `docs`: Documentation changes
- `style`: Code style changes
- `refactor`: Code refactoring
- `test`: Test changes
- `chore`: Maintenance tasks

#### Pre-Commit Checklist
1. ✅ **Build succeeds** with no warnings or errors
2. ✅ **All tests pass** (if applicable)
3. ✅ **Version numbers updated** in all required files
4. ✅ **CHANGELOG.md updated** with new entry
5. ✅ **No unintended files** staged for commit
6. ✅ **Code follows project standards**

#### Build Verification Command
```bash
cd src && dotnet build --no-restore --verbosity minimal
```

#### Test Verification Command
```bash
cd src && dotnet test --verbosity normal
```

#### Commit Commands
```bash
# Stage only intended files
git add <specific-files>

# Commit with conventional format
git commit -m "feat: description of changes

Detailed explanation if needed

Signed-off-by: Copilot"

# Push to correct remote
git push _BlythMeister HEAD:master
```

### File Organization

#### Important Configuration Files
- `src/ClockifyCli/ClockifyCli.csproj` - Main project configuration
- `appveyor.yml` - CI/CD configuration
- `CHANGELOG.md` - Version history
- `README.md` - Project documentation

#### Generated Files to Ignore
- `src/*/bin/**` - Build outputs
- `src/*/obj/**` - Build intermediates
- `*.deps.json` - Dependency files
- Test and debug files (unless specifically needed)

### Common Patterns

#### Version Updates
When bumping from version X.Y to X.(Y+1):
1. Update `ClockifyCli.csproj`
2. Update `appveyor.yml`
3. Update `Program.cs`
4. Add CHANGELOG entry
5. Build and test
6. Commit and push

### Quality Gates

#### Before Submission
- [ ] Code compiles without warnings
- [ ] All tests pass
- [ ] No sensitive information in commit
- [ ] Version numbers are consistent
- [ ] CHANGELOG is updated
- [ ] Commit message follows conventions

#### Code Review Focus Areas
- User experience improvements
- Security implications
- Performance impact
- Breaking change assessment
- Test coverage adequacy

### Emergency Procedures

#### If Build Fails
1. Check compiler errors first
2. Verify all using statements
3. Check for missing dependencies
4. Ensure version numbers are valid
5. Review recent changes for syntax errors

#### If Tests Fail
1. Run tests locally to reproduce
2. Check for environment differences
3. Verify test data assumptions
4. Review recent changes affecting test scenarios

#### If Push Fails
1. Check remote configuration: `git remote -v`
2. Verify branch relationships: `git branch -vv`
3. Use explicit push: `git push _BlythMeister HEAD:master`
4. Check for merge conflicts

### Notes for AI Assistant

- Always verify file paths before making changes
- Build and test before committing
- Ask for clarification on unclear requirements
- Consider the user experience impact of all changes
- Remember to update ALL version-related files
- Never commit debug or temporary files
- Follow the established project patterns and conventions

### Additional Coding Standards

#### Dependency Injection Patterns
- **All commands must use constructor dependency injection** - No parameterless constructors
- **Register services correctly** in `Program.cs` following established patterns:
  ```csharp
  // Register concrete class first
  services.AddTransient<ClockifyClient>(provider => { /* factory logic */ });
  // Then register interface pointing to same instance  
  services.AddTransient<IClockifyClient>(provider => provider.GetRequiredService<ClockifyClient>());
  ```
- **Use proper lifetime scopes**: Transient for most services, Singleton for configuration services

#### Command Architecture
- **Inherit from BaseCommand** or `BaseCommand<TSettings>` classes
- **Return 0 for success**, non-zero for errors
- **Constructor validation**: Throw `ArgumentNullException` for null dependencies
- **Async patterns**: Use `async Task<int>` for all command execution methods

#### Error Handling Standards
- **Graceful degradation**: Handle missing workspaces, projects, tasks with user-friendly messages
- **API error handling**: Wrap HTTP client calls in try-catch with meaningful error messages
- **Configuration errors**: Guide users to run `config set` when API keys are missing
- **Time validation**: Use `IntelligentTimeParser` for all time input validation

#### Testing Standards  
- **Use NUnit framework** with `[TestFixture]` and `[Test]` attributes
- **Follow AAA pattern**: Arrange, Act, Assert
- **Mock HTTP clients** using `MockHttpMessageHandler` for external API calls
- **Use TestConsole** from Spectre.Console.Testing for console interactions
- **Proper cleanup**: Dispose mocks and HTTP clients in tests
- **SetUp/TearDown**: Use for test isolation and resource management

#### Async/Await Patterns
- **Always await async calls** - No `.GetAwaiter().GetResult()` except in DI registration
- **Use ConfigureAwait(false)** is not required in this console application
- **Proper cancellation**: Pass CancellationToken through async chains where applicable
- **Rate limiting**: Use established `RateLimiter` for API calls

#### API Client Patterns
- **HTTP client injection**: Accept `HttpClient` in constructor for testability
- **Rate limiting**: Wrap all API calls with rate limiting using `rateLimiter.WaitIfNeededAsync()`
- **Error response handling**: Check HTTP status codes and parse error responses
- **URI building**: Use relative URIs with base address configuration

#### Time and Date Handling
- **Use IClock interface** for testable time operations instead of `DateTime.Now`
- **UTC for API calls**: Always use UTC timestamps for external API interactions
- **Local time for display**: Convert to local time only for user display
- **Intelligent parsing**: Use `IntelligentTimeParser` for all user time input
- **No seconds in UI**: Never show seconds in user-facing time displays (use `HH:mm` format)

#### User Interface Standards
- **Consistent markup**: Use Spectre.Console markup consistently
- **Color coding**: 
  - `[green]` for success messages
  - `[red]` for errors  
  - `[yellow]` for warnings
  - `[dim]` for secondary information
- **Progress indication**: Use `Status()` blocks for long-running operations
- **Input validation**: Provide clear validation messages for user input

#### Project Structure Patterns
- **Namespace alignment**: Ensure namespace matches folder structure
- **Service interfaces**: Create interfaces for all services to enable mocking
- **Model immutability**: Use record types or readonly properties where appropriate
- **Configuration encryption**: Use `ConfigurationService` encryption for sensitive data

#### Debug and Development Files
- **Never commit**: Files matching patterns `debug_test.cs`, `test_parser.cs`, `test-*.md`
- **Ignore generated**: All files in `bin/`, `obj/`, `*.deps.json` directories
- **Temporary test directories**: Clean up in test teardown methods

#### Breaking Change Detection
- **Interface changes**: Any modification to public interfaces requires major version bump
- **Command parameter changes**: Changing command signatures affects CLI compatibility  
- **Configuration format changes**: May require migration logic for existing users
- **API client changes**: Could affect external integrations
