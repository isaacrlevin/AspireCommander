# Aspire Commander

Aspire Commander adds a Midnight Commander-inspired, dual-pane file browser to any resource in a .NET Aspire AppHost. It
runs as a terminal-enabled companion resource, so the original resource process and debugging experience remain
unchanged.

## Install

Add the hosting integration to the AppHost project:

```powershell
dotnet add package AspireCommander.Hosting --version 0.2.0
```

The package bundles the terminal application and copies it to the AppHost output (`commander/`), so no other package is
needed. It targets .NET 11 and Aspire 13.6.

## Use

Call `WithCommander()` on any Aspire resource:

```csharp
var api = builder.AddProject<Projects.Api>("api");

api.WithCommander(options =>
{
    options.Path = "../Api";
    options.Columns = 160;
    options.Rows = 45;
});
```

This adds an `api-commander` companion. Start the AppHost, select that resource in the Aspire dashboard, and open
**Terminal**. The default path is the AppHost directory. Relative paths are resolved from the AppHost directory.

The return value is the Commander companion builder, so it can be configured like another Aspire executable resource:

```csharp
var commander = api.WithCommander(options =>
{
    options.ResourceName = "api-files";
    options.Path = "../Api";
});

commander.WithEnvironment("MY_SETTING", "value");
```

Use arrow keys to move, **Enter** to open a directory or preview a file, **Backspace** to go up, **Tab** to switch
panes, **R** to refresh, and **Q** to quit.

## Filesystem scope

For container resources, Commander browses the target container through its container runtime, starting at `/`. The
browser cannot navigate above that root, so an Alpine resource shows only the Alpine container filesystem. For other
resource types, Commander continues to browse the configured host directory. On a container, `Path` is an absolute
path inside the container rather than a host path.

`WithTerminal()` is experimental in Aspire 13.6, and the hosting package acknowledges `ASPIRETERMINAL001` internally so
consuming AppHosts do not need a warning suppression.

## Pack locally

```powershell
dotnet pack src/AspireCommander.sln -c Release -o artifacts
```

The solution produces a single package, `AspireCommander.Hosting`, containing the `WithCommander()` Aspire integration
and the bundled terminal executable. `AspireCommander.Console` is an internal project and is not published separately.
