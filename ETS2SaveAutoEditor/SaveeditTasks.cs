using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace ETS2SaveAutoEditor {
    public class SaveeditTasks {
        public void SetSaveFile(ProfileSave file) {
            saveFile = file;
            saveGame = new SiiSaveGame(file.content);
            saveFile.Save(saveGame.ToString());
        }
        private ProfileSave saveFile;
        private SiiSaveGame saveGame;

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
                name = "Specify Cash",
                run = run,
                description = "Specify the desired amount of cash in the game."
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
                description = "Specify the desired amount of experience points (EXP)."
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
                description = "Enables locked GUIs, including skills, for new profiles. Some normally disabled GUIs may also be enabled."
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

                    var engineNames = new string[] { "Scania Electric 450kW", "Renault Electric 490kW", "Scania new 730", "Scania old 730", "Volvo new 750", "Volvo old 750", "Renault Premium 380", "Iveco 310(...)" };
                    var enginePaths = new string[] {
                        "/def/vehicle/truck/scania.s_2024e/engine/450kw.sii",
                        "/def/vehicle/truck/renault.etech_t/engine/490kw.sii",
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
                name = "Modify Truck Engine",
                run = run,
                description = "Switches the truck's engine to one of the available options."
            };
        }
        public SaveEditTask Refuel() {
            var run = new Action(() => {
                try {
                    var player = saveGame.EntityType("player");
                    if (player.Get("assigned_truck").value == "null") {
                        MessageBox.Show("You don't have any truck assigned.", "Done");
                    }

                    var assignedTruckObj = player.GetPointer("assigned_truck");
                    assignedTruckObj.Set("fuel_relative", "1");

                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show("Done", "Done");
                } catch (Exception e) {
                    MessageBox.Show("An unknown error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "Refuel current vehicle",
                run = run,
                description = "Set the fuel(or battery) level of current truck to maximum."
            };
        }
        public SaveEditTask FixEverything() {
            var run = new Action(() => {
                try {
                    var lines = saveFile.content.Split('\n');
                    var fs = new FileStream(saveFile.fullPath + @"\game.sii", FileMode.OpenOrCreate);
                    var sw = new StreamWriter(fs, Encoding.UTF8);

                    var p = new Regex(@"(([a-z_]*_wear(?:_unfixable)?(?:\[\d*\])?|integrity_odometer(?:_float_part)?):) (.*)\b", RegexOptions.Compiled);
                    for (int i = 0; i < lines.Length; i++) {
                        var str = lines[i];

                        if (str.Contains("disco") || str.Contains("unlock") || str.Contains("{") || str.Contains("}") || (!str.Contains("wear") && !str.Contains("integrity_odometer")) || !p.IsMatch(str)) {
                            sw.Write(str);
                            sw.Write('\n');
                            continue;
                        }

                        sw.Write(p.Replace(str, "$1 0"));
                        sw.Write('\n');
                    }
                    sw.Close();
                    fs.Close();

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
                description = "Repairs all trucks and trailers in the current savegame.\n\n1.49 Warning - This will even repair trucks in used truck dealer!"
            };
        }

        public SaveEditTask SharePosition() {
            var run = new Action(() => {
                try {
                    var player = saveGame.EntityType("player");

                    List<float[]> positions = new List<float[]>();

                    string truckPlacement = player.GetValue("truck_placement");
                    positions.Add(SCSSpecialString.DecodeSCSPosition(truckPlacement));

                    var trailerAssigned = player.GetValue("assigned_trailer") != "null";
                    if (trailerAssigned) {
                        string trailerPlacement = player.GetValue("trailer_placement");
                        positions.Add(SCSSpecialString.DecodeSCSPosition(trailerPlacement));
                    }

                    var slaveTrailers = player.Get("slave_trailer_placements");
                    if (slaveTrailers.array != null) {
                        foreach (var slave in slaveTrailers.array) {
                            positions.Add(SCSSpecialString.DecodeSCSPosition(slave));
                        }
                    }

                    var trailerConnected = player.GetValue("assigned_trailer_connected") == "true";
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
                        if (e.Message.Contains("Clipboard")) {
                            MessageBox.Show($"There was an error while copying the position code. However, it may have already worked. Please check your clipboard.", "Complete!");
                        } else {
                            MessageBox.Show($"An error occured.\n{e.GetType().FullName}: {e.Message}\nPlease contact the developer.", "Error");
                        }
                    }
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "Share Player Position",
                run = run,
                description = "Duplicates the position of the player's truck and trailer, allowing you to share it with others by copying it to the clipboard."
            };
        }
        public SaveEditTask ImportPosition() {
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

                    DestroyNavigationData(saveGame.EntityType("economy"));

                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show($"Successfully imported the position code!\nNumber of vehicles in the code: {decoded.Count()}, Connected to trailer: {(positionData.TrailerConnected ? "Yes" : "No")}", "Complete!");
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
                name = "Import Player Position",
                run = run,
                description = "Imports the shared player position data to incorporate it into the current savegame."
            };
        }
        public SaveEditTask ReducePosition() {
            var run = new Action(() => {
                try {
                    var positionData = PositionCodeEncoder.DecodePositionCode(Clipboard.GetText().Trim());
                    var a = positionData.Positions[0];
                    positionData.Positions = new List<float[]> {
                        a
                    };

                    positionData.TrailerConnected = true;

                    string encodedData = PositionCodeEncoder.EncodePositionCode(positionData);
                    Clipboard.SetText(encodedData);
                    MessageBox.Show($"The reduced position code was copied to clipboard!", "Complete!");
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
                name = "Reduce Position Code",
                run = run,
                description = "Reduce the length of position code by removing trailer position data. These are not necessary as long as you create the code with enough space behind."
            };
        }

        public SaveEditTask DecodePosition() {
            var run = new Action(() => {
                try {
                    var code = Clipboard.GetText().Trim();
                    var positionData = PositionCodeEncoder.DecodePositionCode(code);
                    var decoded = (from a in positionData.Positions select SCSSpecialString.EncodeDecimalPosition(a)).ToArray();

                    // xyz and quaternion
                    string res = "(x, y, z) (q1, q2, q3, q4)\nThe position is written as XYZ in meters, and rotation as quaternion.\n\n";
                    for (int i=0; i<decoded.Length; i++) {
                        if (i == 0) res += "Truck placement: ";
                        else res += $"Trailer {i} placement: ";
                        res += decoded[i] + "\n";
                    }

                    res += "\nConnected to trailer: " + (positionData.TrailerConnected ? "Yes" : "No") + "\nPosition code:\n\n" + code;
                    Clipboard.SetText(res);
                    MessageBox.Show($"The decoded position data was copied to clipboard!\n\n" + res, "Complete!");
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
                name = "Decode Player Position",
                run = run,
                description = "Decodes the shared player position data in the current clipboard to a human-readable decimal format. Units are in meters."
            };
        }

        public SaveEditTask ConnectTrailerInstantly() {
            var run = new Action(() => {
                try {
                    var economy = saveGame.EntityType("economy");
                    var player = saveGame.EntityType("player");

                    var truckPlacement = player.Get("truck_placement").value;
                    player.Set("trailer_placement", truckPlacement);
                    player.Set("slave_trailer_placements", "0");
                    player.Set("assigned_trailer_connected", "true");

                    foreach (var item in economy.GetAllPointers("stored_gps_behind_waypoints")) {
                        item.Delete();
                    }
                    foreach (var item in economy.GetAllPointers("stored_gps_ahead_waypoints")) {
                        item.Delete();
                    }
                    economy.Set("stored_gps_behind_waypoints", "0");
                    economy.Set("stored_gps_ahead_waypoints", "0");

                    var registry = economy.GetPointer("registry");
                    var regData = registry.GetArray("data");
                    regData[0] = "0";
                    registry.Set("data", regData);

                    saveFile.Save(saveGame.ToString());
                    MessageBox.Show($"Success!", "Complete!");
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
                name = "Connect Trailer Here",
                run = run,
                description = "Forces the assigned trailer to connect to the truck immediately. Make sure there's enough space for the straight trailer behind!"
            };
        }

        public SaveEditTask VehicleSharingTool(Trucksim game) {
            const string AseVehicleFormat = "1";
            var run = new Action(() => {
                while (true) {
                    var choice = ListInputBox.Show("Vehicle Sharing", "WARNING: Importing an imcompatible vehicle (DLC, MOD, or incompatible version) will break your save. Click 'Cancel' to close this window.", new string[] { "Share Truck", "Share Trailer", "Import Vehicle"/*, "Don't use this"*/ });
                    if (choice == -1) break;

                    if (choice == 3) { // Export the whole save. For testing only.
                        var economy = saveGame.EntityType("economy");
                        var vehicleStr = UnitSerializer.SerializeUnit(economy, new HashSet<string> { "AUTO" });

                        var d = new SaveFileDialog() {
                            Title = "Save Economy",
                            Filter = "ASE Unit Data (*.asu)|*.asu|All files (*.*)|*.*",
                            FilterIndex = 0,
                        };
                        if (d.ShowDialog() != true) continue;

                        File.WriteAllText(d.FileName, vehicleStr, Encoding.UTF8);
                    }
                    if (choice == 0) { // Export truck
                        var player = saveGame.EntityType("player");

                        var assignedTruck = player.GetPointer("assigned_truck");
                        if (assignedTruck == null) {
                            MessageBox.Show("You don't have any truck assigned.", "Error");
                            continue;
                        }

                        string headerStr;
                        {
                            var builder = new StringBuilder();
                            builder.Append("+ ASE Shared Vehicle\n");
                            builder.Append($"+ {game} TRUCK V{AseVehicleFormat}\n");
                            builder.Append("+ It isn't recommended to edit this data manually. No support will be provided for modified files.\n");
                            builder.Append("+\n");
                            headerStr = builder.ToString();
                        }
                        var vehicleStr = UnitSerializer.SerializeUnit(assignedTruck, new HashSet<string> { "vehicle:accessories" });

                        var d = new SaveFileDialog() {
                            Title = "Save Truck",
                            Filter = "ASE Vehicle Data (*.asv)|*.asv|All files (*.*)|*.*",
                            FilterIndex = 0,
                        };
                        if (d.ShowDialog() != true) continue;

                        File.WriteAllText(d.FileName, headerStr + vehicleStr, Encoding.UTF8);
                    }
                    if (choice == 1) { // Export trailer
                        var player = saveGame.EntityType("player");

                        var assignedTrailer = player.GetPointer("assigned_trailer");
                        if (assignedTrailer == null) {
                            MessageBox.Show("You don't have any trailer assigned.", "Error");
                            continue;
                        }

                        string headerStr;
                        {
                            var builder = new StringBuilder();
                            builder.Append("+ ASE Shared Vehicle\n");
                            builder.Append($"+ {game} TRAILER V{AseVehicleFormat}\n");
                            builder.Append("+ It isn't recommended to edit this data manually. No support will be provided for modified files.\n");
                            builder.Append("+\n");
                            headerStr = builder.ToString();
                        }
                        var vehicleStr = UnitSerializer.SerializeUnit(assignedTrailer, new HashSet<string> { "trailer:trailer_definition", "trailer:slave_trailer", "trailer:accessories" });

                        var d = new SaveFileDialog() {
                            Title = "Save Trailer",
                            Filter = "ASE Vehicle Data (*.asv)|*.asv|All files (*.*)|*.*",
                            FilterIndex = 0,
                        };
                        if (d.ShowDialog() != true) continue;

                        File.WriteAllText(d.FileName, headerStr + vehicleStr, Encoding.UTF8);
                    }
                    if (choice == 2) { // Import
                        var d = new OpenFileDialog() {
                            Title = "Open Vehicle",
                            Filter = "ASE Vehicle data (*.asv)|*.asv|All files (*.*)|*.*",
                            FilterIndex = 0,
                        };
                        if (d.ShowDialog() != true) continue;

                        var text = File.ReadAllText(d.FileName, Encoding.UTF8);
                        var lines = text.Split('\n');

                        bool isTruck = false;
                        try {
                            if (lines.Length < 5) {
                                throw new Exception("Invalid file format");
                            }
                            var header = lines[1].Trim().Split(' ');
                            if (lines[0].Trim() != "+ ASE Shared Vehicle" || header.Length != 4) {
                                throw new Exception("Invalid file format");
                            }
                            if (header[3] != $"V{AseVehicleFormat}") {
                                throw new Exception($"Incompatible file version.\n\nSupported: V{AseVehicleFormat}\nThis file: {header[3]}");
                            }
                            if (header[1] != game.ToString()) {
                                throw new Exception($"Incompatible game.\n\nExpected: {game}\nThis file: {header[1]}");
                            }

                            if (header[2] == "TRUCK") isTruck = true;
                            else if (header[2] != "TRAILER") {
                                throw new Exception($"Invalid vehicle type.\n\nExpected: TRUCK or TRAILER\nThis file: {header[2]}");
                            }
                        } catch (Exception e) {
                            MessageBox.Show($"{e.Message}", "Error");
                            continue;
                        }

                        var player = saveGame.EntityType("player");
                        var entities = UnitSerializer.DeserializeUnit(player, text);

                        // Finish up necessary things to prevent crash
                        if(isTruck) {
                            // Add vehicle to truck list
                            // Add empty profit log entry
                            // Remind the user to assign it to a garage to prevent bugs that are confirmed to exist

                            if (entities[0].Type != "vehicle") {
                                MessageBox.Show($"Unexpected root node. Expected 'vehicle' got '{entities[0].Type}'.", "Error");
                                continue;
                            }

                            string truckId = entities[0].Id;
                            string profitLogId = entities[0].Id + "0";
                            player.ArrayAppend("trucks", truckId, true);

                            var newLog = player.InsertAfter("profit_log", profitLogId);
                            newLog.Set("stats_data", "0");
                            newLog.Set("acc_distance_free", "0");
                            newLog.Set("acc_distance_on_job", "0");
                            newLog.Set("history_age", "nil");
                            player.ArrayAppend("truck_profit_logs", profitLogId, true);

                            MessageBox.Show("Successfully imported the truck!\n\nPlease assign the truck to a garage to prevent bugs that are confirmed to exist.", "Complete!");
                        } else {
                            // Add vehicle to trailer list
                            // If 'entities' contains a trailer definition, add it to the list of trailer definitions
                            // Remind the user to assign it to a garage to prevent possible bugs

                            if (entities[0].Type != "trailer") {
                                MessageBox.Show($"Unexpected root node. Expected 'trailer' got '{entities[0].Type}'.", "Error");
                                continue;
                            }

                            string trailerId = entities[0].Id;
                            string trailerDefId = entities[0].GetValue("trailer_definition");

                            player.ArrayAppend("trailers", trailerId, true);
                            if(trailerDefId.StartsWith("_")) {
                                player.ArrayAppend("trailer_defs", trailerDefId, true);
                            }

                            MessageBox.Show("Successfully imported the trailer!\n\nPlease assign the trailer to a garage to prevent possible bugs.", "Complete!");
                        }

                        saveFile.Save(saveGame.ToString());
                    }
                }
            });
            return new SaveEditTask {
                name = "[NEW] Vehicle Sharing",
                run = run,
                description = "You can use this tool to easily share and import trucks and trailers without worrying about collisions and errors."
            };
        }

        class ASEVehicle {
            public string[] Keys;
            public string Data;
        }

        public SaveEditTask SpecialCCTask() {
            var run = new Action(() => {
                var document = @"> Press 'Cancel' to close this window <

** How to apply CC data **
1. Run 'Apply CC Data'
2. Open the received CC data file (*.ddd)

** NOTE **
- Applying the data won't affect existing profile and saves. It will simply add new saves to the profile.
- When a vehicle is added with this utility, they're in a very unstable state because they're not placed in a garage. Therefore, when the event is concluded, MAKE SURE TO RUN 'Delete all CC saves'!

** How to create CC data **
1. Create an empty text file with text editor(CC data file). VS Code is recommended.
2. Repeat this process until you have added all the vehicles needed for the event
2-1. Assign the truck and trailer(optional) in game and save
2-2. Run 'export active vehicle' and paste the clipboard to CC file. You have added a vehicle now.
2-3. Make sure you break a line between each vehicle and there's no empty line in the file.
3. Append 'POSITIONS' in the new line.
4. Repeat this process until you have added all the CC positions needed for the event
(Each CC save consists of three lines from now on)
4-1. Append the name of save of that position in the new line.
4-2. Append the vehicle number(0 means the first vehicle you added above) in the next line.
4-3. Append the position code(generate with 'Share Player Position' utility) in the next line.
5. Append 'END' in the new line.

The file would look like this now.

┌ ASE_VEHICLE <Optional vehicle name(ignored by software)>
└ <Compiled vehicle data>
(repeat every vehicle pair)
POSITIONS
┌ <Save name>
│ <Vehicle number>
└ <Position code>
(repeat every save)
END

6. Now, copy everything in the editor and run 'Compile CC Data'.
7. Share the result file (*.ddd)

** NOTE **
- If you want to place the same truck, but different trailer, or vice-versa, you have to insert another vehicle pair generated with 'export active vehicle' option.";
                while (true) {
                    var res = ListInputBox.Show("ASE CC Tool", document, new string[] { "---- Application ----", "Apply CC Data to this profile", "Delete all CC saves in this profile", "---- Generation ----", "Export Active Vehicle", "Compile CC Data" });
                    if (res == -1) return;
                    if (res == 4) { // Export active vehicle
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

                            sb.AppendLine($"ASE_VEHICLE");

                            var sb0 = new StringBuilder();
                            sb0.AppendLine(trailersToProcess.Count + 1 + "");
                            sb0.AppendLine(assignedTruck);
                            foreach (var trailer in trailersToProcess) {
                                sb0.AppendLine(trailer);
                            }

                            void recurseUnit(UnitEntity e) {
                                sb0.AppendLine(e.GetFullString());

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

                            {
                                var aseVehicleData = AESEncoder.InstanceA.Encode(sb0.ToString());
                                sb.Append(HexEncoder.ByteArrayToHexString(AESEncoder.GetDataChecksum(aseVehicleData)).Substring(0, 6));
                                sb.AppendLine(aseVehicleData);
                            }

                            Clipboard.SetText(sb.ToString());
                        } catch (Exception e) {
                            if (e.Message == "incompatible version") {
                                MessageBox.Show("Data version doesn't match the current version.", "Error");
                            } else {
                                MessageBox.Show($"An error occured.\n{e.GetType().FullName}: {e.Message}\nPlease contact the developer.", "Error");
                            }
                            Console.WriteLine(e);
                        }
                    } else if (res == 1) { // Apply CC Data to this profile
                        var d = new OpenFileDialog() {
                            Title = "Open CC data",
                            Filter = "CC data (*.ddd)|*.ddd|All files (*.*)|*.*",
                            FilterIndex = 0,
                        };
                        if (d.ShowDialog() != true) return;

                        // Decoding the CC data
                        var content = File.ReadAllBytes(d.FileName);
                        var checksum = new byte[32];
                        var data = new byte[content.Length - 32];
                        Buffer.BlockCopy(content, 0, checksum, 0, 32);
                        Buffer.BlockCopy(content, 32, data, 0, data.Length);
                        content = null;

                        if (!AESEncoder.GetDataChecksum(data).SequenceEqual(checksum)) {
                            MessageBox.Show("Corrupted CC data! Checksum failed!");
                            return;
                        }

                        var ms = new MemoryStream(data);
                        var cs = new CryptoStream(ms, AESEncoder.InstanceB.AES.CreateDecryptor(), CryptoStreamMode.Read);
                        var zs = new DeflateStream(ms, CompressionMode.Decompress);
                        var r = new BinaryReader(zs);

                        // Applying CC data to profile where a job is running will cause crash
                        var srcPlayer = saveGame.EntityType("player");
                        if (srcPlayer.Get("current_job").value != "null") {
                            MessageBox.Show("You can't run this action with an active job in your save.");
                            return;
                        }

                        // Injecting data into new saves
                        var namelessIdent = $"_nameless.{DateTime.Now.Ticks % 100000:x}";

                        var vehicleCount = r.ReadInt32();

                        // Parsing data
                        // 1. vehicle data
                        var vehicles = new List<ASEVehicle>();
                        for (int k = 0; k < vehicleCount; k++) {
                            var vehicleData = AESEncoder.InstanceA.Decode(r.ReadString()).Replace("_nameless.", namelessIdent + k + ".");
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
                        var positionCount = r.ReadInt32();

                        // 3. do it
                        var saveToClone = saveGame.ToString();
                        // loading info.sii too
                        var infoSiiPath = saveFile.fullPath + @"\info.sii";
                        var infoContent = File.ReadAllText(infoSiiPath);
                        var infoGame = new SiiSaveGame(infoContent);

                        var startTime = DateTime.Now;
                        for (int k = 0; k < positionCount; k++) {
                            var targetEditedDate = startTime.AddMinutes(k + 1);
                            var saveName = r.ReadString();
                            var vehicleData = vehicles[r.ReadInt32()];
                            var positionData = PositionCodeEncoder.DecodePositionCode(r.ReadString());

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
                                player.ArrayAppend("truck_profit_logs", $"{namelessIdent}.aaaa.bbbb.000");
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
                            // Append profit log entry
                            {
                                var kk = player.InsertAfter("profit_log", $"{namelessIdent}.aaaa.bbbb.000");
                                var newLog = kk;
                                newLog.Set("stats_data", "0");
                                newLog.Set("acc_distance_free", "0");
                                newLog.Set("acc_distance_on_job", "0");
                                newLog.Set("history_age", "nil");
                            }

                            File.WriteAllText(newPath + @"\game.sii", cloned.ToString());
                            File.SetLastWriteTime(newPath + @"\game.sii", targetEditedDate);

                            File.WriteAllText(newPath + @"\ETS2ASE_CC", k + "");

                            Directory.SetLastWriteTime(newPath, targetEditedDate);
                        }
                        MessageBox.Show("Success!");
                        return;
                    } else if (res == 2) { // Delete all CC saves in this profile
                        if (File.Exists(saveFile.fullPath + @"\ETS2ASE_CC")) {
                            MessageBox.Show("Please select a directory that wasn't generated by this tool!");
                            return;
                        }
                        foreach (var dir in Directory.EnumerateDirectories(saveFile.fullPath + @"\..\")) {
                            if (File.Exists(dir + @"\ETS2ASE_CC")) {
                                Directory.Delete(dir, true);
                            }
                        }
                        MessageBox.Show("Success!");
                        return;
                    } else if (res == 5) { // Compile CC Data
                        void CompileData() {
                            var lines = (from a in Clipboard.GetText().Trim().Split('\n') select a.Trim()).ToArray();
                            var len = lines.Length;
                            int i = 0;

                            void WrongFormat(string message = "unspecified error") {
                                var trace = "";
                                int startingLine = Math.Max(0, i - 2);
                                int endingLine = Math.Min(len - 1, i + 2);
                                for (int j = startingLine; j <= endingLine; j++) {
                                    var content = $"{string.Format("{0:000}", j + 1)}: ";
                                    if (j >= 0 && j < len) {
                                        if (lines[j].Length < 25) {
                                            content += lines[j];
                                        } else {
                                            content += lines[j].Substring(0, 25) + "...";
                                        }
                                    } else if (j == len) {
                                        content += "(end of data)";
                                    } else {
                                        content += "(empty)";
                                    }
                                    if (j == i) {
                                        content += "    <<< HERE";
                                    }
                                    trace += content + "\n";
                                }
                                MessageBox.Show($"Compile error: {message}\n\n{trace}\nAborting!", "Compile error!", MessageBoxButton.OK);
                            }

                            var vehicles = new List<string>();
                            var positions = new List<CCDataPosition>();

                            try {
                                // Buffer preparation
                                var ms = new MemoryStream();
                                var bs = new BinaryWriter(ms);

                                // Reading vehicles
                                while (true) {
                                    if (lines[i] == "POSITIONS") {
                                        break;
                                    }
                                    var m = Regex.IsMatch(lines[i], @"^ASE_VEHICLE");
                                    if (!m) {
                                        WrongFormat("Can't find ASE_VEHICLE in the expected position");
                                        return;
                                    }

                                    i++;

                                    if (!Regex.IsMatch(lines[i], @"^[0-9A-F]{7,}$")) {
                                        WrongFormat("Can't find vehicle data");
                                        return;
                                    }

                                    // Next line - Checksum and vehicle data
                                    var cs = lines[i].Substring(0, 6);
                                    var vdHex = lines[i].Substring(6);
                                    if (HexEncoder.ByteArrayToHexString(AESEncoder.GetDataChecksum(vdHex)).Substring(0, 6) != cs) {
                                        WrongFormat("Corrupted vehicle data. Checksum failed.");
                                        return;
                                    }

                                    vehicles.Add(vdHex);

                                    i++;
                                }

                                if (vehicles.Count == 0) {
                                    WrongFormat("empty vehicle data");
                                    return;
                                }

                                i++; // Skip 'POSITIONS' line

                                // Reading positions
                                while (true) {
                                    if (lines[i] == "END") break;

                                    var name = lines[i++];
                                    var vehicleNumber = lines[i++];
                                    if (!Regex.IsMatch(vehicleNumber, @"^\d+$")) {
                                        WrongFormat("not a vehicle number");
                                        return;
                                    }
                                    int vehicleIndex = int.Parse(vehicleNumber);
                                    if (vehicleIndex >= vehicles.Count) {
                                        WrongFormat($"vehicle number out of range (0 - {vehicles.Count - 1})");
                                        return;
                                    }

                                    var positionCode = lines[i++];
                                    try {
                                        PositionCodeEncoder.DecodePositionCode(positionCode);
                                    } catch {
                                        WrongFormat("invalid position code (decode failed)");
                                    }

                                    positions.Add(new CCDataPosition { name = name, vehicleIndex = vehicleIndex, positionCode = positionCode });
                                }

                                if (positions.Count == 0) {
                                    WrongFormat("empty position data");
                                    return;
                                }
                            } catch (IndexOutOfRangeException) {
                                WrongFormat("Unexpected end of data");
                            }

                            byte[] saveData;

                            // Data parse finished
                            // Generation of output data
                            {
                                var ms = new MemoryStream();
                                var cs = new CryptoStream(ms, AESEncoder.InstanceB.AES.CreateEncryptor(), CryptoStreamMode.Write);
                                var zs = new DeflateStream(ms, CompressionLevel.Optimal);
                                var w = new BinaryWriter(zs);

                                w.Write(vehicles.Count);
                                for (int k = 0; k < vehicles.Count; k++) {
                                    w.Write(vehicles[k]);
                                }

                                w.Write(positions.Count);
                                for (int k = 0; k < positions.Count; k++) {
                                    var o = positions[k];
                                    w.Write(o.name); w.Write(o.vehicleIndex); w.Write(o.positionCode);
                                }

                                w.Close();

                                saveData = ms.ToArray();
                            }

                            {
                                var d = new SaveFileDialog() {
                                    Title = "Save CC data",
                                    Filter = "CC data (*.ddd)|*.ddd|All files (*.*)|*.*",
                                    FilterIndex = 0,
                                };
                                if (d.ShowDialog() != true) return;

                                var bytes = new byte[saveData.Length + (256 / 8)];
                                Buffer.BlockCopy(AESEncoder.GetDataChecksum(saveData), 0, bytes, 0, 32);
                                Buffer.BlockCopy(saveData, 0, bytes, 32, saveData.Length);
                                File.WriteAllBytes(d.FileName, bytes);
                            }
                        }
                        CompileData();
                    }
                }
            });
            return new SaveEditTask {
                name = "Event CC Tools",
                run = run,
                description = "Please read the manual inside before using it! Otherwise the consequences can be disastrous!"
            };
        }

        private struct CCDataPosition {
            public string name;
            public int vehicleIndex;
            public string positionCode;
        }

        public SaveEditTask StealCompanyTrailer() {
            var run = new Action(() => {
                try {
                    var economy = saveGame.EntityType("economy");
                    var player = saveGame.EntityType("player", economy.Target.LastFoundStart);

                    var currentJobId = player.Get("current_job").value;
                    if (currentJobId == "null") {
                        MessageBox.Show("You don't have any job now.", "Error");
                        return;
                    }
                    // Current job unit
                    var job = player.EntityIdAround(currentJobId);

                    // Job truck
                    var currentTruckId = player.GetValue("assigned_truck");
                    var isCurrentTruckStealable = job.GetValue("company_truck") == currentTruckId;
                    if (currentTruckId == "null") {
                        MessageBox.Show("You don't have a truck active.", "Error");
                        return;
                    }

                    // Job trailer
                    var currentTrailerId = player.GetValue("assigned_trailer");
                    var isCurrentTrailerStealable = job.GetValue("company_trailer") == currentTrailerId;
                    if (currentTrailerId == "null") {
                        MessageBox.Show("You don't have a trailer connected.", "Error");
                        return;
                    }

                    var stealTruck = isCurrentTruckStealable;
                    var stealTrailer = isCurrentTrailerStealable;

                    if (stealTruck)
                        stealTruck = MessageBox.Show("Do you want to steal the truck?", "Own Job Vehicle", MessageBoxButton.YesNo) == MessageBoxResult.Yes;

                    if (stealTruck) { // Truck steal logic
                        // Create new profit log entry
                        var profitLogId = currentTruckId + "01";

                        // Append profit log entry
                        var newLog = player.InsertAfter("profit_log", profitLogId);
                        newLog.Set("stats_data", "0");
                        newLog.Set("acc_distance_free", "0");
                        newLog.Set("acc_distance_on_job", "0");
                        newLog.Set("history_age", "nil");

                        player.ArrayAppend("trucks", currentTruckId, true);
                        player.ArrayAppend("truck_profit_logs", profitLogId, true);
                    } else if (isCurrentTruckStealable) { // Delete unused job truck
                        var s = UnitIdSelector.Of(currentTruckId);
                        var l = new List<string>();

                        l.AddRange(saveGame.GetUnitItem(s, "accessories").array);

                        l.ForEach((v) => {
                            saveGame.DeleteUnit(UnitIdSelector.Of(v));
                        });

                        saveGame.DeleteUnit(s);
                    }

                    // Return to last position of owned truck, if exists
                    player.Set("assigned_truck", player.Get("my_truck").value);
                    if (player.Get("my_truck_placement_valid").value == "true") {
                        player.Set("truck_placement", player.GetValue("my_truck_placement"));
                        if(player.Contains("my_trailer_placement")) {
                            player.Set("trailer_placement", player.GetValue("my_trailer_placement"));
                        }
                        if(player.Contains("my_slave_trailer_placements")) {
                            player.Set("slave_trailer_placements", player.GetArray("my_slave_trailer_placements"));
                        }
                    }

                    if (stealTruck && stealTrailer)
                        stealTrailer = MessageBox.Show("Do you want to steal the trailer?", "Own Job Vehicle", MessageBoxButton.YesNo) == MessageBoxResult.Yes;

                    if (stealTrailer) { // Trailer steal logic
                        player.ArrayAppend("trailers", currentTrailerId, true);

                        // Adding a dummy accessory with path to "/def/vehicle/trailer_owned/scs.box/data.sii" fixes N/A in trailer listings
                        var trailer = player.EntityIdAround(currentTrailerId);

                        int refundSum = 0;
                        foreach (var item in trailer.GetAllPointers("accessories"))
                        {
                            var red = item.Read();
                            if (int.TryParse(item.Get("refund").value, out int a)) {
                                refundSum += a;
                            }
                        }

                        var newUnitId = currentTrailerId + "02";
                        var newUnit = trailer.InsertAfter("vehicle_accessory", newUnitId);
                        newUnit.Set("data_path", "\"/def/vehicle/trailer_owned/scs.box/data.sii\"");
                        int refund = new Random().Next() % 2 == 0 ? 299792458 : 602214076;
                        newUnit.Set("refund", (refund - refundSum).ToString());

                        trailer.ArrayAppend("accessories", newUnitId, true);
                    } else if (isCurrentTrailerStealable) { // The user decided to only steal the truck and abandon the trailer. Delete the unused job trailer.
                        var s = UnitIdSelector.Of(currentTrailerId);
                        var l = new List<string>();

                        l.AddRange(saveGame.GetUnitItem(s, "accessories").array);

                        l.ForEach((v) => {
                            saveGame.DeleteUnit(UnitIdSelector.Of(v));
                        });

                        saveGame.DeleteUnit(s);
                    }

                    if (player.Get("my_trailer_attached").value == "true") {
                        player.Set("assigned_trailer", player.Get("my_trailer").value);
                        player.Set("assigned_trailer_connected", "true");
                    } else {
                        player.Set("assigned_trailer", "null");
                        player.Set("assigned_trailer_connected", "false");
                    }

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

                    // Delete the job and reset navigation
                    player.Set("current_job", "null");
                    job.Delete();
                    DestroyNavigationData(economy);

                    saveFile.Save(saveGame.ToString());
                    if (stealTruck && stealTrailer) {
                        MessageBox.Show("The truck and trailer are yours now. You can relocate them as needed.\n\nWARNING: The game will buggy until you reloate the new truck to any garage slot!", "Done!");
                    } else if (stealTrailer) {
                        MessageBox.Show("The trailer is yours now. You can relocate the trailer as needed.", "Done!");
                    } else if (stealTruck) {
                        MessageBox.Show("The truck is yours now.\n\nWARNING: The game will buggy until you reloate the new truck to any garage slot!", "Done!");
                    } else {
                        MessageBox.Show("The job has been canceled without removing cargo accessory.");
                    }
                    return;
                } catch (Exception e) {
                    MessageBox.Show("An error occured.", "Error");
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "Own Job Vehicle",
                run = run,
                description = "You can steal the trailer or truck from the job. If the trailer is yours, you can steal the cargo loaded in it."
            };
        }

        private void DestroyNavigationData(UnitEntity economy) {
            var i = economy.GetAllPointers("stored_gps_ahead_waypoints");
            foreach (var t in i) {
                t.Delete();
            }
            economy.Set("stored_gps_ahead_waypoints", "0");

            var registry = economy.GetPointer("registry");
            var regData = registry.GetArray("data");
            if (regData.Length >= 3) regData[0] = "0";
            registry.Set("data", regData);
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
                name = "Adjust Trailer Cargo Mass",
                run = run,
                description = "Modify the cargo mass of the assigned trailer according to your preference."
            };
        }
    }
}
