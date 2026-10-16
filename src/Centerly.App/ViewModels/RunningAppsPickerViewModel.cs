using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Centerly.App.Services;
using Centerly.Core.AutoCenter;

namespace Centerly.App.ViewModels;

internal sealed record RunningAppItem(RunningApp App, string DisplayName, ImageSource? Icon);

/// <summary>List of running applications with a search box, shown in the "add application" dialog.</summary>
internal sealed partial class RunningAppsPickerViewModel : ObservableObject
{
    public RunningAppsPickerViewModel(IEnumerable<RunningApp> apps, IEnumerable<string> excludedProcessNames)
    {
        var excluded = new HashSet<string>(excludedProcessNames, StringComparer.OrdinalIgnoreCase);
        Items = apps
            .Where(a => !excluded.Contains(a.ProcessName))
            .Select(a => new RunningAppItem(a, AppIcons.GetDisplayName(a.ExecutablePath, a.ProcessName), AppIcons.Get(a.ExecutablePath)))
            .OrderBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        View = CollectionViewSource.GetDefaultView(Items);
        View.Filter = Matches;
        Selected = Items.FirstOrDefault();
    }

    public IReadOnlyList<RunningAppItem> Items { get; }

    public ICollectionView View { get; }

    public bool IsEmpty => Items.Count == 0;

    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial RunningAppItem? Selected { get; set; }

    partial void OnFilterChanged(string value)
    {
        View.Refresh();
        // Keep a sensible selection so Enter adds the best match.
        if (Selected is null || !View.Cast<RunningAppItem>().Contains(Selected))
            Selected = View.Cast<RunningAppItem>().FirstOrDefault();
    }

    private bool Matches(object item)
    {
        if (string.IsNullOrWhiteSpace(Filter))
            return true;

        var app = (RunningAppItem)item;
        var filter = Filter.Trim();
        return app.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
               || app.App.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || app.App.WindowTitle.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }
}
