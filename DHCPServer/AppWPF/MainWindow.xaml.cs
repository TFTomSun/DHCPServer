using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.ServiceProcess;
using System.Security.Principal;
using System.Windows.Threading;

namespace AppWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private EventLog eventLog;
        private DateTime eventLogTimeFilter;
        private ServiceController serviceController;
        private DispatcherTimer dispatcherTimer;
        private const string saneTimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

        public MainWindow()
        {
            InitializeComponent();

            eventLog = new EventLog();

            this.eventLog.Log = "DHCPServerLog";
            //this.eventLog.Log = "Application";
            this.eventLog.SynchronizingObject = new DispatcherISyncInvoke(this.Dispatcher);
            this.eventLog.EntryWritten += new System.Diagnostics.EntryWrittenEventHandler(this.eventLog_EntryWritten);
            this.eventLog.EnableRaisingEvents = true;

            SetTimeFilter(DateTime.Now);

            serviceController = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == "DHCPServer");

            if (serviceController == null)
            {
                if (MessageBox.Show("Service has not been installed yet, install?", "DHCP Server", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    //Install();
                }
            }

            UpdateServiceStatus();

            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.IsEnabled = true;
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            UpdateServiceStatus();
        }

        private void UpdateServiceStatus()
        {
            serviceController.Refresh();
            //System.Diagnostics.Debug.WriteLine(m_Service.Status.ToString());

            if (!Utils.HasAdministrativeRight())
            {
                buttonStart.IsEnabled = false;
                buttonStop.IsEnabled = false;
                buttonConfigure.IsEnabled = false;
                buttonElevate.IsEnabled = true;
            }
            else
            {
                buttonStart.IsEnabled = (serviceController.Status == ServiceControllerStatus.Stopped);
                buttonStop.IsEnabled = (serviceController.Status == ServiceControllerStatus.Running);
                buttonConfigure.IsEnabled = true;
                buttonElevate.IsEnabled = false;
            }

            lblServiceStatus.Text = $"Service status: {serviceController.Status}";
        }


        private void SetTimeFilter(DateTime filter)
        {
            eventLogTimeFilter = filter;
            if (eventLogTimeFilter == DateTime.MinValue)
            {
                labelTimeFilter.Content = "Showing all logging";
            }
            else
            {
                labelTimeFilter.Content = $"Showing log starting at: {filter.ToString(saneTimeFormat)}";
            }
            RebuildLog();
        }

        private void RebuildLog()
        {
            try
            {
                textBoxEventLog.BeginChange();
                textBoxEventLog.Clear();
                var sb = new StringBuilder();

                foreach (var entry in eventLog.Entries.OfType<EventLogEntry>().Where(x => x.TimeGenerated > eventLogTimeFilter))
                {
                    sb.AppendLine(TranslateEventLogEntry(entry));
                }

                textBoxEventLog.Text = sb.ToString();
            }
            finally
            {
                textBoxEventLog.EndChange();
            }
        }

        private static string TranslateEventLogEntry(EventLogEntry entry)
        {
            string entryType;
            switch (entry.EntryType)
            {
                case EventLogEntryType.Error:
                    entryType = "ERROR";
                    break;

                case EventLogEntryType.Warning:
                    entryType = "WARNING";
                    break;

                default:
                case EventLogEntryType.Information:
                    entryType = "INFO";
                    break;
            }

            return $"{entry.TimeGenerated.ToString(saneTimeFormat)} : {entryType} : {entry.Message?.TrimEnd('\r','\n','\'')}";
        }

        private void AddEventLogEntry(EventLogEntry entry)
        {
            if (entry.TimeGenerated > eventLogTimeFilter)
            {
                textBoxEventLog.AppendText(TranslateEventLogEntry(entry) + "\r\n");
            }
        }

        private void eventLog_EntryWritten(object sender, EntryWrittenEventArgs e)
        {
            AddEventLogEntry(e.Entry);
        }

        private void ButtonHistoryClear_Click(object sender, RoutedEventArgs e)
        {
            SetTimeFilter(DateTime.Now);
        }

        private void ButtonHistoryAll_Click(object sender, RoutedEventArgs e)
        {
            SetTimeFilter(DateTime.MinValue);
        }

        private void ButtonHistoryOneDay_Click(object sender, RoutedEventArgs e)
        {
            if (eventLogTimeFilter > DateTime.MinValue)
            {
                SetTimeFilter(eventLogTimeFilter.AddDays(-1));
            }
        }

        private void ButtonHistoryOneHour_Click(object sender, RoutedEventArgs e)
        {
            if (eventLogTimeFilter > DateTime.MinValue)
            {
                SetTimeFilter(eventLogTimeFilter.AddHours(-1));
            }
        }

        private void ButtonElevate_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.RunElevated(Process.GetCurrentProcess().MainModule.FileName, ""))
            {
                Close();
            }
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                serviceController.Stop();
            }
            catch (Exception)
            {
            }
            UpdateServiceStatus();
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                serviceController.Start();
            }
            catch (Exception)
            {
            }
            UpdateServiceStatus();
        }
    }
}
