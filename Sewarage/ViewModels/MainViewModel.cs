using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Sewarage.Models;

namespace Sewarage.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private ObservableCollection<Plugin> PluginsList { get; set; } = new();
    
    public MainViewModel()
    {
    }

    public void AddNewFile(string name, string size)
    {
        PluginsList.Add(new Plugin {});
    }
}
