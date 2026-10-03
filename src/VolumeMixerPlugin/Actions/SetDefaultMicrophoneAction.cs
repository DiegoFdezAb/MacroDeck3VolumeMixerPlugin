using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using Serilog;
using VolumeMixerPlugin.Services;

namespace VolumeMixerPlugin.Actions;

public sealed class SetDefaultMicrophoneAction : IActionDefinition, IDynamicOptionsActionDefinition
{
	private readonly PluginIntegration _integration;
	private readonly ILogger _logger;

	public SetDefaultMicrophoneAction(PluginIntegration integration, ILogger logger)
	{
		_integration = integration;
		_logger = logger.ForContext<SetDefaultMicrophoneAction>();
	}

	public string Id => "set-default-mic";

	public LocalizedText Name => Strings.Actions.SetMic.Name();

	public LocalizedText Description => Strings.Actions.SetMic.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("inputId",
			label: Strings.Actions.SetMic.Input.Label(),
			required: true),
		ActionParameter.DynamicChoice("inputId2",
			label: Strings.Actions.SetMic.Input2.Label(),
			required: false),
	];

	public IActionExecutor CreateExecutor() => new Executor(_integration, _logger);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		var devices = _integration.Audio?.GetActiveCaptureDevices() ?? [];
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

			if (context.Parameters.GetValueOrDefault("inputId") is not string inputId
				|| string.IsNullOrWhiteSpace(inputId))
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.Required(Strings.Actions.SetMic.Input.Label())));
			}

			var secondRaw = context.Parameters.GetValueOrDefault("inputId2")?.ToString();
			var current = audio.GetDefaultCaptureDeviceInfo()?.Id;
			var target = DeviceSwitch.ResolveTarget(current, inputId, string.IsNullOrWhiteSpace(secondRaw) ? null : secondRaw);
			if (target == null)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					Strings.Errors.ToggleSameDevice()));
			}

			var available = audio.GetActiveCaptureDevices();
			if (!available.Any(d => string.Equals(d.Id, target, StringComparison.OrdinalIgnoreCase)))
			{
				logger.Warning("Microphone {Target} is not available", target);
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.NotFound,
					Strings.Errors.DeviceNotFound()));
			}

			if (!audio.SetDefaultCaptureDevice(target))
			{
				logger.Warning("Failed to set default microphone {Target}", target);
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.ProviderError,
					Strings.Errors.SwitchFailed()));
			}

			logger.Information("Default microphone set to {Target}", target);
			return ActionResult.SucceededTask;
		}
	}
}
