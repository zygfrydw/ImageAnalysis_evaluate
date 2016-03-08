using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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
                var referenceEvents = ExtractImagesAndEvents(File.ReadAllLines(ReferencePath))
                    .ToDictionary(x => x.Name, y => y.Events);

                if (!File.Exists(ResultsPath))
                {
                    MessageBox.Show("Result file does not exists");
                    return;
                }
                var userResultEvents = ExtractImagesAndEvents(File.ReadAllLines(ResultsPath));

                int correct = 0;
                int falsePositives = 0;
                int falseNegatives = 0;
                var userFiles = new HashSet<string>(userResultEvents.Select(x=>x.Name)); 
                int correctEvents = referenceEvents.Where(x=>userFiles.Contains(x.Key)).SelectMany(x => x.Value).Count();

                var allEvents = referenceEvents.SelectMany(x => x.Value).Distinct().OrderBy(x => x).ToArray();

                var truePositivesPerEv = allEvents.ToDictionary(x => x, _ => 0);
                var trueNegativesPerEv = allEvents.ToDictionary(x => x, _ => 0);
                var falsePositivesPerEv = allEvents.ToDictionary(x => x, _ => 0);
                var falseNegativesPerEv = allEvents.ToDictionary(x => x, _ => 0);

                foreach (var line in userResultEvents)
                {
                    bool mistake = false;
                    Error error = new Error() {Name = line.Name};
                    if (!referenceEvents.ContainsKey(line.Name))
                    {
                        MessageBox.Show("Missing image " + line.Name, "Missing");
                        continue;
                    }
                    var refLine = referenceEvents[line.Name];
                    foreach (var ev in line.Events)
                    {
                        if (refLine.Contains(ev))
                        {
                            truePositivesPerEv[ev]++;
                            correct++;
                        }
                        else
                        {
                            falsePositivesPerEv[ev]++;
                            falsePositives++;
                            mistake = true;
                            error.FalsePositive += " " + ToEventName(ev) + ",";
                        }
                    }
                    foreach (var ev in refLine)
                    {
                        if (!line.Events.Contains(ev))
                        {
                            falseNegativesPerEv[ev]++;
                            falseNegatives++;
                            mistake = true;
                            error.FalseNegative += " " + ToEventName(ev) + ",";
                        }
                    }
                    foreach (var ev in allEvents)
                    {
                        if (!line.Events.Contains(ev) && !refLine.Contains(ev))
                            trueNegativesPerEv[ev]++;
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
                    $"All correct events: {correctEvents}\n" +
                    $"Classified events: {allClassified}\n" +
                    $"Accuracy: {accuracy}\n" +
                    $"False positive: {falsePositives} (Classified but should not be)\n" +
                    $"False negative: {falseNegatives} (Not classified but should be)\n\n";

                foreach (var ev in allEvents)
                    Results +=
                        $"event {ev}:\n" +
                        $"True positives: {truePositivesPerEv[ev]}\n" +
                        $"False positives: {falsePositivesPerEv[ev]}\n" +
                        $"True negatives: {trueNegativesPerEv[ev]}\n" +
                        $"False negatives {falseNegativesPerEv[ev]}\n\n";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unexpected error");
            }
        }

        class ImageEvent
        {
            public string Name;
            public string[] Events;
        }

        private ImageEvent[] ExtractImagesAndEvents(string[] readAllLines)
        {
            var debug = readAllLines.Select(x =>
            {
                var match = Regex.Match(x, @"(.+):\s?(.*)");
                if (match.Success)
                {
                    var eventMatch = Regex.Match(match.Groups[2].Value, @"event\s*(\d+)");
                    List<string> events = new List<string>();
                    while (eventMatch.Success)
                    {
                        events.Add(eventMatch.Groups[1].Value);
                        eventMatch = eventMatch.NextMatch();
                    }

                    return new ImageEvent
                    {
                        Name = Path.GetFileName(match.Groups[1].Value.Trim()),
                        Events = events.ToArray()
                    };
                }
                else
                {
                    Console.WriteLine("Can't parse: " + x);
                    return null;
                }
            }).Where(x => x != null).ToArray();
            return debug;
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
