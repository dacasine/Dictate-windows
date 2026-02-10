using System.Diagnostics;
using DictateForWindows.Core.Constants;
using DictateForWindows.Core.Services.Settings;

namespace DictateForWindows.Core.Services.TargetApp;

public class TargetAppService : ITargetAppService
{
    private const string StorageKey = "dictate_target_apps";
    private readonly ISettingsService _settings;

    public TargetAppService(ISettingsService settings)
    {
        _settings = settings;
    }

    public List<Models.TargetApp> GetAll()
    {
        var apps = _settings.Get<List<Models.TargetApp>>(StorageKey);
        if (apps == null || apps.Count == 0)
        {
            apps = Models.TargetApp.GetDefaults();
            _settings.Set(StorageKey, apps);
            _settings.Save();
        }
        return apps.Where(a => a.IsEnabled).OrderBy(a => a.Position).ToList();
    }

    public void Add(Models.TargetApp app)
    {
        var apps = _settings.Get<List<Models.TargetApp>>(StorageKey) ?? [];
        app.Position = apps.Count;
        apps.Add(app);
        _settings.Set(StorageKey, apps);
        _settings.Save();
    }

    public void Update(Models.TargetApp app)
    {
        var apps = _settings.Get<List<Models.TargetApp>>(StorageKey) ?? [];
        var index = apps.FindIndex(a => a.Id == app.Id);
        if (index >= 0)
        {
            apps[index] = app;
            _settings.Set(StorageKey, apps);
            _settings.Save();
        }
    }

    public void Delete(string appId)
    {
        var apps = _settings.Get<List<Models.TargetApp>>(StorageKey) ?? [];
        apps.RemoveAll(a => a.Id == appId);
        _settings.Set(StorageKey, apps);
        _settings.Save();
    }

    public Task SendToAppAsync(Models.TargetApp app, string text)
    {
        var link = app.BuildLink(text);

        Process.Start(new ProcessStartInfo
        {
            FileName = link,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }
}
