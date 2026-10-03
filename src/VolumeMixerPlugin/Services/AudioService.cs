using System.Diagnostics;
using System.Text.RegularExpressions;
using NAudio.CoreAudioApi;
using Serilog;

namespace VolumeMixerPlugin.Services;

public sealed class AudioService : IDisposable
{
	private static readonly TimeSpan DeviceCacheLifetime = TimeSpan.FromSeconds(15);

	private readonly MMDeviceEnumerator _deviceEnumerator = new();
	private readonly PolicyConfigClient _policyConfig = new();
	private readonly ILogger _logger;
	private readonly object _cacheLock = new();

	private IReadOnlyList<(string Name, string Id)>? _playbackCache;
	private DateTime _playbackCacheTime;
	private IReadOnlyList<(string Name, string Id)>? _captureCache;
	private DateTime _captureCacheTime;
	private bool _disposed;

	public AudioService(ILogger logger)
	{
		_logger = logger.ForContext<AudioService>();
	}

	public (string Name, string Id)? GetDefaultMultimediaDeviceInfo()
	{
		try
		{
			using var device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
			return (device.FriendlyName, device.ID);
		}
		catch (Exception ex)
		{
			_logger.Debug("No default multimedia device: {Message}", ex.Message);
			return null;
		}
	}

