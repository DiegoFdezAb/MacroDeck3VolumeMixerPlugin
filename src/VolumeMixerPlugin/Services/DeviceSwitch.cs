namespace VolumeMixerPlugin.Services;

internal static class DeviceSwitch
{
	// Direct mode when secondId is empty. Toggle mode otherwise: the press activates the device
	// that is not currently default. Identical toggle devices are a configuration error.
	internal static string? ResolveTarget(string? currentId, string firstId, string? secondId)
	{
		if (string.IsNullOrEmpty(secondId)) return firstId;
		if (string.Equals(firstId, secondId, StringComparison.OrdinalIgnoreCase)) return null;
		return string.Equals(currentId, firstId, StringComparison.OrdinalIgnoreCase) ? secondId : firstId;
	}
}
