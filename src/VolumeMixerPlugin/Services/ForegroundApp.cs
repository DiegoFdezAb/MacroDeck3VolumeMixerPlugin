using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VolumeMixerPlugin.Services;

/// <summary>
/// Resolves the currently focused (foreground) application on Windows,
/// inspired by deej's <c>deej.current</c> concept.
/// </summary>
internal static class ForegroundApp
{
	/// <summary>
	/// Sentinel value stored in action configuration when the user selects "Active Window".
	/// At execution time this is resolved to the actual foreground process name.
	/// </summary>
	internal const string Sentinel = "__active_window__";

	/// <summary>
	/// Returns the process name of the application that currently owns the foreground window,
	/// or <c>null</c> if it cannot be determined.
	/// </summary>
	internal static string? GetProcessName()
	{
		if (!OperatingSystem.IsWindows()) return null;

		try
		{
			var hwnd = GetForegroundWindow();
			if (hwnd == IntPtr.Zero) return null;

			_ = GetWindowThreadProcessId(hwnd, out var pid);
			if (pid == 0) return null;

			using var proc = Process.GetProcessById((int)pid);
			return string.IsNullOrWhiteSpace(proc.ProcessName) ? null : proc.ProcessName;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// If <paramref name="appValue"/> is the <see cref="Sentinel"/>, resolves it to the
	/// foreground process name; otherwise returns the original value unchanged.
	/// </summary>
	internal static string? Resolve(string appValue)
		=> string.Equals(appValue, Sentinel, StringComparison.OrdinalIgnoreCase)
			? GetProcessName()
			: appValue;

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
