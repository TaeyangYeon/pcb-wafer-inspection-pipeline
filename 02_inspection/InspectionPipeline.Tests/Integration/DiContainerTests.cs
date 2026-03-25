using System;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using InspectionPipeline.Core.Data;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.UI.DependencyInjection;

namespace InspectionPipeline.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class DiContainerTests
{
    private ServiceCollection _services;
    private ServiceProvider _serviceProvider;

    [SetUp]
    public void SetUp()
    {
        _services = new ServiceCollection();
        _services.AddInspectionPipelineServices("http://localhost:8502");
        _serviceProvider = _services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public void IInspectionRepository_Resolved_IsNotNull()
    {
        var repository = _serviceProvider.GetRequiredService<IInspectionRepository>();
        
        Assert.That(repository, Is.Not.Null);
    }

    [Test]
    public void IInspectionApiClient_Resolved_IsNotNull()
    {
        var apiClient = _serviceProvider.GetRequiredService<IInspectionApiClient>();
        
        Assert.That(apiClient, Is.Not.Null);
    }

    [Test]
    public void IFileWatcherService_Resolved_IsNotNull()
    {
        var fileWatcher = _serviceProvider.GetRequiredService<IFileWatcherService>();
        
        Assert.That(fileWatcher, Is.Not.Null);
    }

    [Test]
    public void IDriftMonitor_Resolved_IsNotNull()
    {
        var driftMonitor = _serviceProvider.GetRequiredService<IDriftMonitor>();
        
        Assert.That(driftMonitor, Is.Not.Null);
    }

    [Test]
    public void IAlarmService_Resolved_IsNotNull()
    {
        var alarmService = _serviceProvider.GetRequiredService<IAlarmService>();
        
        Assert.That(alarmService, Is.Not.Null);
    }

    [Test]
    public void IImageOverlayRenderer_Resolved_IsNotNull()
    {
        var renderer = _serviceProvider.GetRequiredService<IImageOverlayRenderer>();
        
        Assert.That(renderer, Is.Not.Null);
    }

    [Test]
    public void ISettingsService_Resolved_IsNotNull()
    {
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        
        Assert.That(settingsService, Is.Not.Null);
    }

    [Test]
    public void InspectionDbContext_Resolved_IsNotNull()
    {
        var dbContext = _serviceProvider.GetRequiredService<InspectionDbContext>();
        
        Assert.That(dbContext, Is.Not.Null);
    }

    [Test]
    public void AllCoreServices_Resolved_NoDuplicateRegistrations()
    {
        // Verify no scope validation errors by resolving all services
        var repository = _serviceProvider.GetRequiredService<IInspectionRepository>();
        var apiClient = _serviceProvider.GetRequiredService<IInspectionApiClient>();
        var fileWatcher = _serviceProvider.GetRequiredService<IFileWatcherService>();
        var driftMonitor = _serviceProvider.GetRequiredService<IDriftMonitor>();
        var alarmService = _serviceProvider.GetRequiredService<IAlarmService>();
        var renderer = _serviceProvider.GetRequiredService<IImageOverlayRenderer>();
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var dbContext = _serviceProvider.GetRequiredService<InspectionDbContext>();

        // Verify all services are correctly registered and can be resolved
        Assert.That(repository, Is.Not.Null);
        Assert.That(apiClient, Is.Not.Null);
        Assert.That(fileWatcher, Is.Not.Null);
        Assert.That(driftMonitor, Is.Not.Null);
        Assert.That(alarmService, Is.Not.Null);
        Assert.That(renderer, Is.Not.Null);
        Assert.That(settingsService, Is.Not.Null);
        Assert.That(dbContext, Is.Not.Null);

        // Verify singleton services return the same instance
        var driftMonitor2 = _serviceProvider.GetRequiredService<IDriftMonitor>();
        var alarmService2 = _serviceProvider.GetRequiredService<IAlarmService>();
        var renderer2 = _serviceProvider.GetRequiredService<IImageOverlayRenderer>();
        var settingsService2 = _serviceProvider.GetRequiredService<ISettingsService>();

        Assert.That(driftMonitor2, Is.SameAs(driftMonitor));
        Assert.That(alarmService2, Is.SameAs(alarmService));
        Assert.That(renderer2, Is.SameAs(renderer));
        Assert.That(settingsService2, Is.SameAs(settingsService));
    }
}