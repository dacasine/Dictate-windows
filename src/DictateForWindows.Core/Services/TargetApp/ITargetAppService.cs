using DictateForWindows.Core.Models;

namespace DictateForWindows.Core.Services.TargetApp;

public interface ITargetAppService
{
    List<Models.TargetApp> GetAll();
    void Add(Models.TargetApp app);
    void Update(Models.TargetApp app);
    void Delete(string appId);
    Task SendToAppAsync(Models.TargetApp app, string text);
}
