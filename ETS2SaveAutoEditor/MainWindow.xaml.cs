using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Text;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Linq;

namespace ETS2SaveAutoEditor {

    public enum Trucksim {
        ETS2, ATS
    }
    public struct ProfileSave {
        public string savename;
        public string directory;
        public long time;
        public string formattedtime;
        public string fullPath;
        public string content;

        public override string ToString() {
            return savename;
        }
        public string Load() {
            return File.ReadAllText(fullPath + @"\game.sii", Encoding.UTF8).Replace("\r", "");
        }
        public void Save(string newcontent) {
            content = newcontent;
            File.WriteAllText(fullPath + @"\game.sii", newcontent, Encoding.UTF8);
        }
    }
    public struct SaveEditTask {
        public string name;
        public string description;
        public Action run;
        public override string ToString() {
            return name;
        }
    }
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window {
        public static string Version = "1.11";
        public static byte[] StringToByteArray(string hex) {
            int NumberChars = hex.Length / 2;
            byte[] bytes = new byte[NumberChars];
            using (var sr = new StringReader(hex)) {
                for (int i = 0; i < NumberChars; i++)
                    bytes[i] =
                      Convert.ToByte(new string(new char[2] { (char)sr.Read(), (char)sr.Read() }), 16);
            }
            return bytes;
        }

        private static string GetUnescapedSaveName(string originalString) {
            originalString = originalString.Replace("@@noname_save_game@@", "Quick Save");
            if (originalString.Length == 0) {
                originalString = "[Autosave]";
            }
            var ml = Regex.Matches(originalString, @"(?<=[^\\]|^)\\");
            var hexString = "";
            for (int i = 0; i < originalString.Length; i++) {
                var found = false;
                var ch = originalString[i];
                for (int j = 0; j < ml.Count; j++) {
                    if (i == ml[j].Index) {
                        found = true;
                        ++i; // skip backslash
                        hexString += originalString[++i];
                        hexString += originalString[++i];
                    }
                }
                if (!found) {
                    byte[] stringBytes = Encoding.UTF8.GetBytes(ch + "");
                    StringBuilder sbBytes = new StringBuilder(stringBytes.Length * 2);
                    foreach (byte b in stringBytes) {
                        sbBytes.AppendFormat("{0:X2}", b);
                    }
                    hexString += sbBytes.ToString();
                }
            }
            byte[] dBytes = StringToByteArray(hexString);
            return Encoding.UTF8.GetString(dBytes);
        }

        private SaveeditTasks tasks;

        private void LoadTasks() {
            tasks = new SaveeditTasks();
            TaskList.Items.Clear();

            var addAction = new Action<SaveEditTask>((t) => {
                TaskList.Items.Add(t);
            });
            addAction(tasks.MoneySet());
            addAction(tasks.ExpSet());
            addAction(tasks.UnlockScreens());
            addAction(tasks.TruckEngineSet());
            addAction(tasks.Refuel());
            addAction(tasks.FixEverything());
            addAction(tasks.ShareLocation());
            addAction(tasks.InjectLocation());
            addAction(tasks.StealCompanyTrailer());
            addAction(tasks.ChangeCargoMass());

            tasks.StateChanged += (object sender, string data) => {
                AppStatus.Items[0] = data;
            };
        }

        public static DateTime FuckUnixTime(long unixtime) {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddMilliseconds(unixtime).ToLocalTime();
            return dtDateTime;
        }

        private readonly Dictionary<string, string> pNameAndPaths = new Dictionary<string, string>();
        private string currentGamePath = "";
        private Trucksim currentGame = Trucksim.ETS2;

        public MainWindow() {
            if (!File.Exists("SII_Decrypt.exe")) {
                InstallSII("Cannot locate SII_Decrypt. Would you like to install it?");
            } else {
                byte[] currentSII = File.ReadAllBytes("SII_Decrypt.exe");
                byte[] resourceSII = Properties.Resources.SII_Decrypt;
                if (!currentSII.SequenceEqual(resourceSII)) {
                    InstallSII("Outdated SII_Decrypt. Would you like to update it?");
                }
            }

            void InstallSII(string message) {
                var res = MessageBox.Show(message, "Installing requirements", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.Yes) {
                    File.WriteAllBytes("SII_Decrypt.exe", Properties.Resources.SII_Decrypt);
                    MessageBox.Show("Installed SII_Decrypt!");
                } else {
                    Application.Current.Shutdown(0);
                }
            }

            InitializeComponent();
            Title += " " + Version + " EN";
            {
                var ms = new MemoryStream();
                Properties.Resources.Icon.Save(ms, ImageFormat.Png);
                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = ms;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                Icon = img;
            }

            // This code will load ETS2 saves by default
            var gameThatShouldBeAvailable = GetNextAvailableGame(Trucksim.ATS);
            if (!IsGameAvailable(gameThatShouldBeAvailable)) {
                MessageBox.Show("Could not locate the supported saves.", "Error", MessageBoxButton.OK);
                Application.Current.Shutdown(0);
                return;
            }
            LoadGame(gameThatShouldBeAvailable, false);
            ProfileChanged(false); // No need to call GameChanged because we want it visible
        }

        private Trucksim GetNextAvailableGame(Trucksim currentGame) {
            // Get the values of the Trucksim enum
            Trucksim[] games = (Trucksim[])Enum.GetValues(typeof(Trucksim));

            // Find the index of the current game
            int currentIndex = Array.IndexOf(games, currentGame);

            // Loop through the games starting from the next index
            for (int i = currentIndex + 1; i < games.Length; i++) {
                // Check if the game is available
                if (IsGameAvailable(games[i])) {
                    return games[i];
                }
            }

            // If no available game was found, loop from the beginning
            for (int i = 0; i < currentIndex; i++) {
                // Check if the game is available
                if (IsGameAvailable(games[i])) {
                    return games[i];
                }
            }

            // If no available game was found, return the current game
            return currentGame;
        }

        private bool IsGameAvailable(Trucksim game) {
            var path = GetGamePath(game);
            return Directory.Exists(path);
        }

        private string GetGamePath(Trucksim game) {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            switch (game) {
                case Trucksim.ETS2:
                    path = Path.Combine(path, "Euro Truck Simulator 2");
                    break;
                case Trucksim.ATS:
                    path = Path.Combine(path, "American Truck Simulator");
                    break;
                default:
                    throw new ArgumentException("Invalid game specified.");
            }

            return path + @"\profiles";
        }

        private void LoadGame(Trucksim game, bool animate) {
            var path = GetGamePath(game);
            currentGamePath = path;

            var nextGame = GetNextAvailableGame(game);
            GameSwitchBtn.Content = game.ToString();
            if (nextGame == game) {
                GameSwitchBtn.IsEnabled = false;
            }

            currentGame = game;

            LoadTasks();
            LoadProfiles(animate);
        }

        private void LoadProfiles(bool animate) {
            var onEnd = new Action(() => {
                pNameAndPaths.Clear();
                ProfileList.SelectedIndex = -1;
                ProfileList.Items.Clear();
                var dinfo = new DirectoryInfo(currentGamePath);
                var dlist = dinfo.GetDirectories();
                var pattern = @"^([ABCDEF\d]{2,2})+$";
                foreach (var delem in dlist) {
                    var ename = delem.Name.ToUpper();
                    if (!Regex.IsMatch(ename, pattern)) continue;
                    byte[] dBytes = StringToByteArray(ename);
                    string utf8result = Encoding.UTF8.GetString(dBytes);
                    ProfileList.Items.Add(utf8result);
                    pNameAndPaths.Add(utf8result, ename);
                }

                ShowProfiles(animate);
            });

            new Thread(() => {
                if (animate) // Prevent animation duplication
                    Thread.Sleep(250);
                Dispatcher.Invoke(onEnd);
            }).Start();
        }

        private void RefreshProfilesButtonPressed(object sender, RoutedEventArgs e) {
            GameChanged(true);
            LoadProfiles(true);
        }

        private void LoadSaveFile(string path) {
            AppStatus.Items[0] = "Decrypting the save...";

            var onEnd = new Action<string>((string str) => {
                var save = (ProfileSave)SaveList.SelectedItem;
                save.content = str;
                tasks.setSaveFile(save);

                AppStatus.Items[0] = "Finished";
                ShowTasks(true);
                EnableAll();
            });

            new Thread(() => {
                var gameSiiPath = path + @"\game.sii";

                var psi = new ProcessStartInfo("SII_Decrypt.exe") {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = "\"" + gameSiiPath + "\""
                };
                var proc = new Process {
                    StartInfo = psi
                };
                proc.Start();
                proc.WaitForExit();

                var saveFile = File.ReadAllText(gameSiiPath, Encoding.UTF8).Replace("\r", "");

                if (!saveFile.StartsWith("SiiNunit")) {
                    MessageBox.Show("The savegame is corrupted.", "Load failure");
                    Dispatcher.Invoke(onEnd);
                    return;
                }

                Dispatcher.Invoke(onEnd, saveFile);
            }).Start();
        }

        private void LoadSaves(string path) {
            AppStatus.Items[0] = "Loading saves...";
            new Thread(() => {
                Dispatcher.Invoke(() => {
                    SaveList.Items.Clear();
                });
                Thread.Sleep(250);
                foreach (var save in Directory.GetDirectories(path)) {
                    var fpath = save + @"\info.sii";
                    Console.WriteLine(fpath);
                    if (File.ReadLines(fpath).First().StartsWith("SiiNunit")) {
                        Console.WriteLine("Skipping decryption");
                    } else {
                        Console.WriteLine("Decrypting");
                        if (!File.Exists(fpath)) continue; // Not a save file.
                        var psi = new ProcessStartInfo("SII_Decrypt.exe") {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            Arguments = "\"" + fpath + "\""
                        };
                        var proc = new Process {
                            StartInfo = psi
                        };
                        proc.Start();
                        proc.WaitForExit();
                    }
                    var content = File.ReadAllText(fpath);
                    var directoryName = new DirectoryInfo(save).Name;

                    if (content.StartsWith("ScsC")) // decrypt fail
                    {
                        MessageBox.Show("Could not decrypt a savegame. Removing from list.\n" + new DirectoryInfo(save).Name, "Decrypt failure");
                        continue;
                    }
                    if (!content.StartsWith("SiiNunit")) // corrupted file
                    {
                        continue;
                    }

                    string namePattern = "name: (.*)";
                    string fileTimePattern = @"file_time: \b(\d+)\b";

                    if (!Regex.IsMatch(content, namePattern)) continue;
                    var nameResult = "N/A";
                    {
                        var result = Regex.Match(content, namePattern);
                        nameResult = result.Groups[1].Value.Trim();
                        if (nameResult.StartsWith("\"") && nameResult.EndsWith("\"")) {
                            nameResult = nameResult.Substring(1, nameResult.Length - 2);
                        }
                        nameResult = GetUnescapedSaveName(nameResult);
                    }

                    if (!Regex.IsMatch(content, fileTimePattern)) continue;
                    long dateResult = 0;
                    string dateFormatResult = "N/A";
                    {
                        var result = Regex.Match(content, fileTimePattern);
                        long resultLong = long.Parse(result.Groups[1].Value + "000");
                        dateResult = resultLong;
                        var dateTime = FuckUnixTime(dateResult);
                        dateFormatResult = dateTime.ToString("yyyy-MM-ddTHH\\:mm\\:ss");
                    }

                    Dispatcher.Invoke(() => {
                        ProfileSave psave = new ProfileSave {
                            savename = nameResult,
                            directory = directoryName,
                            time = dateResult,
                            formattedtime = dateFormatResult,
                            fullPath = save
                        };

                        SaveList.Items.Add(psave);
                    });
                }
                Dispatcher.Invoke(() => {
                    var l = new List<ProfileSave>();
                    foreach (var item in SaveList.Items) {
                        l.Add((ProfileSave)item);
                    }
                    SaveList.Items.Clear();
                    l.Sort(new Comparison<ProfileSave>((ProfileSave a, ProfileSave b) => {
                        if (a.time > b.time) return -1;
                        if (a.time < b.time) return 1;
                        return 0;
                    }));
                    foreach (var item in l) {
                        SaveList.Items.Add(item);
                    }
                    ShowSavegames(true);
                    AppStatus.Items[0] = "Finished";
                    EnableAll();
                });
            }).Start();
        }

        private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count == 0) return;

