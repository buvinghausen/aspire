// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.Dcp;

internal sealed class DcpOptions
{
    /// <summary>
    /// The path to the DCP executable used for Aspire orchestration
    /// </summary>
    /// <example>
    /// C:\Program Files\dotnet\packs\Aspire.Hosting.Orchestration.win-x64\8.0.0-preview.1.23518.6\tools\dcp.exe
    /// </example>
    public string? CliPath { get; set; }

    /// <summary>
    /// Optional path to a folder containing the DCP extension assemblies (dcpctrl, etc.).
    /// </summary>
    /// <example>
    /// C:\Program Files\dotnet\packs\Aspire.Hosting.Orchestration.win-x64\8.0.0-preview.1.23518.6\tools\ext\
    /// </example>
    public string? ExtensionsPath { get; set; }

    /// <summary>
    /// Optional path to a folder containing the Aspire Dashboard binaries.
    /// </summary>
    /// <example>
    /// When running the playground applications in this repo: <c>..\..\..\artifacts\bin\Aspire.Dashboard\Debug\net8.0\Aspire.Dashboard.dll</c>
    /// </example>
    public string? DashboardPath { get; set; }

    /// <summary>
    /// Optional path to a folder containing additional DCP binaries.
    /// </summary>
    /// <example>
    /// C:\Program Files\dotnet\packs\Aspire.Hosting.Orchestration.win-x64\8.0.0-preview.1.23518.6\tools\ext\bin\
    /// </example>
    public string? BinPath { get; set; }

    /// <summary>
    /// Optional container runtime to override default runtime for DCP containers.
    /// </summary>
    /// <example>
    /// podman
    /// </example>
    public string? ContainerRuntime { get; set; }

    /// <summary>
    /// How long the dependency check will wait (in seconds) for a response before timing out.
    /// Timeout is disabled if set to zero or a negative value.
    /// </summary>
    public int DependencyCheckTimeout { get; set; } = 25;

    /// <summary>
    /// The suffix to use for resource names when creating resources in DCP.
    /// </summary>
    public string? ResourceNameSuffix { get; set; }

    /// <summary>
    /// Whether to randomize ports used by resources during orchestration.
    /// </summary>
    public bool RandomizePorts { get; set; }

    public int KubernetesConfigReadRetryCount { get; set; } = 300;

    public int KubernetesConfigReadRetryIntervalMilliseconds { get; set; } = 100;

    /// <summary>
    /// The duration to wait for the container runtime to become healthy before aborting startup.
    /// </summary>
    /// <remarks>
    /// A value of zero, which is the default value, indicates that the application will not wait for the container
    /// runtime to become healthy.
    /// If this property has a value greater than zero, the application will abort startup if the container runtime
    /// does not become healthy within the specified timeout.
    /// </remarks>
    public TimeSpan ContainerRuntimeInitializationTimeout { get; set; }

    public TimeSpan ServiceStartupWatchTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Whether to wait for resource cleanup to end when stopping DcpExecutor.
    /// This guarantees that application resources (programs, transient containers etc.) are stopped
    /// before DcpExecutor.StopAsync() returns. Default is false (resources are cleaned up asynchronously).
    /// </summary>
    public bool WaitForResourceCleanup { get; set; }

    /// <summary>
    /// Gets or sets the suffix to use for DCP log file names (applicable when verbose DCP logging is enabled).
    /// By default log file name suffix defaults to the current process ID.
    /// </summary>
    public string? LogFileNameSuffix { get; set; }
}

internal class ValidateDcpOptions : IValidateOptions<DcpOptions>
{
    public ValidateOptionsResult Validate(string? name, DcpOptions options)
    {
        var builder = new ValidateOptionsResultBuilder();

        if (string.IsNullOrEmpty(options.CliPath))
        {
            builder.AddError("The path to the DCP executable used for Aspire orchestration is required.", "CliPath");
        }

        if (string.IsNullOrEmpty(options.DashboardPath))
        {
            builder.AddError("The path to the Aspire Dashboard binaries is missing.", "DashboardPath");
        }

        return builder.Build();
    }
}

