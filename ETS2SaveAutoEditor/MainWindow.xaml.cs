﻿using System;
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
using ASE.Utils;
using ASE.SII2Parser;
using System.Windows.Media;
using ASE.Properties;

namespace ASE {

    public enum Trucksim {
        ETS2, ATS
    }
    public class ProfileSave {
        public string savename = "";
        public string directory = "";
        public long time;
        public string formattedtime = "";
        public string fullPath = "";
        public byte[] content = [];

        public override string ToString() {
            return savename;
        }
        public byte[] Load() {
            return File.ReadAllBytes(fullPath + @"\game.sii");
        }
        public void Save(string newcontent) {
            content = Encoding.UTF8.GetBytes(newcontent);
            File.WriteAllBytes(fullPath + @"\game.sii", content);
        }

        public void Save(SII2 instance) {
            var w = GetWriter();
            instance.WriteTo(w);
            w.Close();
        }

        public void Save(Game2 instance) {
            Save(instance.Reader);
        }

        public StreamWriter GetWriter(string filePath = "game.sii") {
            return new StreamWriter(new BufferedStream(new FileStream(fullPath + $"\\{filePath}", FileMode.Create)), BetterThanStupidMS.UTF8);
        }
    }
    public struct SaveEditTask {
        public string name;
        public string description;
        public Action run;
        public override string ToString() => name;
    }
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window {
        public static readonly string Version = "1.34.2";

        private SaveeditTasks tasks = new();

        private void LoadTasks() {
            TaskList.Items.Clear();

            var addAction = new Action<SaveEditTask>((t) => {
                TaskList.Items.Add(t);
            });
            addAction(tasks.MoneySet());
            addAction(tasks.ExpSet());
            addAction(tasks.UnlockScreens());
            addAction(tasks.ExecuteVPS());
            addAction(tasks.TruckEngineSet());
            addAction(tasks.Refuel(false));
            addAction(tasks.Refuel(true));
            if (false && OperatingSystem.IsWindows())
                addAction(tasks.RefuelNow());
            addAction(tasks.FixEverything());
            addAction(tasks.ShareNavigation());
            addAction(tasks.ImportNavigation());
            addAction(tasks.SharePosition());
            addAction(tasks.ImportPosition());
            addAction(tasks.ReducePosition());
            addAction(tasks.DecodePosition());
            addAction(tasks.ConnectTrailerInstantly());
            addAction(tasks.TeleportToCargo());
            addAction(tasks.VehicleSharingTool(currentGame));
            addAction(tasks.SpecialCCTask(currentGame));
            addAction(tasks.CompanyVehicleStealTool());
            addAction(tasks.ChangeCargoMass());
        }

        public static DateTime UnixToDateTime(long unixtime) {
            DateTime dtDateTime = new(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddMilliseconds(unixtime).ToLocalTime();
            return dtDateTime;
        }

        private readonly Dictionary<string, string> pNameAndPaths = [];
        private string currentGamePath = "";
        private Trucksim currentGame = Trucksim.ETS2;

        public MainWindow() {

            InitializeComponent();
            Title += " " + Version;

            // This code will load ETS2 saves by default
            var gameThatShouldBeAvailable = GetNextAvailableGame(Trucksim.ATS);
            if (!IsGameAvailable(gameThatShouldBeAvailable)) {
                MessageBox.Show("Could not locate the supported saves.", "Error", MessageBoxButton.OK);
                Application.Current.Shutdown(0);
                return;
            }
            LoadGame(gameThatShouldBeAvailable, false);
            ProfileChanged(false); // No need to call GameChanged because we want it visible
                                   //QuaternionTester.TestQuaternion();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
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

            path = game switch {
                Trucksim.ETS2 => Path.Combine(path, "Euro Truck Simulator 2"),
                Trucksim.ATS => Path.Combine(path, "American Truck Simulator"),
                _ => throw new ArgumentException("Invalid game specified."),
            };
            return path + @"\profiles";
        }

        private void LoadGame(Trucksim game, bool animate) {
            var path = GetGamePath(game);
            currentGamePath = path;

            var nextGame = GetNextAvailableGame(game);
            GameSwitchButton.Content = game.ToString();
            if (nextGame == game) {
                GameSwitchButton.IsEnabled = false;
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
                    byte[] dBytes = HexEncoder.HexStringToByteArray(ename);
                    string utf8result = Encoding.UTF8.GetString(dBytes);
                    ProfileList.Items.Add(utf8result);
                    pNameAndPaths.Add(utf8result, ename);
                }

                ShowProfiles(animate);
            });

            new Thread(() => {
                if (animate) // Prevent animation duplication
                    Thread.Sleep(50);
                Dispatcher.Invoke(onEnd);
            }).Start();
        }

