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

        public SaveEditTask ShareLocation() {
            var run = new Action(() => {
                try {
                    var player = saveGame.EntityType("player");

                    List<float[]> positions = new List<float[]>();

                    string truckPlacement = player.Get("truck_placement").value;
                    positions.Add(SCSSpecialString.DecodeSCSPosition(truckPlacement));

                    var trailerAssigned = player.Get("assigned_trailer").value != "null";
                    if (trailerAssigned) {
                        string trailerPlacement = player.Get("trailer_placement").value;
                        positions.Add(SCSSpecialString.DecodeSCSPosition(trailerPlacement));
                    }

                    var slaveTrailers = player.Get("slave_trailer_placements");
                    if (slaveTrailers.array != null) {
                        foreach (var slave in slaveTrailers.array) {
                            positions.Add(SCSSpecialString.DecodeSCSPosition(slave));
                        }
                    }

                    var trailerConnected = player.Get("assigned_trailer_connected").value == "true";
                    if (positions.Count == 1) trailerConnected = true;

                    string encodedData = PositionCodeEncoder.EncodePositionCode(new PositionData {
                        TrailerConnected = trailerConnected,
                        Positions = positions
                    });
                    Clipboard.SetText(encodedData);
                    MessageBox.Show($"The location of your truck, trailer was copied to the clipboard.\nNumber of vehicles in the code: {positions.Count}, Connected to trailer: {(trailerConnected ? "Yes" : "No")}", "Complete!");
                } catch (Exception e) {
                    if (e.Message == "incompatible version") {
                        MessageBox.Show("Data version doesn't match the current version.", "Error");
                    } else {
                        MessageBox.Show($"An error occured.\n{e.GetType().FullName}: {e.Message}\nPlease contact the developer.", "Error");
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
                    var player = saveGame.EntityType("player");

                    var positionData = PositionCodeEncoder.DecodePositionCode(Clipboard.GetText().Trim());
                    var decoded = (from a in positionData.Positions select SCSSpecialString.EncodeSCSPosition(a)).ToArray();
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

                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show($"Successfully injected the position code!\nNumber of vehicles in the code: {decoded.Count()}, Connected to trailer: {(positionData.TrailerConnected ? "Yes" : "No")}", "Complete!");
                } catch (Exception e) {
                    if (e.Message == "incompatible version") {
                        MessageBox.Show("Data version doesn't match the current version.", "Error");
                    } else {
                        MessageBox.Show($"An error occured.\n{e.GetType().FullName}: {e.Message}\nPlease contact the developer.", "Error");
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

        class ASEVehicle {
            public string[] Keys;
            public string Data;
        }

        public SaveEditTask SpecialCCTask() {
            var run = new Action(() => {
                var res = ListInputBox.Show("Choose sub-task", "", new string[] { "Export Active Vehicle", "Spread CC Saves", "Delete all CC saves in this profile" });
                if (res == -1) return;
                if (res == 0) {
                    try {
                        var economy = saveGame.EntityType("economy");
                        var player = saveGame.EntityType("player");

                        var sb = new StringBuilder();

                        var assignedTruck = player.Get("assigned_truck").value;
                        if (assignedTruck == "null") {
                            MessageBox.Show("No truck is active.");
                            return;
                        }

                        var trailersToProcess = new List<string>();
                        var nextTrailer = player.Get("assigned_trailer").value;
                        while (nextTrailer != "null") {
                            trailersToProcess.Add(nextTrailer);
                            nextTrailer = player.EntityIdAround(nextTrailer).Get("slave_trailer").value;
                        }

                        sb.AppendLine("ASEVEHICLE-START");
                        sb.AppendLine($"{trailersToProcess.Count + 1}");
                        sb.AppendLine(assignedTruck);
                        foreach (var trailer in trailersToProcess) {
                            sb.AppendLine(trailer);
                        }

                        void recurseUnit(UnitEntity e) {
                            sb.AppendLine(e.GetFullString());

                            var queue = new List<string>();
                            if (e.Type == "vehicle") {
                                queue.AddRange(e.Get("accessories").array);
                            } else if (e.Type == "trailer") {
                                string p = e.Get("trailer_definition").value;
                                if (p.StartsWith("_")) {
                                    queue.Add(p);
                                }

                                queue.AddRange(e.Get("accessories").array);
                            }

                            queue.ForEach((v) => {
                                recurseUnit(e.EntityIdAround(v));
                            });
                        }

                        recurseUnit(player.EntityIdAround(assignedTruck));

                        foreach (var trailer in trailersToProcess) {
                            recurseUnit(player.EntityIdAround(trailer));
                        }

                        sb.AppendLine("ASEVEHICLE-END");

                        Clipboard.SetText(sb.ToString());
                    } catch (Exception e) {
                        if (e.Message == "incompatible version") {
                            MessageBox.Show("Data version doesn't match the current version.", "Error");
                        } else {
                            MessageBox.Show($"An error occured.\n{e.GetType().FullName}: {e.Message}\nPlease contact the developer.", "Error");
                        }
                        Console.WriteLine(e);
                    }
                } else if (res == 1) {
                    var lines = Clipboard.GetText().Trim();
                    if (!lines.Contains("ASEVEHICLE-START") || !lines.Contains("ASEVEHICLE-END") || !lines.Contains("ASEPOS") || lines.Split(new string[] { "ASEPOS" }, StringSplitOptions.None)[1].Trim().Split('\n').Length % 3 != 0) {
                        MessageBox.Show("Incorrect input");
                        return;
                    }

                    // Adding CC to profile with job running will cause crash
                    var srcPlayer = saveGame.EntityType("player");
                    if (srcPlayer.Get("current_job").value != "null") {
                        MessageBox.Show("You can't run this action with an active job in your save.");
                        return;
                    }

                    // Injecting data into new saves
                    var namelessIdent = $"_nameless.{DateTime.Now.Ticks % 100000:x}";

                    // Parsing data
                    // 1. vehicle data
                    var splitResult = lines.Split(new string[] { "ASEVEHICLE-START" }, StringSplitOptions.None);
                    var vehicles = new List<ASEVehicle>();
                    for (int k = 1; k < splitResult.Length; k++) {
                        var vehicleData = splitResult[k].Split(new string[] { "ASEVEHICLE-END" }, StringSplitOptions.None)[0].Trim().Replace("_nameless.", namelessIdent + k + ".");
                        var vehicleLines = new List<string>(vehicleData.Split('\n'));

                        var obj = new ASEVehicle();
                        var cnt = int.Parse(vehicleLines[0]);
                        vehicleLines.RemoveAt(0);

                        var ids = new List<string>();
                        for (int j = 0; j < cnt; j++) {
                            ids.Add(vehicleLines[0].Trim());
                            vehicleLines.RemoveAt(0);
                        }

                        obj.Keys = ids.ToArray();
                        obj.Data = string.Join("\n", vehicleLines);
                        vehicles.Add(obj);
                    }

                    // 2. position data - inject as progresses
                    var positionLines = (from m in lines.Split(new string[] { "ASEPOS" }, StringSplitOptions.None)[1].Trim().Split('\n') select m.Trim()).ToArray();
                    var positionEntryCount = positionLines.Length / 3;

                    // 3. do it
                    var saveToClone = saveGame.ToString();
                    // loading info.sii too
                    var infoSiiPath = saveFile.fullPath + @"\info.sii";
                    var infoContent = File.ReadAllText(infoSiiPath);
                    var infoGame = new SiiSaveGame(infoContent);

                    var startTime = DateTime.Now;
                    for (int k = 0; k < positionEntryCount; k++) {
                        var targetEditedDate = startTime.AddMinutes(k + 1);
                        var saveName = positionLines[k * 3];
                        var vehicleData = vehicles[int.Parse(positionLines[k * 3 + 1].Trim())];
                        var positionData = PositionCodeEncoder.DecodePositionCode(positionLines[k * 3 + 2]);

                        // output path
                        int i = 1;
                        while (Directory.Exists(saveFile.fullPath + @"\..\" + i)) i++;
                        var newPath = saveFile.fullPath + @"\..\" + i;
                        Directory.CreateDirectory(newPath);

                        // info.sii
                        infoGame.EntityType("save_container").Set("name", $@"""{SCSSaveHexEncodingSupport.GetEscapedSaveName(saveName)}""");
                        File.WriteAllText(newPath + @"\info.sii", infoGame.ToString());
                        File.SetLastWriteTime(newPath + @"\info.sii", targetEditedDate);

                        // game.sii
                        var cloned = new SiiSaveGame(saveToClone);

                        var player = cloned.EntityType("player");

                        // Add the vehicle to truck list
                        {
                            var trucks = player.Get("trucks").array ?? (new string[] { });
                            {
                                var t = trucks.ToList();
                                t.Add(vehicleData.Keys[0]);
                                player.Set("trucks", t.ToArray());
                            }

                            player.Set("assigned_truck", vehicleData.Keys[0]);
                            player.Set("my_truck", vehicleData.Keys[0]);

                            // Prevent game CTD bug
                            var truckProfitLogs = player.Get("truck_profit_logs").array ?? (new string[] { });
                            {
                                var t = truckProfitLogs.ToList();
                                t.Add($"{namelessIdent}.aaaa.bbbb.000");
                                player.Set("truck_profit_logs", t.ToArray());
                            }
                        }

                        if (vehicleData.Keys.Length >= 2) { // Add the vehicle to trailer list
                            var trailers = player.Get("trailers").array ?? (new string[] { });
                            {
                                var t = trailers.ToList();
                                t.Add(vehicleData.Keys[1]);
                                player.Set("trailers", t.ToArray());
                            }

                            player.Set("assigned_trailer", vehicleData.Keys[1]);
                            player.Set("my_trailer", vehicleData.Keys[1]);

                            var p = new Regex(@"trailer_definition: (.+?)\r?\n");
                            if (p.Matches(vehicleData.Data)[0].Groups[1].Value.StartsWith("_nameless")) {
                                var trailerDefs = player.Get("trailer_defs").array ?? (new string[] { });
                                {
                                    var t = trailerDefs.ToList();
                                    t.Add(p.Matches(vehicleData.Data)[0].Groups[1].Value);
                                    player.Set("trailer_defs", t.ToArray());
                                }
                            }
                        } else {
                            player.Set("assigned_trailer", "null");
                            player.Set("my_trailer", "null");
                        }

                        // copy paste of InjectLocation
                        {
                            var decoded = (from a in positionData.Positions select SCSSpecialString.EncodeSCSPosition(a)).ToArray();
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
                        }

                        cloned.Lines.Insert(player.ResolvedUnit.end + 1, vehicleData.Data);
                        // Prevent game CTD bug - dummy profit log
                        cloned.Lines.Insert(player.ResolvedUnit.end + 1, $@"profit_log : {namelessIdent}.aaaa.bbbb.000 {{
 stats_data: 0
 acc_distance_free: 600
 acc_distance_on_job: 0
 history_age: nil
}}");

                        File.WriteAllText(newPath + @"\game.sii", cloned.ToString());
                        File.SetLastWriteTime(newPath + @"\game.sii", targetEditedDate);

                        File.WriteAllText(newPath + @"\ETS2ASE_CC", k + "");

                        Directory.SetLastWriteTime(newPath, targetEditedDate);
                    }
                } else if (res == 2) {
                    foreach (var dir in Directory.EnumerateDirectories(saveFile.fullPath + @"\..\")) {
                        if (File.Exists(dir + @"\ETS2ASE_CC")) {
                            Directory.Delete(dir, true);
                        }
                    }
                }
            });
            return new SaveEditTask {
                name = "Special CC Task",
                run = run,
                description = "ETS2 CC Profile sharing toolkit. For advanced users only."
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
                        if (player.Get("my_truck_placement_valid").value == "true")
                            player.Set("truck_placement", player.Get("my_truck_placement").value);
                    }

                    // Set current_job to null
                    player.Set("current_job", "null");

                    // Delete the job unit
                    job.Delete();

                    // Reset navigation
                    DestroyNavigationData(economy);

                    // Get trailers I own now
                    var trailers = player.Get("trailers").array ?? (new string[] { });
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
