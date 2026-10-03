using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;
using Serilog;
using VolumeMixerPlugin.Actions;
using VolumeMixerPlugin.Services;

namespace VolumeMixerPlugin;

public sealed class PluginIntegration : IPluginIntegration, IVariableProvider
{
	private readonly ILogger _logger;
	private AudioService? _audio;

	public PluginIntegration(ILogger logger)
	{
		_logger = logger.ForContext<PluginIntegration>();
		Actions =
		[
			new SetDefaultOutputAction(this, logger),
			new SetDefaultMicrophoneAction(this, logger),
			new SetDefaultPairAction(this, logger),
			new RefreshDevicesAction(this, logger),
		];
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	internal AudioService? Audio => _audio;

	public IReadOnlyList<VariableDefinition> Variables { get; } =
	[
		VariableDefinition.Eager("volumemixer_default_device", VariableType.Text, refreshInterval: TimeSpan.FromSeconds(2))
			with
			{
				Id = "default-device",
				DisplayName = Strings.Variables.DefaultDevice.DisplayName(),
				Description = Strings.Variables.DefaultDevice.Description(),
			},
		VariableDefinition.Eager("volumemixer_comm_device", VariableType.Text, refreshInterval: TimeSpan.FromSeconds(2))
			with
			{
				Id = "comm-device",
				DisplayName = Strings.Variables.CommDevice.DisplayName(),
				Description = Strings.Variables.CommDevice.Description(),
			},
		VariableDefinition.Eager("volumemixer_default_mic", VariableType.Text, refreshInterval: TimeSpan.FromSeconds(2))
			with
			{
				Id = "default-mic",
				DisplayName = Strings.Variables.DefaultMic.DisplayName(),
				Description = Strings.Variables.DefaultMic.Description(),
			},
		VariableDefinition.Eager("volumemixer_comm_mic", VariableType.Text, refreshInterval: TimeSpan.FromSeconds(2))
			with
			{
				Id = "comm-mic",
				DisplayName = Strings.Variables.CommMic.DisplayName(),
				Description = Strings.Variables.CommMic.Description(),
			},
	];

	public ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default)
	{
		if (_audio == null) return ValueTask.FromResult(VariableReading.Unavailable);
		return ValueTask.FromResult(localId switch
		{
			"default-device" => Reading.Of(_audio.GetDefaultMultimediaDeviceInfo()?.Name),
			"comm-device" => Reading.Of(_audio.GetDefaultCommunicationsDeviceInfo()?.Name),
			"default-mic" => Reading.Of(_audio.GetDefaultCaptureDeviceInfo()?.Name),
			"comm-mic" => Reading.Of(_audio.GetDefaultCaptureCommDeviceInfo()?.Name),
			_ => VariableReading.Unavailable,
		});
	}

	public Task InitializeAsync(IIntegrationContext context)
	{
		_audio ??= new AudioService(_logger);
		_logger.Information("Initialized.");
		return Task.CompletedTask;
	}

	public Task ShutdownAsync()
	{
		_audio?.Dispose();
		_audio = null;
		return Task.CompletedTask;
	}

	private static class Reading
	{
		internal static VariableReading Of(string? name)
			=> string.IsNullOrWhiteSpace(name) ? VariableReading.Unavailable : VariableReading.Of(name);
	}
}
