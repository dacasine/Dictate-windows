using DictateForWindows.Core.Models;
using DictateForWindows.Core.Services.Settings;
using DictateForWindows.Core.Services.TargetApp;
using FluentAssertions;
using Moq;
using Xunit;

namespace DictateForWindows.Tests.Services;

public class TargetAppServiceTests
{
    private readonly Mock<ISettingsService> _settingsMock;
    private readonly TargetAppService _service;

    public TargetAppServiceTests()
    {
        _settingsMock = new Mock<ISettingsService>();
        _service = new TargetAppService(_settingsMock.Object);
    }

    [Fact]
    public void GetAll_ShouldReturnDefaultsWhenNoAppsStored()
    {
        _settingsMock.Setup(s => s.Get<List<TargetApp>>(It.IsAny<string>(), null))
            .Returns((List<TargetApp>?)null);

        var result = _service.GetAll();

        result.Should().HaveCount(4);
        _settingsMock.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<List<TargetApp>>()), Times.Once);
        _settingsMock.Verify(s => s.Save(), Times.Once);
    }

    [Fact]
    public void GetAll_ShouldReturnOnlyEnabledApps()
    {
        var apps = new List<TargetApp>
        {
            new() { Id = "a", Name = "App A", IsEnabled = true, Position = 0 },
            new() { Id = "b", Name = "App B", IsEnabled = false, Position = 1 },
            new() { Id = "c", Name = "App C", IsEnabled = true, Position = 2 }
        };
        _settingsMock.Setup(s => s.Get<List<TargetApp>>(It.IsAny<string>(), null))
            .Returns(apps);

        var result = _service.GetAll();

        result.Should().HaveCount(2);
        result.Select(a => a.Name).Should().Contain(new[] { "App A", "App C" });
    }

    [Fact]
    public void GetAll_ShouldReturnAppsOrderedByPosition()
    {
        var apps = new List<TargetApp>
        {
            new() { Id = "a", Name = "App C", IsEnabled = true, Position = 2 },
            new() { Id = "b", Name = "App A", IsEnabled = true, Position = 0 },
            new() { Id = "c", Name = "App B", IsEnabled = true, Position = 1 }
        };
        _settingsMock.Setup(s => s.Get<List<TargetApp>>(It.IsAny<string>(), null))
            .Returns(apps);

        var result = _service.GetAll();

        result.Select(a => a.Name).Should().ContainInOrder("App A", "App B", "App C");
    }

    [Fact]
    public void Add_ShouldSetPositionAndSave()
    {
        var existingApps = new List<TargetApp>
        {
            new() { Id = "a", Name = "App A", Position = 0 }
        };
        _settingsMock.Setup(s => s.Get<List<TargetApp>>(It.IsAny<string>(), null))
            .Returns(existingApps);

        var newApp = new TargetApp { Id = "b", Name = "App B" };
        _service.Add(newApp);

        newApp.Position.Should().Be(1);
        _settingsMock.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<List<TargetApp>>()), Times.Once);
        _settingsMock.Verify(s => s.Save(), Times.Once);
    }

    [Fact]
    public void Delete_ShouldRemoveAppAndSave()
    {
        var apps = new List<TargetApp>
        {
            new() { Id = "a", Name = "App A" },
            new() { Id = "b", Name = "App B" }
        };
        _settingsMock.Setup(s => s.Get<List<TargetApp>>(It.IsAny<string>(), null))
            .Returns(apps);

        _service.Delete("a");

        _settingsMock.Verify(s => s.Set(It.IsAny<string>(),
            It.Is<List<TargetApp>>(l => l.Count == 1 && l[0].Id == "b")), Times.Once);
        _settingsMock.Verify(s => s.Save(), Times.Once);
    }

    [Fact]
    public void Update_ShouldReplaceExistingApp()
    {
        var apps = new List<TargetApp>
        {
            new() { Id = "a", Name = "Original" }
        };
        _settingsMock.Setup(s => s.Get<List<TargetApp>>(It.IsAny<string>(), null))
            .Returns(apps);

        var updated = new TargetApp { Id = "a", Name = "Updated" };
        _service.Update(updated);

        _settingsMock.Verify(s => s.Set(It.IsAny<string>(),
            It.Is<List<TargetApp>>(l => l[0].Name == "Updated")), Times.Once);
    }

    [Fact]
    public void Update_ShouldNotSaveWhenAppNotFound()
    {
        var apps = new List<TargetApp>
        {
            new() { Id = "a", Name = "App A" }
        };
        _settingsMock.Setup(s => s.Get<List<TargetApp>>(It.IsAny<string>(), null))
            .Returns(apps);

        var notFound = new TargetApp { Id = "nonexistent", Name = "Ghost" };
        _service.Update(notFound);

        _settingsMock.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<List<TargetApp>>()), Times.Never);
        _settingsMock.Verify(s => s.Save(), Times.Never);
    }
}