            DisableAll();
            ProfileChanged(true);
            var newItem = e.AddedItems[0].ToString();
            if (pNameAndPaths.ContainsKey(newItem)) {
                if (Directory.Exists(currentGamePath + @"\" + pNameAndPaths[newItem])) {
                    if (pNameAndPaths.ContainsKey(ProfileList.SelectedItem.ToString())) {
                        LoadSaves(currentGamePath + @"\" + pNameAndPaths[newItem] + @"\save");
                    }
                    return;
                }
            }
        }

        private void RefreshSavegamesButtonPressed(object sender, RoutedEventArgs e) {
            ProfileChanged(true);
            var newItem = ProfileList.SelectedItem.ToString();
            if (pNameAndPaths.ContainsKey(newItem)) {
                if (Directory.Exists(currentGamePath + @"\" + pNameAndPaths[newItem])) {
                    if (pNameAndPaths.ContainsKey(ProfileList.SelectedItem.ToString())) {
                        LoadSaves(currentGamePath + @"\" + pNameAndPaths[newItem] + @"\save");
                    }
                    return;
                }
            }
        }

        private void GameSwitchButtonPressed(object sender, RoutedEventArgs e) {
            LoadGame(GetNextAvailableGame(currentGame), true);
            GameChanged(true);
        }

        private void SaveList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var psave = SaveList.SelectedItem;
            if (psave is ProfileSave ps) {
                SaveInfo_Name.Content = "Name: " + ps.savename;
                SaveInfo_Dir.Content = "Dir: " + ps.directory;
                SaveInfo_Date.Content = "Date: " + ps.formattedtime;

                if (SaveInfo.Visibility != Visibility.Visible) {
                    var anim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.2))) {
                        DecelerationRatio = 1
                    };
                    SaveInfo.BeginAnimation(StackPanel.OpacityProperty, anim);

                    var anim0 = new DoubleAnimation(1, 0.8, new Duration(TimeSpan.FromSeconds(0.1))) {
                        DecelerationRatio = 1,
                        AutoReverse = true
                    };
                    SaveList.BeginAnimation(ListBox.OpacityProperty, anim0);
                }

                SaveInfo.Visibility = Visibility.Visible;
                if (TaskListPanel.Visibility == Visibility.Visible) {
                    SavegameChanged(true);
                }
            }
        }

        private void TaskList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (TaskList.SelectedItem == null) return;
            ShowTaskStart(true);
            if (TaskList.SelectedItem is SaveEditTask task) {
                var displayAnim = new Action(() => {
                    TaskDescription.Text = task.description;

                    var anim0 = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.15))) {
                        DecelerationRatio = 1
                    };
                    TaskDescription.BeginAnimation(TextBlock.OpacityProperty, anim0);
                });
                var anim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.15))) {
                    AccelerationRatio = 0.5,
                    DecelerationRatio = 0.5
                };
                anim.Completed += (object s, EventArgs a) => displayAnim();
                if (TaskDescription.Text == "") {
                    displayAnim();
                } else {
                    TaskDescription.BeginAnimation(TextBlock.OpacityProperty, anim);
                }
            } else {
                MessageBox.Show("Invalid task selected!", "Error");
            }
        }

        private void LoadSaveFileButton_Click(object sender, RoutedEventArgs e) {
            SavegameChanged(true);
            DisableAll();
            var ps = (ProfileSave)SaveList.SelectedItem;
            LoadSaveFile(currentGamePath + @"\" + pNameAndPaths[ProfileList.SelectedItem.ToString()] + @"\save" + "\\" + ps.directory);
        }

        private void OpenFolder_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            var ps = (ProfileSave)SaveList.SelectedItem;
            Process.Start("explorer.exe", currentGamePath + @"\" + pNameAndPaths[ProfileList.SelectedItem.ToString()] + @"\save" + "\\" + ps.directory);
        }

        private void StartTaskButton_Click(object sender, RoutedEventArgs e) {
            AppStatus.Items[0] = "Executing the task...";
            ((SaveEditTask)TaskList.SelectedItem).run();
            AppStatus.Items[0] = "Finished";
        }

        private void GameChanged(bool animate) {
            if (animate) {
                ProfileList.BeginAnimation(OpacityProperty, null);
                var anim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.1))) {
                    AccelerationRatio = 1
                };
                anim.Completed += (object sender, EventArgs e) => {
                    ProfileList.Visibility = Visibility.Hidden;
                };
                ProfileList.BeginAnimation(OpacityProperty, anim);
            } else {
                ProfileList.Visibility = Visibility.Hidden;
            }
            ProfileList.SelectedIndex = -1;
            ProfileChanged(animate);
        }

        private void ProfileChanged(bool animate) {
            if (animate) {
                SaveListPanel.BeginAnimation(OpacityProperty, null);
                var anim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.1))) {
                    AccelerationRatio = 1
                };
                anim.Completed += (object sender, EventArgs e) => {
                    SaveListPanel.Visibility = Visibility.Hidden;
                    SaveInfo.Visibility = Visibility.Collapsed;
                };
                SaveListPanel.BeginAnimation(OpacityProperty, anim);
            } else {
                SaveListPanel.Visibility = Visibility.Hidden;
                SaveInfo.Visibility = Visibility.Collapsed;
            }
            SaveList.SelectedIndex = -1;
            SavegameChanged(animate);
        }

        private void SavegameChanged(bool animate) {
            if (animate && TaskListPanel.Visibility == Visibility.Visible) {
                TaskListPanel.BeginAnimation(DockPanel.OpacityProperty, null);
                var anim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.1))) {
                    AccelerationRatio = 1
                };
                anim.Completed += (object sender, EventArgs e) => {
                    TaskListPanel.Visibility = Visibility.Hidden;
                    StartTaskButton.Visibility = Visibility.Hidden;
                };
                TaskListPanel.BeginAnimation(DockPanel.OpacityProperty, anim);
            } else {
                TaskListPanel.Visibility = Visibility.Hidden;
                StartTaskButton.Visibility = Visibility.Hidden;
            }
            TaskList.SelectedIndex = -1;
            TaskList.SelectedItem = null;
            TaskDescription.Text = "";
        }

        private void ShowProfiles(bool animate) {
            ProfileList.Visibility = Visibility.Visible;
            if (animate) {
                ProfileList.BeginAnimation(OpacityProperty, null);
                var anim = new DoubleAnimation(0, 2, new Duration(TimeSpan.FromSeconds(0.1))) {
                    DecelerationRatio = 1
                };
                ProfileList.BeginAnimation(OpacityProperty, anim);

                var blur = new BlurEffect {
                    RenderingBias = RenderingBias.Quality,
                    Radius = 30
                };

                ProfileList.Effect = blur;

                var anim0 = new DoubleAnimation(10, 0, new Duration(TimeSpan.FromSeconds(0.5))) {
                    DecelerationRatio = 1
                };
                anim0.Completed += (object sender, EventArgs e) => {
                    ProfileList.Effect = null;
                };
                blur.BeginAnimation(BlurEffect.RadiusProperty, anim0);
            }
        }

        private void ShowSavegames(bool animate) {
            SaveListPanel.Visibility = Visibility.Visible;
            if (animate) {
                SaveListPanel.BeginAnimation(DockPanel.OpacityProperty, null);
                var anim = new DoubleAnimation(0, 2, new Duration(TimeSpan.FromSeconds(0.1))) {
                    DecelerationRatio = 1
                };
                SaveListPanel.BeginAnimation(DockPanel.OpacityProperty, anim);

                var blur = new BlurEffect {
                    RenderingBias = RenderingBias.Quality,
                    Radius = 30
                };

                SaveListPanel.Effect = blur;

                var anim0 = new DoubleAnimation(10, 0, new Duration(TimeSpan.FromSeconds(0.5))) {
                    DecelerationRatio = 1
                };
                anim0.Completed += (object sender, EventArgs e) => {
                    SaveListPanel.Effect = null;
                };
                blur.BeginAnimation(BlurEffect.RadiusProperty, anim0);
            }
        }

        private void ShowTasks(bool animate) {
            ShowSavegames(false);
            if (animate && TaskListPanel.Visibility == Visibility.Hidden) {
                TaskListPanel.BeginAnimation(DockPanel.OpacityProperty, null);
                var anim = new DoubleAnimation(0, 2, new Duration(TimeSpan.FromSeconds(0.1))) {
                    DecelerationRatio = 1
                };
                TaskListPanel.BeginAnimation(DockPanel.OpacityProperty, anim);

                var blur = new BlurEffect {
                    RenderingBias = RenderingBias.Quality,
                    Radius = 30
                };

                TaskListPanel.Effect = blur;

                var anim0 = new DoubleAnimation(10, 0, new Duration(TimeSpan.FromSeconds(0.5))) {
                    DecelerationRatio = 1
                };
                anim0.Completed += (object sender, EventArgs e) => {
                    SaveListPanel.Effect = null;
                };
                blur.BeginAnimation(BlurEffect.RadiusProperty, anim0);
            }
            TaskListPanel.Visibility = Visibility.Visible;
        }
        private void ShowTaskStart(bool animate) {
            ShowTasks(animate);
            if (animate && StartTaskButton.Visibility != Visibility.Visible) {
                StartTaskButton.BeginAnimation(Button.OpacityProperty, null);
                var anim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.1))) {
                    DecelerationRatio = 1
                };
                StartTaskButton.BeginAnimation(Button.OpacityProperty, anim);
            }
            StartTaskButton.Visibility = Visibility.Visible;
        }
        private void DisableAll() {
            ProfileList.IsEnabled = false;
            SaveList.IsEnabled = false;
            TaskList.IsEnabled = false;
            LoadSaveFileButton.IsEnabled = false;
        }
        private void EnableAll() {
            ProfileList.IsEnabled = true;
            SaveList.IsEnabled = true;
            TaskList.IsEnabled = true;
            LoadSaveFileButton.IsEnabled = true;
        }
    }
}