internal class ConfigureDefaultDcpOptions(
    DistributedApplicationOptions appOptions,
    IConfiguration configuration) : IConfigureOptions<DcpOptions>
{
    private const string DcpCliPathMetadataKey = "DcpCliPath";
    private const string DcpExtensionsPathMetadataKey = "DcpExtensionsPath";
    private const string DcpBinPathMetadataKey = "DcpBinPath";
    private const string DashboardPathMetadataKey = "aspiredashboardpath";

    public static string DcpPublisher = nameof(DcpPublisher);

    public void Configure(DcpOptions options)
    {
        var dcpPublisherConfiguration = configuration.GetSection(DcpPublisher);
        var assemblyMetadata = appOptions.Assembly?.GetCustomAttributes<AssemblyMetadataAttribute>();

        if (!string.IsNullOrEmpty(dcpPublisherConfiguration[nameof(options.CliPath)]))
        {
            // If an explicit path to DCP was provided from configuration, don't try to resolve via assembly attributes
            options.CliPath = dcpPublisherConfiguration[nameof(options.CliPath)];
            if (Path.GetDirectoryName(options.CliPath) is string dcpDir && !string.IsNullOrEmpty(dcpDir))
            {
                options.ExtensionsPath = Path.Combine(dcpDir, "ext");
                options.BinPath = Path.Combine(options.ExtensionsPath, "bin");
            }
        }
        else
        {
            options.CliPath = GetMetadataValue(assemblyMetadata, DcpCliPathMetadataKey);
            options.ExtensionsPath = GetMetadataValue(assemblyMetadata, DcpExtensionsPathMetadataKey);
            options.BinPath = GetMetadataValue(assemblyMetadata, DcpBinPathMetadataKey);
        }

        if (!string.IsNullOrEmpty(dcpPublisherConfiguration[nameof(options.DashboardPath)]))
        {
            // If an explicit path to DCP was provided from configuration, don't try to resolve via assembly attributes
            options.DashboardPath = dcpPublisherConfiguration[nameof(options.DashboardPath)];
        }
        else
        {
            options.DashboardPath = GetMetadataValue(assemblyMetadata, DashboardPathMetadataKey);
        }

        if (!string.IsNullOrEmpty(dcpPublisherConfiguration[nameof(options.ContainerRuntime)]))
        {
            options.ContainerRuntime = dcpPublisherConfiguration[nameof(options.ContainerRuntime)];
        }
        else
        {
            options.ContainerRuntime = configuration.GetString(KnownConfigNames.ContainerRuntime, KnownConfigNames.Legacy.ContainerRuntime);
        }

        if (!string.IsNullOrEmpty(dcpPublisherConfiguration[nameof(options.DependencyCheckTimeout)]))
        {
            if (int.TryParse(dcpPublisherConfiguration[nameof(options.DependencyCheckTimeout)], out var timeout))
            {
                options.DependencyCheckTimeout = timeout;
            }
            else
            {
                throw new InvalidOperationException($"Invalid value \"{dcpPublisherConfiguration[nameof(options.DependencyCheckTimeout)]}\" for \"--dcp-dependency-check-timeout\". Expected an integer value.");
            }
        }
        else
        {
            options.DependencyCheckTimeout = configuration.GetValue(KnownConfigNames.DependencyCheckTimeout, KnownConfigNames.Legacy.DependencyCheckTimeout, options.DependencyCheckTimeout);
        }

        options.KubernetesConfigReadRetryCount = dcpPublisherConfiguration.GetValue(nameof(options.KubernetesConfigReadRetryCount), options.KubernetesConfigReadRetryCount);
        options.KubernetesConfigReadRetryIntervalMilliseconds = dcpPublisherConfiguration.GetValue(nameof(options.KubernetesConfigReadRetryIntervalMilliseconds), options.KubernetesConfigReadRetryIntervalMilliseconds);

        if (!string.IsNullOrEmpty(dcpPublisherConfiguration[nameof(options.ResourceNameSuffix)]))
        {
            options.ResourceNameSuffix = dcpPublisherConfiguration[nameof(options.ResourceNameSuffix)];
        }

        options.RandomizePorts = dcpPublisherConfiguration.GetValue(nameof(options.RandomizePorts), options.RandomizePorts);
        options.WaitForResourceCleanup = dcpPublisherConfiguration.GetValue(nameof(options.WaitForResourceCleanup), options.WaitForResourceCleanup);
        options.ServiceStartupWatchTimeout = configuration.GetValue(KnownConfigNames.ServiceStartupWatchTimeout, KnownConfigNames.Legacy.ServiceStartupWatchTimeout, options.ServiceStartupWatchTimeout);
        options.ContainerRuntimeInitializationTimeout = dcpPublisherConfiguration.GetValue(nameof(options.ContainerRuntimeInitializationTimeout), options.ContainerRuntimeInitializationTimeout);
        options.LogFileNameSuffix = dcpPublisherConfiguration[nameof(options.LogFileNameSuffix)];
    }

    private static string? GetMetadataValue(IEnumerable<AssemblyMetadataAttribute>? assemblyMetadata, string key)
    {
        return assemblyMetadata?.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
    }
}
