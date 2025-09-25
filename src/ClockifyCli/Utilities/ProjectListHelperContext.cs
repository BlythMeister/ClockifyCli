using ClockifyCli.Models;
using ClockifyCli.Services;
using Spectre.Console;
using System.Threading.Tasks;

namespace ClockifyCli.Utilities
{
    public class ProjectListHelperContext
    {
        public IClockifyClient ClockifyClient { get; }
        public IAnsiConsole Console { get; }
        public WorkspaceInfo Workspace { get; }
        public AppConfiguration Config { get; }
        public UserInfo User { get; }

        public ProjectListHelperContext(IClockifyClient clockifyClient, IAnsiConsole console, WorkspaceInfo workspace, AppConfiguration config, UserInfo user)
        {
            ClockifyClient = clockifyClient;
            Console = console;
            Workspace = workspace;
            Config = config;
            User = user;
        }
    }
}
