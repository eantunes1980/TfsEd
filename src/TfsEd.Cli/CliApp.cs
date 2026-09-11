using System.CommandLine;
using TfsEd.Cli.Commands;

namespace TfsEd.Cli;

public static class CliApp
{
    public static RootCommand Create(CliServices services)
    {
        var root = new RootCommand("TfsEd - cross-platform command-line client for TFVC on Azure DevOps Server / TFS.");
        root.Options.Add(GlobalOptions.Collection);
        root.Subcommands.Add(LoginCommand.Create(services));
        root.Subcommands.Add(LogoutCommand.Create(services));
        root.Subcommands.Add(InfoCommand.Create(services));
        root.Subcommands.Add(WorkspaceCommand.Create(services));
        root.Subcommands.Add(GetCommand.Create(services));
        root.Subcommands.Add(StatusCommand.Create(services));
        root.Subcommands.Add(DiffCommand.Create(services));
        root.Subcommands.Add(HistoryCommand.Create(services));
        root.Subcommands.Add(ChangesetCommand.Create(services));
        root.Subcommands.Add(DirCommand.Create(services));
        return root;
    }
}

internal static class GlobalOptions
{
    public static readonly Option<string?> Collection = new("--collection", "-c")
    {
        Description = "Collection URL. Defaults to TFSED_COLLECTION or the collection of the last login.",
        Recursive = true,
    };

    public static Option<string?> Version() => new("--version", "-v")
    {
        Description = "Version: T (latest, default) or C<changeset>, e.g. C1234.",
    };

    public static Argument<string?> OptionalPath(string description) => new("path")
    {
        Description = description,
        Arity = ArgumentArity.ZeroOrOne,
    };
}
