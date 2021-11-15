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
using System.ComponentModel;
using System.Threading;

using Fluent;

using NAudio.Wave;
using NAudio.Codecs;
using NAudio.CoreAudioApi;

using Microsoft.Kinect;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Diagnostics;

namespace SoundReader
{
    /// <summary>
    /// Recorder.xaml の相互作用ロジック
    /// </summary>
    public partial class Recorder : Page
    {
        // for visualize
        private readonly object energyLock = new object();
        private double accumulatedSquareSum;
        private int accumulatedSampleCount;
        private int SamplesPerColumn;
        private readonly double[] energy = new double[(uint)(EnergyBitmapWidth * 3)];
        private int energyIndex;
        private const int EnergyBitmapWidth = 780;
        private const int EnergyBitmapHeight = 195;
        private int newEnergyAvailable;
        bool volume_warning = false;

        // for render
        private Stopwatch lastEnergyRefreshTime = new Stopwatch();
        private double energyError;
        private int energyRefreshIndex;
        private readonly WriteableBitmap energyBitmap;
        private readonly Int32Rect fullEnergyRect = new Int32Rect(0, 0, EnergyBitmapWidth, EnergyBitmapHeight);
        private readonly byte[] backgroundPixels = new byte[EnergyBitmapWidth * EnergyBitmapHeight];
        private byte[] foregroundPixels;
        int frameRate = 120;
        long elapesd_time = 0;

        public delegate double ToDoubleBitConverter(byte[] buffer, int startIndex);

        public Recorder()
        {
            InitializeComponent();

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

            // Initialize foreground pixels
            this.foregroundPixels = new byte[EnergyBitmapHeight];
            for (int i = 0; i < this.foregroundPixels.Length; ++i)
            {
                this.foregroundPixels[i] = 0xff;
            }
            this.waveDisplay.Source = this.energyBitmap;

            CompositionTarget.Rendering += UpdateEnergy;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var new_page = new Render();
            NavigationService.Navigate(new_page);
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

        private void Rec_start_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = (MainWindow)App.Current.MainWindow;
            mainWindow.waveIn = new WasapiCapture(
                new MMDeviceEnumerator()
                    .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .ToArray()
                    .ElementAt(mainWindow.audio_device_list.SelectedIndex));

            // define save files
            var file_base = Rec_base_filename.Text;
            var file_num = $"{Rec_numbering_filename.Value}";
            mainWindow.waveWriter = new WaveFileWriter($"{mainWindow.save_dir}/{file_base}_{file_num}.wav", mainWindow.waveIn.WaveFormat);
            mainWindow.waveIn.DataAvailable += (_, ee) =>
            {
                mainWindow.waveWriter.Write(ee.Buffer, 0, ee.BytesRecorded);
                mainWindow.waveWriter.Flush();
            };
            mainWindow.waveIn.RecordingStopped += (_, __) =>
            {
                if (mainWindow.waveWriter != null)
                    mainWindow.waveWriter.Flush();
            };

            // subscribe Wave Display event
            mainWindow.waveIn.DataAvailable += AudioReadingThread;

            // subscribe Levelmeter event
            mainWindow.waveIn.DataAvailable += UpdateRecLevelMeter;

            // freeze control
            Rec_start.IsEnabled = false;
            Rec_stop.IsEnabled = true;
            mainWindow.audio_device_list.IsEnabled = false;
            Rec_base_filename.IsEnabled = false;
            Rec_numbering_filename.IsEnabled = false;
            Rec_time.IsEnabled = false;

            // itnitialize parameter
            this.newEnergyAvailable = 0;
            this.volume_warning = false;

            if (Rec_time.Value == 0)
            {
                Task.Run(() =>
                {
                    int i = 0;
                    // count down for recording
                    for (i = 3; i > 0; --i)
                    {
                        //test.Write(i);
                        time_count.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            time_count.Content = $"{i}";
                        }));
                        Task.Delay(1000).Wait();
                    }

                    // Recording
                    mainWindow.waveIn.StartRecording();
                    for (i = 0; ; ++i)
                    {
                        time_count.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            time_count.Content = $"{0.1 * i}";
                        }));
                        Task.Delay(100).Wait();

