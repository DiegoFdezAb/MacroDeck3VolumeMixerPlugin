using System.Globalization;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using Serilog;
using VolumeMixerPlugin.Services;

namespace VolumeMixerPlugin.Actions;

public sealed class SetAppVolumeAction : IActionDefinition, IDynamicOptionsActionDefinition
{
	private readonly PluginIntegration _integration;
	private readonly ILogger _logger;

	public SetAppVolumeAction(PluginIntegration integration, ILogger logger)
	{
		_integration = integration;
		_logger = logger.ForContext<SetAppVolumeAction>();
	}

	public string Id => "set-app-volume";

	public LocalizedText Name => Strings.Actions.SetAppVolume.Name();

	public LocalizedText Description => Strings.Actions.SetAppVolume.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("app",
			label: Strings.Actions.SetAppVolume.App.Label(),
			required: true),
		ActionParameter.Slider("volume", 0, 100,
			label: Strings.Actions.SetAppVolume.Volume.Label(),
			step: 1,
			defaultValue: 50),
	];

	public IActionExecutor CreateExecutor() => new Executor(_integration, _logger);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
		=> Task.FromResult(AppOptions.List(_integration.Audio));

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

			if (context.Parameters.GetValueOrDefault("app") is not string app
				|| string.IsNullOrWhiteSpace(app))
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.Required(Strings.Actions.SetAppVolume.App.Label())));
			}

			// Resolve the "Active Window" sentinel to the foreground process name.
			var resolvedApp = ForegroundApp.Resolve(app);
			if (resolvedApp == null)
			{
				logger.Warning("Could not determine the foreground application");
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.NotFound,
					Strings.Errors.ActiveWindowNotFound()));
			}

			if (context.Parameters.GetValueOrDefault("volume") is not double volume)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.InvalidValue(Strings.Actions.SetAppVolume.Volume.Label())));
			}

			volume = Math.Clamp(Convert.ToDouble(volume, CultureInfo.InvariantCulture), 0, 100);
			if (!audio.SetAppVolume(resolvedApp, (float)volume))
			{
				logger.Warning("No audio session for app {App}", resolvedApp);
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.NotFound,
					Strings.Errors.AppNotFound()));
			}

			logger.Information("App {App} volume set to {Volume}", resolvedApp, volume);
			return ActionResult.SucceededTask;
		}
	}
}
