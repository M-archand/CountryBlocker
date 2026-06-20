using System.Globalization;
using System.Text.Json;

namespace CountryBlocker;

internal static class MmdbUpdater
{
	private const string ReleasesApi = "https://api.github.com/repos/P3TERX/GeoLite.mmdb/releases?per_page=1";
	private const string DownloadUrlFormat = "https://github.com/P3TERX/GeoLite.mmdb/releases/download/{0}/GeoLite2-Country.mmdb";

	private static readonly HttpClient Http = CreateClient();

	private static HttpClient CreateClient()
	{
		HttpClient client = new() { Timeout = TimeSpan.FromSeconds(60) };
		client.DefaultRequestHeaders.UserAgent.ParseAdd("CountryBlocker");
		return client;
	}

	/// Returns the tag of the most recent release
	public static async Task<string?> GetLatestTagAsync(CancellationToken token)
	{
		using Stream stream = await Http.GetStreamAsync(ReleasesApi, token);
		using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: token);

		if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
			return null;

		return document.RootElement[0].TryGetProperty("tag_name", out JsonElement tag)
			? tag.GetString()
			: null;
	}

	/// Downloads the GeoLite2-Country.mmdb for the given tag to the plugin root folder
	public static async Task DownloadAsync(string tag, string destinationPath, CancellationToken token)
	{
		string url = string.Format(DownloadUrlFormat, tag);

		using HttpResponseMessage response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
		response.EnsureSuccessStatusCode();

		await using FileStream file = File.Create(destinationPath);
		await response.Content.CopyToAsync(file, token);
	}

	/// Check if tag is a newer date
	public static bool IsNewer(string latestTag, string? currentTag)
	{
		if (string.IsNullOrWhiteSpace(currentTag))
			return true;

		if (TryParseDate(latestTag, out DateTime latest) && TryParseDate(currentTag, out DateTime current))
			return latest > current;

		return !string.Equals(latestTag, currentTag, StringComparison.OrdinalIgnoreCase);
	}

	private static bool TryParseDate(string tag, out DateTime date) =>
		DateTime.TryParseExact(tag, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}