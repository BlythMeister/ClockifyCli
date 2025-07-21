using ClockifyCli.Models;
using ClockifyCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public abstract class BaseCommand : AsyncCommand
{
    public abstract override Task<int> ExecuteAsync(CommandContext context);
}

public abstract class BaseCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : CommandSettings
{
    public abstract override Task<int> ExecuteAsync(CommandContext context, TSettings settings);
}
