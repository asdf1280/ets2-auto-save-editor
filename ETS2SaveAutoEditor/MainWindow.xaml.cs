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

namespace ETS2SaveAutoEditor
{
    public struct ProfileSave
    {
        public string savename;
        public string directory;
        public long time;
        public string formattedtime;
        public string fullPath;
        public string content;

        public override string ToString()
        {
            return savename;
        }
        public string Load()
        {
            return File.ReadAllText(fullPath + @"\game.sii", Encoding.UTF8);
        }
        public void Save(string newcontent)
        {
            content = newcontent;
            File.WriteAllText(fullPath + @"\game.sii", newcontent, Encoding.UTF8);
        }
    }
    public struct SaveEditTask
    {
        public string name;
        public string description;
        public Action run;
        public override string ToString()
        {
            return name;
        }
    }
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string Version = "1.03 Alpha";
        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length / 2;
            byte[] bytes = new byte[NumberChars];
            using (var sr = new StringReader(hex))
            {
                for (int i = 0; i < NumberChars; i++)
                    bytes[i] =
                      Convert.ToByte(new string(new char[2] { (char)sr.Read(), (char)sr.Read() }), 16);
            }
            return bytes;
        }

        private static string GetUnescapedSaveName(string originalString)
        {
            originalString = originalString.Replace("@@noname_save_game@@", "빠른 저장");
            if (originalString.Length == 0)
            {
                originalString = "[자동저장]";
            }
            var ml = Regex.Matches(originalString, "(?<=[^\\\\]|^)\\\\");
            var hexString = "";
            for (int i = 0; i < originalString.Length; i++)
            {
                var found = false;
                var ch = originalString[i];
                for (int j = 0; j < ml.Count; j++)
                {
                    if (i == ml[j].Index)
                    {
                        found = true;
                        ++i; // skip backslash
                        hexString += originalString[++i];
                        hexString += originalString[++i];
                    }
                }
                if (!found)
                {
                    Byte[] stringBytes = Encoding.UTF8.GetBytes(ch + "");
                    StringBuilder sbBytes = new StringBuilder(stringBytes.Length * 2);
                    foreach (byte b in stringBytes)
                    {
                        sbBytes.AppendFormat("{0:X2}", b);
                    }
                    hexString += sbBytes.ToString();
                }
            }
            Console.WriteLine(hexString);
            byte[] dBytes = StringToByteArray(hexString);
            return System.Text.Encoding.UTF8.GetString(dBytes);
        }

        private SaveeditTasks tasks;

        private void LoadTasks()
        {
            tasks = new SaveeditTasks();
            var addAction = new Action<SaveEditTask>((t) =>
            {
                TaskList.Items.Add(t);
            });
            addAction(tasks.OwnTest());
            addAction(tasks.MoneySet());
            addAction(tasks.ExpSet());
            addAction(tasks.UnlockScreens());
            addAction(tasks.TruckEngineSet());
            addAction(tasks.TruckSoundSet());
        }

        public static DateTime FuckUnixTime(long unixtime)
        {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddMilliseconds(unixtime).ToLocalTime();
            return dtDateTime;
        }

        private readonly Dictionary<string, string> pNameAndPaths = new Dictionary<string, string>();
        private readonly string ets2Path = "";

        public MainWindow()
        {
            if (!File.Exists("SII_Decrypt.exe"))
            {
                var res = MessageBox.Show("세이브 파일 복호화 프로그램을 찾을 수 없습니다. 설치하시겠습니까?\n처음 실행 시 설치하십시오.", "안내", MessageBoxButton.YesNo);
                if(res == MessageBoxResult.Yes)
                {
                    File.WriteAllBytes("SII_Decrypt.exe", Properties.Resources.SII_Decrypt);
                    MessageBox.Show("설치했습니다!");
                } else
                {
                    Application.Current.Shutdown(0);
                }
            }

            InitializeComponent();
            Title += " " + Version;
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
            ProfileChanged(false);
            LoadTasks();

            var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            path += "\\Euro Truck Simulator 2\\profiles";
            ets2Path = path;
            if (Directory.Exists(path))
            {
                var dinfo = new DirectoryInfo(path);
                var dlist = dinfo.GetDirectories();
                var pattern = @"^([ABCDEF\d]{2,2})+$";
                foreach (var delem in dlist)
                {
                    var ename = delem.Name;
                    if (!Regex.IsMatch(ename, pattern)) continue;
                    byte[] dBytes = StringToByteArray(ename);
                    string utf8result = System.Text.Encoding.UTF8.GetString(dBytes);
                    ProfileList.Items.Add(utf8result);
                    pNameAndPaths.Add(utf8result, ename);
                }
            }
            else
            {
                MessageBox.Show("ETS2 세이브 폴더를 찾을 수 없습니다.", "오류", MessageBoxButton.OK);
                Application.Current.Shutdown(0);
            }
        }

        private void LoadSaveFile(string path)
        {
            AppStatus.Items[0] = "세이브 파일 해독 중...";

            var onEnd = new Action<string>((string str) =>
            {
                tasks.saveFile = (ProfileSave)SaveList.SelectedItem;
                tasks.saveFile.content = str;

                AppStatus.Items[0] = "완료했습니다.";
                ShowTasks(true);
            });

            new Thread(() =>
            {
                var gameSiiPath = path + @"\game.sii";

                var psi = new ProcessStartInfo("SII_Decrypt.exe")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = "\"" + gameSiiPath + "\""
                };
                var proc = new Process
                {
                    StartInfo = psi
                };
                proc.Start();
                proc.WaitForExit();

                var saveFile = File.ReadAllText(gameSiiPath, Encoding.UTF8);

                if (!saveFile.StartsWith("SiiNunit"))
                {
                    MessageBox.Show("세이브 파일이 정상적인 형태가 아닙니다.", "로드 실패");
                    Dispatcher.Invoke(onEnd);
                }

                Dispatcher.Invoke(onEnd, saveFile);
            }).Start();
        }

        private void LoadSaves(string path)
        {
            AppStatus.Items[0] = "세이브 목록 불러오는 중...";
            new Thread(() =>
            {
                Thread.Sleep(250);
                Dispatcher.Invoke(() =>
                {
                    SaveList.Items.Clear();
                });
                foreach (var save in Directory.GetDirectories(path))
                {
                    var fpath = save + @"\info.sii";
                    Console.WriteLine(fpath);
                    if (!File.Exists(fpath)) continue; // Not a save file.
                    var psi = new ProcessStartInfo("SII_Decrypt.exe")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Arguments = "\"" + fpath + "\""
                    };
                    var proc = new Process
                    {
                        StartInfo = psi
                    };
                    proc.Start();
                    proc.WaitForExit();
                    var content = File.ReadAllText(fpath);

                    if (content.StartsWith("ScsC")) // decrypt fail
                    {
                        MessageBox.Show("암호화된 세이브 파일을 복호화하지 못했습니다. 스킵합니다.\n" + new DirectoryInfo(save).Name, "복호화 실패");
                        continue;
                    }
                    if (!content.StartsWith("SiiNunit")) // corrupted file
                    {
                        continue;
                    }

                    string namePattern = "name: \"(.*)\"";
                    string fileTimePattern = @"file_time: \b(\d+)\b";

                    if (!Regex.IsMatch(content, namePattern)) continue;
                    var nameResult = "N/A";
                    {
                        var result = Regex.Match(content, namePattern);
                        nameResult = result.Groups[1].Value;
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

                    Dispatcher.Invoke(() =>
                    {
                        ProfileSave psave = new ProfileSave
                        {
                            savename = nameResult,
                            directory = new DirectoryInfo(save).Name,
                            time = dateResult,
                            formattedtime = dateFormatResult,
                            fullPath = save
                        };

                        SaveList.Items.Add(psave);
                    });
                }
                Dispatcher.Invoke(() =>
                {
                    var l = new List<ProfileSave>();
                    foreach (var item in SaveList.Items)
                    {
                        l.Add((ProfileSave)item);
                    }
                    SaveList.Items.Clear();
                    l.Sort(new Comparison<ProfileSave>((ProfileSave a, ProfileSave b) =>
                    {
                        if (a.time > b.time) return -1;
                        if (a.time < b.time) return 1;
                        return 0;
                    }));
                    foreach (var item in l)
                    {
                        SaveList.Items.Add(item);
                    }
                    ShowSavegames(true);
                    AppStatus.Items[0] = "완료했습니다.";
                    EnableAll();
                });
            }).Start();
        }

        private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DisableAll();
            ProfileChanged(true);
            var newItem = e.AddedItems[0].ToString();
            if (pNameAndPaths.ContainsKey(newItem))
            {
                if (Directory.Exists(ets2Path + @"\" + pNameAndPaths[newItem]))
                {
                    if (pNameAndPaths.ContainsKey(ProfileList.SelectedItem.ToString()))
                    {
                        LoadSaves(ets2Path + @"\" + pNameAndPaths[newItem] + @"\save");
                    }
                    //EnableAll();
                    return;
                }
            }
        }

        private void SaveList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var psave = SaveList.SelectedItem;
            if (psave is ProfileSave ps)
            {
                SaveInfo_Name.Content = "이름: " + ps.savename;
                SaveInfo_Dir.Content = "폴더: " + ps.directory;
                SaveInfo_Date.Content = "날짜: " + ps.formattedtime;

                if (SaveInfo.Visibility != Visibility.Visible)
                {
                    var anim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.4)))
                    {
                        DecelerationRatio = 1
                    };
                    SaveInfo.BeginAnimation(StackPanel.OpacityProperty, anim);

                    var anim0 = new DoubleAnimation(1, 0.8, new Duration(TimeSpan.FromSeconds(0.1)))
                    {
                        DecelerationRatio = 1,
                        AutoReverse = true
                    };
                    SaveList.BeginAnimation(ListBox.OpacityProperty, anim0);
                }

                SaveInfo.Visibility = Visibility.Visible;
                if(TaskListPanel.Visibility == Visibility.Visible)
                {
                    SavegameChanged(true);
                }
            }
        }

        private void TaskList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TaskList.SelectedItem == null) return;
            ShowTaskStart(true);
            if (TaskList.SelectedItem is SaveEditTask task)
            {
                var displayAnim = new Action(() =>
                {
                    TaskDescription.Text = task.description;

                    var anim0 = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.25)))
                    {
                        DecelerationRatio = 1
                    };
                    TaskDescription.BeginAnimation(TextBlock.OpacityProperty, anim0);
                });
                var anim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.25)))
                {
                    AccelerationRatio = 0.5,
                    DecelerationRatio = 0.5
                };
                anim.Completed += (object s, EventArgs a) => displayAnim();
                if(TaskDescription.Text == "")
                {
                    displayAnim();
                } else
                {
                    TaskDescription.BeginAnimation(TextBlock.OpacityProperty, anim);
                }
            }
            else
            {
                MessageBox.Show("작업이 잘못되었습니다!", "오류");
            }
        }

        private void LoadSaveFileButton_Click(object sender, RoutedEventArgs e)
        {
            SavegameChanged(true);
            new Thread(() =>
            {
                Thread.Sleep(300);
                Dispatcher.Invoke(() =>
                {
                    var ps = (ProfileSave)SaveList.SelectedItem;
                    LoadSaveFile(ets2Path + @"\" + pNameAndPaths[ProfileList.SelectedItem.ToString()] + @"\save" + "\\" + ps.directory);
                });
            }).Start();
        }

        private void StartTaskButton_Click(object sender, RoutedEventArgs e)
        {
            AppStatus.Items[0] = "지정한 세이브 편집 작업 실행 중...";
            ((SaveEditTask)TaskList.SelectedItem).run();
            AppStatus.Items[0] = "완료했습니다.";
        }

        private void ProfileChanged(bool animate)
        {
            if (animate)
            {
                SaveListPanel.BeginAnimation(DockPanel.OpacityProperty, null);
                var anim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.2)))
                {
                    AccelerationRatio = 1
                };
                anim.Completed += (object sender, EventArgs e) =>
                {
                    SaveListPanel.Visibility = Visibility.Hidden;
                    SaveInfo.Visibility = Visibility.Collapsed;
                };
                SaveListPanel.BeginAnimation(DockPanel.OpacityProperty, anim);
            }
            else
            {
                SaveListPanel.Visibility = Visibility.Hidden;
                SaveInfo.Visibility = Visibility.Collapsed;
            }
            SavegameChanged(animate);
        }

        private void SavegameChanged(bool animate)
        {
            if (animate && TaskListPanel.Visibility == Visibility.Visible)
            {
                TaskListPanel.BeginAnimation(DockPanel.OpacityProperty, null);
                var anim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.2)))
                {
                    AccelerationRatio = 1
                };
                anim.Completed += (object sender, EventArgs e) =>
                {
                    TaskListPanel.Visibility = Visibility.Hidden;
                    StartTaskButton.Visibility = Visibility.Hidden;
                };
                TaskListPanel.BeginAnimation(DockPanel.OpacityProperty, anim);
            }
            else
            {
                TaskListPanel.Visibility = Visibility.Hidden;
                StartTaskButton.Visibility = Visibility.Hidden;
            }
            TaskList.SelectedIndex = -1;
            TaskList.SelectedItem = null;
            TaskDescription.Text = "";
        }
        private void ShowSavegames(bool animate)
        {
            SaveListPanel.Visibility = Visibility.Visible;
            if (animate)
            {
                SaveListPanel.BeginAnimation(DockPanel.OpacityProperty, null);
                var anim = new DoubleAnimation(0, 2, new Duration(new TimeSpan(200 * 10000)))
                {
                    DecelerationRatio = 1
                };
                SaveListPanel.BeginAnimation(DockPanel.OpacityProperty, anim);

                var blur = new BlurEffect
                {
                    RenderingBias = RenderingBias.Quality,
                    Radius = 30
                };

                SaveListPanel.Effect = blur;

                var anim0 = new DoubleAnimation(10, 0, new Duration(TimeSpan.FromSeconds(1)))
                {
                    DecelerationRatio = 1
                };
                anim0.Completed += (object sender, EventArgs e) =>
                {
                    SaveListPanel.Effect = null;
                };
                blur.BeginAnimation(BlurEffect.RadiusProperty, anim0);
            }
        }

        private void ShowTasks(bool animate)
        {
            ShowSavegames(false);
            if (animate && TaskListPanel.Visibility == Visibility.Hidden)
            {
                TaskListPanel.BeginAnimation(DockPanel.OpacityProperty, null);
                var anim = new DoubleAnimation(0, 2, new Duration(new TimeSpan(200 * 10000)))
                {
                    DecelerationRatio = 1
                };
                TaskListPanel.BeginAnimation(DockPanel.OpacityProperty, anim);

                var blur = new BlurEffect
                {
                    RenderingBias = RenderingBias.Quality,
                    Radius = 30
                };

                TaskListPanel.Effect = blur;

                var anim0 = new DoubleAnimation(10, 0, new Duration(TimeSpan.FromSeconds(1)))
                {
                    DecelerationRatio = 1
                };
                anim0.Completed += (object sender, EventArgs e) =>
                {
                    SaveListPanel.Effect = null;
                };
                blur.BeginAnimation(BlurEffect.RadiusProperty, anim0);
            }
            TaskListPanel.Visibility = Visibility.Visible;
        }
        private void ShowTaskStart(bool animate)
        {
            ShowTasks(animate);
            if (animate && StartTaskButton.Visibility != Visibility.Visible)
            {
                StartTaskButton.BeginAnimation(Button.OpacityProperty, null);
                var anim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.3)))
                {
                    DecelerationRatio = 1
                };
                StartTaskButton.BeginAnimation(Button.OpacityProperty, anim);
            }
            StartTaskButton.Visibility = Visibility.Visible;
        }
        private void DisableAll()
        {
            ProfileList.IsEnabled = false;
            SaveList.IsEnabled = false;
            TaskList.IsEnabled = false;
        }
        private void EnableAll()
        {
            ProfileList.IsEnabled = true;
            SaveList.IsEnabled = true;
            TaskList.IsEnabled = true;
        }
    }
}