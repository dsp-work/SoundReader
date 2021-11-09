﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
using System.Windows.Forms;

using Fluent;

using NAudio.Wave;
using NAudio.Codecs;
using NAudio.CoreAudioApi;

using Microsoft.Kinect;
using Microsoft.WindowsAPICodePack.Dialogs;



namespace SoundReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RibbonWindow
    {
        int audio_in_device_id = -1;
        IWaveIn waveIn;
        WaveFileWriter waveWriter;
        string save_dir = Environment.CurrentDirectory;

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
                if (cap.ProductName.Contains(name.Substring(0, Math.Min(30, name.Length))))
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

        private void Rec_start_Click(object sender, RoutedEventArgs e)
        {
            waveIn = new WasapiCapture(
                new MMDeviceEnumerator()
                    .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .ToArray()
                    .ElementAt(audio_device_list.SelectedIndex));
            //waveIn.WaveFormat = new WaveFormat(16000, 4);

            var file_base = Rec_base_filename.Text;
            var file_num = Rec_numbering_filename.Text;
            waveWriter = new WaveFileWriter($"{save_dir}/{file_base}_{file_num}.wav" , waveIn.WaveFormat);

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

            // freeze control
            Rec_start.IsEnabled = false;
            Rec_stop.IsEnabled = true;
            audio_device_list.IsEnabled = false;
            Rec_base_filename.IsEnabled = false;
            Rec_numbering_filename.IsEnabled = false;
            Rec_num_prev.IsEnabled = false;
            Rec_num_next.IsEnabled = false;

            waveIn.StartRecording();
        }

        private void Rec_stop_Click(object sender, RoutedEventArgs e)
        {
            waveIn?.StopRecording();
            waveIn?.Dispose();
            waveIn = null;

            waveWriter?.Close();
            waveWriter = null;

            // adopt variable
            Rec_start.IsEnabled = true;
            Rec_stop.IsEnabled = false;
            audio_device_list.IsEnabled = true;
            Rec_base_filename.IsEnabled = true;
            Rec_numbering_filename.IsEnabled = true;
            Rec_num_prev.IsEnabled = true;
            Rec_num_next.IsEnabled = true;


            Rec_numbering_filename.Text = $"{Int32.Parse(Rec_numbering_filename.Text) + 1}";
        }

        private void Rec_num_prev_Click(object sender, RoutedEventArgs e)
        {
            Rec_numbering_filename.Text = $"{Int32.Parse(Rec_numbering_filename.Text) -1}";
        }

        private void Rec_num_next_Click(object sender, RoutedEventArgs e)
        {
            Rec_numbering_filename.Text = $"{Int32.Parse(Rec_numbering_filename.Text) +1}";
        }

        private void Button_Click_Saveto(object sender, RoutedEventArgs e)
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
        private void Rec_start_optional_Click(object sender, RoutedEventArgs e)
        {
            waveIn = new WaveInEvent();
            waveIn.DeviceNumber = GetWaveInDeviceID(audio_device_list.SelectedItem.ToString());
            if (waveIn.DeviceNumber == -1)
            {
                MessageBox.Show("指定されたAudioデバイスが見つかりません。");
                return;
            }
            waveIn.WaveFormat = new WaveFormat(44100, WaveIn.GetCapabilities(waveIn.DeviceNumber).Channels);
            var file_base = Rec_base_filename.Text;
            var file_num = Rec_numbering_filename.Text;
            waveWriter = new WaveFileWriter($"{file_base}_{file_num}.wav", waveIn.WaveFormat);

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

            Rec_time.Text = $"{Int32.Parse(Rec_time.Text)}";
            if (Rec_time.Text == "0")
            {
                waveIn.StartRecording();
            }
            else
            {
                waveIn.StartRecording();
                Thread.Sleep(Int32.Parse(Rec_time.Text) * 1000);
                waveIn?.StopRecording();
                waveIn?.Dispose();
                waveIn = null;

                waveWriter?.Close();
                waveWriter = null;

                Rec_numbering_filename.Text = $"{Int32.Parse(Rec_numbering_filename.Text) + 1}";
            }
        }

    }
}
