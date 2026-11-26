using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Uploader;

// Sends every snapshot in the add-in's outbox to the website, oldest first, and keeps trying until each one lands.
//   <product>.Uploader.exe          keeps running, trying the outbox every minute
//   <product>.Uploader.exe --once   one pass, then exits
//   --site <address> and --outbox <folder> override the settings (for a check against a test site)
// The website's address is a per-PC setting in %AppData%\<product>\uploader.json (never committed: public repo).

const string SNAPSHOT_FILE_PATTERN = "*.snapshot.json.gz";
const string UPLOAD_PATH = "/api/snapshots";
TimeSpan timeBetweenPasses = TimeSpan.FromMinutes(1);
TimeSpan uploadTimeout = TimeSpan.FromMinutes(2);

string productName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Site";
string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
string outboxFolder = ArgumentAfter("--outbox") ?? Path.Combine(localAppData, productName, "Outbox");
string sentFolder = Path.Combine(outboxFolder, "Sent");
string rejectedFolder = Path.Combine(outboxFolder, "Rejected");
string settingsPath = Path.Combine(roamingAppData, productName, "uploader.json");
bool isOnePass = args.Contains("--once");

string siteAddress = ArgumentAfter("--site") ?? ReadSiteAddress(settingsPath);
Directory.CreateDirectory(outboxFolder);
Directory.CreateDirectory(sentFolder);
Directory.CreateDirectory(rejectedFolder);
Console.WriteLine($"Sending {outboxFolder} to {siteAddress}{UPLOAD_PATH}. Settings: {settingsPath}");

using var http = new HttpClient { BaseAddress = new Uri(siteAddress), Timeout = uploadTimeout };
while (true)
{
    foreach (string snapshotPath in Directory.GetFiles(outboxFolder, SNAPSHOT_FILE_PATTERN).OrderBy(path => path))
    {
        await SendAsync(http, snapshotPath);
    }
    if (isOnePass)
    {
        break;
    }
    await Task.Delay(timeBetweenPasses);
}

async Task SendAsync(HttpClient client, string snapshotPath)
{
    string fileName = Path.GetFileName(snapshotPath);
    System.Net.HttpStatusCode? statusCode = null;
    string reason = "";
    try
    {
        using var content = new ByteArrayContent(await File.ReadAllBytesAsync(snapshotPath));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
        using HttpResponseMessage response = await client.PostAsync(UPLOAD_PATH, content);
        statusCode = response.StatusCode;
        reason = await response.Content.ReadAsStringAsync();
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
    {
        reason = exception.Message;
    }

    UploadOutcome outcome = UploadRules.OutcomeOf(statusCode);
    switch (outcome)
    {
        case UploadOutcome.Delivered:
            File.Move(snapshotPath, Path.Combine(sentFolder, fileName), overwrite: true);
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} sent {fileName}");
            break;
        case UploadOutcome.Rejected:
            File.Move(snapshotPath, Path.Combine(rejectedFolder, fileName), overwrite: true);
            await File.WriteAllTextAsync(Path.Combine(rejectedFolder, fileName + ".reason.txt"), $"{(int?)statusCode} {reason}");
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} refused {fileName} ({(int?)statusCode}): {reason}");
            break;
        default:
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} will retry {fileName}: {(statusCode == null ? reason : ((int)statusCode).ToString())}");
            break;
    }
}

string? ArgumentAfter(string name)
{
    int position = Array.IndexOf(args, name);
    return position >= 0 && position + 1 < args.Length ? args[position + 1] : null;
}

static string ReadSiteAddress(string settingsPath)
{
    const string DEFAULT_SITE_ADDRESS = "http://localhost:5080";
    if (!File.Exists(settingsPath))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(new { siteAddress = DEFAULT_SITE_ADDRESS }, new JsonSerializerOptions { WriteIndented = true }));
        return DEFAULT_SITE_ADDRESS;
    }
    using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
    return settings.RootElement.TryGetProperty("siteAddress", out JsonElement address) && !string.IsNullOrWhiteSpace(address.GetString())
        ? address.GetString()!
        : DEFAULT_SITE_ADDRESS;
}