                        if (mainWindow.waveIn == null)
                            return;
                    }
                });
            }
            else
            {
                uint sleep_iter = (uint)(Rec_time.Value / 0.1);
                Task.Run(() =>
                {
                    // count down for recording
                    for (int i = 3; i > 0; --i)
                    {
                        time_count.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            time_count.Content = $"{i}";
                        }));
                        Task.Delay(1000).Wait();

                        if (mainWindow.waveIn == null)
                            return;
                    }

                    // Recording
                    mainWindow.waveIn.StartRecording();
                    for (int i = 0; i < sleep_iter; ++i)
                    {
                        //test.Write(i);
                        time_count.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            time_count.Content = $"{0.1 * i}";
                        }));
                        Task.Delay(100).Wait();

                        if (mainWindow.waveIn == null)
                            return;
                    }
                    time_count.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        time_count.Content = $"{0.1 * sleep_iter}";
                    }));
                    Task.Delay(200).Wait();

                    Dispatcher.Invoke(() =>
                    {
                        if (mainWindow.waveIn != null)
                            WhenToStopRec();
                    });
                });
            }
        }

        private void Rec_stop_Click(object sender, RoutedEventArgs e)
        {
            WhenToStopRec();
        }

        private void WhenToStopRec()
        {
            var mainWindow = (MainWindow)App.Current.MainWindow;

            var freq_time = 0.001 * (double)mainWindow.waveIn.WaveFormat.SampleRate;

            Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    mainWindow.waveIn?.StopRecording();
                    mainWindow.waveIn?.Dispose();
                    mainWindow.waveIn = null;

                    mainWindow.waveWriter?.Close();
                    mainWindow.waveWriter = null;

                    // adopt variable
                    Rec_start.IsEnabled = true;
                    Rec_stop.IsEnabled = false;
                    mainWindow.audio_device_list.IsEnabled = true;
                    Rec_base_filename.IsEnabled = true;
                    Rec_numbering_filename.IsEnabled = true;
                    Rec_base_filename.IsEnabled = true;
                    Rec_numbering_filename.IsEnabled = true;

                    Rec_numbering_filename.Value += 1;
                });
            });
            /* FIXME 録音後の0波形表示
            Task.Run(() =>
            {

                this.accumulatedSquareSum = 0;
                this.accumulatedSampleCount = 0;

                for (int i = 0; i < EnergyBitmapWidth * SamplesPerColumn; i += (int)freq_time)
                {
                    for (int j = 0; j < (int)freq_time; ++j)
                    {
                        this.energy[this.energyIndex] = 0;
                        this.energyIndex = (this.energyIndex + 1) % this.energy.Length;
                        ++this.newEnergyAvailable;
                    }
                    Task.Delay(1).Wait();
                }
            });*/
        }

        private void UpdateRecLevelMeter(object sender, WaveInEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var mainWindow = (MainWindow)App.Current.MainWindow;

                ToDoubleBitConverter converter = null;
                if (mainWindow.waveIn.WaveFormat.Encoding == WaveFormatEncoding.Pcm)
                {
                    converter = (byte[] buffer, int startIndex) =>
                    {
                        return new SpecificBitConverter().FromShort(buffer, startIndex) / (double)short.MaxValue;
                    };
                }
                else if (mainWindow.waveIn.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    converter = new SpecificBitConverter().FromFloat;
                }
                else
                {
                    MessageBox.Show("Unknown WaveIn format types.");
                    if (mainWindow.waveIn != null)
                    {
                        WhenToStopRec();
                        return;
                    }
                }

                var max = 0.0;
                var BytesPerSample = mainWindow.waveIn.WaveFormat.BitsPerSample / 8;


                for (var i = 0; i < e.Buffer.Length; i += BytesPerSample * mainWindow.waveIn.WaveFormat.Channels)
                {
                    var sample = converter(e.Buffer, i);
                    if (sample < 0) sample = -sample;
                    if (sample > max) max = sample;
                }

                Dispatcher.Invoke(() =>
                {
                    var current = 100.0 * max;
                    var lv = 0.0;
                    if (Rec_Level_Meter.Value <= current)
                    {
                        lv = (0.2 * Rec_Level_Meter.Value + 0.8 * current);
                        if (max >= 1.0)
                        {
                            Rec_Level_Meter.Foreground = new SolidColorBrush((Color)Application.Current.Resources["scarlet"]);
                        }
                        else
                        {
                            Rec_Level_Meter.Foreground = new SolidColorBrush((Color)Application.Current.Resources["light_green"]);
                        }
                    }
                    else
                    {
                        lv = (0.95 * Rec_Level_Meter.Value + 0.05 * current);
                        if (max >= 1.0)
                        {
                            Rec_Level_Meter.Foreground = new SolidColorBrush((Color)Application.Current.Resources["scarlet"]);
                        }
                        else
                        {
                            var color = (Color)Application.Current.Resources["light_green"];
                            color.A = 150;
                            Rec_Level_Meter.Foreground = new SolidColorBrush(color);
                        }
                    }
                    Rec_Level_Meter_Value.Text = lv.ToString("0");
                    Rec_Level_Meter.Value = lv;
                });
            });
        }


        private void AudioReadingThread(object sender, WaveInEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var mainWindow = (MainWindow)App.Current.MainWindow;

                // Bottom portion of computed energy signal that will be discarded as noise.
                // Only portion of signal above noise floor will be displayed.
                const double EnergyNoiseFloor = 0.2;

                var BytesPerSample = mainWindow.waveIn.WaveFormat.BitsPerSample / 8;

                ToDoubleBitConverter converter = null;
                if (mainWindow.waveIn.WaveFormat.Encoding == WaveFormatEncoding.Pcm)
                {
                    converter = (byte[] buffer, int startIndex) =>
                    {
                        return new SpecificBitConverter().FromShort(buffer, startIndex) / (double)short.MaxValue;
                    };
                }
                else if (mainWindow.waveIn.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    converter = new SpecificBitConverter().FromFloat;
                }
                else
                {
                    MessageBox.Show("Unknown WaveIn format types.");
                    if (mainWindow.waveIn != null)
                    {
                        WhenToStopRec();
                        return;
                    }
                }

                byte[] audioBuffer = new byte[e.Buffer.Length];
                Buffer.BlockCopy(e.Buffer, 0, audioBuffer, 0, e.Buffer.Length);

                // Calculate energy corresponding to captured audio.
                // In a computationally intensive application, do all the processing like
                // computing energy, filtering, etc. in a separate thread.
                lock (this.energyLock)
                {
                    for (int i = 0; i < audioBuffer.Length; i += BytesPerSample * mainWindow.waveIn.WaveFormat.Channels)
                    {
                        // compute the sum of squares of audio samples that will get accumulated
                        // into a single energy value.
                        double audioSample = short.MaxValue * converter(audioBuffer, i);
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
                        var next_energy = Math.Max(0, amplitude - EnergyNoiseFloor) / (1 - EnergyNoiseFloor);
                        if (next_energy >= 1.0 && volume_warning == false)
                        {
                            MessageBox.Show("入力音声が録音可能なボリュームの上限を超えている可能性があります。デバイスのボリュームコントロールを確認してください。");
                            this.energy[this.energyIndex] = 0.9999;
                        }
                        else if (next_energy >= 1.0 && volume_warning == true)
                        {
                            this.energy[this.energyIndex] = 0.9999;
                        }
                        else
                        {
                            this.energy[this.energyIndex] = next_energy;
                        }
                        this.energyIndex = (this.energyIndex + 1) % this.energy.Length;

                        this.accumulatedSquareSum = 0;
                        this.accumulatedSampleCount = 0;
                        ++this.newEnergyAvailable;
                    }
                }
            });
        }

        private void UpdateEnergy(object sender, EventArgs e)
        {
            var mainWindow = (MainWindow)App.Current.MainWindow;

            lock (this.energyLock)
            {
                // Calculate how many energy samples we need to advance since the last update in order to
                // have a smooth animation effect
                lastEnergyRefreshTime.Stop();
                elapesd_time += lastEnergyRefreshTime.ElapsedMilliseconds;
                lastEnergyRefreshTime.Reset();
                lastEnergyRefreshTime.Start();


                if (mainWindow.waveIn == null)
                    return;
                if (elapesd_time > 1)
                {
                    this.frameRate = (int)((this.frameRate + 1000.0 / (double)elapesd_time) / 2.0);
                    this.SamplesPerColumn = (int)((double)mainWindow.waveIn.WaveFormat.SampleRate / (double)frameRate / 1.75);
                    elapesd_time = 0;
                }
                double energyPerMilliseconds = (double)mainWindow.waveIn.WaveFormat.SampleRate / 1000.0 / (double)SamplesPerColumn;

                // No need to refresh if there is no new energy available to render
                if (this.newEnergyAvailable <= 0)
                {
                    return;
                }

                //                if (elapesd_time != 0)
                {
                    double energyToAdvance = this.energyError + elapesd_time * energyPerMilliseconds;
                    int energySamplesToAdvance = Math.Min(this.newEnergyAvailable, (int)Math.Floor(energyToAdvance));
                    this.energyError = energyToAdvance - (double)energySamplesToAdvance;
                    this.energyRefreshIndex = (this.energyRefreshIndex + this.newEnergyAvailable) % this.energy.Length;
                    this.newEnergyAvailable -= this.newEnergyAvailable;
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

            /* FIX ME 
                private void DisplaySignalArea_SizeChanged(object sender, SizeChangedEventArgs e)
                {
                    DisplaySignalArea.UpdateLayout();
                    DisplaySignalArea.InvalidateVisual();

                    DisplaySignalArea.ActualHeightValue = DisplaySignalArea.ActualHeight;
                    DisplaySignalArea.ActualWidthValue = DisplaySignalArea.ActualWidth;

                    DisplaySignalCanvas.Height = DisplaySignalArea.ActualHeight;
                    DisplaySignalCanvas.Width = DisplaySignalArea.ActualWidth;
                    DisplaySignalCanvas.UpdateLayout();
                    DisplaySignalCanvas.InvalidateVisual();

                    DisplaySignalCanvas_.Height = DisplaySignalArea.ActualHeight;
                    DisplaySignalCanvas_.Width = DisplaySignalArea.ActualWidth;
                    DisplaySignalCanvas_.UpdateLayout();
                    DisplaySignalCanvas_.InvalidateVisual();

                    this.InvalidateArrange();
                    this.InvalidateVisual();
                }*/
        }
    }

    public class SpecificBitConverter
    {
        public double FromDouble(byte[] buffer, int startIndex)
        {
            return BitConverter.ToDouble(buffer, startIndex);
        }

        public double FromShort(byte[] buffer, int startIndex)
        {
            return (double)BitConverter.ToInt16(buffer, startIndex);
        }

        public double FromFloat(byte[] buffer, int startIndex)
        {
            return (double)BitConverter.ToSingle(buffer, startIndex);
        }
    }

    public class NotifyGrid : Grid, INotifyPropertyChanged
    {
        double actual_height;
        double actual_width;

        public double ActualHeightValue
        {
            get
            {
                return actual_height;
            }
            set
            {
                actual_height = value;
                NotifyPropertyChanged("ActualHeightValue");
            }
        }

        public double ActualWidthValue
        {
            get
            {
                return actual_width;
            }
            set
            {
                actual_width = value;
                NotifyPropertyChanged("ActualWidthValue");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class CanvasAutoSize : Canvas
    {
        protected override System.Windows.Size MeasureOverride(System.Windows.Size constraint)
        {
            base.MeasureOverride(constraint);
            double width = base
                .InternalChildren
                .OfType<UIElement>()
                .Max(i => i.DesiredSize.Width + (double)i.GetValue(Canvas.LeftProperty));

            double height = base
                .InternalChildren
                .OfType<UIElement>()
                .Max(i => i.DesiredSize.Height + (double)i.GetValue(Canvas.TopProperty));

            return new Size(width, height);
        }
    }
}
