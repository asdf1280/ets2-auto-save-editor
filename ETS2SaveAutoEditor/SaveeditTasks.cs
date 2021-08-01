using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
                        MessageBox.Show("트럭/트레일러 소유 여부를 찾을 수 없습니다", "오류");
                        return;
                    }
                    else
                    {
                        if (foundTruck)
                        {
                            MessageBox.Show("할당된 트럭이 있습니다.", "테스트");
                        }
                        else
                        {
                            MessageBox.Show("할당된 트럭이 없습니다.", "테스트");
                        }
                        if (foundTrailer)
                        {
                            MessageBox.Show("할당된 트레일러가 있습니다.", "테스트");
                        }
                        else
                        {
                            MessageBox.Show("할당된 트레일러가 없습니다.", "테스트");
                        }
                    }
                }
                else
                {
                    MessageBox.Show("플레이어 정보를 찾을 수 없습니다", "오류");
                }
            });
            return new SaveEditTask
            {
                name = "테스트",
                run = run,
                description = "할당된 트럭과 트레일러가 존재하는지 확인합니다."
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
                        MessageBox.Show("손상된 세이브 파일입니다.", "오류");
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

                    var specifiedCash = NumberInputBox.Show("돈 지정하기", "자본을 몇으로 설정할까요?\n현재 자본: " + resultLine + "\n경고: 너무 높은 값으로 설정하면 게임 로딩 시 오류가 발생할 수 있습니다. 이 경우 개발자는 책임지지 않습니다.");

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
                name = "돈 지정",
                run = run,
                description = "소유하고 있는 돈을 원하는 숫자로 설정합니다. 너무 크면 게임 로딩 시 오류가 발생하며 개발자는 책임지지 않습니다. 100억 이하로 적당히 이용하시기 바랍니다."
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
                        MessageBox.Show("손상된 세이브 파일입니다.", "오류");
                        return;
                    }

                    var specifiedExp = NumberInputBox.Show("경험치 지정하기", "경험치를 몇으로 설정할까요?\n경험치의 절대적인 수치를 입력하세요. 더하는 것이 아니라 완전히 해당 경험치로 설정됩니다.\n현재 소유한 경험치는 " + resultLine + "입니다.\n경고: 너무 높은 값으로 설정하면 게임 로딩 시 오류가 발생할 수 있습니다. 이 경우 개발자는 책임지지 않습니다.");

                    if (specifiedExp == -1)
                    {
                        return;
                    }

                    var sb = new StringBuilder();
                    sb.Append(saveFile.content.Substring(0, resultIndex));
                    sb.Append(specifiedExp.ToString());
                    sb.Append(saveFile.content.Substring(resultIndex + resultLine.Length));
                    saveFile.Save(sb.ToString());
                    MessageBox.Show("[69exp]\n충전이 완료되었습니다!", "69exp");
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
                name = "경험치 지정",
                run = run,
                description = "경험치를 지정합니다."
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

                    var msgBoxRes = MessageBox.Show("[69cheat]\n모든 메뉴를 해금합니다. 트레일러 조정 등 일부 메뉴는 원래 사용이 불가함에도 해금될 수 있으니 주의하세요!\n이 작업은 시간이 조금 걸릴 수 있습니다.", "69cheat", MessageBoxButton.OKCancel);
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
                    MessageBox.Show("[69cheat]\n해금이 완료되었습니다!", "69cheat");
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
                name = "기능 모두 해제",
                run = run,
                description = "새 프로필에서 스킬 등 메뉴를 모두 해금합니다.\n트레일러 조정 등 일부 메뉴는 원래 사용이 불가함에도 해금될 수 있으니 주의하세요!"
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
                        MessageBox.Show("손상된 세이브 파일입니다.", "오류");
                        return;
                    }
                    else if (resultLine == "null")
                    {
                        MessageBox.Show("할당된 트럭이 없습니다. 게임을 실행하여 트럭을 자신에게 할당하세요.", "오류");
                        return;
                    }

                    var engineNames = new string[] { "스카니아 신형 730", "스카니아 구형 730", "볼보 신형 750", "볼보 구형 750", "르노 프리미엄 380", "이베코 310(...)" };
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
                        var res = ListInputBox.Show("엔진 선택하기", "현재 할당된 트럭에 적용할 엔진을 선택하세요.\n참고로 스카니아와 볼보는 구형의 엔진 성능이 신형보다 좋습니다.\n"
                            + "확인 버튼 클릭 후 편집 작업 완료까지 어느 정도 시간이 걸리니 참고하시기 바랍니다.", engineNames);
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
                    MessageBox.Show("엔진을 변경했습니다!", "완료");
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
                name = "트럭 엔진 지정",
                run = run,
                description = "트럭의 엔진을 설정 가능한 몇 가지 엔진으로 변경합니다."
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
                    MessageBox.Show("지도를 초기화했습니다.", "완료");
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
                name = "맵 초기화",
                run = run,
                description = "지도의 탐색한 도로를 초기화합니다."
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

                    var fuelPresetNames = new string[] { "연료통의 1000배", "연료통의 100배", "연료통의 10배", "연료통의 5배", "100%", "50%", "10%", "5%", "0%(...)" };
                    var fullPresetValues = new string[] {
                        "1000", "100", "10", "5", "1", "0.5", "0.1", "0.05", "0"
                    };
                    var fuelId = "";
                    {
                        var res = ListInputBox.Show("연료 수준 선택", "할당된 트럭에 적용할 연료 수준을 선택하십시오.", fuelPresetNames);
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
                    MessageBox.Show("모든 트럭의 연료를 지정한 값으로 변경했습니다.", "완료");
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
                name = "연료 채우기",
                run = run,
                description = "세이브 내의 모든 트럭의 연료를 설정합니다."
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
                    MessageBox.Show("모든 트럭/트레일러를 수리했습니다.", "완료");
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
                name = "모든 트럭/트레일러 수리",
                run = run,
                description = "세이브 내의 모든 트럭/트레일러를 수리합니다."
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
                        MessageBox.Show("손상된 세이브 파일입니다.", "오류");
                        return;
                    }

                    var operationNames = new string[] { "트럭, 불러오기", "트럭, 내보내기", "트레일러, 불러오기", "트레일러, 내보내기" };
                    var truck = false;
                    var import = false;
                    {
                        var res = ListInputBox.Show("작업 선택하기", "무엇의 페인트를 불러올까요? 내보낼까요?", operationNames);
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
                            MessageBox.Show("할당된 트럭이 없습니다.", "오류");
                        else
                            MessageBox.Show("할당된 트레일러가 없습니다.", "오류");
                        return;
                    }

                    string path;

                    var filter = (truck ? "트럭 페인트 파일 (*.paint0)|*.paint0|모든 파일 (*.*)|*.*" : "트레일러 페인트 파일 (*.paint1)|*.paint1|모든 파일 (*.*)|*.*");
                    if (import)
                    { // Import
                        OpenFileDialog dialog = new OpenFileDialog
                        {
                            Title = "페인트 파일 선택",
                            Filter = filter
                        };
                        if (dialog.ShowDialog() != true) return;
                        path = dialog.FileName;
                    }
                    else // Export
                    {
                        SaveFileDialog dialog = new SaveFileDialog
                        {
                            Title = "페인트 내보내기",
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
                            MessageBox.Show("손상된 페인트 파일입니다.", "오류");
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
                        MessageBox.Show("페인트를 불러왔습니다!", "완료");
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
                            MessageBox.Show("페인트를 내보낼 수 없습니다.", "오류");
                            throw;
                        }
                        MessageBox.Show("페인트를 내보냈습니다!", "완료");
                    }
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
                name = "페인트 내보내기/불러오기",
                run = run,
                description = "트럭/트레일러의 페인트를 내보내거나 불러옵니다."
            };
        }

        private enum PositionDataHeader : byte
        {
            KEY,
            END,
            SINGLE,
            MULTI
        }
        private readonly int positionDataVersion = 1;
        private string SimpleCompress(string data)
        {
            return data.Replace("truck_placement", "TRPL").Replace("trailer_placement", "TAPL");
        }
        private string SimpleDecompress(string data)
        {
            return data.Replace("TRPL", "truck_placement").Replace("TAPL", "trailer_placement");
        }
        private string EncodePositionData(Dictionary<string, string> data)
        {
            Encoding encoding = Encoding.UTF8;
            MemoryStream memoryStream = new MemoryStream();
            GZipStream compressionStream = new GZipStream(memoryStream, CompressionLevel.Optimal);
            BinaryWriter binaryWriter = new BinaryWriter(compressionStream, Encoding.ASCII);
            void sendString(string text)
            {
                byte[] bytes = encoding.GetBytes(text);
                binaryWriter.Write(bytes.Length);
                binaryWriter.Write(bytes);
            }
            void sendHeader(PositionDataHeader header)
            {
                binaryWriter.Write((byte)header);
            }
            binaryWriter.Write(positionDataVersion);
            foreach (string key in data.Keys)
            {
                string value = data[key];

                sendHeader(PositionDataHeader.KEY);
                sendString(SimpleCompress(key));

                if (value.StartsWith("m"))
                {
                    sendHeader(PositionDataHeader.MULTI);
                }
                else
                {
                    sendHeader(PositionDataHeader.SINGLE);
                }
                value = value.Substring(1);
                sendString(SimpleCompress(value));
            }
            sendHeader(PositionDataHeader.END);
            binaryWriter.Flush();
            binaryWriter.Close();
            string encoded = Convert.ToBase64String(memoryStream.GetBuffer());
            int Eqs = 0;
            int As = 0;
            int i;
            for (i = encoded.Length - 1; i >= 0; i--)
            {
                if (encoded[i] != '=' && encoded[i] != 'A')
                {
                    break;
                }
                if (encoded[i] == '=')
                {
                    if (As != 0)
                    {
                        break;
                    }
                    else
                    {
                        Eqs++;
                    }
                }
                else if (encoded[i] == 'A')
                {
                    As++;
                }
            }
            return encoded.Substring(0, i + 1) + "A_" + As + "=_" + Eqs;
        }
        private Dictionary<string, string> DecodePositionData(string encoded)
        {
            Encoding encoding = Encoding.UTF8;
            {
                Match matchCompression = Regex.Match(encoded, "A_(\\d+)\\=_(\\d+)");
                int As = int.Parse(matchCompression.Groups[1].Value);
                int Eqs = int.Parse(matchCompression.Groups[2].Value);
                int segmentLength = matchCompression.Groups[0].Value.Length;
                encoded = encoded.Substring(0, encoded.Length - segmentLength);
                for (int i = 0; i < As; i++)
                {
                    encoded += 'A';
                }
                for (int i = 0; i < Eqs; i++)
                {
                    encoded += '=';
                }
            }
            byte[] data = Convert.FromBase64String(encoded);
            Dictionary<string, string> dictionary = new Dictionary<string, string>();

            MemoryStream memoryStream = new MemoryStream(data);
            GZipStream compressionStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            BinaryReader binaryReader = new BinaryReader(compressionStream, Encoding.UTF8);
            string receiveString()
            {
                int length = binaryReader.ReadInt32();
                byte[] bytes = new byte[length];
                _ = binaryReader.Read(bytes, 0, length);
                return encoding.GetString(bytes);
            }
            PositionDataHeader receiveHeader()
            {
                return (PositionDataHeader)binaryReader.ReadByte();
            }
            int version = binaryReader.ReadInt32();
            if (version != positionDataVersion) throw new IOException("incompatible version");
            while (receiveHeader() == PositionDataHeader.KEY)
            {
                string key = SimpleDecompress(receiveString());
                PositionDataHeader type = receiveHeader();
                string value = "";
                if (type == PositionDataHeader.MULTI)
                {
                    value = "m";
                }
                else
                {
                    value = "s";
                }
                value += SimpleDecompress(receiveString());
                dictionary.Add(key, value);
            }
            return dictionary;
        }

        public SaveEditTask ShareLocation()
        {
            var run = new Action(() =>
            {
                try
                {
                    string content = saveFile.content;
                    string[] lines = content.Split('\n');

                    UnitSearchResult playerUnit = UnitTools.FindUnitWithType(lines, "player");
                    UnitChildren[] positions = {
                        UnitTools.SearchChildrenWithId(lines, "my_truck_placement", playerUnit),
                        UnitTools.SearchChildrenWithId(lines, "my_trailer_placement", playerUnit),
                        UnitTools.SearchChildrenWithId(lines, "truck_placement", playerUnit),
                        UnitTools.SearchChildrenWithId(lines, "trailer_placement", playerUnit),
                        UnitTools.SearchChildrenWithId(lines, "slave_trailer_placements", playerUnit),
                    };
                    Dictionary<string, string> dictionary = new Dictionary<string, string>();
                    foreach (UnitChildren children in positions)
                    {
                        if (children.array != null)
                        {
                            string str = "m";
                            foreach (string childLine in children.array)
                            {
                                str += childLine + "\n";
                            }
                            dictionary[children.name] = str.Trim();
                        }
                        else
                        {
                            dictionary[children.name] = "s" + children.header;
                        }
                    }

                    string encodedData = EncodePositionData(dictionary);
                    Clipboard.SetText(encodedData);
                    MessageBox.Show("세이브 차량의 위치를 공유할 수 있도록 클립보드에 복사했습니다.", "완료");
                }
                catch (Exception e)
                {
                    if (e.Message == "incompatible version")
                    {
                        MessageBox.Show("다른 버전의 툴로 만들어진 데이터입니다.", "오류");
                    }
                    else
                    {
                        MessageBox.Show("오류가 발생했습니다.", "오류");
                    }
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask
            {
                name = "플레이어 위치 내보내기",
                run = run,
                description = "플레이어의 트럭과, 연결된 모든 트레일러의 위치를 공유 가능하게 텍스트로 복사합니다."
            };
        }
        public SaveEditTask InjectLocation()
        {
            var run = new Action(() =>
            {
                try
                {
                    var content = saveFile.content;
                    var lines = content.Split('\n');
                    var sb = new StringBuilder();

                    var decoded = DecodePositionData(Clipboard.GetText());
                    string my_truck_placement = decoded["my_truck_placement"].Substring(1);
                    string truck_placement = decoded["truck_placement"].Substring(1);
                    string my_trailer_placement = decoded["my_trailer_placement"].Substring(1);
                    string trailer_placement = decoded["trailer_placement"].Substring(1);
                    string slave_trailer_placements = decoded["slave_trailer_placements"];

                    foreach (string line in lines)
                    {
                        string str = line;
                        if (line.StartsWith(" my_truck_placement:"))
                        {
                            str = " my_truck_placement: " + my_truck_placement;
                        }
                        if (line.StartsWith(" my_trailer_placement:"))
                        {
                            str = " my_trailer_placement: " + my_trailer_placement;
                        }
                        if (line.StartsWith(" truck_placement:"))
                        {
                            str = " truck_placement: " + truck_placement;
                        }
                        if (line.StartsWith(" trailer_placement:"))
                        {
                            str = " trailer_placement: " + trailer_placement;
                        }
                        if (line.StartsWith(" slave_trailer_placements:"))
                        {
                            if (slave_trailer_placements.StartsWith("m"))
                            {
                                str = string.Join("\n", from a in slave_trailer_placements.Substring(1).Split('\n') select " slave_trailer_placements[]: " + a);
                            }
                            else
                            {
                                str = " slave_trailer_placements: " + slave_trailer_placements.Substring(1);
                            }
                        }
                        else if (line.StartsWith(" slave_trailer_placements"))
                        {
                            str = null;
                        }
                        if (str != null)
                            _ = sb.Append(str + "\n");
                    }
                    saveFile.Save(sb.ToString());
                    MessageBox.Show("세이브 속 차량의 위치를 입력한 데이터로 변경했습니다.", "완료");
                }
                catch (Exception e)
                {
                    if (e.Message == "incompatible version")
                    {
                        MessageBox.Show("다른 버전의 툴로 만들어진 데이터입니다.", "오류");
                    }
                    else
                    {
                        MessageBox.Show("오류가 발생했습니다.", "오류");
                    }
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask
            {
                name = "플레이어 위치 적용하기",
                run = run,
                description = "공유된 플레이어의 위치를 클립보드에서 가져와 세이브 파일에 주입합니다."
            };
        }
    }
}
