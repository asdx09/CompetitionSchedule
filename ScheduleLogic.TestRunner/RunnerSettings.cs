using System.Text.Json;

namespace ScheduleLogic.TestRunner;

/// <summary>A felület utolsó beállításai (%AppData%\ScheduleLogic\testrunner.json).</summary>
internal sealed class RunnerSettings
{
    public string Label { get; set; } = ".NET";
    public string Url { get; set; } = "https://localhost:7098";
    public bool IgnoreCertificateErrors { get; set; } = true;

    public string Scenario { get; set; } = "GetEvent";
    public string Mode { get; set; } = "Closed";
    public string Levels { get; set; } = "1,5,10,25,50";
    public int DurationSeconds { get; set; } = 30;
    public int WarmUpSeconds { get; set; } = 5;
    public int PauseSeconds { get; set; } = 5;
    public int Users { get; set; } = 10;
    public int EventSize { get; set; } = 100;
    public string MonitoredProcesses { get; set; } = "ScheduleLogic.Server,php-cgi,php-fpm,php";

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScheduleLogic", "testrunner.json");

    public static RunnerSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<RunnerSettings>(File.ReadAllText(FilePath)) ?? new RunnerSettings();
        }
        catch (Exception)
        {
            // Sérült beállításfájl: alapértékekkel indulunk.
        }
        return new RunnerSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            // A beállítások mentése nem kritikus.
        }
    }
}
