using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView.Views;

public partial class LogsView : UserControl
{
    public LogsView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is LogsViewModel vm)
        {
            vm.Logs.CollectionChanged += OnLogsCollectionChanged;
        }
    }

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && DataContext is LogsViewModel vm && vm.Logs.Count > 0)
        {
            LogList?.ScrollIntoView(vm.Logs[^1]);
        }
    }
}
