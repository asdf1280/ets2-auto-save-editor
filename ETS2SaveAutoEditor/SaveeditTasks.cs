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

                    var specifiedCash = NumberInputBox.Show("Specify cash", "Please specify the new cash.\nCurrent cash: " + currentBank + "\nCaution: Too high value may crash the game. Please be careful.");

                    if (specifiedCash == -1) {
                        return;
                    }

                    saveGame.SetUnitItem(bank, new UnitItem { name = "money_account", value = specifiedCash.ToString() });
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
                    var saveGame = new SiiSaveGame(saveFile.content);
                    var economy = saveGame.FindUnitWithType("economy");
                    var currentExp = saveGame.GetUnitItem(economy, "experience_points").value;

                    var specifiedExp = NumberInputBox.Show("Specify EXP", "Please specify the new exps.\nCurrent exps: " + currentExp + "\nCaution: Too high value may crash the game. Please be careful.");

                    if (specifiedExp == -1) {
                        return;
                    }

                    saveGame.SetUnitItem(economy, new UnitItem { name = "experience_points", value = specifiedExp.ToString() });
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
                    var saveGame = new SiiSaveGame(saveFile.content);
                    var economy = saveGame.FindUnitWithType("economy");

                    var msgBoxRes = MessageBox.Show("Unlock GUIs such as skills. This can even unlock some items which is supposed to be disabled. Would you like to proceed?", "Unlock", MessageBoxButton.OKCancel);
                    if (msgBoxRes == MessageBoxResult.Cancel) {
                        return;
                    }

                    saveGame.SetUnitItem(economy, new UnitItem { name = "screen_access_list", value = "0" });
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
                        MessageBox.Show("Corrupted savegame.", "Error");
                        return;
                    } else if (resultLine == "null") {
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
                    MessageBox.Show("Done!", "Done");
                } catch (Exception e) {
                    MessageBox.Show("An error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "Reset map",
                run = run,
                description = "Reset explorered roads."
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
                        MessageBox.Show("Corrupted save file", "Error");
                        return;
                    }

                    var operationNames = new string[] { "Import, Truck", "Export, Truck", "Import, Trailer", "Export, Trailer" };
                    var truck = false;
                    var import = false;
                    {
                        var res = ListInputBox.Show("Choose job", "Which one do you want? Please choose what to import/export paintjob from.", operationNames);
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
                            MessageBox.Show("There's no assigned truck.", "Error");
                        else
                            MessageBox.Show("There's no assigned trailer.", "Error");
                        return;
                    }

                    string path;

                    var filter = (truck ? "Truck paintjob (*.paint0)|*.paint0|All files (*.*)|*.*" : "Trailer paintjob (*.paint1)|*.paint1|All files (*.*)|*.*");
                    if (import) { // Import
                        OpenFileDialog dialog = new OpenFileDialog {
                            Title = "Choose paintjob file",
                            Filter = filter
                        };
                        if (dialog.ShowDialog() != true) return;
                        path = dialog.FileName;
                    } else // Export
                      {
                        SaveFileDialog dialog = new SaveFileDialog {
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

                    if (import) {
                        var str = File.ReadAllText(path, Encoding.UTF8);
                        var strs = str.Split(';');
                        if (strs.Length != 8) {
                            MessageBox.Show("Corrupted paintjob file", "Error");
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
                        MessageBox.Show("Imported paintjob!", "Done");
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
                            MessageBox.Show("Could not export", "Error");
                            throw;
                        }
                        MessageBox.Show("Exported paintjob!", "Done");
                    }
                } catch (Exception e) {
                    MessageBox.Show("An unknown error occured", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "Export/import paintjob",
                run = run,
                description = "Import/export paintjob of assigned truck/trailer."
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
                    MessageBox.Show("The location of your truck, trailer was copied to the clipboard. Share it to others.", "Done");
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
                    MessageBox.Show("Successfully injected the position of player vehicles.", "Done");
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
                    var saveGame = new SiiSaveGame(saveFile.content);
                    var economy = UnitTypeSelector.Of("economy");
                    var player = UnitTypeSelector.Of("player");

                    var currentTrailerId = "";
                    var currentJobId = "";
                    { // 현재 작업이나 트레일러가 없으면 오류 발생
                        currentTrailerId = saveGame.GetUnitItem(player, "assigned_trailer").value;
                        if (currentTrailerId == "null") {
                            MessageBox.Show("You don't have an assigned trailer.", "Error");
                            return;
                        }

                        currentJobId = saveGame.GetUnitItem(player, "current_job").value;
                        if (currentJobId == "null") {
                            MessageBox.Show("You don't have any job now.", "Error");
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
                        MessageBox.Show("You already own the trailer used for the job. The job was canceled with the cargo accessory remaining.", "Done!");
                        return;
                    }

                    // Not owned trailers - Add to owned trailers and selected garage
                    var garageId = "";
                    {
                        var garageNamesFound = (from item in saveGame.GetUnitItem(economy, "garages").array select item.Split(new string[] { "garage." }, StringSplitOptions.None)[1]).ToList();
                        garageNamesFound.Sort();

                        if (garageNamesFound.Count == 0) {
                            MessageBox.Show("You need to have at least one garage to store the stolen trailer.", "Error");
                            return;
                        }

                        var res = ListInputBox.Show("Choose your garage", "Where would you like to store the stolen trailer? Make sure only to choose the garage that you own in game.", garageNamesFound.ToArray());
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
                    MessageBox.Show("The trailer is yours now.", "Done!");
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

        public SaveEditTask ChangeCargoMass() {
            var run = new Action(() => {
                try {
                    var saveGame = new SiiSaveGame(saveFile.content);
                    var player = UnitTypeSelector.Of("player");

                    var assignedTrailerId = saveGame.GetUnitItem(player, "assigned_trailer").value;
                    if (assignedTrailerId == "null") {
                        MessageBox.Show("You don't have an assigned trailer.", "Error");
                        return;
                    }

                    var specifiedMass = NumberInputBox.Show("Set mass", "How heavy would you like your trailer cargo to be in kilograms? Too high values will result in physics glitch. Set this to 0 if you want to remove the cargo.");

                    if (specifiedMass == -1) {
                        return;
                    }

                    var trailer = UnitIdSelector.Of(assignedTrailerId);
                    saveGame.SetUnitItem(trailer, new UnitItem { name = "cargo_mass", value = $"{specifiedMass}" });

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
