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
}
