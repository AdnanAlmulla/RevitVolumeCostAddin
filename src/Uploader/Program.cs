using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Uploader;

// Sends every snapshot in the add-in's outbox to the website, oldest first, then their 3D shapes, and keeps trying
// until each one lands.
//   <product>.Uploader.exe          keeps running, trying the outbox every minute (started at Windows sign-in by the
//                                   installer); a second copy exits at once
//   <product>.Uploader.exe --once   one pass, then exits
//   --site <address> and --outbox <folder> override the settings (for a check against a test site)
// The website's address and its site password are per-PC settings in %AppData%\<product>\uploader.json, never
// committed (public repo): { "siteAddress": "http://<host PC>:5080", "sitePassword": "<the site password>" }. They are
// read again at every pass, so a corrected setting is used within a minute. It has no window: what it does goes to
// %LocalAppData%\<product>\Logs\uploader.log.

const string SNAPSHOT_FILE_PATTERN = "*" + UploadRules.SNAPSHOT_FILE_EXTENSION;
const string SNAPSHOT_UPLOAD_PATH = "/api/snapshots";
// Each snapshot's 3D shapes, written beside it by the add-in, go to their own address, after the snapshots: the
// website files shapes under the model a snapshot named.
const string SHAPES_FILE_PATTERN = "*" + UploadRules.SHAPES_FILE_EXTENSION;
const string SHAPES_UPLOAD_PATH = "/api/shapes";
// The website's header for its site password (the website repo's SiteGate.PASSWORD_HEADER).
const string SITE_PASSWORD_HEADER = "X-Site-Password";
const string DEFAULT_SITE_ADDRESS = "http://localhost:5080";
// The log keeps going for months on a modeller's PC, a line a minute per file while the website is down: past this size
// it starts again, and the previous one is kept as uploader.previous.log.
const long LOG_SIZE_LIMIT_BYTES = 5L * 1024 * 1024;
TimeSpan timeBetweenPasses = TimeSpan.FromMinutes(1);
TimeSpan uploadTimeout = TimeSpan.FromMinutes(2);

string productName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Site";
string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
string outboxFolder = ArgumentAfter("--outbox") ?? Path.Combine(localAppData, productName, "Outbox");
string sentFolder = Path.Combine(outboxFolder, "Sent");
string rejectedFolder = Path.Combine(outboxFolder, "Rejected");
string settingsPath = Path.Combine(roamingAppData, productName, "uploader.json");
string logPath = Path.Combine(localAppData, productName, "Logs", "uploader.log");
bool isOnePass = args.Contains("--once");

// One running copy per Windows user: two would send and move the same files.
using var runningCopy = new Mutex(initiallyOwned: true, $"Local\\{productName}.Uploader", out bool isOnlyCopy);
if (!isOnePass && !isOnlyCopy)
{
    return;
}

Directory.CreateDirectory(outboxFolder);
Directory.CreateDirectory(sentFolder);
Directory.CreateDirectory(rejectedFolder);
Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
Log($"Started. Sending {outboxFolder}. Settings: {settingsPath}");

while (true)
{
    // Nobody watches this program, so a pass that fails (a mistyped setting, a file still locked) is logged and the
    // next pass tries again, never ending the program.
    try
    {
        await SendOutboxAsync();
    }
    catch (Exception exception)
    {
        Log($"pass failed, trying again in a minute: {exception.GetType().Name}: {exception.Message}");
    }
    if (isOnePass)
    {
        break;
    }
    await Task.Delay(timeBetweenPasses);
}

