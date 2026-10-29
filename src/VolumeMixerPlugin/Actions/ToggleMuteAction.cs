using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using Serilog;
using VolumeMixerPlugin.Services;

namespace VolumeMixerPlugin.Actions;

public sealed class ToggleMuteAction : IActionDefinition, IDynamicOptionsActionDefinition
{
	private readonly PluginIntegration _integration;
	private readonly ILogger _logger;

	public ToggleMuteAction(PluginIntegration integration, ILogger logger)
	{
		_integration = integration;
		_logger = logger.ForContext<ToggleMuteAction>();
	}

	public string Id => "toggle-mute";

	public LocalizedText Name => Strings.Actions.ToggleMute.Name();

	public LocalizedText Description => Strings.Actions.ToggleMute.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("app",
			label: Strings.Actions.ToggleMute.App.Label(),
			required: true),
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
					MacroDeckStrings.Validation.Required(Strings.Actions.ToggleMute.App.Label())));
			}

			// Resolve the "Active Window" sentinel to the foreground process name.
			var resolvedApp = ForegroundApp.Resolve(app);
			if (resolvedApp == null)
			{
				logger.Warning("Could not determine the foreground application");
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.NotFound,
					Strings.Errors.ActiveWindowNotFound()));
			}

			if (!audio.ToggleAppMute(resolvedApp))
			{
				logger.Warning("No audio session for app {App}", resolvedApp);
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.NotFound,
					Strings.Errors.AppNotFound()));
			}

			return ActionResult.SucceededTask;
		}
	}
}
