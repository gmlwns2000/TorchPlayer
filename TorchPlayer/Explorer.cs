using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace TorchPlayer
{
    public class PlaybackOpenedEventArgs : EventArgs, IDisposable
    {
        //public MediaPlayer MediaPlayer { get; set; }
        //public IRandomAccessStream SourceStream { get; set; }
        //public string MIME { get; set; }
        public string Address { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }

        public PlaybackOpenedEventArgs()
        {

        }

        public void Dispose()
        {
            //SourceStream?.Dispose();
            //SourceStream = null;
        }
    }

    public class ExplorerEntry
    {
        public Visibility DirectoryIconVisibility => IsDirectory ? Visibility.Visible : Visibility.Collapsed;
        public Visibility MediaIconVisibility => IsMedia && (!IsDirectory) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FileIconVisibility => (!IsMedia) && (!IsDirectory) ? Visibility.Visible : Visibility.Collapsed;
        public bool IsDirectory { get; set; }
        public bool IsMedia { get; set; }
        public string DisplayName { get; set; }
        public string FullName { get; set; }
        public ICommand Command { get; set; }
    }

    public abstract class Explorer : INotifyPropertyChanged
    {
        ObservableCollection<ExplorerEntry> entryList = new ObservableCollection<ExplorerEntry>();
        public ObservableCollection<ExplorerEntry> EntryList
        {
            get => entryList;
            set { entryList = value; OnPropertyChanged(); }
        }
        public List<ExplorerEntry> ActualEntryList { get; set; } = new List<ExplorerEntry>();
        public char PathSpliter { get; set; } = '/';
        public string RootPath { get; set; } = "/";
        string path = "/";
        public string Path
        {
            get => path;
            set
            {
                path = value;
                var split = path.TrimEnd(PathSpliter).Split(PathSpliter);
                CurrentDirectory = split[split.Length - 1];
                OnPropertyChanged();
            }
        }
        string currentDirectory;
        public string CurrentDirectory
        {
            get => currentDirectory;
            private set { currentDirectory = value; OnPropertyChanged(); }
        }

        public ICommand GoUpCommand => new UICommand(() => GoUp());
        public ICommand RefreshCommand => new UICommand(() => Refresh());
        public ICommand GoBackCommand => new UICommand(() => GoBack());

        string searchTerm;
        public string SearchTerm
        {
            get => searchTerm;
            set { searchTerm = value; Search(); OnPropertyChanged(); }
        }

        public abstract event EventHandler<PlaybackOpenedEventArgs> PlaybackOpened;
        public event PropertyChangedEventHandler PropertyChanged;

        Stack<string> history = new Stack<string>();

        public void GoUp()
        {
            var split = Path.TrimStart(PathSpliter).Split(PathSpliter).ToList();
            if (split.Count > 0)
            {
                split.RemoveAt(split.Count - 1);
                var path = RootPath;
                foreach (var item in split)
                {
                    if (item.Length > 0)
                        path += PathSpliter + item.Trim(PathSpliter);
                }
                if (path != Path)
                    GoToAbs(path);
            }
        }

        public void GoToSubDir(string subDir)
        {
            Path = Path.TrimEnd('/') + "/" + subDir.TrimStart('/');
            GoToAbs(Path);
        }

        public void GoToAbs(string path)
        {
            Path = path;
            history.Push(path);
            Refresh();
        }

        public void GoBack()
        {
            if (history.Count <= 1)
                return;
            history.Pop();
            Path = history.Peek();
            Refresh();
        }

        public void Search()
        {
            if (SearchTerm.Trim().Length > 0)
            {
                var keys = SearchTerm.Trim().Split(' ');
                var searched = new List<(int, ExplorerEntry)>();
                foreach (var item in ActualEntryList)
                {
                    var score = 0;
                    foreach (var key in keys)
                    {
                        if (item.DisplayName.ToLower().IndexOf(key.ToLower()) > -1)
                            score += 1;
                    }
                    if (score > 0 && item.IsDirectory) score += 10000;
                    if (score > 0) searched.Add((score, item));
                }
                searched = searched.OrderByDescending((x) => x.Item1).ToList();
                var entrylist = new List<ExplorerEntry>();
                foreach (var item in searched)
                    entrylist.Add(item.Item2);
                UpdateEntryList(entrylist);
            }
            else
            {
                UpdateEntryList(ActualEntryList);
            }
        }

        public abstract void Refresh();

        protected void UpdateEntryList(List<ExplorerEntry> list)
        {
            EntryList.Clear();
            foreach (var item in list)
                EntryList.Add(item);
            //EntryList = new ObservableCollection<ExplorerEntry>(list);
        }

        protected async void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