	public (string Name, string Id)? GetDefaultCommunicationsDeviceInfo()
	{
		try
		{
			using var device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);
			return (device.FriendlyName, device.ID);
		}
		catch (Exception ex)
		{
			_logger.Debug("No default communications device: {Message}", ex.Message);
			return null;
		}
	}

	public (string Name, string Id)? GetDefaultCaptureDeviceInfo()
	{
		try
		{
			using var device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
			return (device.FriendlyName, device.ID);
		}
		catch (Exception ex)
		{
			_logger.Debug("No default capture device: {Message}", ex.Message);
			return null;
		}
	}

	public (string Name, string Id)? GetDefaultCaptureCommDeviceInfo()
	{
		try
		{
			using var device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
			return (device.FriendlyName, device.ID);
		}
		catch (Exception ex)
		{
			_logger.Debug("No default capture communications device: {Message}", ex.Message);
			return null;
		}
	}

	public IReadOnlyList<(string Name, string Id)> GetActivePlaybackDevices()
	{
		lock (_cacheLock)
		{
			if (_playbackCache != null && DateTime.UtcNow - _playbackCacheTime < DeviceCacheLifetime)
			{
				return _playbackCache;
			}

			_playbackCache = EnumerateDevices(DataFlow.Render);
			_playbackCacheTime = DateTime.UtcNow;
			return _playbackCache;
		}
	}

	public IReadOnlyList<(string Name, string Id)> GetActiveCaptureDevices()
	{
		lock (_cacheLock)
		{
			if (_captureCache != null && DateTime.UtcNow - _captureCacheTime < DeviceCacheLifetime)
			{
				return _captureCache;
			}

			_captureCache = EnumerateDevices(DataFlow.Capture);
			_captureCacheTime = DateTime.UtcNow;
			return _captureCache;
		}
	}

	public void InvalidateCache()
	{
		lock (_cacheLock)
		{
			_playbackCache = null;
			_captureCache = null;
		}
	}

	public bool SetDefaultDevice(string deviceId, bool allRoles = true)
	{
		if (string.IsNullOrEmpty(deviceId)) return false;
		try
		{
			if (allRoles)
			{
				return _policyConfig.SetDefaultEndpoint(deviceId, ERole.Console)
					&& _policyConfig.SetDefaultEndpoint(deviceId, ERole.Multimedia)
					&& _policyConfig.SetDefaultEndpoint(deviceId, ERole.Communications);
			}

			return _policyConfig.SetDefaultEndpoint(deviceId, ERole.Multimedia);
		}
		catch
		{
			return false;
		}
	}

	public bool SetDefaultCaptureDevice(string deviceId, bool allRoles = true)
	{
		if (string.IsNullOrEmpty(deviceId)) return false;
		try
		{
			if (allRoles)
			{
				return _policyConfig.SetDefaultEndpoint(deviceId, ERole.Console)
					&& _policyConfig.SetDefaultEndpoint(deviceId, ERole.Multimedia)
					&& _policyConfig.SetDefaultEndpoint(deviceId, ERole.Communications);
			}

			return _policyConfig.SetDefaultEndpoint(deviceId, ERole.Multimedia);
		}
		catch
		{
			return false;
		}
	}

	private List<(string Name, string Id)> EnumerateDevices(DataFlow flow)
	{
		var result = new List<(string, string)>();
		try
		{
			var devices = _deviceEnumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
			foreach (var device in devices)
			{
				try
				{
					result.Add((device.FriendlyName, device.ID));
				}
				finally
				{
					device.Dispose();
				}
			}
		}
		catch (Exception ex)
		{
			_logger.Warning("Failed to enumerate {Flow} devices: {Message}", flow, ex.Message);
		}

		return result;
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		try
		{
			_deviceEnumerator.Dispose();
		}
		catch
		{
		}
	}

	public IReadOnlyList<string> GetActiveAppNames()
	{
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			var devices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
			foreach (var device in devices)
			{
				try
				{
					var sessions = device.AudioSessionManager.Sessions;
					for (int i = 0; i < sessions.Count; i++)
					{
						try
						{
							var pid = (int)sessions[i].GetProcessID;
							if (pid == 0) continue;
							using var proc = Process.GetProcessById(pid);
							if (!string.IsNullOrWhiteSpace(proc.ProcessName))
							{
								names.Add(proc.ProcessName);
							}
						}
						catch
						{
						}
					}
				}
				catch (Exception ex)
				{
					_logger.Debug("Error scanning device sessions: {Message}", ex.Message);
				}
				finally
				{
					device.Dispose();
				}
			}
		}
		catch (Exception ex)
		{
			_logger.Warning("Failed to enumerate apps: {Message}", ex.Message);
		}

		return names.OrderBy(n => n).ToList();
	}

	public IReadOnlyList<(string ProcessName, int Volume, bool Muted)> SnapshotDefaultDeviceSessions()
	{
		var result = new List<(string, int, bool)>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			var devices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
			foreach (var device in devices)
			{
				try
				{
					var sessions = device.AudioSessionManager.Sessions;
					for (int i = 0; i < sessions.Count; i++)
					{
						try
						{
							var session = sessions[i];
							var pid = (int)session.GetProcessID;
							if (pid == 0) continue;

							string? name = null;
							try
							{
								using var proc = Process.GetProcessById(pid);
								name = proc.ProcessName;
							}
							catch
							{
							}

							if (string.IsNullOrWhiteSpace(name)) continue;
							if (!seen.Add(name)) continue;

							result.Add((name, (int)(session.SimpleAudioVolume.Volume * 100), session.SimpleAudioVolume.Mute));
						}
						catch
						{
						}
					}
				}
				finally
				{
					device.Dispose();
				}
			}
		}
		catch
		{
		}

		return result;
	}

	public bool SetAppVolume(string processName, float volumePercent)
		=> MutateSession(processName, s => s.SimpleAudioVolume.Volume = Math.Clamp(volumePercent / 100f, 0f, 1f));

	public bool AdjustAppVolume(string processName, float deltaPercent)
		=> MutateSession(processName, s =>
		{
			var current = s.SimpleAudioVolume.Volume;
			s.SimpleAudioVolume.Volume = Math.Clamp(current + (deltaPercent / 100f), 0f, 1f);
		});

	public bool ToggleAppMute(string processName)
		=> MutateSession(processName, s => s.SimpleAudioVolume.Mute = !s.SimpleAudioVolume.Mute);

	public static string SanitizeAppName(string name)
	{
		var lower = name.ToLowerInvariant();
		var cleaned = Regex.Replace(lower, @"[^a-z0-9_]+", "_").Trim('_');
		if (cleaned.Length == 0) cleaned = "unknown";
		if (!char.IsLetter(cleaned[0])) cleaned = "app_" + cleaned;
		return cleaned;
	}

	private bool MutateSession(string processName, Action<AudioSessionControl> mutate)
	{
		var found = false;
		try
		{
			var devices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
			foreach (var device in devices)
			{
				try
				{
					var sessions = device.AudioSessionManager.Sessions;
					for (int i = 0; i < sessions.Count; i++)
					{
						try
						{
							var session = sessions[i];
							var pid = (int)session.GetProcessID;
							if (pid == 0) continue;

							string? name = null;
							try
							{
								using var proc = Process.GetProcessById(pid);
								name = proc.ProcessName;
							}
							catch
							{
							}

							if (!string.Equals(name, processName, StringComparison.OrdinalIgnoreCase)) continue;

							mutate(session);
							found = true;
						}
						catch
						{
						}
					}
				}
				catch
				{
				}
				finally
				{
					device.Dispose();
				}
			}
		}
		catch
		{
		}

		return found;
	}
}
