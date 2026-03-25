using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;
using InspectionPipeline.UI.ViewModels;

namespace InspectionPipeline.Tests.ViewModels;

[TestFixture]
public class HistoryViewModelTests
{
    private Mock<IInspectionRepository> _mockRepository;
    private HistoryViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _mockRepository = new Mock<IInspectionRepository>();
        _viewModel = new HistoryViewModel(_mockRepository.Object);
    }

    [Test]
    public void Constructor_Initialized_ResultFilterOptionsContainAll()
    {
        // Arrange & Act is done in SetUp

        // Assert
        Assert.That(_viewModel.ResultFilterOptions, Contains.Item("All"));
        Assert.That(_viewModel.ResultFilterOptions, Contains.Item("OK"));
        Assert.That(_viewModel.ResultFilterOptions, Contains.Item("NG"));
        Assert.That(_viewModel.ResultFilterOptions, Contains.Item("ANOMALY"));
        Assert.That(_viewModel.ResultFilterOptions.Count, Is.EqualTo(4));
        Assert.That(_viewModel.ResultFilter, Is.EqualTo("All"));
        Assert.That(_viewModel.ExportPath, Is.Not.Empty);
    }

    [Test]
    public async Task SearchCommand_NoFilters_LoadsAllRecords()
    {
        // Arrange
        var expectedRecords = new List<InspectionRecord>
        {
            new() { Id = 1, ImagePath = "test1.jpg", FinalResult = "OK", StageUsed = "Pipeline", Timestamp = DateTime.Now },
            new() { Id = 2, ImagePath = "test2.jpg", FinalResult = "NG", StageUsed = "Pipeline", Timestamp = DateTime.Now.AddMinutes(-1) }
        };

        _mockRepository.Setup(r => r.GetRecordsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(expectedRecords);

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);
        await Task.Delay(100); // Allow async operations to complete

        // Assert
        Assert.That(_viewModel.Records.Count, Is.EqualTo(2));
        Assert.That(_viewModel.TotalCount, Is.EqualTo(2));
        Assert.That(_viewModel.OkCount, Is.EqualTo(1));
        Assert.That(_viewModel.NgCount, Is.EqualTo(1));
        Assert.That(_viewModel.StatusMessage, Does.Contain("Found 2 records"));
        _mockRepository.Verify(r => r.GetRecordsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task SearchCommand_WithResultFilter_PassesFilterToRepository()
    {
        // Arrange
        var fromDate = DateTime.Today.AddDays(-7);
        var toDate = DateTime.Today;
        _viewModel.FromDate = fromDate;
        _viewModel.ToDate = toDate;
        _viewModel.ResultFilter = "NG";

        var expectedRecords = new List<InspectionRecord>
        {
            new() { Id = 1, ImagePath = "test1.jpg", FinalResult = "NG", StageUsed = "Pipeline", Timestamp = DateTime.Now }
        };

        _mockRepository.Setup(r => r.GetRecordsAsync(fromDate, toDate, "NG"))
                      .ReturnsAsync(expectedRecords);

        // Act
        await _viewModel.SearchCommand.ExecuteAsync(null);
        await Task.Delay(100); // Allow async operations to complete

        // Assert
        Assert.That(_viewModel.Records.Count, Is.EqualTo(1));
        Assert.That(_viewModel.NgCount, Is.EqualTo(1));
        Assert.That(_viewModel.OkCount, Is.EqualTo(0));
        _mockRepository.Verify(r => r.GetRecordsAsync(fromDate, toDate, "NG"), Times.Once);
    }

    [Test]
    public void ClearFiltersCommand_ResetsAllFilters()
    {
        // Arrange
        _viewModel.FromDate = DateTime.Today.AddDays(-7);
        _viewModel.ToDate = DateTime.Today;
        _viewModel.ResultFilter = "NG";

        var resetRecords = new List<InspectionRecord>
        {
            new() { Id = 1, ImagePath = "reset.jpg", FinalResult = "OK", StageUsed = "Pipeline", Timestamp = DateTime.Now }
        };

        _mockRepository.Setup(r => r.GetRecordsAsync())
                      .ReturnsAsync(resetRecords);

        // Act
        _viewModel.ClearFiltersCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.FromDate, Is.Null);
        Assert.That(_viewModel.ToDate, Is.Null);
        Assert.That(_viewModel.ResultFilter, Is.EqualTo("All"));
    }

    [Test]
    public async Task ExportCommand_ValidPath_CallsRepository()
    {
        // Arrange
        var exportPath = "/tmp/test_export.csv";
        _viewModel.ExportPath = exportPath;

        _mockRepository.Setup(r => r.ExportToCsvAsync(exportPath))
                      .Returns(Task.CompletedTask);

        // Act
        await _viewModel.ExportCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Does.Contain("Exported"));
        Assert.That(_viewModel.StatusMessage, Does.Contain(exportPath));
        _mockRepository.Verify(r => r.ExportToCsvAsync(exportPath), Times.Once);
    }

    [Test]
    public async Task ExportCommand_EmptyPath_ShowsErrorMessage()
    {
        // Arrange
        _viewModel.ExportPath = "";

        // Act
        await _viewModel.ExportCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Error: Export path is required"));
        _mockRepository.Verify(r => r.ExportToCsvAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void GetImageFileName_ValidPath_ReturnsFileName()
    {
        // Arrange
        var fullPath = "/path/to/image/test_image.jpg";

        // Act
        var result = _viewModel.GetImageFileName(fullPath);

        // Assert
        Assert.That(result, Is.EqualTo("test_image.jpg"));
    }

    [Test]
    public void GetDefectCount_WithDefects_ReturnsCount()
    {
        // Arrange
        var record = new InspectionRecord
        {
            ImagePath = "test.jpg",
            FinalResult = "NG",
            StageUsed = "Pipeline",
            DefectDetails = new List<DefectDetail>
            {
                new() { ClassName = "missing_hole", BboxX = 10, BboxY = 20, BboxW = 30, BboxH = 40 },
                new() { ClassName = "mouse_bite", BboxX = 50, BboxY = 60, BboxW = 70, BboxH = 80 }
            }
        };

        // Act
        var result = _viewModel.GetDefectCount(record);

        // Assert
        Assert.That(result, Is.EqualTo(2));
    }

    [Test]
    public void GetDefectCount_NoDefects_ReturnsZero()
    {
        // Arrange
        var record = new InspectionRecord 
        { 
            ImagePath = "test.jpg",
            FinalResult = "OK", 
            StageUsed = "Pipeline",
            DefectDetails = null 
        };

        // Act
        var result = _viewModel.GetDefectCount(record);

        // Assert
        Assert.That(result, Is.EqualTo(0));
    }
}