        private void RefreshProfilesButtonPressed(object sender, RoutedEventArgs e) {
            GameChanged(false);
            LoadProfiles(true);
        }

        private void LoadSaveFile(string path) {
            var save = (ProfileSave)SaveList.SelectedItem;
            var onEnd = new Action<byte[]?>((byte[]? data) => {
                if (data is null) {
                    EnableAll();
                    return;
                }
                save.content = data;

                ShowTasks(true);
                EnableAll();
            });

            new Thread(() => {
                var gameSiiPath = path + @"\game.sii";
                var saveData = File.ReadAllBytes(gameSiiPath);

                if (!SIIParser2.IsSupported(saveData)) {
                    Dispatcher.Invoke(() => {
                        MessageBox.Show("The savegame is corrupted.", "Load failure");
                        onEnd(null);
                    });
                    return;
                }

                Dispatcher.Invoke(onEnd, saveData);

                tasks.SetSaveFile(save);
            }).Start();
        }

        private void LoadSaves(string path) {
            new Thread(() => {
                Thread.Sleep(50);
                List<ProfileSave> saves = [];
                foreach (var save in Directory.GetDirectories(path)) {
                    var fpath = save + @"\info.sii";

                    // File does not exist
                    if (!File.Exists(fpath)) {
                        continue;
                    }

                    var content = File.ReadAllBytes(fpath);
                    var directoryName = new DirectoryInfo(save).Name;

                    if (!SIIParser2.IsSupported(content)) {
                        MessageBox.Show("Unsupported save. Removing from list.\n" + new DirectoryInfo(save).Name, "Unsupported save (1)");
                        continue;
                    }

                    SII2 infoSii;
                    try {
                        infoSii = SIIParser2.Parse(content);
                    } catch {
                        MessageBox.Show("Unsupported save. Removing from list.\n" + new DirectoryInfo(save).Name, "Unsupported save (2)");
                        continue;
                    }
                    Game2 info = new(infoSii);

                    Entity2? unit = info.EntityType("save_container");
                    if (unit is null) {
                        MessageBox.Show("Unsupported save. Removing from list.\n" + new DirectoryInfo(save).Name, "Unsupported save (3)");
                        continue;
                    }

                    var nameResult = "N/A";
                    {
                        nameResult = unit.GetValue("name");
                        if (nameResult.StartsWith('\"') && nameResult.EndsWith('\"')) {
                            nameResult = nameResult[1..^1];
                        }
                        nameResult = SCSSaveHexEncodingSupport.GetUnescapedSaveName(nameResult);
                    }

                    long dateResult = 0;
                    string dateFormatResult = "N/A";
                    {
                        long resultLong = long.Parse(unit.GetValue("file_time") + "000");
                        dateResult = resultLong;
                        var dateTime = UnixToDateTime(dateResult);
                        dateFormatResult = dateTime.ToString("yyyy-MM-ddTHH\\:mm\\:ss");
                    }

                    ProfileSave psave = new() {
                        savename = nameResult,
                        directory = directoryName,
                        time = dateResult,
                        formattedtime = dateFormatResult,
                        fullPath = save
                    };
                    saves.Add(psave);
                }
                saves.Sort(new Comparison<ProfileSave>((ProfileSave a, ProfileSave b) => {
                    if (a.time > b.time) return -1;
                    if (a.time < b.time) return 1;
                    return 0;
                }));

                Dispatcher.Invoke(() => {
                    SaveList.Items.Clear();
                    foreach (var item in saves) {
                        SaveList.Items.Add(item);
                    }
                    ShowSavegames(true);
                    EnableAll();
                });
            }).Start();
        }

