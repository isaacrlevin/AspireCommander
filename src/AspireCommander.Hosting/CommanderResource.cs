using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>A terminal-enabled Commander companion associated with another Aspire resource.</summary>
public sealed class CommanderResource(string name, string targetResourceName, string workingDirectory)
    : ExecutableResource(name, "dotnet", workingDirectory)
{
    /// <summary>The Aspire resource this companion was created for.</summary>
    public string TargetResourceName { get; } = targetResourceName;
}
