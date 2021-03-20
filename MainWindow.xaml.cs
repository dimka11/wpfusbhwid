using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Usb.Events;

namespace UsbHwID
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static List<string> _drives = new List<string>();
        private CancellationTokenSource cancelTokenSource;
        private CancellationToken token;
        static readonly IUsbEventWatcher usbEventWatcher = new UsbEventWatcher();

        public MainWindow()
        {
            InitializeComponent();
            UpdateDeviceList();
        }

        private void MainWindowLoaded(object sender, EventArgs e)
        {
            if (File.Exists("hwid.txt"))
            {
                var hwid = ReadHwidFromFile();
                Label1.Content = "установленный идентификатор:" + hwid;
                Button1.IsEnabled = false;
                CreateCancelToken();
                _ = CompareSavedDeviceHwID(hwid);
                var pt = PeriodicTask.Run(CompareSavedDeviceHwID_, new TimeSpan(0, 0, 5), token, hwid);
                usbEventWatcher.UsbDriveEjected += (_, path) => MessageBox.Show($"usb device was ejected {path}");
                //todo https://github.com/Jinjinov/Usb.Events
            }
        }

        async Task CompareSavedDeviceHwID(string hwid)
        {
            foreach (var usbDevice in GetUSBDevices())
            {
                if (hwid == usbDevice.PnpDeviceID)
                {
                    MessageBox.Show("hwid установлен и устройство подключено");
                    return;
                }
            }

            MessageBox.Show("hwid установлен и устройство не подключено программа будет завершена");
            await Task.Delay(2000);
            Application.Current.Shutdown();
        }

        void CompareSavedDeviceHwID_(string hwid)
        {
            UpdateDeviceList();
            foreach (var usbDevice in GetUSBDevices())
            {
                if (hwid == usbDevice.PnpDeviceID)
                {
                    return;
                }
            }
            MessageBox.Show("hwid установлен и устройство не подключено программа будет завершена");
            Application.Current.Shutdown();
        }

        void CreateCancelToken()
        {
            cancelTokenSource = new CancellationTokenSource();
            token = cancelTokenSource.Token;
        }

        void UpdateDeviceList()
        {
            var usbDevices = GetUSBDevices();
            ListBox.Items.Clear();
            foreach (var usbDevice in usbDevices)
            {
                if (usbDevice.Description.Equals("USB Mass Storage Device"))
                {
                    Console.WriteLine(usbDevice.PnpDeviceID);
                    ListBox.Items.Add(usbDevice.PnpDeviceID);
                }
            }
        }

        string ReadHwidFromFile()
        {
            return File.ReadAllText("hwid.txt");
        }
        void WriteHwidFromFile(string hwidstring)
        {
            string path = "hwid.txt";
            File.WriteAllText(path, hwidstring);
        }

        private void ButtonSet(object sender, RoutedEventArgs e)
        {
            var selected = (String)ListBox.SelectedItem;
            if (selected != null)
            {
                MessageBox.Show("usb hw id установлен");
                Label1.Content = "установленный идентификатор:" + selected;
                CreateCancelToken();
                Button1.IsEnabled = false;
                WriteHwidFromFile(selected);
                var pt = PeriodicTask.Run(CompareSavedDeviceHwID_, new TimeSpan(0, 0, 5), token, selected);
            }
        }

        private void ButtonUpdate(object sender, RoutedEventArgs e)
        {
            UpdateDeviceList();
        }

        static List<USBDeviceInfo> GetUSBDevices()
        {
            var devices = new List<USBDeviceInfo>();

            ManagementObjectCollection collection;
            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
                collection = searcher.Get();

            foreach (var device in collection)
            {
                devices.Add(new USBDeviceInfo(
                    (string)device.GetPropertyValue("DeviceID"),
                    (string)device.GetPropertyValue("PNPDeviceID"),
                    (string)device.GetPropertyValue("Description")
                ));
            }
            collection.Dispose();
            return devices;
        }

        private void ButtonDelete(object sender, RoutedEventArgs e)
        {
            if (File.Exists("hwid.txt"))
            {
                File.Delete("hwid.txt");
                MessageBox.Show("hwid удален");
                Button1.IsEnabled = true;
                cancelTokenSource.Cancel();
            }
            else
            {
                MessageBox.Show("usb hwid еще не установлен");
            }
        }
    }

    public class PeriodicTask
    {
        public static async Task Run(Action<string> action, TimeSpan period, CancellationToken cancellationToken, string hwid)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(period, cancellationToken);

                if (!cancellationToken.IsCancellationRequested)
                    action(hwid);
            }
        }

        public static Task Run(Action<string> action, TimeSpan period, string hwid)
        {
            return Run(action, period, CancellationToken.None, hwid);
        }
    }
}
