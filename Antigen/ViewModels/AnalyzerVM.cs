using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Antigen.Models.Analyzer;
using Antigen.Models.Settings;
using Antigen.Services;
using Antigen.ViewModels.Analyzer;
using DynamicData;
using DynamicData.Binding;
using Mutagen.Bethesda.Analyzers.SDK.Topics;
using Noggog;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels;

public sealed partial class AnalyzerVM : ResizablePanelVM, IActiveScoped
{
    public static Severity[] SeverityValues { get; } = Enum.GetValues<Severity>();

    private readonly NavigationController _navigation;
    private readonly Func<AnalyzerVM, SettingsVM> _settingsVMFactory;
    private readonly Func<AnalyzerVM, DashboardVM> _dashboardVMFactory;
    private readonly AnalyzerResultVM.Factory _resultVMFactory;

    private SettingsVM? _settingsVM;
    private DashboardVM? _dashboardVM;
    private AnalyzerResultVM? _configuringResult;

    public ModWatcherVM ModWatcher { get; }
    public ObservableCollectionExtended<Severity> EnabledSeverities { get; } = new(Enum.GetValues<Severity>());
    public ReadOnlyObservableCollection<AnalyzerResultVM> FilteredResults { get; }

    [Reactive] public partial string SearchText { get; set; } = string.Empty;

    public AnalyzerVM(
        NavigationController navigation,
        Func<AnalyzerVM, SettingsVM> settingsVMFactory,
        ModWatcherVM modWatcher,
        Func<AnalyzerVM, DashboardVM> dashboardVMFactory,
        AnalyzerResultVM.Factory resultVMFactory)
    {
        _navigation = navigation;
        _settingsVMFactory = settingsVMFactory;
        ModWatcher = modWatcher;
        _dashboardVMFactory = dashboardVMFactory;
        _resultVMFactory = resultVMFactory;
        IsExpanded = true;

        // Transform to vms and apply filters
        ModWatcher.ApplicableResults
            .Connect()
            .Transform(info =>
            {
                var vm = _resultVMFactory(info, ModWatcher.IgnoreResult);

                // Only one row's ignore overlay is open at a time; close the previous one
                vm.ConfigureRequested
                    .Subscribe(targetVm =>
                    {
                        if (_configuringResult is { } previous && previous != targetVm)
                        {
                            previous.IsConfiguring = false;
                        }

                        _configuringResult = targetVm;
                    })
                    .DisposeWith(vm);

                return vm;
            })
            .DisposeMany()
            .Filter(EnabledSeverities.ObserveCollectionChanges()
                .Unit()
                .StartWith(Unit.Default)
                .Select(_ => new Func<AnalyzerResultVM, bool>(result => EnabledSeverities.Contains(result.Result.Topic.Severity))))
            .Filter(this.WhenAnyValue(x => x.SearchText)
                .Unit()
                .StartWith(Unit.Default)
                .Select(_ => new Func<AnalyzerResultVM, bool>(result =>
                {
                    if (string.IsNullOrWhiteSpace(SearchText)) return true;

                    return result.ModName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                        result.RecordDisplayName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                        result.ParentDisplayName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                        result.Result.Topic.TopicDefinition.Title?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                        result.Result.Topic.FormattedTopic.TopicDefinition.MessageFormat?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true;
                })))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Bind(out var readOnlyObservableCollection)
            .Subscribe()
            .DisposeWith(this);

        FilteredResults = readOnlyObservableCollection;
    }

    [ReactiveCommand]
    private void ToggleSeverity(Severity severity)
    {
        if (!EnabledSeverities.Remove(severity))
        {
            EnabledSeverities.Add(severity);
        }
    }

    [ReactiveCommand]
    private void OpenDashboard()
    {
        _navigation.GoTo(_dashboardVM ??= _dashboardVMFactory(this));
    }

    [ReactiveCommand]
    private void OpenSettings()
    {
        _navigation.GoTo(_settingsVM ??= _settingsVMFactory(this));
    }
}
