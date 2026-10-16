using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ServerDesk.Core;

public sealed class BatchObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceWith(IEnumerable<T> values)
    {
        // Materialize first: a failed query must not erase the current list.
        var replacement=values.ToArray(); CheckReentrancy();
        Items.Clear(); foreach(var value in replacement)Items.Add(value);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
