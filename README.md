<!-- PROJECT LOGO -->
<br />
<div align="center">
  <h3 align="center">CountryBlocker</h3>
  <a align="center">Control who can join your CS2 server based on the country their IP resolves to.</a>

  <p align="center">
    <br />
    <a href="https://github.com/M-archand/CountryBlocker/releases">Download</a>
  </p>
</div>

<!-- ABOUT THE PROJECT -->

## About

CountryBlocker checks every connecting player's IP address against the MaxMind GeoLite2 country database and kicks them if their country isn't allowed. It supports two modes:

- **Blacklist** (default) - block a specific list of countries, allow everyone else.
- **Whitelist** - allow only a specific list of countries, block everyone else.

A SteamID whitelist lets you exempt trusted players (admins, regulars) from the country check entirely. Bots and SourceTV/HLTV connections are always ignored.

### Features

- Country-based connection filtering in blacklist or whitelist mode.
- Per-SteamID exemptions that bypass all country checks.
- Optional kicking of players whose country can't be resolved.
- Optional connection logging (player name, SteamID, resolved country).
- Automatic GeoLite2 database updates from GitHub — the plugin keeps the country data current on its own.

### Dependencies

- [**CounterStrikeSharp**](https://github.com/roflmuffin/CounterStrikeSharp/releases) (minimum API version 369)

The MaxMind `GeoLite2-Country.mmdb` database is required for lookups, but you do **not** need to download it manually - see [Country database](#country-database) below.

## Installation

1. Install [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases).
2. Extract the plugin into `addons/counterstrikesharp/plugins/CountryBlocker/`.
3. Start the server once. The plugin generates its config and (with `AutoUpdate` enabled) downloads the country database automatically.
4. Edit `addons/counterstrikesharp/configs/plugins/CountryBlocker/CountryBlocker.json` to desired settings and reload.

## Configuration

The config is generated on first load at `addons/counterstrikesharp/configs/plugins/CountryBlocker/CountryBlocker.json`.

```json
{
  "Countries": ["DE", "FR", "HU"],
  "SteamIDWhitelist": ["76561197960434622", "76561197962313932"],
  "KickUnknown": false,
  "LogConnections": false,
  "WhitelistMode": false,
  "AutoUpdate": true,
  "ConfigVersion": 3
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Countries` | string[] | `["DE", "FR", "HU"]` | [ISO 3166-1 alpha-2](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2) country codes. In blacklist mode these countries are blocked; in whitelist mode these are the only countries allowed. |
| `SteamIDWhitelist` | string[] |  | SteamID64s that are always allowed to join, regardless of country. |
| `KickUnknown` | bool | `false` | When `true`, also kick players whose country can't be determined (private/LAN IPs, addresses missing from the database). |
| `LogConnections` | bool | `false` | When `true`, log every connection with the player's name, SteamID, and resolved country. |
| `WhitelistMode` | bool | `false` | `false` = blacklist (kick the countries in `Countries`). `true` = whitelist (kick everyone **except** the countries in `Countries`). |
| `AutoUpdate` | bool | `true` | When `true`, the plugin checks GitHub on load and downloads a newer country database if one is available. See [Country database](#country-database). |
| `ConfigVersion` | int | `3` | Schema version, managed by the plugin. Don't edit this. |

Country codes are matched case-insensitively, so `de` and `DE` are equivalent.

## Country database

Lookups use MaxMind's `GeoLite2-Country.mmdb`, sourced from the [P3TERX/GeoLite.mmdb](https://github.com/P3TERX/GeoLite.mmdb/releases) mirror, which publishes a new, date-tagged release (e.g. `2026.06.19`) multiple times per week.

- **With `AutoUpdate` enabled (default):** on every load (server start, restart, or plugin reload) the plugin checks for the latest release. If it's newer than the last one it downloaded, it pulls the updated `GeoLite2-Country.mmdb` automatically. The last downloaded tag is recorded in `mmdb-updater.txt` next to the database so it only downloads when there's something newer.
- **With `AutoUpdate` disabled:** download `GeoLite2-Country.mmdb` yourself from the [releases page](https://github.com/P3TERX/GeoLite.mmdb/releases) and place it in the plugin directory (`addons/counterstrikesharp/plugins/CountryBlocker/`).

If the database is missing and auto-update is off, the plugin loads but won't block anyone (it logs an error).

<!-- LICENSE -->

## License

Distributed under the GPL-3.0 License. See `LICENSE.md` for more information.
