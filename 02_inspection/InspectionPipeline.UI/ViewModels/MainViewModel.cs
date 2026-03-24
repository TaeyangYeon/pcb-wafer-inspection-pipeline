using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InspectionPipeline.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? currentPage;

    [ObservableProperty]
    private bool isApiConnected;

    [ObservableProperty]
    private string statusMessage = "Disconnected";

    public MainViewModel()
    {
        // Initialize with placeholder - will be replaced with actual ViewModels
        CurrentPage = "Dashboard";
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
            "Dashboard" => "Dashboard",
            "Inspection" => "Inspection", 
            "History" => "History",
            "Monitor" => "Monitor",
            "Settings" => "Settings",
            _ => CurrentPage
        };
    }
}