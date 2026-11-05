namespace Aspire.Hosting;

/// <summary>Configures a Commander companion resource.</summary>
public sealed class CommanderOptions
{
    /// <summary>The host directory exposed in Commander. Defaults to the AppHost directory.</summary>
    public string? Path { get; set; }

    /// <summary>The companion resource name. Defaults to &lt;resource-name&gt;-commander.</summary>
    public string? ResourceName { get; set; }

    /// <summary>The initial terminal width.</summary>
    public int Columns { get; set; } = 140;

    /// <summary>The initial terminal height.</summary>
    public int Rows { get; set; } = 40;
}
