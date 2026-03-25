using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using InspectionPipeline.Core.Data;
using InspectionPipeline.Core.Models;
using InspectionPipeline.Core.Services;

namespace InspectionPipeline.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class DatabaseIntegrationTests
{
    private string _dbPath;
    private DbContextOptions<InspectionDbContext> _options;
    private InspectionDbContext _context;
    private InspectionRepository _repository;
    private ModelVersion _testModelVersion;

    [SetUp]
    public async Task SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _options = new DbContextOptionsBuilder<InspectionDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        
        _context = new InspectionDbContext(_options);
        _context.Database.Migrate();
        
        // Create a test ModelVersion that can be used for foreign key references
        _testModelVersion = new ModelVersion
        {
            ModelName = "TestModel",
            Version = "1.0.0",
            LoadedAt = DateTime.Now,
            FilePath = "/test/model.onnx"
        };
        _context.ModelVersions.Add(_testModelVersion);
        await _context.SaveChangesAsync();
        
        _repository = new InspectionRepository(_context);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            _context?.Dispose();
        }
        finally
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }

    [Test]
    public void Migration_Applied_CreatesAllThreeTables()
    {
        // Arrange & Act
        var connection = _context.Database.GetDbConnection();
        connection.Open();
        
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT name FROM sqlite_master 
            WHERE type='table' AND name IN ('InspectionRecords', 'DefectDetails', 'ModelVersions')
            ORDER BY name";
        
        var tableNames = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tableNames.Add(reader.GetString(0));
        }

        // Assert
        Assert.That(tableNames, Contains.Item("InspectionRecords"));
        Assert.That(tableNames, Contains.Item("DefectDetails"));
        Assert.That(tableNames, Contains.Item("ModelVersions"));
        Assert.That(tableNames.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task SaveAndQuery_InspectionRecord_RoundtripSucceeds()
    {
        // Arrange
        var originalRecord = new InspectionRecord
        {
            ImagePath = "/test/image.jpg",
            FinalResult = "OK",
            StageUsed = "Pipeline",
            AnomalyScore = 0.15,
            InferenceTimeMs = 250.5,
            Timestamp = DateTime.Now,
            ModelVersionId = _testModelVersion.Id,
            DefectDetails = new List<DefectDetail>
            {
                new()
                {
                    ClassName = "missing_hole",
                    Confidence = 0.95,
                    BboxX = 100, BboxY = 150, BboxW = 50, BboxH = 30,
                    MaskArea = 1500
                }
            }
        };

        // Act
        var savedRecord = await _repository.SaveRecordAsync(originalRecord);
        var retrievedRecord = await _context.InspectionRecords
            .Include(r => r.DefectDetails)
            .FirstOrDefaultAsync(r => r.Id == savedRecord.Id);

        // Assert
        Assert.That(retrievedRecord, Is.Not.Null);
        Assert.That(retrievedRecord.ImagePath, Is.EqualTo(originalRecord.ImagePath));
        Assert.That(retrievedRecord.FinalResult, Is.EqualTo(originalRecord.FinalResult));
        Assert.That(retrievedRecord.StageUsed, Is.EqualTo(originalRecord.StageUsed));
        Assert.That(retrievedRecord.AnomalyScore, Is.EqualTo(originalRecord.AnomalyScore).Within(0.001));
        Assert.That(retrievedRecord.InferenceTimeMs, Is.EqualTo(originalRecord.InferenceTimeMs).Within(0.1));
        Assert.That(retrievedRecord.DefectDetails.Count, Is.EqualTo(1));
        
        var defect = retrievedRecord.DefectDetails.First();
        Assert.That(defect.ClassName, Is.EqualTo("missing_hole"));
        Assert.That(defect.Confidence, Is.EqualTo(0.95).Within(0.001));
        Assert.That(defect.BboxX, Is.EqualTo(100));
        Assert.That(defect.MaskArea, Is.EqualTo(1500));
    }

    [Test]
    public async Task SaveRecord_WithDefects_CascadeDeleteRemovesDefects()
    {
        // Arrange
        var record = new InspectionRecord
        {
            ImagePath = "/test/cascade.jpg",
            FinalResult = "NG",
            StageUsed = "Pipeline",
            ModelVersionId = _testModelVersion.Id,
            DefectDetails = new List<DefectDetail>
            {
                new()
                {
                    ClassName = "missing_hole",
                    Confidence = 0.9,
                    BboxX = 10, BboxY = 20, BboxW = 30, BboxH = 40,
                    MaskArea = 1200
                },
                new()
                {
                    ClassName = "mouse_bite",
                    Confidence = 0.8,
                    BboxX = 100, BboxY = 120, BboxW = 25, BboxH = 35,
                    MaskArea = 875
                }
            }
        };

        // Act
        var savedRecord = await _repository.SaveRecordAsync(record);
        var defectCountBefore = await _context.DefectDetails.CountAsync();
        
        _context.InspectionRecords.Remove(savedRecord);
        await _context.SaveChangesAsync();
        
        var defectCountAfter = await _context.DefectDetails.CountAsync();
        var recordExists = await _context.InspectionRecords.AnyAsync(r => r.Id == savedRecord.Id);

        // Assert
        Assert.That(defectCountBefore, Is.EqualTo(2));
        Assert.That(defectCountAfter, Is.EqualTo(0));
        Assert.That(recordExists, Is.False);
    }

    [Test]
    public async Task GetStatsAsync_WithMixedResults_ReturnsCorrectCounts()
    {
        // Arrange
        var records = new List<InspectionRecord>
        {
            new() { ImagePath = "ok1.jpg", FinalResult = "OK", StageUsed = "Pipeline", AnomalyScore = 0.1, InferenceTimeMs = 100, ModelVersionId = _testModelVersion.Id },
            new() { ImagePath = "ok2.jpg", FinalResult = "OK", StageUsed = "Pipeline", AnomalyScore = 0.2, InferenceTimeMs = 150, ModelVersionId = _testModelVersion.Id },
            new() { ImagePath = "ok3.jpg", FinalResult = "OK", StageUsed = "Pipeline", AnomalyScore = 0.3, InferenceTimeMs = 200, ModelVersionId = _testModelVersion.Id },
            new() { ImagePath = "ng1.jpg", FinalResult = "NG", StageUsed = "Pipeline", AnomalyScore = 0.8, InferenceTimeMs = 250, ModelVersionId = _testModelVersion.Id },
            new() { ImagePath = "ng2.jpg", FinalResult = "NG", StageUsed = "Pipeline", AnomalyScore = 0.9, InferenceTimeMs = 300, ModelVersionId = _testModelVersion.Id },
            new() { ImagePath = "anomaly1.jpg", FinalResult = "ANOMALY", StageUsed = "Pipeline", AnomalyScore = 0.7, InferenceTimeMs = 175, ModelVersionId = _testModelVersion.Id }
        };

        foreach (var record in records)
        {
            await _repository.SaveRecordAsync(record);
        }

        // Act
        var stats = await _repository.GetStatsAsync();

        // Assert
        Assert.That(stats.TotalInspections, Is.EqualTo(6));
        Assert.That(stats.OkCount, Is.EqualTo(3));
        Assert.That(stats.NgCount, Is.EqualTo(2));
        Assert.That(stats.AnomalyCount, Is.EqualTo(1));
        Assert.That(stats.AverageInferenceTime, Is.EqualTo(195.833333).Within(0.1));
        Assert.That(stats.AverageAnomalyScore, Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public async Task ExportToCsvAsync_WithRecords_WritesCorrectCsvContent()
    {
        // Arrange
        var csvPath = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.csv");
        
        var records = new List<InspectionRecord>
        {
            new()
            {
                ImagePath = "/test/image1.jpg",
                FinalResult = "OK",
                StageUsed = "Pipeline",
                AnomalyScore = 0.25,
                InferenceTimeMs = 120.5,
                Timestamp = new DateTime(2024, 1, 1, 10, 30, 45),
                ModelVersionId = _testModelVersion.Id,
                DefectDetails = new List<DefectDetail>()
            },
            new()
            {
                ImagePath = "/test/image2.jpg",
                FinalResult = "NG",
                StageUsed = "Pipeline",
                AnomalyScore = 0.85,
                InferenceTimeMs = 200.0,
                Timestamp = new DateTime(2024, 1, 2, 14, 15, 30),
                ModelVersionId = _testModelVersion.Id,
                DefectDetails = new List<DefectDetail>
                {
                    new()
                    {
                        ClassName = "missing_hole",
                        Confidence = 0.9,
                        BboxX = 50, BboxY = 60, BboxW = 20, BboxH = 30,
                        MaskArea = 600
                    }
                }
            }
        };

        foreach (var record in records)
        {
            await _repository.SaveRecordAsync(record);
        }

        try
        {
            // Act
            await _repository.ExportToCsvAsync(csvPath);

            // Assert
            Assert.That(File.Exists(csvPath), Is.True);
            
            var lines = File.ReadAllLines(csvPath);
            Assert.That(lines.Length, Is.EqualTo(3)); // Header + 2 records
            
            var header = lines[0];
            Assert.That(header, Is.EqualTo("Timestamp,ImagePath,FinalResult,StageUsed,AnomalyScore,InferenceTimeMs,DefectCount"));
            
            var firstRecord = lines[1];
            Assert.That(firstRecord, Does.Contain("image1.jpg"));
            Assert.That(firstRecord, Does.Contain("OK"));
            Assert.That(firstRecord, Does.Contain("0.25"));
            Assert.That(firstRecord, Does.Contain(",0"));  // DefectCount = 0
            
            var secondRecord = lines[2];
            Assert.That(secondRecord, Does.Contain("image2.jpg"));
            Assert.That(secondRecord, Does.Contain("NG"));
            Assert.That(secondRecord, Does.Contain("0.85"));
            Assert.That(secondRecord, Does.Contain(",1"));  // DefectCount = 1
        }
        finally
        {
            if (File.Exists(csvPath))
            {
                File.Delete(csvPath);
            }
        }
    }

    [Test]
    public async Task GetAnomalyScoresAsync_WithRecords_ReturnsLastNInOrder()
    {
        // Arrange
        var records = new List<InspectionRecord>();
        for (int i = 1; i <= 10; i++)
        {
            records.Add(new InspectionRecord
            {
                ImagePath = $"image{i}.jpg",
                FinalResult = "OK",
                StageUsed = "Pipeline",
                AnomalyScore = i * 0.1, // 0.1, 0.2, 0.3, ..., 1.0
                InferenceTimeMs = 100,
                Timestamp = DateTime.Now.AddMinutes(i), // Increasing timestamps
                ModelVersionId = _testModelVersion.Id
            });
        }

        // Save records in order
        foreach (var record in records)
        {
            await _repository.SaveRecordAsync(record);
            await Task.Delay(1); // Ensure different timestamps
        }

        // Act
        var lastFiveScores = await _repository.GetAnomalyScoresAsync(5);

        // Assert
        Assert.That(lastFiveScores.Count, Is.EqualTo(5));
        
        // Should return scores from the 5 most recent records in descending order (latest first)
        var expectedScores = new List<double> { 1.0, 0.9, 0.8, 0.7, 0.6 };
        
        Assert.That(lastFiveScores.Count, Is.EqualTo(5));
        for (int i = 0; i < expectedScores.Count; i++)
        {
            Assert.That(lastFiveScores[i], Is.EqualTo(expectedScores[i]).Within(0.0001));
        }
    }
}