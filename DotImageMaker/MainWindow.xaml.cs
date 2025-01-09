using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DotImageMaker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private int _height = 128;

        public int height
        {
            get { return _height; }
            set
            {
                if (_height != value)
                {
                    _height = value;
                }
            }
        }

        private int _width = 128;

        public int width
        {
            get { return _width; }
            set
            {
                if (width != value)
                {
                    _width = value;
                }
            }
        }

        private int dotSize = 10;

        public int DotSize
        {
            get { return dotSize; }
            set
            {
                if (dotSize != value)
                {
                    if (value < dotRadius * 2)
                    {
                        dotSize = dotRadius * 2;
                    }
                    else
                    {
                        dotSize = value;
                    }
                    OnPropertyChanged();
                }
            }
        }

        private int dotRadius = 4;

        public int DotRadius
        {
            get { return dotRadius; }
            set
            {
                if (dotRadius != value)
                {
                    if (value * 2 > dotSize)
                    {
                        dotRadius = dotSize / 2;
                    }
                    else
                    {
                        dotRadius = value;
                    }
                    OnPropertyChanged();
                }
            }
        }

        private bool useGray;

        public bool UseGray
        {
            get { return useGray; }
            set 
            {
                useGray = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseSrgb));
            }
        }

        public bool UseSrgb
        {
            get { return !useGray; }
            set 
            { 
                useGray = !value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseGray));
            }
        }

        public string FileName => nowPath.Split('\\')[^1];

        private string nowPath = "";

        public string NowPath
        {
            get { return nowPath; }
            set
            {
                if (nowPath != value)
                {
                    nowPath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FileName));
                }
            }
        }

        private int totalbyte = 1;

        private int nowbyte = 0;

        private DispatcherTimer timer = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName]string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        public MainWindow()
        {
            InitializeComponent();
            timer.Tick += UpdateInfo;
            timer.Interval = TimeSpan.FromMilliseconds(50);
        }

        private void ImportImageBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFile = new()
            {
                Filter = "Image File|*.png;*.jpg;*.jpeg;*.heic",
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true,
            };
            bool? result = openFile.ShowDialog();
            if (result == true)
            {
                NowPath = openFile.FileName;
            }
        }

        private async void SummonBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NowPath)) return;
            SummonBtn.IsEnabled = false;
            timer.Start();
            BitmapSource? bs;
            if (useGray)
                bs = await SummonGray();
            else
                bs = await SummonSRGB();
            timer.Stop();
            Info.Text = "preparing for downloading...";
            SaveImage(bs);
            Info.Text = "finished!";
            SummonBtn.IsEnabled = true;
            bs = null;
            GC.Collect();
        }

        private async Task<BitmapSource> SummonGray()
        {
            nowbyte = 0;
            BitmapImage img = new();
            img.BeginInit();
            img.UriSource = new Uri(NowPath, UriKind.Absolute);
            img.DecodePixelWidth = width;
            img.DecodePixelHeight = height;
            img.EndInit();
            FormatConvertedBitmap fbm = new(img, PixelFormats.Gray8, null, 0);
            byte[] imgbuf = new byte[width * height];
            fbm.CopyPixels(imgbuf, img.PixelWidth, 0);
            fbm.Freeze();
            img.Freeze();
            totalbyte = width * height;
            byte[] imgarr = new byte[(100 + width * DotSize) * (100 + height * DotSize) * 4];
            Array.Fill(imgarr, (byte)0);
            Lock arrlock = new();
            PixelFormat format = PixelFormats.Bgra32;
            int stride = (100 + width * DotSize) * 4;

            List<(int, int)> circle = CaculateCircle();

            await Task.Run(() => {
                Parallel.For(0, height, (h) =>
                {
                    for (int w = 0; w < width; w++)
                    {
                        byte bit = imgbuf[h * width + w];
                        int _x = 50 + DotSize * w;
                        int _y = 50 + DotSize * h;
                        int bitindex = (h * width + w) * 3;
                        foreach ((int, int) i in circle)
                        {
                            lock (arrlock)
                            {
                                int index = ((i.Item2 + _y) * (width * DotSize + 100) + (i.Item1 + _x)) * 4;
                                imgarr[index] = bit;
                                imgarr[index + 1] = bit;
                                imgarr[index + 2] = bit;
                                imgarr[index + 3] = 255;

                            }
                        }
                        ++nowbyte;
                    }
                });
            });
            return BitmapSource.Create(100 + width * DotSize, 100 + height * DotSize, 96, 96, format, null, imgarr, stride);
            
        }

        private async Task<BitmapSource> SummonSRGB()
        {
            nowbyte = 0;
            BitmapImage img = new();
            img.BeginInit();
            img.UriSource = new Uri(NowPath, UriKind.Absolute);
            img.DecodePixelWidth = width;
            img.DecodePixelHeight = height;
            img.EndInit();
            FormatConvertedBitmap fbm = new(img, PixelFormats.Bgr24, null, 0);
            byte[] imgbuf = new byte[width * height * 3];
            fbm.CopyPixels(imgbuf, img.PixelWidth * 3, 0);
            totalbyte = width * height;
            byte[] imgarr = new byte[(100 + width * DotSize) * (100 + height * DotSize) * 4];
            Array.Fill(imgarr, (byte)0);
            Lock arrlock = new();
            PixelFormat format = PixelFormats.Bgra32;
            int stride = (100 + width * DotSize) * 4;
            List<(int, int)> circle = CaculateCircle();

            await Task.Run(() => {
                Parallel.For(0, height, (h) =>
                {
                    for (int w = 0; w < width; w++)
                    {
                        int _x = 50 + DotSize * w;
                        int _y = 50 + DotSize * h;
                        int bitindex = (h * width + w) * 3;
                        foreach ((int, int) i in circle)
                        {
                            lock (arrlock)
                            {
                                int index = ((i.Item2 + _y) * (width * DotSize + 100) + (i.Item1 + _x)) * 4;
                                imgarr[index] = imgbuf[bitindex];
                                imgarr[index + 1] = imgbuf[bitindex + 1];
                                imgarr[index + 2] = imgbuf[bitindex + 2];
                                imgarr[index + 3] = 255;

                            }
                        }
                        ++nowbyte;
                    }
                });
            });
            return BitmapSource.Create(100 + width * DotSize, 100 + height * DotSize, 96, 96, format, null, imgarr, stride);
        }

        private void SaveImage(BitmapSource bs)
        {
            OpenFolderDialog openFolder = new()
            {
                Multiselect = false,
            };
            bool? result = openFolder.ShowDialog();
            if (result == true)
            {
                FileStream fs = File.Create($"{openFolder.FolderName}\\{DateTime.Now.ToFileTimeUtc()}.png");
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(bs));
                encoder.Save(fs);
                fs.Close();
            }
        }

        private void UpdateInfo(object? sender, object? e)
        {
            Info.Text = $"summoning... {100 * nowbyte / totalbyte}%";
        }

        private List<(int, int)> CaculateCircle()
        {
            List<(int, int)> list = new();
            int mid_x = (int)(DotSize * 0.5);
            int mid_y = (int)(DotSize * 0.5);
            int max_x = DotSize;
            int max_y = DotSize;
            for (int y = 0; y < max_y; ++y)
            {
                int bk = 0;
                for (int x = 0; x < max_x; ++x)
                {
                    if (Math.Pow(mid_x - x, 2) + Math.Pow(mid_y - y, 2) <= Math.Pow(DotRadius, 2))
                    {
                        bk = 1;
                        list.Add((x, y));
                    }
                    else
                    {
                        if (bk == 1) break;
                    }
                }
            }
            return list;
        }
    }
}