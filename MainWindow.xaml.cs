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

using Microsoft.Kinect;




namespace SoundReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RibbonWindow
    {
        int audio_in_device_id = -1;
        WaveInEvent waveIn;
        WaveFileWriter waveWriter;

        public MainWindow()
        {
            InitializeComponent();

            // device list update
            List<string> lt = GetDevices();
            audio_device_list.ItemsSource = lt.ToArray();

            // Input volume slider initialize
            MMDeviceEnumerator DevEnum = new MMDeviceEnumerator();
            MMDevice device = DevEnum.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            Input_volume.Value = device.AudioEndpointVolume.MasterVolumeLevelScalar;

        }

        public List<string> GetDevices()
        {
            List<string> deviceList = new List<string>();
            /*           for (int i = 0; i < WaveIn.DeviceCount; i++)
                       {
                           var capabilities = WaveIn.GetCapabilities(i);
                           deviceList.Add(capabilities.ProductName);
                       }*/
            var MMdeviceList = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (MMDevice device in MMdeviceList)
            {
                deviceList.Add(device.FriendlyName);
            }
            return deviceList;
        }

        public int GetWaveInDeviceID(string name)
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var cap = WaveIn.GetCapabilities(i);
                if (cap.ProductName.Contains(name))
                    return i;
            }
            return -1;
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

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MMDeviceEnumerator DevEnum = new MMDeviceEnumerator();
            MMDevice device = DevEnum.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Input_volume.Value;
        }

        private void Rec_start_Click(object sender, RoutedEventArgs e)
        {
            waveIn = new WaveInEvent();
            waveIn.DeviceNumber = GetWaveInDeviceID(audio_device_list.SelectedItem.ToString());
            waveIn.WaveFormat = new WaveFormat(44100, WaveIn.GetCapabilities(waveIn.DeviceNumber).Channels);

            waveWriter = new WaveFileWriter("test1.wav", waveIn.WaveFormat);

            waveIn.DataAvailable += (_, ee) =>
            {
                waveWriter.Write(ee.Buffer, 0, ee.BytesRecorded);
                waveWriter.Flush();
            };
            waveIn.RecordingStopped += (_, __) =>
            {
                if (waveWriter != null)
                    waveWriter.Flush();
            };

            waveIn.StartRecording();
        }

        private void Rec_stop_Click(object sender, RoutedEventArgs e)
        {
            waveIn?.StopRecording();
            waveIn?.Dispose();
            waveIn = null;

            waveWriter?.Close();
            waveWriter = null;
        }
    }

}
