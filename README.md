# TfsEd

Cross-platform command-line client for **TFVC** (Team Foundation Version Control) on
Azure DevOps Server / TFS — a .NET alternative to the Java-based
[Team Explorer Everywhere](https://github.com/JetBrains/team-explorer-everywhere).

Runs natively on Windows x64, Linux x64/arm64 and macOS x64/arm64 as a single
self-contained executable (no .NET or Java installation required).

> **Status:** early development. Reading works (`get`, `status`, `diff`, `history`, …);
> pending changes and check-in (`add`, `delete`, `checkin`, …) are in progress.

## Installation

Download the archive for your platform from the
[releases](../../releases), extract it and put `tfsed` on your `PATH`.

On macOS, remove the quarantine flag after download:

```sh
xattr -d com.apple.quarantine tfsed
```

## Getting started

1. Create a personal access token (PAT) in the web UI: *User settings → Personal access tokens*.
   Required scopes: **Code (Read & write)**; optionally **Work Items (Read)**.
2. Log in — the token is verified and stored in the OS keychain:

   ```sh
   tfsed login https://tfs.example.com/DefaultCollection
   ```

3. Check the connection:

   ```sh
   tfsed info
   ```

## Working with a workspace

A workspace maps one server folder to one local directory. Its state is kept in a
`.tf` directory at the workspace root — no server-side workspace is created, and
`status` works offline.

```sh
tfsed workspace create '$/Project/Main' ~/src/main   # quote $/ paths in bash/zsh
cd ~/src/main
tfsed get                  # download the latest version
tfsed get -v C1234         # ... or a specific changeset
tfsed status               # M = modified, ! = missing, ? = not under version control
tfsed diff                 # unified diff of local modifications
tfsed history -n 10        # changesets affecting the current directory
tfsed changeset 1234       # details of a changeset
tfsed dir '$/Project'      # list server items
```

`get` never overwrites local modifications; they are reported as conflicts.
Use `get --force` to replace them with the server version, or `get --preview`
to see what would happen. Untracked files can be excluded from `status` with
`.tfignore` files (same syntax as Visual Studio).

| Command | Description |
|---------|-------------|
| `login`, `logout`, `info` | manage credentials, show connection details |
| `workspace create\|list\|delete` | manage local workspaces (`delete` keeps the files) |
| `get [path] [-v C123] [--force] [--preview]` | synchronize with the server |
| `status [path]` | local changes |
| `diff [path]` | local modifications as unified diff |
| `history [path] [-n N]` | changeset history |
| `changeset <id>` | changeset details |
| `dir [path] [-r] [-v C123]` | server folder listing |

### Credential storage

| OS      | Store                                                        |
|---------|--------------------------------------------------------------|
| Windows | Windows Credential Manager                                   |
| macOS   | login keychain                                               |
| Linux   | Secret Service via `secret-tool` (libsecret); without a desktop session a user-only file `~/.config/tfsed/credentials.json` (mode 0600) |

For CI or scripts, set `TFSED_PAT` (and optionally `TFSED_COLLECTION`) instead of logging in.

### Servers with an internal certificate authority

If the server certificate is issued by a company CA, either install the root CA
certificate into the system trust store, or pass it at login:

```sh
tfsed login https://tfs.example.com/DefaultCollection --ca-cert ./company-root-ca.pem
```

TLS validation is never disabled.

## Building

Requires the .NET 10 SDK.

```sh
dotnet test TfsEd.slnx
dotnet publish src/TfsEd.Cli -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
```

## License

MIT
