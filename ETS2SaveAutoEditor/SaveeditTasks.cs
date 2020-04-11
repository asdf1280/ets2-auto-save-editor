using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace ETS2SaveAutoEditor
{
    class EditUtils
    {
        public static bool HasClass(string content, string classname)
        {
            return Regex.IsMatch(content, @"\b" + classname + @"\b : ([\w\.]+) {");
        }
        public static string ObjectValue(string content, string elementName)
        {
            var sp = content.Split('\n');
            foreach (var str in sp)
            {
                if (str.Trim() == "}")
                {
                    return null;
                }
                var pa = @"\b" + Regex.Escape(elementName) + @": ([\w\.]+)\b";
                if (Regex.IsMatch(str, pa))
                {
                    return Regex.Match(str, pa).Groups[1].Value;
                }
            }
            return null;
        }
        public static string ObjectValueIn(string content, string elementName, string parentId)
        {
            var startIndex = Regex.Match(content, @"\b\w+? : " + Regex.Escape(parentId) + " {").Index;
            var sr = content.Substring(startIndex);
            var sp = sr.Split('\n');
            foreach (var str in sp)
            {
                if (str.Trim() == "}")
                {
                    return null;
                }
                var pa = @"\b" + Regex.Escape(elementName) + @": ([\w\.]+)\b";
                if (Regex.IsMatch(str, pa))
                {
                    return Regex.Match(str, pa).Groups[1].Value;
                }
            }
            return null;
        }
        public static int IndexIn(string content, string elementName, string parentId)
        {
            var startIndex = Regex.Match(content, @"\b\w+? : " + Regex.Escape(parentId) + " {").Index;
            var cutData = content.Substring(startIndex);
            var sp = cutData.Split('\n');
            var index = 0;
            foreach (var str in sp)
            {
                if (str.Trim() == "}")
                {
                    return -1;
                }
                var patternA = @"\b" + Regex.Escape(elementName) + @": ([\w\.]+)\b";
                if (Regex.IsMatch(str, patternA))
                {
                    var matchResult = Regex.Match(str, patternA);
                    return matchResult.Index + matchResult.Groups[1].Index + startIndex + index;
                }
                index += str.Length; // length + line break;
            }
            return -1;
        }
        public static int LineIndexIn(string content, string elementName, string parentId)
        {
            var startIndex = Regex.Match(content, @"\b\w+? : " + Regex.Escape(parentId) + " {").Index;
            var cutData = content.Substring(startIndex);
            var sp = cutData.Split('\n');
            var index = 0;
            foreach (var str in sp)
            {
                if (str.Trim() == "}")
                {
                    return -1;
                }
                var patternA = @"\b" + Regex.Escape(elementName) + @": [\w\.]+\b";
                if (Regex.IsMatch(str, patternA))
                {
                    var matchResult = Regex.Match(str, patternA);
                    return matchResult.Index + startIndex + index;
                }
                index += str.Length; // length + line break;
            }
            return -1;
        }
        public static string ObjectValueInClass(string content, string elementName, string parentClass)
        {
            var startIndex = Regex.Match(content, @"\b" + Regex.Escape(parentClass) + @" : [\d\.] {").Index;
            var sr = content.Substring(startIndex);
            var sp = sr.Split('\n');
            foreach (var str in sp)
            {
                if (str.Trim() == "}")
                {
                    return null;
                }
                var pa = @"\b" + Regex.Escape(elementName) + @": ([\w\.]+)\b";
                if (Regex.IsMatch(str, pa))
                {
                    return Regex.Match(str, pa).Groups[1].Value;
                }
            }
            return null;
        }
        public static int IndexInClass(string content, string elementName, string parentClass)
        {
            var startIndex = Regex.Match(content, @"\b" + Regex.Escape(parentClass) + @" : [\d\.] {").Index;
            var sr = content.Substring(startIndex);
            var sp = sr.Split('\n');
            var index = 0;
            foreach (var str in sp)
            {
                if (str.Trim() == "}")
                {
                    return -1;
                }
                var pa = @"\b" + Regex.Escape(elementName) + @": ([\w\.]+)\b";
                if (Regex.IsMatch(str, pa))
                {
                    return Regex.Match(str, pa).Groups[1].Index + startIndex + index;
                }
                index += str.Length; // length + line break;
            }
            return -1;
        }
        public static int LineIndexInClass(string content, string elementName, string parentClass)
        {
            var startIndex = Regex.Match(content, @"\b" + Regex.Escape(parentClass) + @" : [\d\.] {").Index;
            var sr = content.Substring(startIndex);
            var sp = sr.Split('\n');
            var index = 0;
            foreach (var str in sp)
            {
                if (str.Trim() == "}")
                {
                    return -1;
                }
                var pa = @"\b" + Regex.Escape(elementName) + @": [\w\.]+\b";
                if (Regex.IsMatch(str, pa))
                {
                    return Regex.Match(str, pa).Index + startIndex + index;
                }
                index += str.Length; // length + line break;
            }
            return -1;
        }
        public static int IndexOfElementId(string content, string elementid)
        {
            try
            {
                var startIndex = Regex.Match(content, @"\b\w+? : " + Regex.Escape(elementid) + @" {").Index;
                return startIndex;
            }
            catch
            {
                return -1;
            }
        }
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
                        foreach(var str in substr.Split('\n'))
                        {
                            if(Regex.IsMatch(str, pattern1))
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
                    var listCount = int.Parse(EditUtils.ObjectValueInClass(saveFile.content, "screen_access_list", "economy"));
                    var listCountStr = EditUtils.ObjectValueInClass(saveFile.content, "screen_access_list", "economy");
                    var listCountIndex = EditUtils.IndexInClass(saveFile.content, "screen_access_list", "economy");

                    var msgBoxRes = MessageBox.Show("Unlock GUIs such as skills. For new profiles.\nSome GUIs that's normally disabled can be enabled too.\nThis job may take a while.", "Unlock", MessageBoxButton.OKCancel);
                    if (msgBoxRes == MessageBoxResult.Cancel)
                    {
                        return;
                    }

                    var content = saveFile.content;

                    var sb = new StringBuilder();
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
                        sb.Append(str.Substring(0, str.Length).Replace("\r", "") + "\n");
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
                        var res = ListInputBox.Show("Choose engine", "Choose a new engine for current assigned truck.\nIn face, old Scania/Volvo engines are better than new ones.\n"
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
                                    Console.WriteLine((int)(matchIndex0 + index));
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
    }
}
