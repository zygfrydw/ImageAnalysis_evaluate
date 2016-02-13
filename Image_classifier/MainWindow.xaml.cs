using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Image_classifier.Annotations;
using Image_classifier.View.ViewModel;

namespace Image_classifier
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private bool _barrier;

        private ImageSource _currentFrame;
        private bool _entering;
        private bool _leaving;
        private bool _onTrack;
        private bool _train;

        private int currentFrameIndex;

        private string[] imageList;
        private Data[] imageTags;
        private string _imageSetPath;


        public MainWindow()
        {
            InitializeMethods();
            ImageSetPath = AppSettings.Default.ImageSetPath;
        
            InitializeComponent();
        }

        private void LoadImageSet()
        {
            currentFrameIndex = 0;
            imageList = Directory.GetFiles(ImageSetPath, "*.png", SearchOption.AllDirectories);
            imageList = (from im in imageList
                select new {_Path = im, Name = Path.GetFileNameWithoutExtension(im)}).GroupBy(x => x.Name)
                .Select(x => x.First()._Path).ToArray();
            imageTags = new Data[imageList.Length];

            if (imageList.Length == 0)
            {
                CurrentFrame = null;
                MessageBox.Show("You selected empty folder. Please select folder with png images.", "Empty folder");
                return;
            }
            LoadTags();
            ClearData();
            LoadCurrentImage();
        }

        public bool OnTrack
        {
            get { return _onTrack; }
            set
            {
                if (value == _onTrack) return;
                _onTrack = value;
                OnPropertyChanged();
            }
        }

        public bool Entering
        {
            get { return _entering; }
            set
            {
                if (value == _entering) return;
                _entering = value;
                OnPropertyChanged();
            }
        }

        public bool Leaving
        {
            get { return _leaving; }
            set
            {
                if (value == _leaving) return;
                _leaving = value;
                OnPropertyChanged();
            }
        }

        public bool Barrier
        {
            get { return _barrier; }
            set
            {
                if (value == _barrier) return;
                _barrier = value;
                OnPropertyChanged();
            }
        }

        public bool Train
        {
            get { return _train; }
            set
            {
                if (value == _train) return;
                _train = value;
                OnPropertyChanged();
            }
        }

        public ImageSource CurrentFrame
        {
            get { return _currentFrame; }
            set
            {
                if (Equals(value, _currentFrame)) return;
                _currentFrame = value;
                OnPropertyChanged();
            }
        }

        public string TaggedImagesFile { get; set; } = "tagged_images.txt";

        public String ImageSetPath
        {
            get { return _imageSetPath; }
            set
            {
                if (value == _imageSetPath) return;
                _imageSetPath = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand NextImage { get; set; }

        public RelayCommand PrevImage { get; set; }
        public RelayCommand Event1 { get; set; }
        public RelayCommand Event2 { get; set; }
        public RelayCommand Event3 { get; set; }
        public RelayCommand Event4 { get; set; }
        public RelayCommand Event5 { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void ClearData()
        {
            OnTrack = imageTags[currentFrameIndex].OnTrack;
            Entering = imageTags[currentFrameIndex].Entering;
            Leaving = imageTags[currentFrameIndex].Leaving;
            Barrier = imageTags[currentFrameIndex].Barrier;
            Train = imageTags[currentFrameIndex].Train;
        }

        private void SaveData()
        {
            imageTags[currentFrameIndex].OnTrack = OnTrack;
            imageTags[currentFrameIndex].Entering = Entering;
            imageTags[currentFrameIndex].Leaving = Leaving;
            imageTags[currentFrameIndex].Barrier = Barrier;
            imageTags[currentFrameIndex].Train = Train;
        }

        private void LoadTags()
        {
            if (File.Exists(TaggedImagesFile))
            {
                var lines = File.ReadAllLines(TaggedImagesFile);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("event 1"))
                        imageTags[i].OnTrack = true;
                    if (lines[i].Contains(" event 2"))
                        imageTags[i].Entering = true;
                    if (lines[i].Contains(" event 3"))
                        imageTags[i].Leaving = true;
                    if (lines[i].Contains(" event 4"))
                        imageTags[i].Barrier = true;
                    if (lines[i].Contains(" event 5"))
                        imageTags[i].Train = true;
                }
            }
            else
            {
                var allImages = Directory.GetFiles(ImageSetPath, "*.png", SearchOption.AllDirectories);
                var imDictionary = (from im in allImages
                    select new {_Path = im, Name = Path.GetFileName(im)}).GroupBy(x => x.Name)
                    .ToDictionary(x => x.Key, y => y.Select(z => z._Path).ToList());
                for (var i = 0; i < imageList.Length; i++)
                {
                    var image = imDictionary[Path.GetFileName(imageList[i])];
                    if (image.Any(x => x.Contains("ontrack")))
                        imageTags[i].OnTrack = true;
                    if (image.Any(x => x.Contains("entering")))
                        imageTags[i].Entering = true;
                    if (image.Any(x => x.Contains("leaving")))
                        imageTags[i].Leaving = true;
                    if (image.Any(x => x.Contains("barrier")))
                        imageTags[i].Barrier = true;
                    if (image.Any(x => x.Contains("train")))
                        imageTags[i].Train = true;
                }
            }
        }

        private void InitializeMethods()
        {
            NextImage = new RelayCommand(obj =>
            {
                SaveData();

                if (currentFrameIndex < imageList.Length - 1)
                    currentFrameIndex++;
                else
                    MessageBox.Show("All images tagged");
                ClearData();
                LoadCurrentImage();
            });

            PrevImage = new RelayCommand(obj =>
            {
                SaveData();
                if (currentFrameIndex > 0)
                    currentFrameIndex--;
                ClearData();
                LoadCurrentImage();
            });

            Event1 = new RelayCommand(obj => OnTrack = !OnTrack);
            Event2 = new RelayCommand(obj => Entering = !Entering);
            Event3 = new RelayCommand(obj => Leaving = !Leaving);
            Event4 = new RelayCommand(obj => Barrier = !Barrier);
            Event5 = new RelayCommand(obj => Train = !Train);
        }

        private void LoadCurrentImage()
        {
            Title = "Image: " + (currentFrameIndex + 1) + "/" + imageList.Length + "   " +
                    imageList[currentFrameIndex].Remove(0, ImageSetPath.Length);
            if (imageList.Length > currentFrameIndex)
                CurrentFrame = new BitmapImage(new Uri(imageList[currentFrameIndex]));
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (imageTags == null || imageTags.Length == 0)
            {
                return;
            }
            var result = MessageBox.Show("Do you want to save tags?", "Save results?", MessageBoxButton.YesNoCancel);
            if (result == MessageBoxResult.Yes)
            {
                SaveDataToFile();
            }
            else if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }

        }

        private void SaveDataToFile()
        {
            SaveData();
            using (var writer = new StreamWriter(TaggedImagesFile))
            {
                for (var i = 0; i < imageList.Length; i++)
                {
                    var tag = "";
                    if (imageTags[i].OnTrack)
                        tag += " event 1";
                    if (imageTags[i].Entering)
                        tag += " event 2";
                    if (imageTags[i].Leaving)
                        tag += " event 3";
                    if (imageTags[i].Barrier)
                        tag += " event 4";
                    if (imageTags[i].Train)
                        tag += " event 5";
                    var line = Path.GetFileName(imageList[i]) + ":" + tag;
                    writer.WriteLine(line);
                }
            }
        }

        private void LoadButtonClick(object sender, RoutedEventArgs e)
        {
            LoadTags();
            ClearData();
        }

        private void SaveButtonClick(object sender, RoutedEventArgs e)
        {
            SaveDataToFile();
        }

        private struct Data
        {
            public bool OnTrack;
            public bool Entering;
            public bool Leaving;
            public bool Barrier;
            public bool Train;
        }

        private void ShowFolderDialog(object sender, MouseButtonEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result= dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                ImageSetPath = dialog.SelectedPath;
            }
        }

        private void LoadImageSetButtonClick(object sender, RoutedEventArgs e)
        {
            LoadImageSet();
            AppSettings.Default.ImageSetPath = ImageSetPath;
            AppSettings.Default.Save();
        }
    }
}