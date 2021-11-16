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
            mainWindow.waveIn = new WaveInEvent();
            mainWindow.waveIn.DeviceNumber = GetWaveInDeviceID(mainWindow.audio_device_list.SelectedItem.ToString());
            if (mainWindow.waveIn.DeviceNumber == -1)
                if (mainWindow.waveIn.DeviceNumber == -1)
            {
                MessageBox.Show("指定されたAudioデバイスが見つかりません。");
                return;
            }
            mainWindow.waveIn.WaveFormat = new WaveFormat(44100, WaveIn.GetCapabilities(mainWindow.waveIn.DeviceNumber).Channels);

            var file_base = Rec_base_filename.Text;
            var file_num = Rec_numbering_filename.Text;
            mainWindow.waveWriter = new WaveFileWriter($"{mainWindow.save_dir}/{file_base}_{file_num}.wav", mainWindow.waveIn.WaveFormat);

            mainWindow.waveIn.DataAvailable += (_, ee) =>
            {
                mainWindow.waveWriter.Write(ee.Buffer, 0, ee.BytesRecorded);
                mainWindow.waveWriter.Flush();
            };
            mainWindow.waveIn.DataAvailable += UpdateRecLevelMeter;
            mainWindow.waveIn.RecordingStopped += (_, __) =>
            {
                if (mainWindow.waveWriter != null)
                    mainWindow.waveWriter.Flush();
            };

            // freeze control
            Rec_start.IsEnabled = false;
            Rec_stop.IsEnabled = true;
            mainWindow.audio_device_list.IsEnabled = false;
            Rec_base_filename.IsEnabled = false;
            Rec_numbering_filename.IsEnabled = false;
            Rec_num_prev.IsEnabled = false;
            Rec_num_next.IsEnabled = false;

            mainWindow.waveIn.StartRecording();
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
            Rec_num_prev.IsEnabled = true;
            Rec_num_next.IsEnabled = true;

            Rec_numbering_filename.Value = Int32.Parse(Rec_numbering_filename.Text) + 1;
        }

        private void Rec_num_prev_Click(object sender, RoutedEventArgs e)
        {
            //Rec_numbering_filename.Text = $"{Int32.Parse(Rec_numbering_filename.Text) - 1}";
        }

        private void Rec_num_next_Click(object sender, RoutedEventArgs e)
        {
           // Rec_numbering_filename.Text = $"{Int32.Parse(Rec_numbering_filename.Text) + 1}";
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
                    lv = (0.8 * Rec_Level_Meter.Value + 0.2 * current);
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
