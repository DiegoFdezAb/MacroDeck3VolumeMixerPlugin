using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using Serilog;
using VolumeMixerPlugin.Services;

namespace VolumeMixerPlugin.Actions;

public sealed class SetDefaultOutputAction : IActionDefinition, IDynamicOptionsActionDefinition
{
	private readonly PluginIntegration _integration;
	private readonly ILogger _logger;

	public SetDefaultOutputAction(PluginIntegration integration, ILogger logger)
	{
		_integration = integration;
		_logger = logger.ForContext<SetDefaultOutputAction>();
	}

	public string Id => "set-default-output";

	public LocalizedText Name => Strings.Actions.SetOutput.Name();

	public LocalizedText Description => Strings.Actions.SetOutput.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("deviceId",
			label: Strings.Actions.SetOutput.Device.Label(),
			required: true),
		ActionParameter.DynamicChoice("deviceId2",
			label: Strings.Actions.SetOutput.Device2.Label(),
			required: false),
	];

	public IActionExecutor CreateExecutor() => new Executor(_integration, _logger);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		var devices = _integration.Audio?.GetActivePlaybackDevices() ?? [];
		return Task.FromResult(new DynamicOptionsResult
		{
			Options =
			[
				.. devices.Select(d => new ActionParameterOption { Value = d.Id, Label = d.Name })
			],
			CacheSeconds = 15,
			Error = devices.Count == 0 ? Strings.Errors.NoDevices() : null,
		});
	}

	private sealed class Executor(PluginIntegration integration, ILogger logger) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var audio = integration.Audio;
			if (audio == null)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.Unavailable,
					Strings.Errors.SwitchFailed()));
			}

			if (context.Parameters.GetValueOrDefault("deviceId") is not string deviceId
				|| string.IsNullOrWhiteSpace(deviceId))
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.Required(Strings.Actions.SetOutput.Device.Label())));
			}

			var secondRaw = context.Parameters.GetValueOrDefault("deviceId2")?.ToString();
			var current = audio.GetDefaultMultimediaDeviceInfo()?.Id;
			var target = DeviceSwitch.ResolveTarget(current, deviceId, string.IsNullOrWhiteSpace(secondRaw) ? null : secondRaw);
			if (target == null)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					Strings.Errors.ToggleSameDevice()));
			}

			var available = audio.GetActivePlaybackDevices();
			if (!available.Any(d => string.Equals(d.Id, target, StringComparison.OrdinalIgnoreCase)))
			{
				logger.Warning("Output device {Target} is not available", target);
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.NotFound,
					Strings.Errors.DeviceNotFound()));
			}

			if (!audio.SetDefaultDevice(target))
			{
				logger.Warning("Failed to set default output {Target}", target);
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.ProviderError,
					Strings.Errors.SwitchFailed()));
			}

			logger.Information("Default output set to {Target}", target);
			return ActionResult.SucceededTask;
		}
	}
}
