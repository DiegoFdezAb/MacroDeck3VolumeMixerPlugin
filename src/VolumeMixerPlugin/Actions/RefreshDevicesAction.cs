using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using Serilog;

namespace VolumeMixerPlugin.Actions;

public sealed class RefreshDevicesAction : IActionDefinition
{
	private readonly PluginIntegration _integration;
	private readonly ILogger _logger;

	public RefreshDevicesAction(PluginIntegration integration, ILogger logger)
	{
		_integration = integration;
		_logger = logger.ForContext<RefreshDevicesAction>();
	}

	public string Id => "refresh-devices";

	public LocalizedText Name => Strings.Actions.RefreshDevices.Name();

	public LocalizedText Description => Strings.Actions.RefreshDevices.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } = [];

	public IActionExecutor CreateExecutor() => new Executor(_integration, _logger);

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

			try
			{
				audio.InvalidateCache();
				audio.GetActivePlaybackDevices();
				audio.GetActiveCaptureDevices();
				return ActionResult.SucceededTask;
			}
			catch (Exception ex)
			{
				logger.Warning("Failed to refresh devices: {Message}", ex.Message);
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.ProviderError,
					Strings.Errors.SwitchFailed()));
			}
		}
	}
}
