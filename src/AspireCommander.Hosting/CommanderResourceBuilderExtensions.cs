using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>Extensions for adding a Commander view to Aspire resources.</summary>
public static class CommanderResourceBuilderExtensions
{
    /// <summary>
    /// Adds a terminal-enabled Commander companion for <paramref name="builder"/>.
    /// The companion browses the AppHost directory unless <see cref="CommanderOptions.Path"/> is set.
    /// </summary>
    public static IResourceBuilder<CommanderResource> WithCommander<TResource>(
        this IResourceBuilder<TResource> builder,
        Action<CommanderOptions>? configure = null)
        where TResource : IResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new CommanderOptions();
        configure?.Invoke(options);

        if (options.Columns < 1) throw new ArgumentOutOfRangeException(nameof(options.Columns));
        if (options.Rows < 1) throw new ArgumentOutOfRangeException(nameof(options.Rows));

        var resourceName = options.ResourceName ?? $"{builder.Resource.Name}-commander";
        var container = builder.Resource as ContainerResource;
        var configuredPath = container is null
            ? options.Path ?? builder.ApplicationBuilder.AppHostDirectory
            : builder.ApplicationBuilder.AppHostDirectory;
        var workingDirectory = Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(builder.ApplicationBuilder.AppHostDirectory, configuredPath));
        if (!Directory.Exists(workingDirectory))
            throw new DirectoryNotFoundException($"Commander path '{workingDirectory}' does not exist.");

        var hostingDirectory = Path.GetDirectoryName(typeof(CommanderResource).Assembly.Location)!;
        var executable = Path.Combine(hostingDirectory, "commander", "AspireCommander.Console.dll");
        if (!File.Exists(executable))
            throw new FileNotFoundException(
                "The bundled Commander console application was not found. Reinstall the AspireCommander.Hosting package.",
                executable);
        var resource = new CommanderResource(resourceName, builder.Resource.Name, workingDirectory);
        var target = builder.ApplicationBuilder.CreateResourceBuilder<IResource>(builder.Resource);
        var commander = builder.ApplicationBuilder.AddResource(resource)
            .WithArgs("exec", executable)
            .WithEnvironment("ASPIRE_COMMANDER_TARGET", builder.Resource.Name)
            .WithParentRelationship(target)
            .WaitFor(target);

        if (container is not null)
        {
            var containerName = container.Annotations.OfType<ContainerNameAnnotation>().LastOrDefault()?.Name;
            if (string.IsNullOrWhiteSpace(containerName))
            {
                var generatedName = $"aspire-commander-{container.Name}-{Guid.NewGuid():N}";
                containerName = generatedName[..Math.Min(63, generatedName.Length)];
                container.Annotations.Add(new ContainerNameAnnotation { Name = containerName });
            }

            commander.WithEnvironment("ASPIRE_COMMANDER_CONTAINER", containerName);
            commander.WithEnvironment("ASPIRE_COMMANDER_PATH", options.Path ?? "/");
        }

#pragma warning disable ASPIRETERMINAL001
        commander.WithTerminal(terminal =>
        {
            terminal.Columns = options.Columns;
            terminal.Rows = options.Rows;
        });
#pragma warning restore ASPIRETERMINAL001

        return commander;
    }
}