        private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count == 0) return;

            DisableAll();
            ProfileChanged(false);
            var newItem = e.AddedItems[0]!.ToString()!;
            if (pNameAndPaths.ContainsKey(newItem)) {
                if (Directory.Exists(currentGamePath + @"\" + pNameAndPaths[newItem])) {
                    if (pNameAndPaths.ContainsKey(ProfileList.SelectedItem.ToString()!)) {
                        LoadSaves(currentGamePath + @"\" + pNameAndPaths[newItem] + @"\save");
                    }
                    return;
                }
            }
        }

        private void RefreshSavegamesButtonPressed(object sender, RoutedEventArgs e) {
            ProfileChanged(false);
            var newItem = ProfileList.SelectedItem.ToString()!;
            if (pNameAndPaths.ContainsKey(newItem)) {
                if (Directory.Exists(currentGamePath + @"\" + pNameAndPaths[newItem])) {
                    if (pNameAndPaths.ContainsKey(ProfileList.SelectedItem.ToString()!)) {
                        LoadSaves(currentGamePath + @"\" + pNameAndPaths[newItem] + @"\save");
                    }
                    return;
                }
            }
        }

        private void GameSwitchButtonPressed(object sender, RoutedEventArgs e) {
            LoadGame(GetNextAvailableGame(currentGame), true);
            GameChanged(false);
        }

        private void SaveList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var psave = SaveList.SelectedItem;
            if (psave is ProfileSave ps) {
                SaveInfo_Name.Content = Texts.MainWindow_Label_SaveInfoName + ": " + ps.savename;
                SaveInfo_Dir.Content = Texts.MainWindow_Label_SaveInfoDir + ": " + ps.directory;
                SaveInfo_Date.Content = Texts.MainWindow_Label_SaveInfoDate + ": " + ps.formattedtime;

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
                anim.Completed += (object? s, EventArgs a) => displayAnim();
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
            LoadSaveFile(currentGamePath + @"\" + pNameAndPaths[ProfileList.SelectedItem.ToString()!] + @"\save" + "\\" + ps.directory);
        }

        private void OpenFolder_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            var ps = (ProfileSave)SaveList.SelectedItem;
            Process.Start("explorer.exe", currentGamePath + @"\" + pNameAndPaths[ProfileList.SelectedItem.ToString()!] + @"\save" + "\\" + ps.directory);
        }

        private void StartTaskButton_Click(object sender, RoutedEventArgs e) {
            ((SaveEditTask)TaskList.SelectedItem).run();
            var ps = (ProfileSave)SaveList.SelectedItem;
            LoadSaveFile(currentGamePath + @"\" + pNameAndPaths[ProfileList.SelectedItem.ToString()!] + @"\save" + "\\" + ps.directory);
        }

        private void GameChanged(bool animate) {
            if (animate) {
                ProfileList.BeginAnimation(OpacityProperty, null);
                var anim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.1))) {
                    AccelerationRatio = 1
                };
                anim.Completed += (object? sender, EventArgs e) => {
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
                anim.Completed += (object? sender, EventArgs e) => {
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
                TaskListPanel.BeginAnimation(OpacityProperty, null);
                var anim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.1))) {
                    AccelerationRatio = 1
                };
                anim.Completed += (object? sender, EventArgs e) => {
                    TaskListPanel.Visibility = Visibility.Hidden;
                    ExecuteButton.Visibility = Visibility.Hidden;
                };
                TaskListPanel.BeginAnimation(DockPanel.OpacityProperty, anim);
            } else {
                TaskListPanel.Visibility = Visibility.Hidden;
                ExecuteButton.Visibility = Visibility.Hidden;
            }
            TaskList.SelectedIndex = -1;
            TaskList.SelectedItem = null;
            TaskDescription.Text = "";
        }

        private void ShowProfiles(bool animate) {
            ProfileList.Visibility = Visibility.Visible;
            if (animate) {
                ProfileList.BeginAnimation(OpacityProperty, null);
                var anim = new DoubleAnimation(0, 2, new Duration(TimeSpan.FromSeconds(0.25))) {
                    DecelerationRatio = 1
                };
                ProfileList.BeginAnimation(OpacityProperty, anim);
            }
        }

        private void ShowSavegames(bool animate) {
            SaveListPanel.Visibility = Visibility.Visible;
            if (animate) {
                SaveListPanel.BeginAnimation(OpacityProperty, null);
                var anim = new DoubleAnimation(0, 2, new Duration(TimeSpan.FromSeconds(0.25))) {
                    DecelerationRatio = 1
                };
                SaveListPanel.BeginAnimation(OpacityProperty, anim);
            }
        }

        private void ShowTasks(bool animate) {
            ShowSavegames(false);
            if (animate && TaskListPanel.Visibility == Visibility.Hidden) {
                TaskListPanel.BeginAnimation(DockPanel.OpacityProperty, null);
                var anim = new DoubleAnimation(0, 2, new Duration(TimeSpan.FromSeconds(0.25))) {
                    DecelerationRatio = 1
                };
                TaskListPanel.BeginAnimation(DockPanel.OpacityProperty, anim);
            }
            TaskListPanel.Visibility = Visibility.Visible;
        }
        private void ShowTaskStart(bool animate) {
            ShowTasks(animate);
            if (animate && ExecuteButton.Visibility != Visibility.Visible) {
                ExecuteButton.BeginAnimation(Button.OpacityProperty, null);
                var anim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.1))) {
                    DecelerationRatio = 1
                };
                ExecuteButton.BeginAnimation(Button.OpacityProperty, anim);
            }
            ExecuteButton.Visibility = Visibility.Visible;
        }
        private void DisableAll() {
            ProfileList.IsEnabled = false;
            SaveList.IsEnabled = false;
            TaskList.IsEnabled = false;
            LoadSaveButton.IsEnabled = false;
        }
        private void EnableAll() {
            ProfileList.IsEnabled = true;
            SaveList.IsEnabled = true;
            TaskList.IsEnabled = true;
            LoadSaveButton.IsEnabled = true;
        }

        private void CreditOpen_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            var w = new CreditWindow {
                Owner = this
            };
            w.ShowDialog();
        }

        // Unhandled exception handling

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            HandleException(e.ExceptionObject as Exception);
        }

        private void HandleException(Exception? ex) {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string logFileName = $"Details.{timestamp}.txt";

            StringBuilder details = new();
            details.AppendLine("Unhandled Exception Occurred");
            details.AppendLine($"Timestamp: {DateTime.Now.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC ({DateTime.Now})");

            if (ex is null) {
                details.AppendLine("No exception object provided");
            } else {
                // Print exception details

                details.AppendLine($"Message: {ex.Message}");
                details.AppendLine($"Source: {ex.Source}");
            }

            void recurseException(Exception ex) {
                details.AppendLine($"{ex.GetType().FullName} : {ex.Message} <- {ex.Source}");
                details.AppendLine(ex.StackTrace + "\n");

                if (ex is AggregateException aex) {
                    foreach (var iex in aex.InnerExceptions) {
                        recurseException(iex);
                    }
                }

                if (ex.InnerException is not null) {
                    recurseException(ex.InnerException);
                }
            }
            if (ex is not null)
                recurseException(ex);

            try {
                // Write to log file
                File.WriteAllText(logFileName, details.ToString());
            } catch (Exception fileException) {
                MessageBox.Show($"Failed to write log file: {fileException.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Show error in message box
            MessageBox.Show($"An unexpected error occurred. Details have been written to {logFileName}.\n\nError: {ex?.Message}",
                "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}