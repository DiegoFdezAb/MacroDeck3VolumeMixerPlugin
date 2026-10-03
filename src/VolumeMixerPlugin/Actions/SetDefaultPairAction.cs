using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using Serilog;
using VolumeMixerPlugin.Services;

namespace VolumeMixerPlugin.Actions;

public sealed class SetDefaultPairAction : IActionDefinition, IDynamicOptionsActionDefinition
{
	private readonly PluginIntegration _integration;
	private readonly ILogger _logger;

	public SetDefaultPairAction(PluginIntegration integration, ILogger logger)
	{
		_integration = integration;
		_logger = logger.ForContext<SetDefaultPairAction>();
	}

	public string Id => "set-default-pair";

	public LocalizedText Name => Strings.Actions.SetPair.Name();

	public LocalizedText Description => Strings.Actions.SetPair.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("outputId",
			label: Strings.Actions.SetPair.Output.Label(),
			required: true),
		ActionParameter.DynamicChoice("outputId2",
			label: Strings.Actions.SetPair.Output2.Label(),
			required: false),
		ActionParameter.DynamicChoice("inputId",
			label: Strings.Actions.SetPair.Input.Label(),
			required: true),
		ActionParameter.DynamicChoice("inputId2",
			label: Strings.Actions.SetPair.Input2.Label(),
			required: false),
	];

	public IActionExecutor CreateExecutor() => new Executor(_integration, _logger);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		var audio = _integration.Audio;
		var playback = audio?.GetActivePlaybackDevices() ?? [];
		var capture = audio?.GetActiveCaptureDevices() ?? [];
		var options = context.ParameterName.StartsWith("input", StringComparison.OrdinalIgnoreCase) ? capture : playback;
		return Task.FromResult(new DynamicOptionsResult
		{
			Options =
			[
				.. options.Select(d => new ActionParameterOption { Value = d.Id, Label = d.Name })
			],
			CacheSeconds = 15,
			Error = options.Count == 0 ? Strings.Errors.NoDevices() : null,
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

			var outputId = context.Parameters.GetValueOrDefault("outputId")?.ToString();
			var outputId2 = context.Parameters.GetValueOrDefault("outputId2")?.ToString();
			var inputId = context.Parameters.GetValueOrDefault("inputId")?.ToString();
			var inputId2 = context.Parameters.GetValueOrDefault("inputId2")?.ToString();

			if (string.IsNullOrWhiteSpace(outputId))
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.Required(Strings.Actions.SetPair.Output.Label())));
			}

			if (string.IsNullOrWhiteSpace(inputId))
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.Required(Strings.Actions.SetPair.Input.Label())));
			}

			var outputTarget = DeviceSwitch.ResolveTarget(
				audio.GetDefaultMultimediaDeviceInfo()?.Id,
				outputId,
				string.IsNullOrWhiteSpace(outputId2) ? null : outputId2);
			var inputTarget = DeviceSwitch.ResolveTarget(
				audio.GetDefaultCaptureDeviceInfo()?.Id,
				inputId,
				string.IsNullOrWhiteSpace(inputId2) ? null : inputId2);

			if (outputTarget == null || inputTarget == null)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					Strings.Errors.ToggleSameDevice()));
			}

			if (!audio.GetActivePlaybackDevices().Any(d => string.Equals(d.Id, outputTarget, StringComparison.OrdinalIgnoreCase))
				|| !audio.GetActiveCaptureDevices().Any(d => string.Equals(d.Id, inputTarget, StringComparison.OrdinalIgnoreCase)))
			{
				logger.Warning("Pair targets are not available");
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.NotFound,
					Strings.Errors.DeviceNotFound()));
			}

			var outputOk = audio.SetDefaultDevice(outputTarget);
			var inputOk = audio.SetDefaultCaptureDevice(inputTarget);
			if (!outputOk || !inputOk)
			{
				logger.Warning("Failed to set pair output={Output} input={Input}", outputTarget, inputTarget);
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.ProviderError,
					Strings.Errors.SwitchFailed()));
			}

			logger.Information("Pair set to output={Output} input={Input}", outputTarget, inputTarget);
			return ActionResult.SucceededTask;
		}
	}
}
