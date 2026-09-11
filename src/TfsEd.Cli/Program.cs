using TfsEd.Cli;

return await CliApp.Create(CliServices.CreateDefault()).Parse(args).InvokeAsync();
