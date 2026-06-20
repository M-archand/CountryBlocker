using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Microsoft.Extensions.Logging;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using static CounterStrikeSharp.API.Core.Listeners;
using CounterStrikeSharp.API;

namespace CountryBlocker;

public sealed class PluginConfig : BasePluginConfig
{
	[JsonPropertyName("Countries")]
	public List<string> BlockedCountries { get; set; } = new List<string> { "DE", "FR", "HU" };

	[JsonPropertyName("SteamIDWhitelist")]
	public List<string> SteamIDWhitelist { get; set; } = new List<string> { "76561197960434622", "76561197962313932" };

	[JsonPropertyName("KickUnknown")]
	public bool KickUnknownCountry { get; set; } = false;

	[JsonPropertyName("LogConnections")]
	public bool LogConnections { get; set; } = false;

	[JsonPropertyName("WhitelistMode")]
	public bool WhitelistMode { get; set; } = false;

	[JsonPropertyName("AutoUpdate")]
	public bool AutoUpdate { get; set; } = true;

	[JsonPropertyName("ConfigVersion")]
	public override int Version { get; set; } = 3;
}

[MinimumApiVersion(369)]
public class PluginCountryBlocker : BasePlugin, IPluginConfig<PluginConfig>
{
	public override string ModuleName => "CountryBlocker";
	public override string ModuleAuthor => "K4ryuu, Marchand";
	public override string ModuleVersion => "1.2.0";

	public required PluginConfig Config { get; set; } = new PluginConfig();

	private DatabaseReader? _geoDatabase;
	private CancellationTokenSource? _updateCts;

	public void OnConfigParsed(PluginConfig config)
	{
		if (config.Version < Config.Version)
		{
			Logger.LogWarning("Configuration version mismatch (Expected: {ExpectedVersion} | Current: {CurrentVersion})", Config.Version, config.Version);
		}

		Config = config;
	}

	public override void Load(bool hotReload)
	{
		string mmdbPath = Path.Combine(ModuleDirectory, "GeoLite2-Country.mmdb");

		if (File.Exists(mmdbPath))
			OpenDatabase(mmdbPath);

		RegisterListener<OnClientConnected>((slot) =>
		{
			CCSPlayerController? player = Utilities.GetPlayerFromSlot(slot);

			if (player is null || !player.IsValid || player.IsBot || player.IsHLTV || player.IpAddress == null)
				return;

			string countryCode = GetPlayerCountryCode(player);

			if (Config.LogConnections)
				Logger.LogInformation("Player {PlayerName} ({SteamId}) connected from {Country}", player.PlayerName, player.SteamID, countryCode);

			bool countryOnList = Config.BlockedCountries.Contains(countryCode, StringComparer.OrdinalIgnoreCase);

			if ((Config.WhitelistMode != countryOnList) || (Config.KickUnknownCountry && countryCode == "??"))
			{
				if (Config.SteamIDWhitelist.Contains(player.SteamID.ToString()))
					return;

				int? userId = player.UserId;

				Logger.LogInformation("Blocked player {PlayerName} ({SteamId}) from joining the server. Country: {Country}", player.PlayerName, player.SteamID, countryCode);

				Server.NextFrame(() =>
				{
					if (userId.HasValue)
						Server.ExecuteCommand($"kickid {userId.Value} \"You are not allowed to join this server from this country.\"");
				});
			}
		});

		if (Config.AutoUpdate)
		{
			_updateCts = new CancellationTokenSource();
			CancellationToken token = _updateCts.Token;
			_ = Task.Run(() => CheckForUpdatesAsync(mmdbPath, token), token);
		}
		else if (_geoDatabase is null)
		{
			Logger.LogError("GeoLite2-Country.mmdb not found and auto-update is disabled. Download it from https://github.com/P3TERX/GeoLite.mmdb/releases");
		}
	}

	public override void Unload(bool hotReload)
	{
		_updateCts?.Cancel();
		_updateCts?.Dispose();
		_updateCts = null;

		_geoDatabase?.Dispose();
		_geoDatabase = null;
	}

	private void OpenDatabase(string mmdbPath)
	{
		try
		{
			_geoDatabase = new DatabaseReader(mmdbPath);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to open the GeoLite2 database at {Path}", mmdbPath);
		}
	}

	private async Task CheckForUpdatesAsync(string mmdbPath, CancellationToken token)
	{
		try
		{
			string updaterPath = Path.Combine(ModuleDirectory, "mmdb-updater.txt");
			string? currentTag = File.Exists(updaterPath)
				? (await File.ReadAllTextAsync(updaterPath, token)).Trim()
				: null;

			string? latestTag = await MmdbUpdater.GetLatestTagAsync(token);
			if (string.IsNullOrWhiteSpace(latestTag))
			{
				Logger.LogWarning("Could not determine the latest GeoLite2 release from GitHub.");
				return;
			}

			if (File.Exists(mmdbPath) && !MmdbUpdater.IsNewer(latestTag, currentTag))
			{
				Logger.LogInformation("GeoLite2 database is up to date (tag {Tag}).", currentTag);
				return;
			}

			string downloadPath = mmdbPath + ".new";
			Logger.LogInformation("Downloading GeoLite2 database {Tag} from GitHub...", latestTag);
			await MmdbUpdater.DownloadAsync(latestTag, downloadPath, token);

			if (token.IsCancellationRequested)
			{
				TryDelete(downloadPath);
				return;
			}

			// File replacement and reader swap must happen on the main thread so we never
			// dispose the reader while a connect lookup is mid-flight.
			Server.NextWorldUpdate(() => ApplyUpdate(mmdbPath, downloadPath, updaterPath, latestTag, token));
		}
		catch (OperationCanceledException)
		{
			// Plugin unloaded mid-update; nothing to do.
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "GeoLite2 auto-update failed.");
		}
	}

	private void ApplyUpdate(string mmdbPath, string downloadPath, string updaterPath, string tag, CancellationToken token)
	{
		if (token.IsCancellationRequested)
		{
			TryDelete(downloadPath);
			return;
		}

		try
		{
			_geoDatabase?.Dispose();
			_geoDatabase = null;

			File.Move(downloadPath, mmdbPath, overwrite: true);
			File.WriteAllText(updaterPath, tag);

			OpenDatabase(mmdbPath);
			Logger.LogInformation("GeoLite2 database updated to {Tag}.", tag);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to apply the GeoLite2 database update.");
			TryDelete(downloadPath);

			// The move may have failed after the reader was disposed; recover the existing file.
			if (_geoDatabase is null && File.Exists(mmdbPath))
				OpenDatabase(mmdbPath);
		}
	}

	private static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
				File.Delete(path);
		}
		catch
		{
			// Best-effort cleanup.
		}
	}

	public string GetPlayerCountryCode(CCSPlayerController player)
	{
		string? playerIp = player.IpAddress;

		if (playerIp == null)
			return "??";

		string[] parts = playerIp.Split(':');
		string realIP = parts.Length == 2 ? parts[0] : playerIp;

		if (_geoDatabase is null)
			return "??";

		try
		{
			MaxMind.GeoIP2.Responses.CountryResponse response = _geoDatabase.Country(realIP);
			return response.Country.IsoCode ?? "??";
		}
		catch (AddressNotFoundException)
		{
			Logger.LogError("The address {Address} is not in the database.", realIP);
			return "??";
		}
		catch (GeoIP2Exception ex)
		{
			Logger.LogError(ex, "GeoIP2 lookup failed for {Address}", realIP);
			return "??";
		}
	}
}