async Task SendOutboxAsync()
{
    int expiredCount = UploadRules.DeleteExpiredSentFiles(sentFolder, DateTime.UtcNow);
    if (expiredCount > 0)
    {
        Log($"deleted {expiredCount} files sent more than {UploadRules.SENT_FILE_KEEPING_DAYS} days ago from {sentFolder}");
    }
    string siteAddress = ArgumentAfter("--site") ?? ReadSetting(settingsPath, "siteAddress") ?? DEFAULT_SITE_ADDRESS;
    string? sitePassword = ReadSetting(settingsPath, "sitePassword");
    string[] snapshotPaths = Directory.GetFiles(outboxFolder, SNAPSHOT_FILE_PATTERN).OrderBy(path => path).ToArray();
    string[] shapesPaths = Directory.GetFiles(outboxFolder, SHAPES_FILE_PATTERN).OrderBy(path => path).ToArray();
    if (snapshotPaths.Length == 0 && shapesPaths.Length == 0)
    {
        return;
    }
    using var http = new HttpClient { BaseAddress = new Uri(siteAddress), Timeout = uploadTimeout };
    if (sitePassword != null)
    {
        http.DefaultRequestHeaders.Add(SITE_PASSWORD_HEADER, sitePassword);
    }
    foreach (string snapshotPath in snapshotPaths)
    {
        await SendAsync(http, snapshotPath, SNAPSHOT_UPLOAD_PATH);
    }
    foreach (string shapesPath in shapesPaths)
    {
        string shapesFileName = Path.GetFileName(shapesPath);
        string refusedSnapshotPath = Path.Combine(rejectedFolder, UploadRules.SnapshotFileNameOf(shapesFileName));
        if (File.Exists(refusedSnapshotPath))
        {
            File.Move(shapesPath, Path.Combine(rejectedFolder, shapesFileName), overwrite: true);
            Log($"refused {shapesFileName}: its snapshot was refused");
            continue;
        }
        await SendAsync(http, shapesPath, SHAPES_UPLOAD_PATH);
    }
}

async Task SendAsync(HttpClient client, string filePath, string uploadPath)
{
    string fileName = Path.GetFileName(filePath);
    System.Net.HttpStatusCode? statusCode = null;
    string reason = "";
    try
    {
        using var content = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
        using HttpResponseMessage response = await client.PostAsync(uploadPath, content);
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
            string sentPath = Path.Combine(sentFolder, fileName);
            File.Move(filePath, sentPath, overwrite: true);
            UploadRules.MarkAsSent(sentPath, DateTime.UtcNow);
            Log($"sent {fileName} to {client.BaseAddress}");
            break;
        case UploadOutcome.Rejected:
            File.Move(filePath, Path.Combine(rejectedFolder, fileName), overwrite: true);
            await File.WriteAllTextAsync(Path.Combine(rejectedFolder, fileName + ".reason.txt"), $"{(int?)statusCode} {reason}");
            Log($"refused {fileName} ({(int?)statusCode}): {reason}");
            break;
        default:
            Log($"will retry {fileName}: {(statusCode == null ? reason : $"{(int)statusCode} {reason}")}");
            break;
    }
}

// A log that can't be written must never stop the uploader: it is called from the loop's own catch.
void Log(string line)
{
    string stampedLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}";
    Console.WriteLine(stampedLine);
    try
    {
        var logFile = new FileInfo(logPath);
        if (logFile.Exists && logFile.Length > LOG_SIZE_LIMIT_BYTES)
        {
            File.Move(logPath, Path.Combine(logFile.DirectoryName!, "uploader.previous.log"), overwrite: true);
        }
        File.AppendAllText(logPath, stampedLine + Environment.NewLine);
    }
    catch (Exception)
    {
        // Nothing else to tell: the uploader has no window.
    }
}

string? ArgumentAfter(string name)
{
    int position = Array.IndexOf(args, name);
    return position >= 0 && position + 1 < args.Length ? args[position + 1] : null;
}

// A missing settings file is written with the default address and an empty password, for the technician to fill in.
// A blank setting reads as none: no address means this PC's site, no password sends none (the host PC needs none).
static string? ReadSetting(string settingsPath, string settingName)
{
    if (!File.Exists(settingsPath))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var newSettings = new { siteAddress = DEFAULT_SITE_ADDRESS, sitePassword = "" };
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(newSettings, new JsonSerializerOptions { WriteIndented = true }));
    }
    using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
    string? value = settings.RootElement.TryGetProperty(settingName, out JsonElement setting) ? setting.GetString() : null;
    return string.IsNullOrWhiteSpace(value) ? null : value;
}
