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
using System.Windows.Forms;

using Fluent;

using NAudio.Wave;
using NAudio.Codecs;
using NAudio.CoreAudioApi;

using Microsoft.Kinect;



namespace SoundReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RibbonWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            List<string> lt = GetDevices();
            audio_device_list.ItemsSource = lt.ToArray();
        }

        public List<string> GetDevices()
        {
            List<string> deviceList = new List<string>();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                deviceList.Add(capabilities.ProductName);
            }
            return deviceList;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            List<string> lt = GetDevices();
            audio_device_list.ItemsSource = lt.ToArray();
        }
    }

}
