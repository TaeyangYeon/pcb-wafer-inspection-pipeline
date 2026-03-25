using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;
using InspectionPipeline.UI.ViewModels;

namespace InspectionPipeline.Tests.ViewModels;

[TestFixture]
public class SettingsViewModelTests
{
    private Mock<ISettingsService> _mockSettingsService;
    private Mock<IInspectionApiClient> _mockApiClient;
    private Mock<IFileWatcherService> _mockFileWatcherService;
    private SettingsViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockApiClient = new Mock<IInspectionApiClient>();
        _mockFileWatcherService = new Mock<IFileWatcherService>();

        var mockSettings = new AppSettings
        {
            ApiBaseUrl = "http://localhost:8502",
            WatchFolderPath = "/test/watch",
            AnomalyThreshold = 0.5,
            YoloConfThreshold = 0.25,
            AutoSaveNg = true,
            NgSavePath = "/test/ng",
            PatchcoreModelDir = "models/patchcore/",
            YoloOnnxPath = "models/best.onnx"
        };

        _mockSettingsService.Setup(s => s.LoadAsync()).ReturnsAsync(mockSettings);
        _mockFileWatcherService.Setup(f => f.IsRunning).Returns(false);

        _viewModel = new SettingsViewModel(_mockSettingsService.Object, _mockApiClient.Object, _mockFileWatcherService.Object);

