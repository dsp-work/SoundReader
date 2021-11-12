using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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
using System.Windows.Forms;

using Fluent;

using NAudio.Wave;
using NAudio.CoreAudioApi;
using Microsoft.WindowsAPICodePack.Dialogs;



namespace SoundReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>


    public partial class MainWindow : RibbonWindow
    {
        int audio_in_device_id = -1;
        public IWaveIn waveIn;
        public WaveFileWriter waveWriter;
        public string save_dir = Environment.CurrentDirectory;

        public delegate double ToDoubleBitConverter(byte[] buffer, int startIndex);
        byte[] audio_buffer;



        public MainWindow()
        {
            InitializeComponent();

            Uri uri = new Uri("/Recorder.xaml", UriKind.Relative);
            frame.Source = uri;

            // device list update
            List<string> lt = GetDevices();
            audio_device_list.ItemsSource = lt.ToArray();

            // Input volume slider initialize
            MMDeviceEnumerator DevEnum = new MMDeviceEnumerator();
            MMDevice device = DevEnum.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            Input_volume.Value = device.AudioEndpointVolume.MasterVolumeLevelScalar;
            Tab_Close();
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

            MMDeviceEnumerator DevEnum = new MMDeviceEnumerator();
            MMDevice device = DevEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToArray().ElementAt(audio_device_list.SelectedIndex);

            Input_volume.Value = device.AudioEndpointVolume.MasterVolumeLevelScalar;

            var local_waveIn = new WasapiCapture(
                new MMDeviceEnumerator()
                    .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .ToArray()
                    .ElementAt(audio_device_list.SelectedIndex));
            Rec_status.Text = $"{device.State}";
            Rec_ch.Text = $"{device.AudioEndpointVolume.Channels.Count}ch";
            Rec_bit.Text = $"{local_waveIn.WaveFormat.BitsPerSample}bit";
            Rec_sample.Text = $"{local_waveIn.WaveFormat.SampleRate}Hz";
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (audio_device_list.SelectedIndex <= 0)
                return;
            MMDeviceEnumerator DevEnum = new MMDeviceEnumerator();
            MMDevice device = DevEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToArray().ElementAt(audio_device_list.SelectedIndex);
            device.AudioEndpointVolume.MasterVolumeLevel = (float)Input_volume.Value;
        }

        private void Button_Click_Workin(object sender, RoutedEventArgs e)
        {
            using (var cofd = new CommonOpenFileDialog()
            {
                Title = "作業フォルダを選択してください",

                // フォルダ選択モードにする
                IsFolderPicker = true,
                RestoreDirectory = true,
            })
            {
                if (cofd.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    return;
                }

                // FileNameで選択されたフォルダを取得する
                System.Windows.MessageBox.Show($"【{cofd.FileName}】を選択しました。");
                save_dir = cofd.FileName;
            }
        }

        private void Button_Click_Render(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("/Render.xaml", UriKind.Relative);
            frame.Source = uri;
        }

        private void Button_Click_Recorder(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("/Recorder.xaml", UriKind.Relative);
            frame.Source = uri;
        }

        private void Button_Tab_Open(object sender, RoutedEventArgs e)
        {
            TabMenu.Visibility = Visibility.Visible;
            TabMenuControlerOpen.Visibility = Visibility.Hidden;
            TabMenuControlerClose.Visibility = Visibility.Visible;
        }

        private void Button_Tab_Close(object sender, RoutedEventArgs e)
        {
            Tab_Close();
        }

        private void Tab_Close()
        {
            TabMenu.Visibility = Visibility.Hidden;
            TabMenuControlerOpen.Visibility = Visibility.Visible;
            TabMenuControlerClose.Visibility = Visibility.Hidden;
        }
    }
}

