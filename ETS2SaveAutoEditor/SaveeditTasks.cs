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
        public void setSaveFile(ProfileSave file) {
            saveFile = file;
            saveGame = new SiiSaveGame(file.content);
            saveFile.Save(saveGame.ToString());
        }
        private ProfileSave saveFile;
        private SiiSaveGame saveGame;

        public event EventHandler<string> StateChanged;

        public SaveEditTask MoneySet() {
            var run = new Action(() => {
                try {
                    var bank = saveGame.EntityType("bank");
                    var currentBank = bank.Get("money_account").value;

                    var specifiedCash = NumberInputBox.Show("자본 설정", "자본을 몇으로 설정할까요?\n현재 자본: " + currentBank + "\n수가 너무 크면 세이브 파일이 손상될 수 있습니다.");

                    if (specifiedCash == -1) {
                        return;
                    }

                    bank.Set("money_account", specifiedCash.ToString());
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
                    var economy = saveGame.EntityType("economy");
                    var currentExp = economy.Get("experience_points").value;

                    var specifiedExp = NumberInputBox.Show("경험치 설정", "경험치를 몇으로 설정할까요?\n입력한 값으로 누적 경험치가 설정됩니다. 더하는 것이 아닙니다.\n현재 소유한 경험치는 " + currentExp + "xp입니다.\n수가 너무 크면 세이브 파일이 손상될 수 있습니다.");

                    if (specifiedExp == -1) {
                        return;
                    }

                    economy.Set("experience_points", specifiedExp.ToString());
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
                    var economy = saveGame.EntityType("economy");

                    var msgBoxRes = MessageBox.Show("모든 메뉴를 잠금 해제합니다. '트레일러 조정' 등 게임 상황에 따라 비활성화 상태여야 하는 메뉴도 강제로 열릴 수 있습니다. 계속 진행하시겠습니까?", "기능 잠금 해제", MessageBoxButton.OKCancel);
                    if (msgBoxRes == MessageBoxResult.Cancel) {
                        return;
                    }

                    economy.Set("screen_access_list", "0");
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
                    var player = saveGame.EntityType("player");
                    var assignedTruckId = player.Get("assigned_truck").value;

                    if (assignedTruckId == "null") {
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

                    var truck = player.EntityIdAround(assignedTruckId);
                    var accessories = truck.GetAllPointers("accessories");
                    foreach (var accessory in accessories) {
                        if (Regex.IsMatch(accessory.Get("data_path").value, @"""\/def\/vehicle\/truck\/[^/]+?\/engine\/")) {
                            accessory.Set("data_path", $"\"{enginePath}\"");
                        }
                    }

                    saveFile.Save(saveGame.ToString());
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

        private enum PositionDataHeader : byte {
            KEY,
            END
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
                    var player = saveGame.EntityType("player");

                    List<float[]> placements = new List<float[]>();

                    string truckPlacement = player.Get("truck_placement").value;
                    placements.Add(DecodeSCSPosition(truckPlacement));

                    string trailerPlacement = player.Get("trailer_placement").value;
                    placements.Add(DecodeSCSPosition(trailerPlacement));

                    var slaveTrailers = player.Get("slave_trailer_placements");
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
                    var player = saveGame.EntityType("player");

                    var decoded = (from a in DecodePositionData(Clipboard.GetText().Trim()) select EncodeSCSPosition(a)).ToArray();
                    if (decoded.Count() >= 1) {
                        player.Set("truck_placement", decoded[0]);
                    }
                    if (decoded.Count() >= 2) {
                        player.Set("trailer_placement", decoded[1]);
                    }
                    if (decoded.Count() > 2) {
                        player.Set("slave_trailer_placements", decoded.Skip(2).ToArray());
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
                    var economy = saveGame.EntityType("economy");
                    var player = saveGame.EntityType("player", economy.Target.LastFoundStart);

                    var currentTrailerId = "";
                    var currentJobId = "";
                    { // 현재 작업이나 트레일러가 없으면 오류 발생
                        currentTrailerId = player.Get("assigned_trailer").value;
                        if (currentTrailerId == "null") {
                            MessageBox.Show("활성화된 트레일러가 없습니다.", "오류");
                            return;
                        }

                        currentJobId = player.Get("current_job").value;
                        if (currentJobId == "null") {
                            MessageBox.Show("활성화된 작업이 없습니다.", "오류");
                            return;
                        }
                    }

                    // Detach trailers
                    player.Set("my_trailer", "null");
                    player.Set("assigned_trailer", "null");

                    // Current job unit
                    var job = player.EntityIdAround(currentJobId);

                    // Special transport
                    {
                        var special = job.Get("special").value;
                        if (special != "null") { // Special transport - we need to delete some more units
                            job.EntityIdAround(special).Delete();

                            var specialSave = economy.GetPointer("stored_special_job");
                            var l = new List<string>();

                            var i1 = specialSave.Get("trajectory_orders");
                            if (i1.array != null)
                                l.AddRange(i1.array);
                            var i2 = specialSave.Get("active_blocks_rules");
                            if (i2.array != null)
                                l.AddRange(i2.array);

                            l.ForEach((v) => {
                                saveGame.DeleteUnit(UnitIdSelector.Of(v));
                            });

                            specialSave.Delete();
                            economy.Set("stored_special_job", "null");
                        }
                    }

                    // Check if company truck exists and remove it ( Quick Job )
                    {
                        var v1 = job.Get("company_truck");
                        if (v1.value != "null") {
                            var truck = UnitIdSelector.Of(v1.value);
                            var l = new List<string>();

                            l.AddRange(saveGame.GetUnitItem(truck, "accessories").array);

                            l.ForEach((v) => {
                                saveGame.DeleteUnit(UnitIdSelector.Of(v));
                            });

                            saveGame.DeleteUnit(truck);
                        }

                        player.Set("assigned_truck", player.Get("my_truck").value);
                    }

                    // Set current_job to null
                    player.Set("current_job", "null");

                    // Delete the job unit
                    job.Delete();

                    // Reset navigation
                    {
                        var i = economy.GetAllPointers("stored_gps_ahead_waypoints");
                        foreach (var t in i) {
                            t.Delete();
                        }
                        economy.Set("stored_gps_ahead_waypoints", "0");
                    }

                    // Get trailers I own now
                    var trailers = player.Get("trailers").array;
                    if (trailers.Contains(currentTrailerId)) { // Owned trailer - cancel job and return
                        saveFile.Save(saveGame.ToString());
                        MessageBox.Show("작업에 사용된 트레일러를 이미 보유하고 있습니다. 화물이 남은 상태로 작업을 취소했습니다.", "완료");
                        return;
                    }

                    // Add trailer to player trailer list
                    {
                        var t = trailers.ToList();
                        t.Add(currentTrailerId);
                        player.Set("trailers", t.ToArray());
                    }

                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show("작업 트레일러를 소유한 트레일러에 추가했습니다. 필요할 시 트레일러 관리자에서 트레일러를 차고에 넣으십시오.", "완료");
                    return;
                } catch (Exception e) {
                    MessageBox.Show("오류가 발생했습니다.", "오류");
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "작업 트레일러 훔치기",
                run = run,
                description = "빠른 작업, 화물 시장 등으로 얻은 회사 트레일러를 훔쳐서 내 것으로 만듭니다."
            };
        }

        public SaveEditTask ChangeCargoMass() {
            var run = new Action(() => {
                try {
                    var player = saveGame.EntityType("player");

                    var assignedTrailerId = player.Get("assigned_trailer").value;
                    if (assignedTrailerId == "null") {
                        MessageBox.Show("활성화된 트레일러가 없습니다.", "오류");
                        return;
                    }

                    var specifiedMass = NumberInputBox.Show("무게 설정", "화물 무게를 몇 kg으로 설정하시겠습니까? 지나치게 높은 값으로 설정하면 물리 엔진 오류가 발생합니다. 화물을 없애는 경우 0으로 설정하면 됩니다.");

                    if (specifiedMass == -1) {
                        return;
                    }

                    var trailer = player.EntityIdAround(assignedTrailerId);
                    trailer.Set("cargo_mass", $"{specifiedMass}");

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
