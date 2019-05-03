using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TaskManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Process> ProcessesList = new ObservableCollection<Process>(Process.GetProcesses());
        public Timer RefreshTimer;

        private ManagedProcessesWindow _managedWindow;
        private ListSortDirection _sortDirection;
        private GridViewColumnHeader _sortColumn;
 
        public MainWindow()
        {
            InitializeComponent();
            Closing += this.OnWindowClosing;

            var tmp = (CollectionViewSource)(this.FindResource("MyProcesses"));
            tmp.Source = ProcessesList;
            tmp.View.Filter = ProcessFilter;

            RefreshTimer = new Timer(1000);
            RefreshTimer.Elapsed += refresh;
            RefreshTimer.AutoReset = true;
            RefreshTimer.Enabled = true;
        }

        private bool ProcessFilter(object item)
        {
            Process process = item as Process;
            
            if (process.BasePriority <= 4 && (checkBox_idle.IsChecked ?? false))
                return true;
            if (process.BasePriority > 4 && process.BasePriority < 8 && (checkBox_belowNormal.IsChecked ?? false))
                return true;
            if (process.BasePriority == 8 && (checkBox_normal.IsChecked ?? false))
                return true;
            if (process.BasePriority > 8 && process.BasePriority < 13 && (checkBox_aboveNormal.IsChecked ?? false))
                return true;
            if (process.BasePriority == 13 && (checkBox_high.IsChecked ?? false))
                return true;
            if (process.BasePriority == 24 && (checkBox_realTime.IsChecked ?? false))
                return true;
            return false;
        }

        public void refresh(object sender, EventArgs e)
        {
            var NewProcesses = new List<Process>(Process.GetProcesses());
            
            int index = 0;
            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                ICollectionView view = CollectionViewSource.GetDefaultView(MyListView.ItemsSource);
                index = view.CurrentPosition;
            }));

            lock (ProcessesList)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => ProcessesList.Clear()));
                foreach (var process in NewProcesses)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => this.ProcessesList.Add(process)));
                }
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ICollectionView view = CollectionViewSource.GetDefaultView(MyListView.ItemsSource);
                view.Refresh();
                try
                {
                    view.MoveCurrentToPosition(index);
                }
                catch (Exception)
                {
                    MyListView.UnselectAll();
                }
            }));
        }

        //Source:
        //https://code.msdn.microsoft.com/windowsdesktop/Sorting-a-WPF-ListView-by-5769086f
        private void MyProcessesColumnClick(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = e.OriginalSource as GridViewColumnHeader;
            if (column == null)
            {
                return;
            }

            if (_sortColumn == column)
            {
                // Toggle sorting direction 
                _sortDirection = _sortDirection == ListSortDirection.Ascending ?
                                                   ListSortDirection.Descending :
                                                   ListSortDirection.Ascending;
            }
            else
            {
                // Remove arrow from previously sorted header 
                if (_sortColumn != null)
                {
                    _sortColumn.Column.HeaderTemplate = null;
                    _sortColumn.Column.Width = _sortColumn.ActualWidth - 20;
                }

                _sortColumn = column;
                _sortDirection = ListSortDirection.Ascending;
                column.Column.Width = column.ActualWidth + 20;
            }

            if (_sortDirection == ListSortDirection.Ascending)
            {
                column.Column.HeaderTemplate = Application.Current.Resources["ArrowUp"] as DataTemplate;
            }
            else
            {
                column.Column.HeaderTemplate = Application.Current.Resources["ArrowDown"] as DataTemplate;
            }

            string header = string.Empty;

            // if binding is used and property name doesn't match header content 
            Binding b = _sortColumn.Column.DisplayMemberBinding as Binding;
            if (b != null)
            {
                header = b.Path.Path;
            }

            ICollectionView resultDataView = CollectionViewSource.GetDefaultView(MyListView.ItemsSource);
            resultDataView.SortDescriptions.Clear();
            resultDataView.SortDescriptions.Add(new SortDescription(header, _sortDirection));
        }

        private void KillProcess_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            var process = item.DataContext;
            try
            {
                ((Process)process).Kill();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ChangePriority_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            var process = item.DataContext;
            try
            {
                switch (item.Header)
                {
                    case "Idle":
                        ((Process)process).PriorityClass = ProcessPriorityClass.Idle;
                        break;
                    case "BelowNormal":
                        ((Process)process).PriorityClass = ProcessPriorityClass.BelowNormal;
                        break;
                    case "Normal":
                        ((Process)process).PriorityClass = ProcessPriorityClass.Normal;
                        break;
                    case "AboveNormal":
                        ((Process)process).PriorityClass = ProcessPriorityClass.AboveNormal;
                        break;
                    case "High":
                        ((Process)process).PriorityClass = ProcessPriorityClass.High;
                        break;
                    case "RealTime":
                        ((Process)process).PriorityClass = ProcessPriorityClass.RealTime;
                        break;
                    default:
                        break;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Access Denied");
            }
        }

        private void StackPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MyListView.UnselectAll();
        }

        private void ManagedProcessesButton_Click(object sender, RoutedEventArgs e)
        {
            _managedWindow = new ManagedProcessesWindow(this);
            _managedWindow.Show();
            ManagedProcessesButton.IsEnabled = false;
        }

        private void AddToManaged_Click(object sender, RoutedEventArgs e)
        {
            if(!ManagedProcessesButton.IsEnabled)
            {
                var item = (MenuItem)sender;
                var process = item.DataContext;
                try
                {
                    if(!((Process)process).HasExited)
                    {
                        _managedWindow.AddProcess((Process)process);
                    }
                }
                catch (Exception ex)
                {
                    if(ex is ArgumentException)
                        MessageBox.Show("Process already in list.");
                    else if(ex is Win32Exception)
                        MessageBox.Show("Access denied.");
                    else
                        MessageBox.Show(ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Open managed processes window first.");
            }
        }

        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            RefreshTimer.Dispose();
        }
    }
}
