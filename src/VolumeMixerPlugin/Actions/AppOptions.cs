using MacroDeck.Sdk.Actions;
using VolumeMixerPlugin.Services;

namespace VolumeMixerPlugin.Actions;

internal static class AppOptions
{
	internal static DynamicOptionsResult List(AudioService? audio)
	{
		var apps = audio?.GetActiveAppNames() ?? [];
		return new DynamicOptionsResult
		{
			Options =
			[
				new ActionParameterOption { Value = ForegroundApp.Sentinel, Label = Strings.Actions.ActiveWindow.Label() },
				.. apps.Select(a => new ActionParameterOption { Value = a, Label = a })
			],
			CacheSeconds = 15,
			Error = apps.Count == 0 ? Strings.Errors.NoApps() : null,
		};
	}
}
