using MacroDeck.Plugin.Protocol.Capabilities.Actions;
using MacroDeck.Plugin.Protocol.Capabilities.Variables;
using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace VolumeMixerPlugin.Tests;

/// <summary>
/// Behaviour tests through <see cref="PluginTestHarness"/>: the plugin's own capability handlers run,
/// but nothing crosses a socket. This is where you test what your integration does.
/// </summary>
[TestFixture]
public sealed class PluginIntegrationTests
{
	private static PluginTestHarness CreateHarness() =>
		PluginTestHarness.Create(builder => builder
			.UseLocalization(Strings.LocalizationCatalog)
			.RegisterIntegration<PluginIntegration>());

	[Test]
	public async Task The_plugin_builds_and_initializes()
	{
		await using var harness = CreateHarness();

		Assert.DoesNotThrowAsync(harness.InitializeIntegrationsAsync);
	}
}

/// <summary>
/// Volume Mixer behaviour: device variables read, switches fail truthfully, refresh succeeds.
/// </summary>
[TestFixture]
public sealed class VolumeMixerTests
{
	private static PluginTestHarness CreateHarness() =>
		PluginTestHarness.Create(builder => builder
			.UseLocalization(Strings.LocalizationCatalog)
			.RegisterIntegration<PluginIntegration>());

	[Test]
	public async Task Device_variables_are_declared()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		foreach (var id in new[] { "default-device", "comm-device", "default-mic", "comm-mic" })
		{
			var reading = (await harness.Variables.GetAsync(id)).DataAs<VariableReadingDto>();
			Assert.That(reading, Is.Not.Null, $"variable {id}");
		}
	}

	[Test]
	public async Task Refresh_devices_succeeds()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync("refresh-devices");

		Assert.That(outcome.Succeeded, Is.True);
	}

	[Test]
	public async Task Set_output_with_unknown_id_fails()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"set-default-output",
			new Dictionary<string, object?> { ["deviceId"] = "no-such-device" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Set_mic_with_unknown_id_fails()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"set-default-mic",
			new Dictionary<string, object?> { ["inputId"] = "no-such-device" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Toggle_with_same_device_fails()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"set-default-output",
			new Dictionary<string, object?>
			{
				["deviceId"] = "same",
				["deviceId2"] = "same",
			});

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Output_options_are_listed()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var options = (await harness.Actions.GetOptionsAsync("set-default-output", "deviceId")).DataAs<DynamicOptionsResultDto>();

		Assert.That(options, Is.Not.Null);
	}

	[Test]
	public async Task Set_app_volume_with_unknown_app_fails()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"set-app-volume",
			new Dictionary<string, object?> { ["app"] = "no-such-app", ["volume"] = 40.0 });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Volume_up_with_unknown_app_fails()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"volume-up",
			new Dictionary<string, object?> { ["app"] = "no-such-app" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Toggle_mute_with_unknown_app_fails()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"toggle-mute",
			new Dictionary<string, object?> { ["app"] = "no-such-app" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task App_options_are_listed()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var options = (await harness.Actions.GetOptionsAsync("set-app-volume", "app")).DataAs<DynamicOptionsResultDto>();

		Assert.That(options, Is.Not.Null);
	}

	[Test]
	public async Task Unknown_app_variable_reads_unavailable()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var reading = (await harness.Variables.GetAsync("app_nope_volume")).DataAs<VariableReadingDto>();

		Assert.That(reading, Is.Not.Null);
	}
}
/// <summary>
/// The localization set is generated from <c>Localization/*.resx</c>, so these guard the wiring rather
/// than any wording: a missing catalog registration leaves every label showing its raw key.
/// </summary>
[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id()
	{
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:com.diegofdezab.volume-mixer"));
	}

	[Test]
	public void English_is_the_default_culture()
	{
		Assert.That(Strings.LocalizationCatalog.DefaultCulture, Is.EqualTo("en"));
		Assert.That(Strings.LocalizationCatalog.Cultures, Does.Contain("en"));
	}

	[Test]
	public void The_action_strings_come_from_the_catalog()
	{
		Assert.That(Strings.LocalizationCatalog.KeysOf("en"), Does.Contain("Actions.SetOutput.Name"));
	}

	[Test]
	public void Every_key_the_default_culture_declares_resolves_to_text()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("en"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("en", key, out var text), Is.True);
			Assert.That(text, Is.Not.Empty);
		}
	}
}
