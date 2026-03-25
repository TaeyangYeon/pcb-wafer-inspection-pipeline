using System;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InspectionPipeline.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private object? currentPage;

    [ObservableProperty]
    private bool isApiConnected;

    [ObservableProperty]
    private string statusMessage = "Disconnected";

    public MainViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        
        // Initialize with Dashboard
        CurrentPage = _serviceProvider.GetRequiredService<DashboardViewModel>();
        IsApiConnected = false;
        StatusMessage = "API Disconnected";
    }

    [RelayCommand]
    private void Navigate(string? pageName)
    {
        if (string.IsNullOrEmpty(pageName))
            return;

        CurrentPage = pageName switch
        {
            "Dashboard" => _serviceProvider.GetRequiredService<DashboardViewModel>(),
            "Inspection" => _serviceProvider.GetRequiredService<InspectionViewModel>(),
            "History" => _serviceProvider.GetRequiredService<HistoryViewModel>(),
            "Monitor" => _serviceProvider.GetRequiredService<MonitorViewModel>(),
            "Settings" => _serviceProvider.GetRequiredService<SettingsViewModel>(),
            _ => CurrentPage
        };
    }
}