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

using Fluent;

using NAudio.Wave;
using NAudio.Codecs;
using NAudio.CoreAudioApi;



namespace SoundReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RibbonWindow
    {
        int audio_in_device_id = -1;
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

        private void audio_device_list_DropDownOpened(object sender, EventArgs e)
        {
            List<string> lt = GetDevices();
            audio_device_list.ItemsSource = lt.ToArray();
        }

        private void audio_device_list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            audio_in_device_id = audio_device_list.SelectedIndex;
            if (audio_in_device_id == -1)
                MessageBox.Show("指定されたAudioデバイスが見つかりません。");
        }
    }

}
