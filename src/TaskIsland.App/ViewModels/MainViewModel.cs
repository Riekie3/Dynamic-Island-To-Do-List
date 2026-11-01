using CommunityToolkit.Mvvm.ComponentModel;
using TaskIsland.Core.Services;

namespace TaskIsland.App.ViewModels;

/// <summary>Placeholder view model for the Phase 1 skeleton window. Replaced by IslandViewModel in Phase 2/3.</summary>
public sealed partial class MainViewModel(ITodoListRepository lists, ITaskRepository tasks) : ObservableObject
{
    [ObservableProperty]
    private string _status = "Starting...";

    public async Task RefreshAsync()
    {
        var allLists = await lists.GetAllAsync();
        var stats = TaskStats.Compute(await tasks.GetAllAsync());
        Status = $"Database ready. {allLists.Count} lists ({string.Join(", ", allLists.Select(l => l.Name))}), " +
                 $"{stats.Total} tasks.";
    }
}
