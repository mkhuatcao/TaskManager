using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Management;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TaskManager
{
    /// <summary>
    /// Interaction logic for ManagedProcessesWindow.xaml
    /// </summary>
    /// 
    public partial class ManagedProcessesWindow : Window
    {
        public ObservableCollection<Process> ProcessesList = new ObservableCollection<Process>();
        static public Dictionary<int, bool> ResumeFlags = new Dictionary<int, bool>();
        static public Dictionary<int, bool> LogFlags = new Dictionary<int, bool>();
        public Timer RefreshTimer;

        private ListSortDirection _sortDirection;
        private GridViewColumnHeader _sortColumn;
        private MainWindow _parentWindow;
        private Dictionary<int, String> _processesExecPath = new Dictionary<int, string>();

        public ManagedProcessesWindow(MainWindow parent)
        {
            _parentWindow = parent;
            InitializeComponent();
            Closing += this.OnWindowClosing;

            var tmp = (CollectionViewSource)(this.FindResource("MyProcesses"));
            tmp.Source = ProcessesList;

            RefreshTimer = new Timer(1000);
            RefreshTimer.Elapsed += refresh;
            RefreshTimer.AutoReset = true;
            RefreshTimer.Enabled = true;

        }

        public void AddProcess(Process process)
        {            
            lock (ProcessesList)
            {
                ResumeFlags.Add(process.Id, false);
                LogFlags.Add(process.Id, false);
                _processesExecPath.Add(process.Id, GetMainModuleFilepath(process.Id));
                ProcessesList.Add(process);
            }
        }
        
        //Source:
        //https://stackoverflow.com/questions/9501771
        private string GetMainModuleFilepath(int processId)
        {
            string wmiQueryString = "SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE ProcessId = " + processId;
            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            {
                using (var results = searcher.Get())
                {
                    ManagementObject mo = results.Cast<ManagementObject>().FirstOrDefault();
                    if (mo != null)
                    {
                        return (string)mo["ExecutablePath"];
                    }
                }
            }
            return null;
        }

        public void refresh(object sender, EventArgs e)
        {
            int index = 0;
            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                ICollectionView view = CollectionViewSource.GetDefaultView(MyListView.ItemsSource);
                index = view.CurrentPosition;
            }));

            lock (ProcessesList)
            {
                var processesToRemove = new List<Process>();
                foreach (var process in ProcessesList)
                {
                    if (process.HasExited)
                    {
                        bool remove = true;
                        if (LogFlags[process.Id])
                        {
                            File.AppendAllText(@"log.txt", "Process " + process.ProcessName + " has exited at " + DateTime.Now.ToString() + Environment.NewLine);
                        }
                        if (ResumeFlags[process.Id])
                        {
                            try
                            {
                                var old_id = process.Id;
                                process.StartInfo.FileName = _processesExecPath[process.Id];
                                process.Start();
                                //add records
                                ResumeFlags.Add(process.Id, ResumeFlags[old_id]);
                                LogFlags.Add(process.Id, LogFlags[old_id]);
                                _processesExecPath.Add(process.Id, _processesExecPath[old_id]);
                                //remove old records
                                ResumeFlags.Remove(old_id);
                                LogFlags.Remove(old_id);
                                _processesExecPath.Remove(old_id);
                                remove = false;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message);
                                remove = true;
                            }
                        }
                        if(remove)
                        {   
                            processesToRemove.Add(process);
                            ResumeFlags.Remove(process.Id);
                            LogFlags.Remove(process.Id);
                            _processesExecPath.Remove(process.Id);
                        }
                    }
                    else
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => process.Refresh()));
                    }
                }
                foreach (var process in processesToRemove)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                        ProcessesList.Remove(process);
                    }));
                }
            }

            

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ICollectionView view = CollectionViewSource.GetDefaultView(MyListView.ItemsSource);
                view.Refresh();
                view.MoveCurrentToPosition(index);
            }));
        }

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

        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            RefreshTimer.Dispose();
            _parentWindow.ManagedProcessesButton.IsEnabled = true;
        }

        private void ResumeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var item = (CheckBox)sender;
            var process = (Process)item.DataContext;
            ResumeFlags[process.Id] = item.IsChecked ?? false;
        }
        private void LogCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var item = (CheckBox)sender;
            var process = (Process)item.DataContext;
            LogFlags[process.Id] = item.IsChecked ?? false;
        }

        private void RemoveProcess_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            var process = item.DataContext;
            lock (ProcessesList)
            {
                ProcessesList.Remove((Process)process);
                ResumeFlags.Remove(((Process)process).Id);
                LogFlags.Remove(((Process)process).Id);
                _processesExecPath.Remove(((Process)process).Id);
            }
        }
    }
}
