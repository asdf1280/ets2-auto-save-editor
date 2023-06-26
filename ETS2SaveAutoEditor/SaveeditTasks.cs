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

                    var specifiedCash = NumberInputBox.Show("Specify cash", "Please specify the new cash.\nCurrent cash: " + currentBank + "\nCaution: Too high value may crash the game. Please be careful.");

                    if (specifiedCash == -1) {
                        return;
                    }

                    bank.Set("money_account", specifiedCash.ToString());
                    saveFile.Save(saveGame.MergeResult());
                    MessageBox.Show("Done!", "Done");
                } catch (Exception e) {
                    MessageBox.Show("An error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "Specify cash",
                run = run,
                description = "Specify cash"
            };
        }
        public SaveEditTask ExpSet() {
            var run = new Action(() => {
                try {
                    var economy = saveGame.EntityType("economy");
                    var currentExp = economy.Get("experience_points").value;

                    var specifiedExp = NumberInputBox.Show("Specify EXP", "Please specify the new exps.\nCurrent exps: " + currentExp + "\nCaution: Too high value may crash the game. Please be careful.");

                    if (specifiedExp == -1) {
                        return;
                    }

                    economy.Set("experience_points", specifiedExp.ToString());
                    saveFile.Save(saveGame.MergeResult());
                    MessageBox.Show("Done!", "Done");
                } catch (Exception e) {
                    MessageBox.Show("An unexpected error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "Specify EXP",
                run = run,
                description = "Specify EXP"
            };
        }
        public SaveEditTask UnlockScreens() {
            var run = new Action(() => {
                try {
                    var economy = saveGame.EntityType("economy");

                    var msgBoxRes = MessageBox.Show("Unlock GUIs such as skills. This can even unlock some items which is supposed to be disabled. Would you like to proceed?", "Unlock", MessageBoxButton.OKCancel);
                    if (msgBoxRes == MessageBoxResult.Cancel) {
                        return;
                    }

                    economy.Set("screen_access_list", "0");
                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show("Done!", "Done");
                } catch (Exception e) {
                    MessageBox.Show("An unexpected error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "Unlock all UI",
                run = run,
                description = "Unlock GUIs such as skills. For new profiles.\nSome GUIs that's normally disabled can be enabled too."
            };
        }
        public SaveEditTask TruckEngineSet() {
            var run = new Action(() => {
                try {
                    var player = saveGame.EntityType("player");
                    var assignedTruckId = player.Get("assigned_truck").value;

                    if (assignedTruckId == "null") {
                        MessageBox.Show("You're not driving a truck now.", "Error");
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
                    MessageBox.Show("Successfully changed!", "Done");
                } catch (Exception e) {
                    MessageBox.Show("An unexpected error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "Set truck engine",
                run = run,
                description = "Change the truck's engine to a few engines available."
            };
        }
        public SaveEditTask Refuel() {
            var run = new Action(() => {
                try {
                    var content = saveFile.content;
                    var sb = new StringBuilder();

                    var fuelPresetNames = new string[] { "1000x Tank", "100x Tank", "10x Tank", "5x Tank", "100%", "50%", "10%", "5%", "0%(...)" };
                    var fullPresetValues = new string[] {
                        "1000", "100", "10", "5", "1", "0.5", "0.1", "0.05", "0"
                    };
                    var fuelId = "";
                    {
                        var res = ListInputBox.Show("Choose fuel level", "Choose fuel level for your assigned truck.", fuelPresetNames);
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
                    MessageBox.Show("Modifyed fuel level of all trucks!", "Done");
                } catch (Exception e) {
                    MessageBox.Show("An unknown error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "Fill fuel tank",
                run = run,
                description = "Set the fuel level of all trucks in chosen savegame."
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
                    MessageBox.Show("Repaired all truck/trailers.", "Done");
                } catch (Exception e) {
                    MessageBox.Show("An unknown error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "Repair All",
                run = run,
                description = "Repair all truck/trailers in current savegame."
            };
        }

        private enum PositionDataHeader : byte {
            KEY,
            END
        }
        private readonly int positionDataVersion = 3;

        private struct PositionData {
            public List<float[]> Positions;
            public bool TrailerConnected;
        }

        private string EncodePositionData(PositionData data) {
            MemoryStream memoryStream = new MemoryStream();
            BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.ASCII);
            binaryWriter.Write(positionDataVersion);

            void sendPlacement(float[] p) {
                for (int t = 0; t < 7; t++) {
                    binaryWriter.Write(p[t]);
                }
            }
            binaryWriter.Write((byte)(data.Positions.Count + (data.TrailerConnected ? 1 << 7 : 0)));
            foreach (float[] p in data.Positions) {
                sendPlacement(p);
            }

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
        private PositionData DecodePositionData(string encoded) {
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
            BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);

            // Compatibility layer
            int version = binaryReader.ReadInt32();
            if (version != positionDataVersion) {
                if (version == 2) {
                    var v2Positions = DecodePositionDataV2(encoded);
                    return new PositionData {
                        TrailerConnected = true,
                        Positions = v2Positions
                    };
                }
                throw new IOException("incompatible version");
            }

            // Data exchange
            float[] receivePlacement() {
                float[] result = new float[7];
                for (int i = 0; i < 7; i++) {
                    result[i] = binaryReader.ReadSingle();
                }
                return result;
            }

            var length = binaryReader.ReadByte();
            var trailerConnected = (length & 1 << 7) > 0;
            length = (byte)(length & (~(1 << 7)));
            for (int i = 0; i < length; i++) {
                list.Add(receivePlacement());
            }
            return new PositionData {
                Positions = list,
                TrailerConnected = trailerConnected
            };
        }

        private List<float[]> DecodePositionDataV2(string encoded) {
            byte[] data = Convert.FromBase64String(encoded);
            List<float[]> list = new List<float[]>();

            MemoryStream memoryStream = new MemoryStream(data);
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
            if (version != 2) throw new IOException("incompatible version");
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

                    List<float[]> positions = new List<float[]>();

                    string truckPlacement = player.Get("truck_placement").value;
                    positions.Add(DecodeSCSPosition(truckPlacement));

                    var trailerAssigned = player.Get("assigned_trailer").value != "null";
                    if (trailerAssigned) {
                        string trailerPlacement = player.Get("trailer_placement").value;
                        positions.Add(DecodeSCSPosition(trailerPlacement));
                    }

                    var slaveTrailers = player.Get("slave_trailer_placements");
                    if (slaveTrailers.array != null) {
                        foreach (var slave in slaveTrailers.array) {
                            positions.Add(DecodeSCSPosition(slave));
                        }
                    }

                    var trailerConnected = player.Get("assigned_trailer_connected").value == "true";
                    if (positions.Count == 1) trailerConnected = true;

                    string encodedData = EncodePositionData(new PositionData {
                        TrailerConnected = trailerConnected,
                        Positions = positions
                    });
                    Clipboard.SetText(encodedData);
                    MessageBox.Show($"The location of your truck, trailer was copied to the clipboard.\nNumber of vehicles in the code: {positions.Count}, Connected to trailer: {(trailerConnected ? "Yes" : "No")}", "Complete!");
                } catch (Exception e) {
                    if (e.Message == "incompatible version") {
                        MessageBox.Show("Data version doesn't match the current version.", "Error");
                    } else {
                        MessageBox.Show("An error occured.", "Error");
                    }
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "Share Player Position",
                run = run,
                description = "Copies the position of player's truck and trailer, which you can share to others, to clipboard."
            };
        }
        public SaveEditTask InjectLocation() {
            var run = new Action(() => {
                try {
                    var economy = saveGame.EntityType("economy");
                    var player = saveGame.EntityType("player");

                    var positionData = DecodePositionData(Clipboard.GetText().Trim());
                    var decoded = (from a in positionData.Positions select EncodeSCSPosition(a)).ToArray();
                    if (decoded.Count() >= 1) {
                        player.Set("truck_placement", decoded[0]);
                    }
                    if (decoded.Count() >= 2) {
                        player.Set("trailer_placement", decoded[1]);
                    } else {
                        player.Set("trailer_placement", decoded[0]);
                    }
                    if (decoded.Count() > 2) {
                        player.Set("slave_trailer_placements", decoded.Skip(2).ToArray());
                    } else {
                        player.Set("slave_trailer_placements", "0");
                    }

                    player.Set("assigned_trailer_connected", positionData.TrailerConnected ? "true" : "false");

                    // Reset navigation
                    //DestroyNavigationData(economy);

                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show($"Successfully injected the position code!\nNumber of vehicles in the code: {positions.Count}, Connected to trailer: {(trailerConnected ? "Yes" : "No")}", "Complete!");
                } catch (Exception e) {
                    if (e.Message == "incompatible version") {
                        MessageBox.Show("Data version doesn't match the current version.", "Error");
                    } else {
                        MessageBox.Show("An error occured.", "Error");
                    }
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "Inject Player Position",
                run = run,
                description = "Imports the shared player position data to inject into this savegame."
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
                            MessageBox.Show("You don't have an assigned trailer.", "Error");
                            return;
                        }

                        currentJobId = player.Get("current_job").value;
                        if (currentJobId == "null") {
                            MessageBox.Show("You don't have any job now.", "Error");
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
                    DestroyNavigationData(economy);

                    // Get trailers I own now
                    var trailers = player.Get("trailers").array;
                    if (trailers.Contains(currentTrailerId)) { // Owned trailer - cancel job and return
                        saveFile.Save(saveGame.ToString());
                        MessageBox.Show("You already own the trailer used for the job. The job was canceled with the cargo accessory remaining.", "Done!");
                        return;
                    }

                    // Add trailer to player trailer list
                    {
                        var t = trailers.ToList();
                        t.Add(currentTrailerId);
                        player.Set("trailers", t.ToArray());
                    }

                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show("The trailer is yours now. You can relocate the trailer as needed.", "Done!");
                    return;
                } catch (Exception e) {
                    MessageBox.Show("An error occured.", "Error");
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "Job trailer stealer",
                run = run,
                description = "Steals the trailer you are currently using for the job."
            };
        }

        private void DestroyNavigationData(UnitEntity economy) {
            var i = economy.GetAllPointers("stored_gps_ahead_waypoints");
            foreach (var t in i) {
                t.Delete();
            }
            economy.Set("stored_gps_ahead_waypoints", "0");
        }

        public SaveEditTask ChangeCargoMass() {
            var run = new Action(() => {
                try {
                    var player = saveGame.EntityType("player");

                    var assignedTrailerId = player.Get("assigned_trailer").value;
                    if (assignedTrailerId == "null") {
                        MessageBox.Show("You don't have an assigned trailer.", "Error");
                        return;
                    }

                    var specifiedMass = NumberInputBox.Show("Set mass", "How heavy would you like your trailer cargo to be in kilograms? Too high values will result in physics glitch. Set this to 0 if you want to remove the cargo.");

                    if (specifiedMass == -1) {
                        return;
                    }

                    var trailer = player.EntityIdAround(assignedTrailerId);
                    trailer.Set("cargo_mass", $"{specifiedMass}");

                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show("Changed the trailer cargo mass!", "Done");
                    return;
                } catch (Exception e) {
                    MessageBox.Show("An error occured.", "Error");
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "Set trailer cargo mass",
                run = run,
                description = "Change the cargo mass of the assigned trailer as you want."
            };
        }
    }
}
