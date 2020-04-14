using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace ETS2SaveAutoEditor
{
    struct Paintjob
    {
        public string mask_r_color;
        public string mask_g_color;
        public string mask_b_color;
        public string flake_color;
        public string flip_color;
        public string base_color;
        public string data_path;
    }
    public class SaveeditTasks
    {
        public ProfileSave saveFile;
        public SaveEditTask OwnTest()
        {
            var run = new Action(() =>
            {
                var pattern = @"\bplayer : [\w\.]+ {";
                if (Regex.IsMatch(saveFile.content, pattern))
                {
                    var mr = Regex.Match(saveFile.content, pattern);
                    var sr = saveFile.content.Substring(mr.Index);
                    var sp = sr.Split('\n');
                    var notFound = false;
                    var foundTruck = false;
                    var foundTrailer = false;
                    foreach (var str in sp)
                    {
                        if (str.Trim() == "}")
                        {
                            if (!foundTrailer && !foundTruck)
                                notFound = true;
                            break;
                        }
                        var pa = @"\bassigned_truck: ([\w\.]+)\b";
                        var pb = @"\bassigned_trailer: ([\w\.]+)\b";
                        if (Regex.IsMatch(str, pa))
                        {
                            if (Regex.Match(str, pa).Groups[1].Value.ToLower() != "null")
                                foundTruck = true;
                        }
                        if (Regex.IsMatch(str, pb))
                        {
                            if (Regex.Match(str, pb).Groups[1].Value.ToLower() != "null")
                                foundTrailer = true;
                        }
                    }
                    if (notFound)
                    {
                        MessageBox.Show("Could not figure out if you have trailer or not.", "Error");
                        return;
                    }
                    else
                    {
                        if (foundTruck)
                        {
                            MessageBox.Show("There is assigned truck.", "테스트");
                        }
                        else
                        {
                            MessageBox.Show("There isn't assigned truck.", "테스트");
                        }
                        if (foundTrailer)
                        {
                            MessageBox.Show("There is assigned trailer.", "테스트");
                        }
                        else
                        {
                            MessageBox.Show("There isn't assigned trailer.", "테스트");
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Could not find pleayer info", "Error");
                }
            });
            return new SaveEditTask
            {
                name = "Test",
                run = run,
                description = "Checks if the savegame has assigned truck/trailer"
            };
        }
        public SaveEditTask MoneySet()
        {
            var run = new Action(() =>
            {
                try
                {
                    var content = saveFile.content;
                    var pattern0 = @"\beconomy : [\w\.]+ {";
                    var pattern1 = @"\bbank: ([\w\.]+)\b";
                    var matchIndex = Regex.Match(content, pattern0).Index;
                    var substr = content.Substring(matchIndex);
                    string resultLine = null;
                    int resultIndex = -1;
                    {
                        var index = matchIndex;
                        foreach (var str in substr.Split('\n'))
                        {
                            if (Regex.IsMatch(str, pattern1))
                            {
                                resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                                resultIndex = Regex.Match(str, pattern1).Groups[1].Index + index;
                                break;
                            }
                            if (str.Trim() == "}") break;
                            index += str.Length + 1;
                        }
                    }

                    if (resultLine == null)
                    {
                        MessageBox.Show("Corrupted savegame.", "Error");
                        return;
                    }

                    pattern0 = @"\bbank : " + resultLine + " {";
                    pattern1 = @"\bmoney_account: ([\w\.]+)\b";
                    matchIndex = Regex.Match(content, pattern0).Index;
                    substr = content.Substring(matchIndex);
                    resultLine = null;
                    resultIndex = -1;
                    {
                        int index = matchIndex;
                        foreach (var str in substr.Split('\n'))
                        {
                            if (Regex.IsMatch(str, pattern1))
                            {
                                resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                                resultIndex = Regex.Match(str, pattern1).Groups[1].Index + index;
                                break;
                            }
                            if (str.Trim() == "}") break;
                            index += str.Length + 1;
                        }
                    }

                    var specifiedCash = NumberInputBox.Show("Specify cash", "Please specify the new cash.\nCurrent cash: " + resultLine + "\nCaution: Too high value may crash the game. Please be careful.");

                    if (specifiedCash == -1)
                    {
                        return;
                    }

                    var sb = new StringBuilder();
                    sb.Append(saveFile.content.Substring(0, resultIndex));
                    sb.Append(specifiedCash.ToString());
                    sb.Append(saveFile.content.Substring(resultIndex + resultLine.Length));
                    saveFile.Save(sb.ToString());
                    MessageBox.Show("완료되었습니다!", "완료");
                }
                catch (Exception e)
                {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask
            {
                name = "Specify cash",
                run = run,
                description = "Specify cash"
            };
        }
        public SaveEditTask ExpSet()
        {
            var run = new Action(() =>
            {
                try
                {
                    var content = saveFile.content;
                    var pattern0 = @"\beconomy : [\w\.]+ {";
                    var pattern1 = @"\bexperience_points: ([\w\.]+)\b";
                    var matchIndex = Regex.Match(content, pattern0).Index;
                    var substr = content.Substring(matchIndex);
                    string resultLine = null;
                    int resultIndex = 0;
                    {
                        var index = matchIndex;
                        foreach (var str in substr.Split('\n'))
                        {
                            if (Regex.IsMatch(str, pattern1))
                            {
                                resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                                resultIndex = Regex.Match(str, pattern1).Groups[1].Index + index;
                                break;
                            }
                            if (str.Trim() == "}") break;
                            index += str.Length + 1;
                        }
                    }

                    if (resultLine == null)
                    {
                        MessageBox.Show("Corrupted savegame.", "Error");
                        return;
                    }

                    var specifiedExp = NumberInputBox.Show("Specify EXP", "Please specify the new exps.\nCurrent exps: " + resultLine + "\nCaution: Too high value may crash the game. Please be careful.");

                    if (specifiedExp == -1)
                    {
                        return;
                    }

                    var sb = new StringBuilder();
                    sb.Append(saveFile.content.Substring(0, resultIndex));
                    sb.Append(specifiedExp.ToString());
                    sb.Append(saveFile.content.Substring(resultIndex + resultLine.Length));
                    saveFile.Save(sb.ToString());
                    MessageBox.Show("Finished!", "Done");
                }
                catch (Exception e)
                {
                    MessageBox.Show("An unexpected error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask
            {
                name = "Specify EXP",
                run = run,
                description = "Specify EXP"
            };
        }
        public SaveEditTask UnlockScreens()
        {
            var run = new Action(() =>
            {
                try
                {
                    var content = saveFile.content;
                    var pattern0 = @"\beconomy : [\w\.]+ {";
                    var pattern1 = @"\bscreen_access_list: ([\w\.]+)\b";
                    var matchIndex = Regex.Match(content, pattern0).Index;
                    var substr = content.Substring(matchIndex);
                    string resultLine = null;
                    int resultLineIndex = 0;
                    {
                        var index = matchIndex;
                        foreach (var str in substr.Split('\n'))
                        {
                            if (Regex.IsMatch(str, pattern1))
                            {
                                resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                                resultLineIndex = index;
                                break;
                            }
                            if (str.Trim() == "}") break;
                            index += str.Length + 1;
                        }
                    }

                    var msgBoxRes = MessageBox.Show("Unlock GUIs such as skills. For new profiles.\nSome GUIs that's normally disabled can be enabled too.\nThis job may take a while.", "Unlock", MessageBoxButton.OKCancel);
                    if (msgBoxRes == MessageBoxResult.Cancel)
                    {
                        return;
                    }

                    var sb = new StringBuilder();
                    sb.Append(content.Substring(0, resultLineIndex));

                    content = content.Substring(resultLineIndex);
                    foreach (var line in content.Split('\n'))
                    {
                        var str = line;
                        if (str.Contains("screen_access_list:"))
                        {
                            str = " screen_access_list: 0";
                        }
                        else if (str.Contains("screen_access_list["))
                        {
                            continue;
                        }
                        sb.Append(str.Replace("\r", "") + "\n");
                    }

                    saveFile.Save(sb.ToString());
                    MessageBox.Show("Successfully unlocked!", "Done");
                }
                catch (Exception e)
                {
                    MessageBox.Show("An unexpected error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask
            {
                name = "Unlock all UI",
                run = run,
                description = "Unlock GUIs such as skills. For new profiles.\nSome GUIs that's normally disabled can be enabled too."
            };
        }
        public SaveEditTask TruckEngineSet()
        {
            var run = new Action(() =>
            {
                try
                {
                    var content = saveFile.content;
                    var pattern0 = @"\beconomy : [\w\.]+ {";
                    var pattern1 = @"\bplayer: ([\w\.]+)\b";
                    var matchIndex = Regex.Match(content, pattern0).Index;
                    var substr = content.Substring(matchIndex);
                    string resultLine = null;
                    foreach (var str in substr.Split('\n'))
                    {
                        if (Regex.IsMatch(str, pattern1))
                        {
                            resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                            break;
                        }
                        if (str.Trim() == "}") break;
                    }

                    pattern0 = @"\bplayer : " + resultLine + " {";
                    pattern1 = @"\bassigned_truck: ([\w\.]+)\b";
                    matchIndex = Regex.Match(content, pattern0).Index;
                    substr = content.Substring(matchIndex);
                    resultLine = null;
                    foreach (var str in substr.Split('\n'))
                    {
                        if (Regex.IsMatch(str, pattern1))
                        {
                            resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                            break;
                        }
                        if (str.Trim() == "}") break; // End of the class
                    }

                    if (resultLine == null)
                    {
                        MessageBox.Show("Corrupted savegame.", "Error");
                        return;
                    }
                    else if (resultLine == "null")
                    {
                        MessageBox.Show("No assigned truck found. Assign a truck in game.", "Error");
                        return;
                    }

                    var engineNames = new string[] { "Scania new 730", "Scania old 730", "Volvo new 750", "Volvo old 750", "Renault Premium 380", "Iveco 310(...)" };
                    var enginePaths = new string[] {
                        "/def/vehicle/truck/scania.s_2016/engine/dc16_730.sii",
                        "/def/vehicle/truck/scania.streamline/engine/dc16_730_2.sii",
                        "/def/vehicle/truck/volvo.fh16_2012/engine/d16g750.sii",
                        "/def/vehicle/truck/volvo.fh16/engine/d16g750.sii",
                        "/def/vehicle/truck/renault.premium/engine/dxi11_380.sii",
                        "/def/vehicle/truck/iveco.stralis/engine/cursor8_310hp.sii"
                    };
                    var enginePath = "";
                    {
                        var res = ListInputBox.Show("Choose engine", "Choose a new engine for current assigned truck.\nIn fact, old Scania/Volvo engines are better than new ones.\n"
                            + "It may take a while to change your engine.", engineNames);
                        if (res == -1)
                        {
                            return;
                        }
                        enginePath = enginePaths[res];
                    }

                    pattern0 = @"\bvehicle : " + resultLine + " {";
                    pattern1 = @"\baccessories\[\d*\]: ([\w\.]+)\b";
                    matchIndex = Regex.Match(content, pattern0).Index;
                    substr = content.Substring(matchIndex);
                    resultLine = null;
                    foreach (var line in substr.Split('\n'))
                    {
                        if (Regex.IsMatch(line, pattern1))
                        {
                            var id = Regex.Match(line, pattern1).Groups[1].Value;
                            var p50 = @"\bvehicle_(addon_|sound_|wheel_|drv_plate_|paint_job_)?accessory : " + id + " {";
                            var matchIndex0 = Regex.Match(substr, p50).Index + matchIndex;
                            var substr0 = content.Substring(matchIndex0);
                            var index = 0;
                            foreach (var line0 in substr0.Split('\n'))
                            {
                                if (Regex.IsMatch(line0, @"\bdata_path:\s""\/def\/vehicle\/truck\/[^/]+?\/engine\/"))
                                {
                                    var sb = new StringBuilder();
                                    sb.Append(content.Substring(0, (int)(matchIndex0 + index)));
                                    sb.Append(" data_path: \"" + enginePath + "\"\n");
                                    sb.Append(content.Substring((int)(matchIndex0 + index + line0.Length + 1)));
                                    content = sb.ToString();
                                }
                                if (line0.Trim() == "}") break;
                                index += line0.Length + 1;
                            }
                        }
                        if (line.Trim() == "}") break; // End of the class
                    }
                    saveFile.Save(content);
                    MessageBox.Show("Successfully changed!", "Done");
                }
                catch (Exception e)
                {
                    MessageBox.Show("An unexpected error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask
            {
                name = "Set truck engine",
                run = run,
                description = "Change the truck's engine to a few engines available."
            };
        }
        public SaveEditTask TruckSoundSet()
        {
            var run = new Action(() =>
            {
                try
                {
                    var content = saveFile.content;
                    var pattern0 = @"\beconomy : [\w\.]+ {";
                    var pattern1 = @"\bplayer: ([\w\.]+)\b";
                    var matchIndex = Regex.Match(content, pattern0).Index;
                    var substr = content.Substring(matchIndex);
                    string resultLine = null;
                    foreach (var str in substr.Split('\n'))
                    {
                        if (Regex.IsMatch(str, pattern1))
                        {
                            resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                            break;
                        }
                        if (str.Trim() == "}") break;
                    }

                    pattern0 = @"\bplayer : " + resultLine + " {";
                    pattern1 = @"\bassigned_truck: ([\w\.]+)\b";
                    matchIndex = Regex.Match(content, pattern0).Index;
                    substr = content.Substring(matchIndex);
                    resultLine = null;
                    foreach (var str in substr.Split('\n'))
                    {
                        if (Regex.IsMatch(str, pattern1))
                        {
                            resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                            break;
                        }
                        if (str.Trim() == "}") break; // End of the class
                    }

                    if (resultLine == null)
                    {
                        MessageBox.Show("Corrupted savegame.", "Error");
                        return;
                    }
                    else if (resultLine == "null")
                    {
                        MessageBox.Show("You don't have an assigned truck.", "Error");
                        return;
                    }

                    var soundNames = new string[] { "Scania S 2016 V8", "Scania S 2016", "Scania R 2016 V8", "Scania R 2016", "Scania R 2009 V8", "Scania R 2009", "Scania Streamline V8", "Scania Streamline", "Volvo FH16 2012", "Volvo FH 2012", "Volvo FH16 2009" };
                    var interiorPaths = new string[] {
                    "/def/vehicle/truck/scania.s_2016/sound/interior_v8.sii",
                    "/def/vehicle/truck/scania.s_2016/sound/interior.sii",
                    "/def/vehicle/truck/scania.r_2016/sound/interior_v8.sii",
                    "/def/vehicle/truck/scania.r_2016/sound/interior.sii",
                    "/def/vehicle/truck/scania.r/sound/interior_v8.sii",
                    "/def/vehicle/truck/scania.r/sound/interior.sii",
                    "/def/vehicle/truck/scania.streamline/sound/interior_v8.sii",
                    "/def/vehicle/truck/scania.streamline/sound/interior.sii",
                    "/def/vehicle/truck/scania.streamline/sound/interior_v8.sii",
                    "/def/vehicle/truck/scania.streamline/sound/interior.sii",
                    "/def/vehicle/truck/volvo.fh16_2012/sound/interior_16.sii",
                    "/def/vehicle/truck/volvo.fh16_2012/sound/interior.sii",
                    "/def/vehicle/truck/volvo.fh16/sound/interior.sii",
                };
                    var exteriorPaths = new string[] {
                    "/def/vehicle/truck/scania.s_2016/sound/exterior_v8.sii",
                    "/def/vehicle/truck/scania.s_2016/sound/exterior.sii",
                    "/def/vehicle/truck/scania.r_2016/sound/exterior_v8.sii",
                    "/def/vehicle/truck/scania.r_2016/sound/exterior.sii",
                    "/def/vehicle/truck/scania.r/sound/exterior_v8.sii",
                    "/def/vehicle/truck/scania.r/sound/exterior.sii",
                    "/def/vehicle/truck/scania.streamline/sound/exterior_v8.sii",
                    "/def/vehicle/truck/scania.streamline/sound/exterior.sii",
                    "/def/vehicle/truck/volvo.fh16_2012/sound/exterior_16.sii",
                    "/def/vehicle/truck/volvo.fh16_2012/sound/exterior.sii",
                    "/def/vehicle/truck/volvo.fh16/sound/exterior.sii",
                };
                    var interiorPath = "";
                    var exteriorPath = "";
                    {
                        var res = ListInputBox.Show("Choose sound", "Choose new sound for your assigned truck.\n"
                            + "The task may take a while.\nEach Scania S/R and R 2009/Streamline have same sounds.", soundNames);
                        if (res == -1)
                        {
                            return;
                        }
                        interiorPath = interiorPaths[res];
                        exteriorPath = exteriorPaths[res];
                    }

                    pattern0 = @"\bvehicle : " + resultLine + " {";
                    pattern1 = @"\baccessories\[\d*\]: ([\w\.]+)\b";
                    matchIndex = Regex.Match(content, pattern0).Index;
                    substr = content.Substring(matchIndex);
                    resultLine = null;
                    foreach (var line in substr.Split('\n'))
                    {
                        if (Regex.IsMatch(line, pattern1))
                        {
                            var id = Regex.Match(line, pattern1).Groups[1].Value;
                            var p50 = @"\bvehicle_sound_accessory : " + id + " {";
                            if (!Regex.IsMatch(substr, p50)) continue;
                            var matchIndex0 = Regex.Match(substr, p50).Index + matchIndex;
                            var substr0 = content.Substring(matchIndex0);
                            var index = 0;
                            foreach (var line0 in substr0.Split('\n'))
                            {
                                if (Regex.IsMatch(line0, @"\bdata_path:\s""\/def\/vehicle\/truck\/[^/]+?\/sound\/"))
                                {
                                    var path = Regex.IsMatch(line0, @"\bdata_path:\s""\/def\/vehicle\/truck\/[^/]+?\/sound\/interior") ? interiorPath : exteriorPath;

                                    var sb = new StringBuilder();
                                    sb.Append(content.Substring(0, (int)(matchIndex0 + index)));
                                    sb.Append(" data_path: \"" + path + "\"\n");
                                    sb.Append(content.Substring((int)(matchIndex0 + index + line0.Length + 1)));
                                    content = sb.ToString();
                                }
                                if (line0.Trim() == "}") break;
                                index += line0.Length + 1;
                            }
                        }
                        if (line.Trim() == "}") break; // End of the class
                    }
                    saveFile.Save(content);
                    MessageBox.Show("Done!", "Done");
                }
                catch (Exception e)
                {
                    MessageBox.Show("An error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask
            {
                name = "Set truck sound",
                run = run,
                description = "Change truck sound"
            };
        }
        public SaveEditTask MapReset()
        {
            var run = new Action(() =>
            {
                try
                {
                    var content = saveFile.content;
                    var sb = new StringBuilder();
                    foreach (var line in content.Split('\n'))
                    {
                        var str = line;
                        if (line.Contains("discovered_items:"))
                            str = " discovered_items: 0";
                        else if (line.Contains("discovered_items")) continue;
                        sb.AppendLine(str);
                    }
                    saveFile.Save(sb.ToString());
                    MessageBox.Show("Done!", "Done");
                }
                catch (Exception e)
                {
                    MessageBox.Show("An error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask
            {
                name = "Reset map",
                run = run,
                description = "Reset explorered roads."
            };
        }
        public SaveEditTask Refuel()
        {
            var run = new Action(() =>
            {
                try
                {
                    var content = saveFile.content;
                    var sb = new StringBuilder();

                    var fuelPresetNames = new string[] { "1000x Tank", "100x Tank", "10x Tank", "5x Tank", "100%", "50%", "10%", "5%", "0%(...)" };
                    var fullPresetValues = new string[] {
                        "1000", "100", "10", "5", "1", "0.5", "0.1", "0.05", "0"
                    };
                    var fuelId = "";
                    {
                        var res = ListInputBox.Show("Choose fuel level", "Choose engine for your assigned truck.\nIn fact, old engines are better than new engines.\n"
                            + "The task may take a while.", fuelPresetNames);
                        if (res == -1)
                        {
                            return;
                        }
                        fuelId = fullPresetValues[res];
                    }

                    foreach (var line in content.Split('\n'))
                    {
                        var str = line;
                        if (line.Contains("fuel_relative:"))
                            str = " fuel_relative: " + fuelId;
                        sb.Append(str + "\n");
                    }
                    saveFile.Save(sb.ToString());
                    MessageBox.Show("Modifyed fuel level of all trucks!", "Done");
                }
                catch (Exception e)
                {
                    MessageBox.Show("An unknown error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask
            {
                name = "Fill fuel tank",
                run = run,
                description = "Set the fuel level of all trucks in chosen savegame."
            };
        }
        public SaveEditTask FixEverything()
        {
            var run = new Action(() =>
            {
                try
                {
                    var content = saveFile.content;
                    var sb = new StringBuilder();

                    foreach (var line in content.Split('\n'))
                    {
                        var str = line;
                        if (line.Contains(" wear:"))
                            str = " wear: 0";
                        sb.Append(str + "\n");
                    }
                    saveFile.Save(sb.ToString());
                    MessageBox.Show("Repaired all truck/trailers.", "Done");
                }
                catch (Exception e)
                {
                    MessageBox.Show("An unknown error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask
            {
                name = "Repair All",
                run = run,
                description = "Repair all truck/trailers in current savegame."
            };
        }
        public SaveEditTask SharePaint()
        {
            var run = new Action(() =>
            {
                try
                {
                    var content = saveFile.content;
                    var pattern0 = @"\beconomy : [\w\.]+ {";
                    var pattern1 = @"\bplayer: ([\w\.]+)\b";
                    var matchIndex = Regex.Match(content, pattern0).Index;
                    var substr = content.Substring(matchIndex);
                    string resultLine = null;
                    foreach (var str in substr.Split('\n'))
                    {
                        if (Regex.IsMatch(str, pattern1))
                        {
                            resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                            break;
                        }
                        if (str.Trim() == "}") break;
                    }

                    if (resultLine == null)
                    {
                        MessageBox.Show("Corrupted save file", "Error");
                        return;
                    }

                    var operationNames = new string[] { "Import, Truck", "Export, Truck", "Import, Trailer", "Export, Trailer" };
                    var truck = false;
                    var import = false;
                    {
                        var res = ListInputBox.Show("Choose job", "Which one do you want? Please choose what to import/export paintjob from.", operationNames);
                        if (res == -1)
                        {
                            return;
                        }
                        truck = res < 2;
                        import = res % 2 == 0;
                    }

                    pattern0 = @"\bplayer : " + resultLine + " {";
                    pattern1 = @"\bassigned_" + (truck ? "truck" : "trailer") + @": ([\w\.]+)\b";
                    matchIndex = Regex.Match(content, pattern0).Index;
                    substr = content.Substring(matchIndex);
                    resultLine = null;
                    foreach (var str in substr.Split('\n'))
                    {
                        if (Regex.IsMatch(str, pattern1))
                        {
                            resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                            break;
                        }
                        if (str.Trim() == "}") break; // End of the class
                    }
                    if (resultLine == "null")
                    {
                        if (truck)
                            MessageBox.Show("There's no assigned truck.", "Error");
                        else
                            MessageBox.Show("There's no assigned trailer.", "Error");
                        return;
                    }

                    string path;

                    var filter = (truck ? "Truck paintjob (*.paint0)|*.paint0|All files (*.*)|*.*" : "Trailer paintjob (*.paint1)|*.paint1|All files (*.*)|*.*");
                    if (import)
                    { // Import
                        OpenFileDialog dialog = new OpenFileDialog
                        {
                            Title = "Choose paintjob file",
                            Filter = filter
                        };
                        if (dialog.ShowDialog() != true) return;
                        path = dialog.FileName;
                    }
                    else // Export
                    {
                        SaveFileDialog dialog = new SaveFileDialog
                        {
                            Title = "Export paintjob",
                            Filter = filter
                        };
                        if (dialog.ShowDialog() != true) return;
                        path = dialog.FileName;
                    }

                    pattern0 = @"\b" + (truck ? "vehicle" : "trailer") + " : " + resultLine + " {";
                    pattern1 = @"\baccessories\[\d*\]: ([\w\.]+)\b";
                    matchIndex = Regex.Match(content, pattern0).Index;
                    substr = content.Substring(matchIndex);
                    resultLine = null;

                    if (import)
                    {
                        var str = File.ReadAllText(path, Encoding.UTF8);
                        var strs = str.Split(';');
                        if (strs.Length != 8)
                        {
                            MessageBox.Show("Corrupted paintjob file", "Error");
                            return;
                        }
                        var paintjob = new Paintjob
                        {
                            mask_r_color = strs[0],
                            mask_g_color = strs[1],
                            mask_b_color = strs[2],
                            flake_color = strs[3],
                            flip_color = strs[4],
                            base_color = strs[5],
                            data_path = strs[6]
                        };

                        foreach (var line in substr.Split('\n'))
                        {
                            if (Regex.IsMatch(line, pattern1))
                            {
                                var id = Regex.Match(line, pattern1).Groups[1].Value;
                                var p50 = @"\bvehicle_paint_job_accessory : " + id + " {";
                                if (!Regex.IsMatch(substr, p50)) continue;
                                var matchIndex0 = Regex.Match(substr, p50).Index + matchIndex; // TODO you must escape p50 id can contain .
                                var substr0 = content.Substring(matchIndex0);
                                var index = 0;
                                var sb = new StringBuilder();
                                sb.Append(content.Substring(0, matchIndex0));
                                foreach (var line0 in substr0.Split('\n'))
                                {
                                    var str0 = line0;
                                    if (Regex.IsMatch(line0, @"\bdata_path:\s"".*?"""))
                                    {
                                        str0 = " data_path: " + paintjob.data_path + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_r_color: \(.*?\)"))
                                    {
                                        str0 = " mask_r_color: " + paintjob.mask_r_color + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_g_color: \(.*?\)"))
                                    {
                                        str0 = " mask_g_color: " + paintjob.mask_g_color + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_b_color: \(.*?\)"))
                                    {
                                        str0 = " mask_b_color: " + paintjob.mask_b_color + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bflake_color: \(.*?\)"))
                                    {
                                        str0 = " flake_color: " + paintjob.flake_color + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bflip_color: \(.*?\)"))
                                    {
                                        str0 = " flip_color: " + paintjob.flip_color + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bbase_color: \(.*?\)"))
                                    {
                                        str0 = " base_color: " + paintjob.base_color + "";
                                    }
                                    Console.WriteLine("'" + Regex.Escape(str0) + "'");
                                    sb.AppendLine(str0);
                                    if (line0.Trim() == "}") break;
                                    index += line0.Length + 1;
                                }
                                sb.Append(content.Substring((int)(matchIndex0 + index + 1)));
                                content = sb.ToString();
                            }
                            if (line.Trim() == "}") break; // End of the class
                        }
                        saveFile.Save(content);
                        MessageBox.Show("Imported paintjob!", "Done");
                    }
                    else
                    {
                        var paintjob = new Paintjob();

                        foreach (var line in substr.Split('\n'))
                        {
                            if (Regex.IsMatch(line, pattern1))
                            {
                                var id = Regex.Match(line, pattern1).Groups[1].Value;
                                var p50 = @"\bvehicle_paint_job_accessory : " + id + " {";
                                if (!Regex.IsMatch(substr, p50)) continue;
                                var matchIndex0 = Regex.Match(substr, p50).Index + matchIndex; // TODO you must escape p50 id can contain .
                                var substr0 = content.Substring(matchIndex0);
                                var index = 0;
                                foreach (var line0 in substr0.Split('\n'))
                                {
                                    if (Regex.IsMatch(line0, @"\bdata_path:\s("".*?"")"))
                                    {
                                        paintjob.data_path = Regex.Match(line0, @"\bdata_path:\s("".*?"")").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_r_color: (\(.*?\))"))
                                    {
                                        paintjob.mask_r_color = Regex.Match(line0, @"\bmask_r_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_g_color: (\(.*?\))"))
                                    {
                                        paintjob.mask_g_color = Regex.Match(line0, @"\bmask_g_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_b_color: (\(.*?\))"))
                                    {
                                        paintjob.mask_b_color = Regex.Match(line0, @"\bmask_b_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bflake_color: (\(.*?\))"))
                                    {
                                        paintjob.flake_color = Regex.Match(line0, @"\bflake_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bflip_color: (\(.*?\))"))
                                    {
                                        paintjob.flip_color = Regex.Match(line0, @"\bflip_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bbase_color: (\(.*?\))"))
                                    {
                                        paintjob.base_color = Regex.Match(line0, @"\bbase_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (line0.Trim() == "}") break;
                                    index += line0.Length + 1;
                                }
                            }
                            if (line.Trim() == "}") break; // End of the class
                        }
                        try
                        {
                            var data = paintjob.mask_r_color + ";" + paintjob.mask_g_color + ";" + paintjob.mask_b_color + ";" + paintjob.flake_color + ";" + paintjob.flip_color + ";" + paintjob.base_color + ";" + paintjob.data_path + ";";
                            File.WriteAllText(path, data, Encoding.UTF8);
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Could not export", "Error");
                            throw;
                        }
                        MessageBox.Show("Exported paintjob!", "Done");
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("An unknown error occured", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask
            {
                name = "Export/import paintjob",
                run = run,
                description = "Import/export paintjob of assigned truck/trailer."
            };
        }
    }
}
