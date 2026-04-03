using System.Collections.ObjectModel;
using System.Windows.Input;
using Shed_Security_AP.Core;
using Shed_Security_AP.Models.Local;
using Shed_Security_AP.Services;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// Shows the local audit trail — every action you've taken through the dashboard
/// (kicks, bans, config saves, spawns, etc.) pulled from the daily JSON logs.
/// Useful for reviewing what happened or proving you didn't break something.
/// </summary>
public class HistoryViewModel : ViewModelBase
{
    private readonly IAuditService _auditService;

    public ObservableCollection<AuditEntry> Entries { get; } = [];

    public ICommand RefreshCommand { get; }
    public ICommand ClearHistoryCommand { get; }

    public HistoryViewModel(IAuditService auditService)
    {
        _auditService = auditService;

        RefreshCommand = new RelayCommand(_ => Refresh());

        ClearHistoryCommand = new RelayCommand(_ =>
        {
            _auditService.ClearAll();
            Entries.Clear();
        });

        Refresh();
    }

    public void Refresh()
    {
        Entries.Clear();
        foreach (var entry in _auditService.LoadEntries())
            Entries.Add(entry);
    }
}
