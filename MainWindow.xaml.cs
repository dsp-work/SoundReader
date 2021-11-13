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

        // for visualize
        private readonly object energyLock = new object();
        private double accumulatedSquareSum;
        private int accumulatedSampleCount;
        private const int SamplesPerColumn = 40;
        private readonly double[] energy = new double[(uint)(EnergyBitmapWidth * 1.25)];
        private int energyIndex;
        private const int EnergyBitmapWidth = 780;
        private const int EnergyBitmapHeight = 195;
        private int newEnergyAvailable;

            // for render
        private DateTime? lastEnergyRefreshTime;
        private double energyError;
        private int energyRefreshIndex;
        private readonly WriteableBitmap energyBitmap;
        private readonly Int32Rect fullEnergyRect = new Int32Rect(0, 0, EnergyBitmapWidth, EnergyBitmapHeight);
        private readonly byte[] backgroundPixels = new byte[EnergyBitmapWidth * EnergyBitmapHeight];
        private byte[] foregroundPixels;



        public delegate double ToDoubleBitConverter(byte[] buffer, int startIndex);

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

            // Initialize bitmap
            this.energyBitmap = 
                new WriteableBitmap(
                    EnergyBitmapWidth,
                    EnergyBitmapHeight,
                    96,
                    96,
                    PixelFormats.Indexed1,
                    new BitmapPalette(
                        new List<Color> {
                            Colors.White,
                            Color.FromRgb(204, 255, 102)
                        }
                    )
                );
        }

        private void RibbonWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize foreground pixels
            this.foregroundPixels = new byte[EnergyBitmapHeight];
            for (int i = 0; i < this.foregroundPixels.Length; ++i)
            {
                this.foregroundPixels[i] = 0xff;
            }

            this.waveDisplay.Source = this.energyBitmap;

            CompositionTarget.Rendering += UpdateEnergy;
        }

        private void RibbonWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.waveIn)
            {
                CompositionTarget.Rendering -= UpdateEnergy;
            }
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

        
            // Bottom portion of computed energy signal that will be discarded as noise.
            // Only portion of signal above noise floor will be displayed.
            const double EnergyNoiseFloor = 0.2;

            var BytesPerSample = waveIn.WaveFormat.BitsPerSample / 8;

            ToDoubleBitConverter converter = null;
            if (waveIn.WaveFormat.Encoding == WaveFormatEncoding.Pcm) {
                converter = new SpecificBitConverter().FromShort;
            }
            else if (waveIn.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat) {
                converter = new SpecificBitConverter().FromFloat;
            } else {
                MessageBox.Show("Unknown WaveIn format types.");
                if (waveIn != null)
                {
                    WhenToStopRec();
                    return;
                }
            }

            //int readCount = audioStream.Read(audioBuffer, 0, audioBuffer.Length);
            byte[] audioBuffer = new byte[e.Buffer.Length];
            Buffer.BlockCopy(e.Buffer, 0, audioBuffer, 0, e.Buffer.Length);

            // Calculate energy corresponding to captured audio.
            // In a computationally intensive application, do all the processing like
            // computing energy, filtering, etc. in a separate thread.
            lock (this.energyLock)
            {
                for (int i = 0; i < audioBuffer.Length; i += BytesPerSample*waveIn.WaveFormat.Channels)
                {
                    // compute the sum of squares of audio samples that will get accumulated
                    // into a single energy value.
                    double audioSample = short.MaxValue*converter(audioBuffer, i);
                    this.accumulatedSquareSum += audioSample * audioSample;
                    ++this.accumulatedSampleCount;

                    if (this.accumulatedSampleCount < SamplesPerColumn)
                    {
                        continue;
                    }

                    // Each energy value will represent the logarithm of the mean of the
                    // sum of squares of a group of audio samples.
                    double meanSquare = this.accumulatedSquareSum / SamplesPerColumn;
                    double amplitude = Math.Log(meanSquare) / Math.Log(int.MaxValue);

                    // Renormalize signal above noise floor to [0,1] range.
                    this.energy[this.energyIndex] = Math.Max(0, amplitude - EnergyNoiseFloor) / (1 - EnergyNoiseFloor);
                    this.energyIndex = (this.energyIndex + 1) % this.energy.Length;

                    this.accumulatedSquareSum = 0;
                    this.accumulatedSampleCount = 0;
                    ++this.newEnergyAvailable;
                }
            }
        }

        private void UpdateEnergy(object sender, EventArgs e)
        {
            lock (this.energyLock)
            {
                // Calculate how many energy samples we need to advance since the last update in order to
                // have a smooth animation effect
                DateTime now = DateTime.UtcNow;
                DateTime? previousRefreshTime = this.lastEnergyRefreshTime;
                this.lastEnergyRefreshTime = now;

                if (waveIn == null)
                    return;
                double SamplesPerMillisecond = 1e-3 / (double)waveIn.WaveFormat.SampleRate;

                // No need to refresh if there is no new energy available to render
                if (this.newEnergyAvailable <= 0)
                {
                    return;
                }

                if (previousRefreshTime != null)
                {
                    double energyToAdvance = this.energyError + (((now - previousRefreshTime.Value).TotalMilliseconds * SamplesPerMillisecond) / SamplesPerColumn);
                    int energySamplesToAdvance = Math.Min(this.newEnergyAvailable, (int)Math.Round(energyToAdvance));
                    this.energyError = energyToAdvance - energySamplesToAdvance;
                    this.energyRefreshIndex = (this.energyRefreshIndex + energySamplesToAdvance) % this.energy.Length;
                    this.newEnergyAvailable -= energySamplesToAdvance;
                }

                // clear background of energy visualization area
                this.energyBitmap.WritePixels(fullEnergyRect, this.backgroundPixels, EnergyBitmapWidth, 0);

                // Draw each energy sample as a centered vertical bar, where the length of each bar is
                // proportional to the amount of energy it represents.
                // Time advances from left to right, with current time represented by the rightmost bar.
                int baseIndex = (this.energyRefreshIndex + this.energy.Length - EnergyBitmapWidth) % this.energy.Length;
                for (int i = 0; i < EnergyBitmapWidth; ++i)
                {
                    const int HalfImageHeight = EnergyBitmapHeight / 2;

                    // Each bar has a minimum height of 1 (to get a steady signal down the middle) and a maximum height
                    // equal to the bitmap height.
                    int barHeight = (int)Math.Max(1.0, (this.energy[(baseIndex + i) % this.energy.Length] * EnergyBitmapHeight));

                    // Center bar vertically on image
                    var barRect = new Int32Rect(i, HalfImageHeight - (barHeight / 2), 1, barHeight);

                    // Draw bar in foreground color
                    this.energyBitmap.WritePixels(barRect, foregroundPixels, 1, 0);
                }
            }
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

