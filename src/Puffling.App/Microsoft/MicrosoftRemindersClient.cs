using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Puffling.Core.Reminders;

namespace Puffling.App.Microsoft;

/// <summary>Read-only Microsoft Graph access with an encrypted, per-user MSAL token cache.</summary>
public sealed class MicrosoftRemindersClient : IDisposable
{
    private static readonly string[] Scopes = ["Calendars.ReadBasic", "Tasks.Read"];
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string _clientId;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPublicClientApplication? _app;
    private MsalCacheHelper? _cacheHelper;

    public MicrosoftRemindersClient(string clientId) => _clientId = clientId;

    private async Task<IPublicClientApplication> AppAsync()
    {
        if (_app != null) return _app;
        var app = PublicClientApplicationBuilder.Create(_clientId)
            .WithAuthority(AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
            .WithRedirectUri("http://localhost")
            .Build();
        string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Puffling");
        Directory.CreateDirectory(cacheDir);
        var storage = new StorageCreationPropertiesBuilder($"msal-{_clientId}.cache", cacheDir).Build();
        _cacheHelper = await MsalCacheHelper.CreateAsync(storage).ConfigureAwait(false);
        _cacheHelper.RegisterCache(app.UserTokenCache);
        return _app = app;
    }

    public async Task<string> ConnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var app = await AppAsync().ConfigureAwait(false);
            var result = await app.AcquireTokenInteractive(Scopes).WithUseEmbeddedWebView(false)
                .ExecuteAsync().ConfigureAwait(false);
            return result.Account.Username;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> HasAccountAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { return (await (await AppAsync().ConfigureAwait(false)).GetAccountsAsync().ConfigureAwait(false)).Any(); }
        finally { _gate.Release(); }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var app = await AppAsync().ConfigureAwait(false);
            foreach (var account in await app.GetAccountsAsync().ConfigureAwait(false))
                await app.RemoveAsync(account).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<ReminderSnapshot> FetchAsync(DateTimeOffset now)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var app = await AppAsync().ConfigureAwait(false);
            var account = (await app.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault()
                ?? throw new InvalidOperationException("Connect a Microsoft account in Settings first.");
            string token = (await app.AcquireTokenSilent(Scopes, account).ExecuteAsync().ConfigureAwait(false)).AccessToken;
            string start = Uri.EscapeDataString(now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            string end = Uri.EscapeDataString(now.AddHours(3).UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            var events = new List<CalendarReminder>();
            await foreach (var e in PagesAsync($"https://graph.microsoft.com/v1.0/me/calendar/calendarView?startDateTime={start}&endDateTime={end}&$select=id,subject,start,isAllDay,isCancelled", token))
            {
                if (Bool(e, "isCancelled")) continue;
                var when = DateTimeValue(e, "start");
                if (when is { } starts)
                    events.Add(new CalendarReminder(String(e, "id"), String(e, "subject"), starts, Bool(e, "isAllDay")));
            }

            var tasks = new List<TodoReminder>();
            await foreach (var list in PagesAsync("https://graph.microsoft.com/v1.0/me/todo/lists", token))
            {
                string id = String(list, "id");
                if (id.Length == 0) continue;
                await foreach (var task in PagesAsync($"https://graph.microsoft.com/v1.0/me/todo/lists/{Uri.EscapeDataString(id)}/tasks", token))
                    tasks.Add(new TodoReminder(String(task, "id"), String(task, "title"),
                        DateTimeValue(task, "dueDateTime"), String(task, "status") == "completed"));
            }
            return new ReminderSnapshot(events, tasks);
        }
        finally { _gate.Release(); }
    }

    private static async IAsyncEnumerable<JsonElement> PagesAsync(string url, string token)
    {
        for (int page = 0; url.Length > 0 && page < 100; page++)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "graph.microsoft.com")
                throw new InvalidOperationException("Unexpected Microsoft Graph page URL.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("Prefer", "outlook.timezone=\"UTC\"");
            using var response = await Http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            foreach (var item in document.RootElement.GetProperty("value").EnumerateArray()) yield return item.Clone();
            url = document.RootElement.TryGetProperty("@odata.nextLink", out var next) ? next.GetString() ?? "" : "";
        }
    }

    private static string String(JsonElement value, string property) =>
        value.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";

    private static bool Bool(JsonElement value, string property) =>
        value.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.True;

    private static DateTimeOffset? DateTimeValue(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var p) || p.ValueKind != JsonValueKind.Object) return null;
        string date = String(p, "dateTime"), zone = String(p, "timeZone");
        if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return null;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(zone.Length == 0 ? "UTC" : zone);
            return new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified), tz.GetUtcOffset(parsed));
        }
        catch (TimeZoneNotFoundException) { return null; }
    }

    public void Dispose() => _gate.Dispose();
}
