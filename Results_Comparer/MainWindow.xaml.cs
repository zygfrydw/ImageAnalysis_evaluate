using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Results_Comparer.Annotations;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = System.IO.Path;

namespace Results_Comparer
{
    public class Error
    {
        public string Name { get; set; }
        public ImageSource ImageSource { get; set; }
        public string FalseNegative { get; set; }
        public string FalsePositive { get; set; }
        public string CorrectTag { get; set; }
        public string UserTag { get; set; }
        public string Text { get; set; }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _referencePath = "tagged_images.txt";
        private string _resultsPath = "events.txt";
        private string _results;

        public string ReferencePath
        {
            get { return _referencePath; }
            set
            {
                if (value == _referencePath) return;
                _referencePath = value;
                OnPropertyChanged();
            }
        } 

        public string ResultsPath
        {
            get { return _resultsPath; }
            set
            {
                if (value == _resultsPath) return;
                _resultsPath = value;
                OnPropertyChanged();
            }
        }

        public string Results
        {
            get { return _results; }
            set
            {
                if (value == _results) return;
                _results = value;
                OnPropertyChanged();
            }
        }

        private bool imagesPathModified = true;
        public string ImageSetPath
        {
            get { return _imageSetPath; }
            set
            {
                if (value == _imageSetPath) return;
                imagesPathModified = true;
                _imageSetPath = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Error> Errors { get; set; } = new ObservableCollection<Error>();
        public Dictionary<string, string> ImagesDictionary;
        private string _imageSetPath;

        public MainWindow()
        {
            InitializeComponent();
            ImageSetPath = AppSettings.Default.ImageSetPath;
            ReferencePath = AppSettings.Default.ReferenceFile;
            ResultsPath = AppSettings.Default.ResultEventsFile;
        }

        private void LoadImages()
        {

            try
            {
                var strings = Directory.GetFiles(ImageSetPath, "*.png", SearchOption.AllDirectories);
                ImagesDictionary = (from im in strings
                    select new {_Path = im, Name = Path.GetFileNameWithoutExtension(im)}).GroupBy(x => x.Name)
                    .Select(x => x.First()._Path).ToDictionary(Path.GetFileName);
                imagesPathModified = false;
            }
            catch
            {
                MessageBox.Show("Error while loading reference images. Please specify path to reference image set.",
                    "Error while loading images");
                ImagesDictionary = new Dictionary<string, string>();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettings();
                if (imagesPathModified)
                    LoadImages();

                Errors.Clear();
                if (!File.Exists(ReferencePath))
                {
                    MessageBox.Show("Reference file does not exists");
                    return;
                }
                var refLines = File.ReadAllLines(ReferencePath).Select(x =>
                {
                    var tmp = x.Split(':');
                    return new {Name = tmp[0], Events = tmp[1].Trim()};
                })
                    .ToDictionary(x => x.Name,
                        y =>
                            y.Events.Replace("event", "")
                                .Trim()
                                .Replace("  ", " ")
                                .Split(' ')
                                .Where(x => !string.IsNullOrEmpty(x))
                                .ToArray());
                if (!File.Exists(ResultsPath))
                {
                    MessageBox.Show("Result file does not exists");
                    return;
                }
                var testLines = File.ReadAllLines(ResultsPath).Select(x =>
                {
                    var tmp = x.Split(':');
                    return
                        new
                        {
                            Name = Path.GetFileName(tmp[0]),
                            Events =
                                tmp[1].Trim()
                                    .Replace("event", "")
                                    .Trim()
                                    .Replace("  ", " ")
                                    .Split(' ')
                                    .Where(y => !string.IsNullOrEmpty(y))
                                    .ToArray()
                        };
                }).ToList();
                int correctEvents = refLines.SelectMany(x => x.Value).Count();
                int correct = 0;
                int falsePositives = 0;
                int falseNegatives = 0;
                foreach (var line in testLines)
                {
                    bool mistake = false;
                    Error error = new Error() {Name = line.Name};
                    var refLine = refLines[line.Name];
                    foreach (var ev in line.Events)
                    {
                        if (refLine.Contains(ev))
                        {
                            correct++;
                        }
                        else
                        {
                            falsePositives++;
                            mistake = true;
                            error.FalsePositive += " " + ToEventName(ev) + ",";
                        }
                    }
                    foreach (var ev in refLine)
                    {
                        if (!line.Events.Contains(ev))
                        {
                            falseNegatives++;
                            mistake = true;
                            error.FalseNegative += " " + ToEventName(ev) + ",";
                        }
                    }

                    if (mistake)
                    {
                        error.CorrectTag =
                            refLine.Select(ToEventName)
                                .Aggregate("", (sum, eve) => sum + " " + eve + ",")?
                                .Trim(',', ' ');
                        error.UserTag =
                            line.Events.Select(ToEventName)
                                .Aggregate("", (sum, eve) => sum + " " + eve + ",")?
                                .Trim(',', ' ');
                        error.FalsePositive = error.FalsePositive?.Trim(',', ' ');
                        error.FalseNegative = error.FalseNegative?.Trim(',', ' ');
                        error.ImageSource = ImagesDictionary.ContainsKey(error.Name)
                            ? new BitmapImage(new Uri(ImagesDictionary[error.Name]))
                            : ((Image) Resources["NoImageSource"]).Source;
                        error.Text = "Missing: " + error.FalseNegative + "Over classified: " + error.FalsePositive;
                        Errors.Add(error);
                    }
                }

                var allClassified = correctEvents + falsePositives;
                double accuracy = correct/(double) allClassified;
                Results =
                    $"All correct events: {correctEvents}\nClassified events: {allClassified}\nAccuracy: {accuracy}\nFalse positive: {falsePositives} (Classified but should not be)\nFalse negative: {falseNegatives} (Not classified but should be)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unexpected error");
            }
        }

        private void SaveSettings()
        {
            AppSettings.Default.ReferenceFile = ReferencePath;
            AppSettings.Default.ImageSetPath = ImageSetPath;
            AppSettings.Default.ResultEventsFile = ResultsPath;
            AppSettings.Default.Save();
        }

        string ToEventName(string name)
        {
            switch (name)
            {
                case "1":
                    return "OnTrack";
                case "2":
                    return "Entering";
                case "3":
                    return "Leaving";
                case "4":
                    return "Barrier";
                case "5":
                    return "Train";
                default:
                    return name;
            }
        }

        private void ReferencePath_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(ReferencePath));
            dialog.FileName = ReferencePath;
            var results = dialog.ShowDialog();
            if (results == true)
            {
                ReferencePath = GetPathRelativeToCurrentWorkingDirectory(dialog.FileName);
            }
        }

        private string GetPathRelativeToCurrentWorkingDirectory(string name)
        {
            return GetRelativePath(name,ApplicationDirectory);
        }

        private static string ApplicationDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        private void EventPath_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(ResultsPath)); ;
            dialog.FileName = ResultsPath;
            var results = dialog.ShowDialog();
            if (results == true)
            {
                ResultsPath = GetPathRelativeToCurrentWorkingDirectory(dialog.FileName);
            }
        }

        private void ImageSet_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.SelectedPath = ImageSetPath;
            var results = dialog.ShowDialog();
            if (results == System.Windows.Forms.DialogResult.OK)
            {
                ImageSetPath = dialog.SelectedPath;
            }
        }
    }
}
