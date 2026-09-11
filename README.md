# TfsEd

Cross-platform command-line client for **TFVC** (Team Foundation Version Control) on
Azure DevOps Server / TFS — a .NET alternative to the Java-based
[Team Explorer Everywhere](https://github.com/JetBrains/team-explorer-everywhere).

Runs natively on Windows x64, Linux x64/arm64 and macOS x64/arm64 as a single
self-contained executable (no .NET or Java installation required).

> **Status:** early development. Available: `login`, `logout`, `info`.
> Workspace commands (`get`, `status`, `checkin`, …) are in progress.

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
