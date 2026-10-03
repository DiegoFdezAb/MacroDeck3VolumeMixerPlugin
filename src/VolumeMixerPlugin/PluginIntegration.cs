using System.Globalization;
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
			new SetAppVolumeAction(this, logger),
			new VolumeUpAction(this, logger),
			new VolumeDownAction(this, logger),
			new ToggleMuteAction(this, logger),
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

	public bool SupportsCatalog => true;

	public bool SupportsSearch => false;

	public string CatalogName => "volume-mixer-apps";

	public ValueTask<VariableCatalogPage> DiscoverAsync(VariableCatalogQuery query, CancellationToken cancellationToken = default)
	{
		if (_audio == null) return ValueTask.FromResult(VariableCatalogPage.Empty);

		var items = new List<VariableDefinition>();
		foreach (var app in _audio.GetActiveAppNames())
		{
			var safe = AudioService.SanitizeAppName(app);
			items.Add(VariableDefinition.OnDemand($"volumemixer_app_{safe}_volume", VariableType.Numeric)
				with { Id = $"app_{safe}_volume", Write = new VariableWriteCapability() });
			items.Add(VariableDefinition.OnDemand($"volumemixer_app_{safe}_muted", VariableType.Boolean)
				with { Id = $"app_{safe}_muted" });
		}

		return ValueTask.FromResult(new VariableCatalogPage { Items = items });
	}

	public ValueTask<VariableDefinition?> ResolveAsync(string localId, CancellationToken cancellationToken = default)
	{
		if (TrySplitAppId(localId, out var safe, out var kind) && safe != null && kind != null)
		{
			var name = $"volumemixer_app_{safe}_{kind}";
			var type = kind == "volume" ? VariableType.Numeric : VariableType.Boolean;
			VariableDefinition def = VariableDefinition.OnDemand(name, type) with { Id = localId };
			if (kind == "volume")
			{
				def = def with { Write = new VariableWriteCapability() };
			}

			return ValueTask.FromResult<VariableDefinition?>(def);
		}

		return ValueTask.FromResult<VariableDefinition?>(null);
	}

	public ValueTask<VariableWriteResult> SetValueAsync(string localId, object? value, CancellationToken cancellationToken = default)
	{
		if (!TrySplitAppId(localId, out var safe, out var kind) || safe == null || kind == null)
		{
			return ValueTask.FromResult(VariableWriteResult.NotFound());
		}

		if (kind != "volume")
		{
			return ValueTask.FromResult(VariableWriteResult.NotWritable());
		}

		if (value is not (double or int or long))
		{
			return ValueTask.FromResult(VariableWriteResult.InvalidValue());
		}

		if (_audio == null)
		{
			return ValueTask.FromResult(VariableWriteResult.Unavailable());
		}

		var volume = Math.Clamp(Convert.ToDouble(value, CultureInfo.InvariantCulture), 0, 100);
		foreach (var session in _audio.SnapshotDefaultDeviceSessions())
		{
			if (!string.Equals(AudioService.SanitizeAppName(session.ProcessName), safe, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			_audio.SetAppVolume(session.ProcessName, (float)volume);
			_logger.Information("App {App} volume set to {Volume} via slider", session.ProcessName, volume);
			return ValueTask.FromResult(VariableWriteResult.Applied());
		}

		return ValueTask.FromResult(VariableWriteResult.Unavailable());
	}

	public ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default)
	{
		if (_audio == null) return ValueTask.FromResult(VariableReading.Unavailable);
		return ValueTask.FromResult(localId switch
		{
			"default-device" => Reading.Of(_audio.GetDefaultMultimediaDeviceInfo()?.Name),
			"comm-device" => Reading.Of(_audio.GetDefaultCommunicationsDeviceInfo()?.Name),
			"default-mic" => Reading.Of(_audio.GetDefaultCaptureDeviceInfo()?.Name),
			"comm-mic" => Reading.Of(_audio.GetDefaultCaptureCommDeviceInfo()?.Name),
			_ => ReadAppVariable(localId),
		});
	}

	private VariableReading ReadAppVariable(string localId)
	{
		if (_audio == null || !TrySplitAppId(localId, out var safe, out var kind) || safe == null || kind == null)
		{
			return VariableReading.Unavailable;
		}

		foreach (var session in _audio.SnapshotDefaultDeviceSessions())
		{
			if (!string.Equals(AudioService.SanitizeAppName(session.ProcessName), safe, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			return kind == "volume"
				? VariableReading.Of(session.Volume, 0, 100, 1)
				: VariableReading.Of(session.Muted);
		}

		return VariableReading.Unavailable;
	}

	private static bool TrySplitAppId(string localId, out string? safe, out string? kind)
	{
		safe = null;
		kind = null;
		if (!localId.StartsWith("app_", StringComparison.Ordinal)) return false;

		string? suffix = null;
		if (localId.EndsWith("_volume", StringComparison.Ordinal)) suffix = "volume";
		else if (localId.EndsWith("_muted", StringComparison.Ordinal)) suffix = "muted";
		if (suffix == null) return false;

		var middle = localId.Substring(4, localId.Length - 4 - suffix.Length - 1);
		if (middle.Length == 0) return false;

		safe = middle;
		kind = suffix;
		return true;
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
