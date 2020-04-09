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
                    var bank = EditUtils.ObjectValueInClass(saveFile.content, "bank", "economy");
                    var cash = EditUtils.ObjectValueIn(saveFile.content, "money_account", bank);

                    var specifiedCash = NumberInputBox.Show("돈 지정하기", "자본을 몇으로 설정할까요?\n현재 자본: " + cash + "\n경고: 너무 높은 값으로 설정하면 게임 로딩 시 오류가 발생할 수 있습니다. 이 경우 개발자는 책임지지 않습니다.");

                    if (specifiedCash == -1)
                    {
                        return;
                    }

                    var bankIndex = EditUtils.IndexIn(saveFile.content, "money_account", bank);
                    var sb = new StringBuilder();
                    sb.Append(saveFile.content.Substring(0, bankIndex));
                    sb.Append(specifiedCash.ToString());
                    sb.Append(saveFile.content.Substring(bankIndex + cash.Length));
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
                    var exp = EditUtils.ObjectValueInClass(saveFile.content, "experience_points", "economy");
                    var expIndex = EditUtils.IndexInClass(saveFile.content, "experience_points", "economy");

                    var specifiedExp = NumberInputBox.Show("경험치 지정하기", "경험치를 몇으로 설정할까요?\n경험치의 절대적인 수치를 입력하세요. 더하는 것이 아니라 완전히 해당 경험치로 설정됩니다.\n현재 소유한 경험치는 " + exp + "입니다.\n경고: 너무 높은 값으로 설정하면 게임 로딩 시 오류가 발생할 수 있습니다. 이 경우 개발자는 책임지지 않습니다.");

                    if (specifiedExp == -1)
                    {
                        return;
                    }

                    var sb = new StringBuilder();
                    sb.Append(saveFile.content.Substring(0, expIndex));
                    sb.Append(specifiedExp.ToString());
                    sb.Append(saveFile.content.Substring(expIndex + exp.Length));
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
                name = "경험치/레벨 지정",
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
                    var listCount = int.Parse(EditUtils.ObjectValueInClass(saveFile.content, "screen_access_list", "economy"));
                    var listCountStr = EditUtils.ObjectValueInClass(saveFile.content, "screen_access_list", "economy");
                    var listCountIndex = EditUtils.IndexInClass(saveFile.content, "screen_access_list", "economy");

                    var msgBoxRes = MessageBox.Show("[69cheat]\n모든 메뉴를 해금합니다. 트레일러 조정 등 일부 메뉴는 원래 사용이 불가함에도 해금될 수 있으니 주의하세요!\n이 작업은 시간이 조금 걸릴 수 있습니다.", "69cheat", MessageBoxButton.OKCancel);
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
                    var cc = saveFile.content;
                    var p00 = @"\beconomy : [\w\.]+ {";
                    var p01 = @"\bplayer: ([\w\.]+)\b";
                    var ms = Regex.Match(cc, p00).Index;
                    var tmp = cc.Substring(ms);
                    string pl = null;
                    foreach (var str in tmp.Split('\n'))
                    {
                        if (Regex.IsMatch(str, p01))
                        {
                            pl = Regex.Match(str, p01).Groups[1].Value;
                            break;
                        }
                        if (str.Trim() == "}") break;
                    }

                    p00 = @"\bplayer : " + pl + " {";
                    p01 = @"\bassigned_truck: ([\w\.]+)\b";
                    ms = Regex.Match(cc, p00).Index;
                    tmp = cc.Substring(ms);
                    pl = null;
                    foreach (var str in tmp.Split('\n'))
                    {
                        if (Regex.IsMatch(str, p01))
                        {
                            pl = Regex.Match(str, p01).Groups[1].Value;
                            break;
                        }
                        if (str.Trim() == "}") break;
                    }

                    if (pl == null)
                    {
                        MessageBox.Show("손상된 세이브 파일입니다.", "오류");
                        return;
                    }
                    else if (pl == "null")
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
                        var res = ListInputBox.Show("엔진 선택하기", "현재 할당된 트럭에 적용할 엔진을 선택하세요.\n참고로 스카니아와 볼보는 구형의 엔진 성능이 신형보다 좋습니다.", engineNames);
                        if(res == -1)
                        {
                            return;
                        }
                        enginePath = enginePaths[res];
                    }

                    p00 = @"\bvehicle : " + pl + " {";
                    p01 = @"\baccessories\[\d*\]: ([\w\.]+)\b";
                    ms = Regex.Match(cc, p00).Index;
                    tmp = cc.Substring(ms);
                    pl = null;
                    foreach (var str in tmp.Split('\n'))
                    {
                        if (Regex.IsMatch(str, p01))
                        {
                            var id = Regex.Match(str, p01).Groups[1].Value;
                            Console.WriteLine(str);
                            var p50 = @"\bvehicle_(addon_|sound_|wheel_|drv_plate_|paint_job_)?accessory : " + id + " {";
                            var ms0 = Regex.Match(tmp, p50).Index + ms;
                            var tmp0 = cc.Substring(ms0);
                            var index = 0;
                            foreach (var str0 in tmp0.Split('\n'))
                            {
                                if (Regex.IsMatch(str0, @"\bdata_path:\s""\/def\/vehicle\/truck\/[^/]+?\/engine\/"))
                                {
                                    Console.WriteLine(ms0 + index);
                                    var sb = new StringBuilder();
                                    sb.Append(cc.Substring(0, ms0 + index));
                                    sb.Append(" data_path: \"" + enginePath + "\"\n");
                                    sb.Append(cc.Substring(ms0 + index + str0.Length + 1));
                                    cc = sb.ToString();
                                }
                                if (str0.Trim() == "}") break;
                                index += str0.Length + 1;
                            }
                        }
                        if (str.Trim() == "}") break;
                    }
                    saveFile.Save(cc);
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
    }
}