        // Allow async constructor operations to complete
        Task.Delay(50).Wait();
    }

    [Test]
    public void Constructor_Initialized_LoadsSettingsFromService()
    {
        // Arrange & Act is done in SetUp

        // Assert
        Assert.That(_viewModel.ApiBaseUrl, Is.EqualTo("http://localhost:8502"));
        Assert.That(_viewModel.WatchFolderPath, Is.EqualTo("/test/watch"));
        Assert.That(_viewModel.AnomalyThreshold, Is.EqualTo(0.5));
        Assert.That(_viewModel.YoloConfThreshold, Is.EqualTo(0.25));
        Assert.That(_viewModel.AutoSaveNg, Is.True);
        Assert.That(_viewModel.NgSavePath, Is.EqualTo("/test/ng"));
        Assert.That(_viewModel.PatchcoreModelDir, Is.EqualTo("models/patchcore/"));
        Assert.That(_viewModel.YoloOnnxPath, Is.EqualTo("models/best.onnx"));
        
        _mockSettingsService.Verify(s => s.LoadAsync(), Times.AtLeastOnce);
    }

    [Test]
    public async Task SaveCommand_Called_CallsSettingsServiceSave()
    {
        // Arrange
        _mockSettingsService.Setup(s => s.SaveAsync(It.IsAny<AppSettings>()))
                           .Returns(Task.CompletedTask);

        _viewModel.ApiBaseUrl = "http://newurl:8080";
        _viewModel.AnomalyThreshold = 0.7;

        // Act
        await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Settings saved successfully"));
        _mockSettingsService.Verify(s => s.SaveAsync(It.Is<AppSettings>(settings =>
            settings.ApiBaseUrl == "http://newurl:8080" &&
            settings.AnomalyThreshold == 0.7)), Times.Once);
    }

    [Test]
    public void ResetCommand_Called_RestoresDefaultValues()
    {
        // Arrange
        var defaultSettings = new AppSettings
        {
            ApiBaseUrl = "http://localhost:8502",
            AnomalyThreshold = 0.5,
            YoloConfThreshold = 0.25,
            AutoSaveNg = true,
            WatchFolderPath = "",
            NgSavePath = "~/Desktop/ng_images/",
            PatchcoreModelDir = "models/patchcore/transistor/",
            YoloOnnxPath = "models/pcb_seg/best.onnx"
        };

        _mockSettingsService.Setup(s => s.GetDefaults()).Returns(defaultSettings);

        // Modify some values first
        _viewModel.ApiBaseUrl = "http://changed:9000";
        _viewModel.AnomalyThreshold = 0.9;

        // Act
        _viewModel.ResetToDefaultsCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.ApiBaseUrl, Is.EqualTo("http://localhost:8502"));
        Assert.That(_viewModel.AnomalyThreshold, Is.EqualTo(0.5));
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Settings reset to defaults"));
        _mockSettingsService.Verify(s => s.GetDefaults(), Times.Once);
    }

    [Test]
    public async Task TestConnectionCommand_ServerHealthy_StatusMessageShowsConnected()
    {
        // Arrange
        _viewModel.ApiBaseUrl = "http://healthy-server:8502";
        _mockApiClient.Setup(a => a.IsServerHealthyAsync()).ReturnsAsync(true);

        // Act
        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Connection successful"));
        Assert.That(_viewModel.IsTestingConnection, Is.False);
        _mockApiClient.Verify(a => a.IsServerHealthyAsync(), Times.Once);
    }

    [Test]
    public async Task TestConnectionCommand_ServerUnhealthy_StatusMessageShowsError()
    {
        // Arrange
        _viewModel.ApiBaseUrl = "http://unhealthy-server:8502";
        _mockApiClient.Setup(a => a.IsServerHealthyAsync()).ReturnsAsync(false);

        // Act
        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Cannot connect to http://unhealthy-server:8502"));
        Assert.That(_viewModel.IsTestingConnection, Is.False);
        _mockApiClient.Verify(a => a.IsServerHealthyAsync(), Times.Once);
    }

    [Test]
    public async Task TestConnectionCommand_EmptyUrl_ShowsValidationError()
    {
        // Arrange
        _viewModel.ApiBaseUrl = "";

        // Act
        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Please enter an API URL"));
        _mockApiClient.Verify(a => a.IsServerHealthyAsync(), Times.Never);
    }

    [Test]
    public async Task TestConnectionCommand_Exception_ShowsErrorMessage()
    {
        // Arrange
        _viewModel.ApiBaseUrl = "http://error-server:8502";
        _mockApiClient.Setup(a => a.IsServerHealthyAsync())
                     .ThrowsAsync(new Exception("Network error"));

        // Act
        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Connection failed: Network error"));
        Assert.That(_viewModel.IsTestingConnection, Is.False);
    }

    [Test]
    public async Task ToggleWatcherCommand_WatcherStopped_StartsWatcher()
    {
        // Arrange - use temp directory that actually exists
        var tempDir = System.IO.Path.GetTempPath();
        _viewModel.WatchFolderPath = tempDir;
        _mockFileWatcherService.Setup(f => f.IsRunning).Returns(false);
        _mockFileWatcherService.Setup(f => f.Start(It.IsAny<string>()));

        // Act
        await _viewModel.ToggleWatcherCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Does.Contain($"File watcher started for: {tempDir}"));
        _mockFileWatcherService.Verify(f => f.Start(tempDir), Times.Once);
        _mockFileWatcherService.Verify(f => f.Stop(), Times.Never);
    }

    [Test]
    public async Task ToggleWatcherCommand_WatcherRunning_StopsWatcher()
    {
        // Arrange
        _mockFileWatcherService.Setup(f => f.IsRunning).Returns(true);
        _mockFileWatcherService.Setup(f => f.Stop());

        // Act
        await _viewModel.ToggleWatcherCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("File watcher stopped"));
        _mockFileWatcherService.Verify(f => f.Stop(), Times.Once);
        _mockFileWatcherService.Verify(f => f.Start(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ToggleWatcherCommand_InvalidPath_ShowsValidationError()
    {
        // Arrange
        _viewModel.WatchFolderPath = "/invalid/nonexistent/path";
        _mockFileWatcherService.Setup(f => f.IsRunning).Returns(false);

        // Act
        await _viewModel.ToggleWatcherCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Please specify a valid watch folder path"));
        _mockFileWatcherService.Verify(f => f.Start(It.IsAny<string>()), Times.Never);
    }
}