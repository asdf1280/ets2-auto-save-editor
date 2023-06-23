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

namespace ETS2SaveAutoEditor {
    struct Paintjob {
        public string mask_r_color;
        public string mask_g_color;
        public string mask_b_color;
        public string flake_color;
        public string flip_color;
        public string base_color;
        public string data_path;
    }
    public class SaveeditTasks {
        public ProfileSave saveFile;
        public event EventHandler<string> StateChanged;

        public SaveEditTask MoneySet() {
            var run = new Action(() => {
                try {
                    var saveGame = new SiiSaveGame(saveFile.content);
                    var bank = saveGame.FindUnitWithType("bank");
                    var currentBank = saveGame.GetUnitItem(bank, "money_account").value;

                    var specifiedCash = NumberInputBox.Show("자본 설정", "자본을 몇으로 설정할까요?\n현재 자본: " + currentBank + "\n수가 너무 크면 세이브 파일이 손상될 수 있습니다.");

                    if (specifiedCash == -1) {
                        return;
                    }

                    saveGame.SetUnitItem(bank, new UnitItem { name = "money_account", value = specifiedCash.ToString() });
                    saveFile.Save(saveGame.MergeResult());
                    MessageBox.Show("완료되었습니다!", "완료");
                } catch (Exception e) {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "자본 설정",
                run = run,
                description = "자본을 원하는 값으로 설정합니다."
            };
        }
        public SaveEditTask ExpSet() {
            var run = new Action(() => {
                try {
                    var saveGame = new SiiSaveGame(saveFile.content);
                    var economy = saveGame.FindUnitWithType("economy");
                    var currentExp = saveGame.GetUnitItem(economy, "experience_points").value;

                    var specifiedExp = NumberInputBox.Show("경험치 설정", "경험치를 몇으로 설정할까요?\n입력한 값으로 누적 경험치가 설정됩니다. 더하는 것이 아닙니다.\n현재 소유한 경험치는 " + currentExp + "xp입니다.\n수가 너무 크면 세이브 파일이 손상될 수 있습니다.");

                    if (specifiedExp == -1) {
                        return;
                    }

                    saveGame.SetUnitItem(economy, new UnitItem { name = "experience_points", value = specifiedExp.ToString() });
                    saveFile.Save(saveGame.MergeResult());
                    MessageBox.Show("완료되었습니다!", "완료");
                } catch (Exception e) {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "경험치 설정",
                run = run,
                description = "경험치를 원하는 값으로 설정합니다."
            };
        }
        public SaveEditTask UnlockScreens() {
            var run = new Action(() => {
                try {
                    var saveGame = new SiiSaveGame(saveFile.content);
                    var economy = saveGame.FindUnitWithType("economy");

                    var msgBoxRes = MessageBox.Show("모든 메뉴를 잠금 해제합니다. '트레일러 조정' 등 게임 상황에 따라 비활성화 상태여야 하는 메뉴도 강제로 열릴 수 있습니다. 계속 진행하시겠습니까?", "기능 잠금 해제", MessageBoxButton.OKCancel);
                    if (msgBoxRes == MessageBoxResult.Cancel) {
                        return;
                    }

                    saveGame.SetUnitItem(economy, new UnitItem { name = "screen_access_list", value = "0" });
                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show("완료되었습니다!", "완료");
                } catch (Exception e) {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "메뉴 잠금 해제",
                run = run,
                description = "새로 만든 프로파일에서 아직 잠금 해제되지 않은 모든 기능을 강제로 활성화합니다."
            };
        }
        public SaveEditTask TruckEngineSet() {
            var run = new Action(() => {
                try {
                    var content = saveFile.content;
                    var pattern0 = @"\beconomy : [\w\.]+ {";
                    var pattern1 = @"\bplayer: ([\w\.]+)\b";
                    var matchIndex = Regex.Match(content, pattern0).Index;
                    var substr = content.Substring(matchIndex);
                    string resultLine = null;
                    foreach (var str in substr.Split('\n')) {
                        if (Regex.IsMatch(str, pattern1)) {
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
                    foreach (var str in substr.Split('\n')) {
                        if (Regex.IsMatch(str, pattern1)) {
                            resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                            break;
                        }
                        if (str.Trim() == "}") break; // End of the class
                    }

                    if (resultLine == null) {
                        MessageBox.Show("손상된 세이브 파일입니다.", "오류");
                        return;
                    } else if (resultLine == "null") {
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
                        if (res == -1) {
                            return;
                        }
                        enginePath = enginePaths[res];
                    }

                    pattern0 = @"\bvehicle : " + resultLine + " {";
                    pattern1 = @"\baccessories\[\d*\]: ([\w\.]+)\b";
                    matchIndex = Regex.Match(content, pattern0).Index;
                    substr = content.Substring(matchIndex);
                    resultLine = null;
                    foreach (var line in substr.Split('\n')) {
                        if (Regex.IsMatch(line, pattern1)) {
                            var id = Regex.Match(line, pattern1).Groups[1].Value;
                            var p50 = @"\bvehicle_(addon_|sound_|wheel_|drv_plate_|paint_job_)?accessory : " + id + " {";
                            var matchIndex0 = Regex.Match(substr, p50).Index + matchIndex;
                            var substr0 = content.Substring(matchIndex0);
                            var index = 0;
                            foreach (var line0 in substr0.Split('\n')) {
                                if (Regex.IsMatch(line0, @"\bdata_path:\s""\/def\/vehicle\/truck\/[^/]+?\/engine\/")) {
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
                } catch (Exception e) {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "트럭 엔진 지정",
                run = run,
                description = "트럭의 엔진을 설정 가능한 몇 가지 엔진으로 변경합니다."
            };
        }
        public SaveEditTask MapReset() {
            var run = new Action(() => {
                try {
                    var content = saveFile.content;
                    var sb = new StringBuilder();
                    foreach (var line in content.Split('\n')) {
                        var str = line;
                        if (line.Contains("discovered_items:"))
                            str = " discovered_items: 0";
                        else if (line.Contains("discovered_items")) continue;
                        sb.AppendLine(str);
                    }
                    saveFile.Save(sb.ToString());
                    MessageBox.Show("지도를 초기화했습니다.", "완료");
                } catch (Exception e) {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "맵 초기화",
                run = run,
                description = "지도의 탐색한 도로를 초기화합니다."
            };
        }
        public SaveEditTask Refuel() {
            var run = new Action(() => {
                try {
                    var content = saveFile.content;
                    var sb = new StringBuilder();

                    var fuelPresetNames = new string[] { "연료통의 1000배", "연료통의 100배", "연료통의 10배", "연료통의 5배", "100%", "50%", "10%", "5%", "0%(...)" };
                    var fullPresetValues = new string[] {
                        "1000", "100", "10", "5", "1", "0.5", "0.1", "0.05", "0"
                    };
                    var fuelId = "";
                    {
                        var res = ListInputBox.Show("연료 수준 선택", "할당된 트럭에 적용할 연료 수준을 선택하십시오.", fuelPresetNames);
                        if (res == -1) {
                            return;
                        }
                        fuelId = fullPresetValues[res];
                    }

                    foreach (var line in content.Split('\n')) {
                        var str = line;
                        if (line.Contains("fuel_relative:"))
                            str = " fuel_relative: " + fuelId;
                        sb.Append(str + "\n");
                    }
                    saveFile.Save(sb.ToString());
                    MessageBox.Show("모든 트럭의 연료를 지정한 값으로 변경했습니다.", "완료");
                } catch (Exception e) {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "연료 채우기",
                run = run,
                description = "세이브 내의 모든 트럭의 연료를 설정합니다."
            };
        }
        public SaveEditTask FixEverything() {
            var run = new Action(() => {
                try {
                    var content = saveFile.content;
                    var sb = new StringBuilder();

                    foreach (var line in content.Split('\n')) {
                        var str = line;
                        if (Regex.IsMatch(str, @"([a-z_]*_wear(?:\[\d*\])?:) (.*)\b")) {
                            str = Regex.Replace(str, @"([a-z_]*_wear(?:\[\d*\])?:) (.*)\b", "$1 0");
                        }

                        sb.Append(str + "\n");
                    }
                    saveFile.Save(sb.ToString());
                    MessageBox.Show("모든 트럭/트레일러를 수리했습니다.", "완료");
                } catch (Exception e) {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "모든 트럭/트레일러 수리",
                run = run,
                description = "세이브 내의 모든 트럭/트레일러를 수리합니다."
            };
        }
        public SaveEditTask SharePaint() {
            var run = new Action(() => {
                try {
                    var content = saveFile.content;
                    var pattern0 = @"\beconomy : [\w\.]+ {";
                    var pattern1 = @"\bplayer: ([\w\.]+)\b";
                    var matchIndex = Regex.Match(content, pattern0).Index;
                    var substr = content.Substring(matchIndex);
                    string resultLine = null;
                    foreach (var str in substr.Split('\n')) {
                        if (Regex.IsMatch(str, pattern1)) {
                            resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                            break;
                        }
                        if (str.Trim() == "}") break;
                    }

                    if (resultLine == null) {
                        MessageBox.Show("손상된 세이브 파일입니다.", "오류");
                        return;
                    }

                    var operationNames = new string[] { "트럭, 불러오기", "트럭, 내보내기", "트레일러, 불러오기", "트레일러, 내보내기" };
                    var truck = false;
                    var import = false;
                    {
                        var res = ListInputBox.Show("작업 선택하기", "무엇의 페인트를 불러올까요? 내보낼까요?", operationNames);
                        if (res == -1) {
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
                    foreach (var str in substr.Split('\n')) {
                        if (Regex.IsMatch(str, pattern1)) {
                            resultLine = Regex.Match(str, pattern1).Groups[1].Value;
                            break;
                        }
                        if (str.Trim() == "}") break; // End of the class
                    }
                    if (resultLine == "null") {
                        if (truck)
                            MessageBox.Show("할당된 트럭이 없습니다.", "오류");
                        else
                            MessageBox.Show("할당된 트레일러가 없습니다.", "오류");
                        return;
                    }

                    string path;

                    var filter = (truck ? "트럭 페인트 파일 (*.paint0)|*.paint0|모든 파일 (*.*)|*.*" : "트레일러 페인트 파일 (*.paint1)|*.paint1|모든 파일 (*.*)|*.*");
                    if (import) { // Import
                        OpenFileDialog dialog = new OpenFileDialog {
                            Title = "페인트 파일 선택",
                            Filter = filter
                        };
                        if (dialog.ShowDialog() != true) return;
                        path = dialog.FileName;
                    } else // Export
                      {
                        SaveFileDialog dialog = new SaveFileDialog {
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

                    if (import) {
                        var str = File.ReadAllText(path, Encoding.UTF8);
                        var strs = str.Split(';');
                        if (strs.Length != 8) {
                            MessageBox.Show("손상된 페인트 파일입니다.", "오류");
                            return;
                        }
                        var paintjob = new Paintjob {
                            mask_r_color = strs[0],
                            mask_g_color = strs[1],
                            mask_b_color = strs[2],
                            flake_color = strs[3],
                            flip_color = strs[4],
                            base_color = strs[5],
                            data_path = strs[6]
                        };

                        foreach (var line in substr.Split('\n')) {
                            if (Regex.IsMatch(line, pattern1)) {
                                var id = Regex.Match(line, pattern1).Groups[1].Value;
                                var p50 = @"\bvehicle_paint_job_accessory : " + id + " {";
                                if (!Regex.IsMatch(substr, p50)) continue;
                                var matchIndex0 = Regex.Match(substr, p50).Index + matchIndex; // TODO you must escape p50 id can contain .
                                var substr0 = content.Substring(matchIndex0);
                                var index = 0;
                                var sb = new StringBuilder();
                                sb.Append(content.Substring(0, matchIndex0));
                                foreach (var line0 in substr0.Split('\n')) {
                                    var str0 = line0;
                                    if (Regex.IsMatch(line0, @"\bdata_path:\s"".*?""")) {
                                        str0 = " data_path: " + paintjob.data_path + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_r_color: \(.*?\)")) {
                                        str0 = " mask_r_color: " + paintjob.mask_r_color + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_g_color: \(.*?\)")) {
                                        str0 = " mask_g_color: " + paintjob.mask_g_color + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_b_color: \(.*?\)")) {
                                        str0 = " mask_b_color: " + paintjob.mask_b_color + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bflake_color: \(.*?\)")) {
                                        str0 = " flake_color: " + paintjob.flake_color + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bflip_color: \(.*?\)")) {
                                        str0 = " flip_color: " + paintjob.flip_color + "";
                                    }
                                    if (Regex.IsMatch(line0, @"\bbase_color: \(.*?\)")) {
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
                    } else {
                        var paintjob = new Paintjob();

                        foreach (var line in substr.Split('\n')) {
                            if (Regex.IsMatch(line, pattern1)) {
                                var id = Regex.Match(line, pattern1).Groups[1].Value;
                                var p50 = @"\bvehicle_paint_job_accessory : " + id + " {";
                                if (!Regex.IsMatch(substr, p50)) continue;
                                var matchIndex0 = Regex.Match(substr, p50).Index + matchIndex; // TODO you must escape p50 id can contain .
                                var substr0 = content.Substring(matchIndex0);
                                var index = 0;
                                foreach (var line0 in substr0.Split('\n')) {
                                    if (Regex.IsMatch(line0, @"\bdata_path:\s("".*?"")")) {
                                        paintjob.data_path = Regex.Match(line0, @"\bdata_path:\s("".*?"")").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_r_color: (\(.*?\))")) {
                                        paintjob.mask_r_color = Regex.Match(line0, @"\bmask_r_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_g_color: (\(.*?\))")) {
                                        paintjob.mask_g_color = Regex.Match(line0, @"\bmask_g_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bmask_b_color: (\(.*?\))")) {
                                        paintjob.mask_b_color = Regex.Match(line0, @"\bmask_b_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bflake_color: (\(.*?\))")) {
                                        paintjob.flake_color = Regex.Match(line0, @"\bflake_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bflip_color: (\(.*?\))")) {
                                        paintjob.flip_color = Regex.Match(line0, @"\bflip_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (Regex.IsMatch(line0, @"\bbase_color: (\(.*?\))")) {
                                        paintjob.base_color = Regex.Match(line0, @"\bbase_color: (\(.*?\))").Groups[1].Value;
                                    }
                                    if (line0.Trim() == "}") break;
                                    index += line0.Length + 1;
                                }
                            }
                            if (line.Trim() == "}") break; // End of the class
                        }
                        try {
                            var data = paintjob.mask_r_color + ";" + paintjob.mask_g_color + ";" + paintjob.mask_b_color + ";" + paintjob.flake_color + ";" + paintjob.flip_color + ";" + paintjob.base_color + ";" + paintjob.data_path + ";";
                            File.WriteAllText(path, data, Encoding.UTF8);
                        } catch (Exception) {
                            MessageBox.Show("페인트를 내보낼 수 없습니다.", "오류");
                            throw;
                        }
                        MessageBox.Show("페인트를 내보냈습니다!", "완료");
                    }
                } catch (Exception e) {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "페인트 내보내기/불러오기",
                run = run,
                description = "트럭/트레일러의 페인트를 내보내거나 불러옵니다."
            };
        }

        private enum PositionDataHeader : byte {
            KEY,
            END,
            SINGLE,
            MULTI
        }
        private readonly int positionDataVersion = 2;

        private string EncodePositionData(List<float[]> data) {
            MemoryStream memoryStream = new MemoryStream();
            //GZipStream compressionStream = new GZipStream(memoryStream, CompressionLevel.Optimal);
            BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.ASCII);
            void sendHeader(PositionDataHeader h) {
                binaryWriter.Write((byte)h);
            }
            void sendPlacement(float[] p) {
                for (int t = 0; t < 7; t++) {
                    binaryWriter.Write(p[t]);
                }
            }
            binaryWriter.Write(positionDataVersion);
            foreach (float[] p in data) {
                sendHeader(PositionDataHeader.KEY);
                sendPlacement(p);
            }
            sendHeader(PositionDataHeader.END);


            binaryWriter.Flush();
            binaryWriter.Close();
            string encoded = Convert.ToBase64String(memoryStream.ToArray());
            int Eqs = 0;
            int i;
            for (i = encoded.Length - 1; i >= 0; i--) {
                if (encoded[i] == '=') {
                    Eqs++;
                } else break;
            }
            return encoded.Substring(0, i + 1) + Eqs.ToString("X");
        }
        private List<float[]> DecodePositionData(string encoded) {
            {
                Match matchCompression = Regex.Match(encoded, "(.)$");
                int Eqs = Convert.ToInt32(matchCompression.Groups[1].Value, 16);
                int segmentLength = matchCompression.Groups[0].Value.Length;
                MessageBox.Show(matchCompression.Groups[1].Value);
                encoded = encoded.Substring(0, encoded.Length - segmentLength);
                for (int i = 0; i < Eqs; i++) {
                    encoded += '=';
                }
            }
            byte[] data = Convert.FromBase64String(encoded);
            List<float[]> list = new List<float[]>();

            MemoryStream memoryStream = new MemoryStream(data);
            //GZipStream compressionStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);
            PositionDataHeader receiveHeader() {
                return (PositionDataHeader)binaryReader.ReadByte();
            }
            float[] receivePlacement() {
                float[] result = new float[7];
                for (int i = 0; i < 7; i++) {
                    result[i] = binaryReader.ReadSingle();
                }
                return result;
            }
            int version = binaryReader.ReadInt32();
            if (version != positionDataVersion) throw new IOException("incompatible version");
            while (receiveHeader() == PositionDataHeader.KEY) {
                list.Add(receivePlacement());
            }
            return list;
        }

        public float ParseScsFloat(string data) {
            if (data.StartsWith("&")) {
                byte[] bytes = new byte[4];
                for (int i = 0; i < 4; i++) {
                    bytes[i] = byte.Parse(data.Substring(i * 2 + 1, 2), System.Globalization.NumberStyles.HexNumber);
                }
                return BitConverter.ToSingle(bytes, 0);
            } else {
                return float.Parse(data);
            }
        }

        public string EncodeScsFloat(float value) {
            byte[] bytes = BitConverter.GetBytes(value);
            string hexString = BitConverter.ToString(bytes).Replace("-", "").ToLower();
            return "&" + hexString;
        }

        public float[] DecodeSCSPosition(string placement) {
            var a = placement.Split(new string[] { "(", ")", ",", ";" }, StringSplitOptions.RemoveEmptyEntries);
            var q = from v in a select v.Trim() into b where b.Length > 0 select ParseScsFloat(b);
            return q.ToArray();
        }

        public string EncodeSCSPosition(float[] data) {
            var data0 = (from d in data select EncodeScsFloat(d)).ToArray();
            return $"({data0[0]}, {data0[1]}, {data0[2]}) ({data0[3]}; {data0[4]}, {data0[5]}, {data0[6]})";
        }

        public SaveEditTask ShareLocation() {
            var run = new Action(() => {
                try {
                    var saveGame = new SiiSaveGame(saveFile.content);
                    var player = saveGame.FindUnitWithType("player");

                    List<float[]> placements = new List<float[]>();

                    string truckPlacement = saveGame.GetUnitItem(player, "truck_placement").value;
                    placements.Add(DecodeSCSPosition(truckPlacement));

                    string trailerPlacement = saveGame.GetUnitItem(player, "trailer_placement").value;
                    placements.Add(DecodeSCSPosition(trailerPlacement));

                    var slaveTrailers = saveGame.GetUnitItem(player, "slave_trailer_placements");
                    if (slaveTrailers.array != null) {
                        foreach (var slave in slaveTrailers.array) {
                            placements.Add(DecodeSCSPosition(slave));
                        }
                    }

                    string encodedData = EncodePositionData(placements);
                    Clipboard.SetText(encodedData);
                    MessageBox.Show("세이브 차량의 위치를 공유할 수 있도록 클립보드에 복사했습니다.", "완료");
                } catch (Exception e) {
                    if (e.Message == "incompatible version") {
                        MessageBox.Show("다른 버전의 툴로 만들어진 데이터입니다.", "오류");
                    } else {
                        MessageBox.Show("오류가 발생했습니다.", "오류");
                    }
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "플레이어 위치 내보내기",
                run = run,
                description = "플레이어의 트럭과, 연결된 모든 트레일러의 위치를 공유 가능하게 텍스트로 복사합니다."
            };
        }
        public SaveEditTask InjectLocation() {
            var run = new Action(() => {
                try {
                    var saveGame = new SiiSaveGame(saveFile.content);
                    var player = saveGame.FindUnitWithType("player");

                    var decoded = (from a in DecodePositionData(Clipboard.GetText().Trim()) select EncodeSCSPosition(a)).ToArray();
                    if (decoded.Count() >= 1) {
                        saveGame.SetUnitItem(player, new UnitItem { name = "truck_placement", value = decoded[0] });
                    }
                    if (decoded.Count() >= 2) {
                        saveGame.SetUnitItem(player, new UnitItem { name = "trailer_placement", value = decoded[1] });
                    }
                    if (decoded.Count() > 2) {
                        saveGame.SetUnitItem(player, new UnitItem { name = "slave_trailer_placements", array = decoded.Skip(2).ToArray() });
                    }

                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show("세이브 속 차량의 위치를 입력한 데이터로 변경했습니다.", "완료");
                } catch (Exception e) {
                    if (e.Message == "incompatible version") {
                        MessageBox.Show("다른 버전의 툴로 만들어진 데이터입니다.", "오류");
                    } else {
                        MessageBox.Show("오류가 발생했습니다.", "오류");
                    }
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "플레이어 위치 적용하기",
                run = run,
                description = "공유된 플레이어의 위치를 클립보드에서 가져와 세이브 파일에 주입합니다."
            };
        }

        public SaveEditTask StealCompanyTrailer() {
            var run = new Action(() => {
                try {
                    var saveGame = new SiiSaveGame(saveFile.content);
                    var economy = UnitTypeSelector.Of("economy");
                    var player = UnitTypeSelector.Of("player");

                    var currentTrailerId = "";
                    var currentJobId = "";
                    { // 현재 작업이나 트레일러가 없으면 오류 발생
                        currentTrailerId = saveGame.GetUnitItem(player, "assigned_trailer").value;
                        if (currentTrailerId == "null") {
                            MessageBox.Show("활성화된 트레일러가 없습니다.", "오류");
                            return;
                        }

                        currentJobId = saveGame.GetUnitItem(player, "current_job").value;
                        if (currentJobId == "null") {
                            MessageBox.Show("활성화된 작업이 없습니다.", "오류");
                            return;
                        }
                    }

                    StateChanged(null, "작업 정보 지우는 중...");

                    // Set current_job to null
                    saveGame.SetUnitItem(player, new UnitItem { name = "current_job", value = "null" });

                    // Set my_trailer to this
                    saveGame.SetUnitItem(player, new UnitItem { name = "my_trailer", value = currentTrailerId });

                    // Current job unit
                    var job = UnitIdSelector.Of(currentJobId);
                    { // Special transport
                        var special = saveGame.GetUnitItem(job, "special").value;
                        if (special != "null") { // Special transport - we need to delete some more units
                            saveGame.DeleteUnit(UnitIdSelector.Of(special));

                            var specialSave = UnitIdSelector.Of(saveGame.GetUnitItem(economy, "stored_special_job").value);
                            var l = new List<string>();

                            var i1 = saveGame.GetUnitItem(specialSave, "trajectory_orders");
                            if (i1.array != null)
                                l.AddRange(i1.array);
                            var i2 = saveGame.GetUnitItem(specialSave, "active_blocks_rules");
                            if (i2.array != null)
                                l.AddRange(i2.array);

                            l.ForEach((v) => {
                                saveGame.DeleteUnit(UnitIdSelector.Of(v));
                            });

                            saveGame.DeleteUnit(specialSave);
                            saveGame.SetUnitItem(economy, new UnitItem { name = "stored_special_job", value = "null" });
                        }
                    }

                    // Check if company truck exists and remove it
                    {
                        var v1 = saveGame.GetUnitItem(job, "company_truck");
                        if (v1.value != "null") {
                            var truck = UnitIdSelector.Of(v1.value);
                            var l = new List<string>();

                            l.AddRange(saveGame.GetUnitItem(truck, "accessories").array);

                            l.ForEach((v) => {
                                saveGame.DeleteUnit(UnitIdSelector.Of(v));
                            });

                            saveGame.DeleteUnit(truck);
                        }

                        saveGame.SetUnitItem(player, new UnitItem { name = "assigned_truck", value = saveGame.GetUnitItem(player, "my_truck").value });
                    }

                    // Delete the job unit
                    saveGame.DeleteUnit(job);

                    // Get trailers I own now
                    var trailers = saveGame.GetUnitItem(player, "trailers").array;
                    if (trailers.Contains(currentTrailerId)) { // Owned trailer - cancel job and return
                        saveFile.Save(saveGame.ToString());
                        MessageBox.Show("작업에 사용된 트레일러를 이미 보유하고 있습니다. 화물이 남은 상태로 작업을 취소했습니다.", "완료");
                        return;
                    }

                    // Not owned trailers - Add to owned trailers and selected garage
                    var garageId = "";
                    {
                        var garageNamesFound = (from item in saveGame.GetUnitItem(economy, "garages").array select item.Split(new string[] { "garage." }, StringSplitOptions.None)[1]).ToList();
                        garageNamesFound.Sort();

                        if (garageNamesFound.Count == 0) {
                            MessageBox.Show("보유한 차고가 있어야 합니다.", "오류");
                            return;
                        }

                        var res = ListInputBox.Show("차고 선택", "훔친 트레일러를 어디에 놓겠습니까? 게임에서 구입한 차고만 선택해야 합니다. 안 그러면 무슨 일이 생길지 모릅니다.", garageNamesFound.ToArray());
                        if (res == -1) {
                            return;
                        }
                        garageId = "garage." + garageNamesFound.ToArray()[res];
                    }

                    // Add trailer to player trailer list
                    {
                        var t = trailers.ToList();
                        t.Add(currentTrailerId);
                        saveGame.SetUnitItem(player, new UnitItem { name = "trailers", array = t.ToArray() });
                    }

                    // Add trailer to garage trailer list
                    {
                        var garage = UnitIdSelector.Of(garageId);

                        var tValues = saveGame.GetUnitItem(garage, "trailers");
                        var ts = new string[] { };
                        if (tValues.array != null) {
                            ts = tValues.array;
                        }
                        var t = ts.ToList();
                        t.Add(currentTrailerId);
                        saveGame.SetUnitItem(garage, new UnitItem { name = "trailers", array = t.ToArray() });
                    }

                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show("이 트레일러는 이제 제 겁니다. 제 마음대로 탈 수 있는 겁니다.", "완료");
                    return;
                } catch (Exception e) {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "작업 트레일러 훔치기",
                run = run,
                description = "이 트레일러는 이제 제 겁니다. 제 마음대로 탈 수 있는 겁니다."
            };
        }

        public SaveEditTask ChangeCargoMass() {
            var run = new Action(() => {
                try {
                    var saveGame = new SiiSaveGame(saveFile.content);
                    var player = UnitTypeSelector.Of("player");

                    var assignedTrailerId = saveGame.GetUnitItem(player, "assigned_trailer").value;
                    if (assignedTrailerId == "null") {
                        MessageBox.Show("활성화된 트레일러가 없습니다.", "오류");
                        return;
                    }

                    var specifiedMass = NumberInputBox.Show("무게 설정", "화물 무게를 몇 kg으로 설정하시겠습니까? 지나치게 높은 값으로 설정하면 물리 엔진 오류가 발생합니다. 화물을 없애는 경우 0으로 설정하면 됩니다.");

                    if (specifiedMass == -1) {
                        return;
                    }

                    var trailer = UnitIdSelector.Of(assignedTrailerId);
                    saveGame.SetUnitItem(trailer, new UnitItem { name = "cargo_mass", value = $"{specifiedMass}" });

                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show("트레일러 화물 무게를 변경했습니다!", "완료");
                    return;
                } catch (Exception e) {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "트레일러 화물 무게 설정",
                run = run,
                description = "트레일러의 화물 무게 값을 임의로 변경할 수 있습니다."
            };
        }
    }
}
