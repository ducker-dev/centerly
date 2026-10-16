using System.Runtime.CompilerServices;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centerly.App.Services;
using Centerly.Core.AutoCenter;

namespace Centerly.App.ViewModels;

/// <summary>One auto-centering rule in the list.</summary>
internal sealed partial class RuleItemViewModel : ObservableObject
{
    private readonly Action<AppRule> _update;
    private readonly Action<RuleItemViewModel> _remove;

    public RuleItemViewModel(AppRule rule, Action<AppRule> update, Action<RuleItemViewModel> remove)
    {
        Rule = rule;
        _update = update;
        _remove = remove;
        Icon = AppIcons.Get(rule.ExecutablePath);
    }

    public AppRule Rule { get; private set; }

    public string Title => Rule.Title;

    public string ProcessName => Rule.ProcessName;

    /// <summary>Exclusions have nothing to configure besides removal.</summary>
    public bool IsCentering => Rule.Action == RuleAction.Center;

    public ImageSource? Icon { get; }

    public bool IsEnabled
    {
        get => Rule.IsEnabled;
        set => Update(Rule with { IsEnabled = value });
    }

    public bool OnlyFirstWindow
    {
        get => Rule.OnlyFirstWindow;
        set => Update(Rule with { OnlyFirstWindow = value });
    }

    public bool IncludeDialogs
    {
        get => Rule.IncludeDialogs;
        set => Update(Rule with { IncludeDialogs = value });
    }

    [RelayCommand]
    private void Remove() => _remove(this);

    private void Update(AppRule updated, [CallerMemberName] string? propertyName = null)
    {
        if (updated == Rule)
            return;

        Rule = updated;
        _update(updated);
        OnPropertyChanged(propertyName);
    }
}
