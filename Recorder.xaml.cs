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

namespace SoundReader
{
    /// <summary>
    /// Recorder.xaml の相互作用ロジック
    /// </summary>
    public partial class Recorder : Page
    {
        public Recorder()
        {
            InitializeComponent();
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

            // subscribe Levelmeter event
            mainWindow.waveIn.DataAvailable += UpdateRecLevelMeter;

            // freeze control
            Rec_start.IsEnabled = false;
            Rec_stop.IsEnabled = true;
            mainWindow.audio_device_list.IsEnabled = false;
            Rec_base_filename.IsEnabled = false;
            Rec_numbering_filename.IsEnabled = false;
            Rec_time.IsEnabled = false;

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
                        Thread.Sleep(1000);
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
                        Thread.Sleep(100);

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
                        Thread.Sleep(1000);

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
                        Thread.Sleep(100);

                        if (mainWindow.waveIn == null)
                            return;
                    }
                    time_count.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        time_count.Content = $"{0.1 * sleep_iter}";
                    }));
                    Thread.Sleep(200);

                    // shutdown
                    mainWindow.waveIn?.StopRecording();
                    mainWindow.waveIn?.Dispose();
                    mainWindow.waveIn = null;

                    mainWindow.waveWriter?.Close();
                    mainWindow.waveWriter = null;

                    Dispatcher.Invoke(() =>
                    {
                        Rec_numbering_filename.Value += 1;

                        // enable to be variable
                        Rec_start.IsEnabled = true;
                        mainWindow.audio_device_list.IsEnabled = true;
                        Rec_base_filename.IsEnabled = true;
                        Rec_numbering_filename.IsEnabled = true;
                        Rec_time.IsEnabled = true;
                    });
                });
            }
        }

        private void Rec_stop_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = (MainWindow)App.Current.MainWindow;

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

            Rec_numbering_filename.Value += 1;
        }
        private void UpdateRecLevelMeter(object sender, WaveInEventArgs e)
        {
            var max = 0f;
            for (var i = 0; i < e.BytesRecorded; i += 2)
            {
                var sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i + 0]);
                var sample32 = sample / 32768f;
                if (sample32 < 0) sample32 = -sample32;
                if (sample32 > max) max = sample32;
            }

            Dispatcher.Invoke(() =>
            {
                var current = 100.0 * max;
                var lv = 0.0;
                if (mainWindow.Rec_Level_Meter.Value <= current)
                {
                    lv = (0.2 * mainWindow.Rec_Level_Meter.Value + 0.8 * current);
                    if (max >= 1.0)
                    {
                        mainWindow.Rec_Level_Meter.Foreground = new SolidColorBrush((Color)this.Resources["scarlet"]);
                    }
                    else
                    {
                        mainWindow.Rec_Level_Meter.Foreground = new SolidColorBrush((Color)this.Resources["light_green"]);
                    }
                }
                else
                {
                    lv = (0.8 * mainWindow.Rec_Level_Meter.Value + 0.2 * current);
                    if (max >= 1.0)
                    {
                        mainWindow.Rec_Level_Meter.Foreground = new SolidColorBrush((Color)this.Resources["scarlet"]);
                    }
                    else
                    {
                        var color = (Color)Application.Current.Resources["light_green"];
                        color.A = 150;
                        mainWindow.Rec_Level_Meter.Foreground = new SolidColorBrush(color);
                    }
                }
                mainWindow.Rec_Level_Meter_Value.Text = lv.ToString("0");
                mainWindow.Rec_Level_Meter.Value = lv;
            });
        }

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
}
