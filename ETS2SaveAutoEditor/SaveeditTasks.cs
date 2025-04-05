using ASE.Utils;
using ASE.SII2Parser;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ASE {
    public class SaveeditTasks {
        public void SetSaveFile(ProfileSave file) {
            saveFile = file;
            saveGame = new(SIIParser2.Parse(saveFile.content));

            saveFile.Save(saveGame);
            if (saveGame.Reader.StructureData is Dictionary<int, BSIIStruct> dictionary) {
                // Save structure data next to the save file
                BSIIStructureDumper.WriteStructureDataTo(saveFile.GetWriter("bsii.txt"), dictionary);
            }
        }

#pragma warning disable CS8618
        private ProfileSave saveFile;
        private Game2 saveGame;
#pragma warning restore CS8618

        public SaveEditTask MoneySet() {
            var run = new Action(() => {
                try {
                    var bank = saveGame.EntityType("bank")!;
                    var currentBank = bank.GetValue("money_account");

                    var specifiedCash = NumberInputBox.Show("Specify cash", "Please specify the new cash.\nCurrent balance: " + currentBank + "\nCaution: Too high value may crash the game. Please be careful.");

                    if (specifiedCash == -1) {
                        return;
                    }

                    bank.Set("money_account", specifiedCash.ToString());
                    saveFile.Save(saveGame);
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
                    var economy = saveGame.EntityType("economy")!;
                    var currentExp = economy.GetValue("experience_points");

                    var specifiedExp = NumberInputBox.Show("Specify EXP", "Please specify the new exps.\nCurrent exps: " + currentExp + "\nCaution: Too high value may crash the game. Please be careful.");

                    if (specifiedExp == -1) {
                        return;
                    }

                    economy.Set("experience_points", specifiedExp.ToString());
                    saveFile.Save(saveGame);
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
                    var economy = saveGame.EntityType("economy")!;

                    var msgBoxRes = MessageBox.Show("Unlock GUIs such as skills. This can even unlock some items which is supposed to be disabled. Would you like to proceed?", "Unlock", MessageBoxButton.OKCancel);
                    if (msgBoxRes == MessageBoxResult.Cancel) {
                        return;
                    }

                    economy.Set("screen_access_list", "0");
                    saveFile.Save(saveGame);
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

        public SaveEditTask WinterlandPortals() {
            var run = new Action(() => {
                try {
                    var economy = saveGame.EntityType("economy")!;

                    string[] WINTERLAND_HOUSES = [
                        "company.volatile.x_choco.x_choco",
                        "company.volatile.x_market.x_market",
                        "company.volatile.x_mountain.x_mountain",
                        "company.volatile.x_post.x_post",
                        "company.volatile.x_work.x_work"
                        ];

                    if (!economy.ArrayContains("companies", WINTERLAND_HOUSES[0])) {
                        // The event is over. Task not applicable.
                        MessageBox.Show("The Winterland event is over. This task is not applicable.", "Error");
                    }

                    var player = saveGame.EntityType("player")!;
                    if (!player.TryGetPointer("current_job", out var currentJob)) {
                        MessageBox.Show("No job found. Please follow the instructions.", "Error");
                        return;
                    }

                    // set 'target_company' of currentJob to any Winterland house (chosen randomly)
                    currentJob.Set("target_company", WINTERLAND_HOUSES[new Random().Next(WINTERLAND_HOUSES.Length)]);

                    // Remove the job reward by setting 'planned_distance_km' to 0
                    currentJob.Set("planned_distance_km", "0");

                    // Set 'time_upper_limit' to UInt32 max
                    currentJob.Set("time_upper_limit", "4294967295");

                    // Remove all possible online job connection (1) set 'online_job_id' to UInt64 max, (2) set 'online_job_trailer_model' to null
                    currentJob.Set("online_job_id", "18446744073709551615");
                    currentJob.Set("online_job_trailer_model", "null");

                    // Erase navigation data about the original job
                    DestroyNavigationData(economy);

                    saveFile.Save(saveGame);
                    MessageBox.Show("Done!", "Done");
                } catch (Exception e) {
                    MessageBox.Show("An unexpected error occured.", "Error");
                    Console.WriteLine(e);
                    throw;
                }
            });
            return new SaveEditTask {
                name = "Make Winterland Portals",
                run = run,
                description = "Creates portals to Winterland which you normally need WoT jobs to enter.\n\nYOU MUST FOLLOW THE INSTSRUCTION:\n1. Take any job in the game.\n2. Save the game and load it in ase.\n3. Run this task and load the save. It's normal that the navigation's reset.\n4. Move to the nearest portal and teleport.\n5. Cancel the job. The job is broken."
            };
        }

        public SaveEditTask TruckEngineSet() {
            var run = new Action(() => {
                try {
                    var player = saveGame.EntityType("player")!;
                    var assignedTruckId = player.GetValue("assigned_truck");

                    if (assignedTruckId == "null") {
                        MessageBox.Show("You're not driving a truck now.", "Error");
                        return;
                    }

                    var engineNames = new string[] { "Volvo FH6 780 hp", "Volvo old 750 hp", "Scania Electric 450 kW", "Renault Electric 490 kW", "Scania new 730 hp", "Scania old 730 hp", "Iveco 310" };
                    var enginePaths = new string[] {
                        "/def/vehicle/truck/volvo.fh_2024/engine/d17a780.sii",
                        "/def/vehicle/truck/volvo.fh16/engine/d16g750.sii",
                        "/def/vehicle/truck/scania.s_2024e/engine/450kw.sii",
                        "/def/vehicle/truck/renault.etech_t/engine/490kw.sii",
                        "/def/vehicle/truck/scania.s_2016/engine/dc16_730.sii",
                        "/def/vehicle/truck/scania.streamline/engine/dc16_730_2.sii",
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

                    var truck = saveGame[assignedTruckId];
                    var accessories = truck.GetAllPointers("accessories");
                    foreach (var accessory in accessories) {
                        if (Regex.IsMatch(accessory.GetValue("data_path"), @"""\/def\/vehicle\/truck\/[^/]+?\/engine\/")) {
                            accessory.Set("data_path", $"\"{enginePath}\"");
                        }
                    }

                    saveFile.Save(saveGame);
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
                    var player = saveGame.EntityType("player")!;

                    Entity2 assignedTruck;
                    if (!player.TryGetPointer("assigned_truck", out assignedTruck!)) {
                        MessageBox.Show("You don't have any truck assigned.", "Done");
                        return;
                    }

                    assignedTruck.Set("fuel_relative", "1");

                    saveFile.Save(saveGame);
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
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [SupportedOSPlatform("Windows")]
        public SaveEditTask RefuelNow() {
            var run = new Action(() => {
                try {
                    var proc = Process.GetProcessesByName("eurotrucks2").FirstOrDefault();
                    ArgumentNullException.ThrowIfNull(proc, "ETS2 is not running.");

                    SCSMemoryReader reader = new("eurotrucks2");
                    var ba = reader.GetBaseAddress(null);

                    var gameVersion = Encoding.UTF8.GetString(reader.Read(ba + 0x2046759, 24));
                    if (!gameVersion.StartsWith("1.53.3.14", StringComparison.Ordinal)) {
                        MessageBox.Show("This tool only supports ETS2 1.53.3.14. As it directly modifies the memory, it is disabled for other versions.", "Error");
#if !DEBUG
                        return;
#endif
                    }

                    var current = BitConverter.ToSingle(reader.ReadPath(ba + 0x2F57B38, [0x2870, 0x20, 0xE8, 0x148, 0x178], 4));
                    if (!(current >= 0 && current <= 1)) { // Out of range hap
                        MessageBox.Show("The current amount of fuel is out of range (0 - 1). This can happen if the current version isn't supported, or because of save editing. For safety, the refuel operation is cancelled.\n\nPlease use 'Refuel current vehicle' tool first and try again.", "Error");
                        return;
                    }
                    reader.WritePath(ba + 0x2F57B38, [0x2870, 0x20, 0xE8, 0x148, 0x178], BitConverter.GetBytes((float)1));
                    //MessageBox.Show("Done", "Done");
                    // Instead of showing Done, focus on the game window

                    if (proc != null) {
                        SetForegroundWindow(proc.MainWindowHandle);
                        ShowWindow(proc.MainWindowHandle, 9); // SW_RESTORE

                        Console.Beep(660, 150); // First tone: A5 (880 Hz), duration: 150 ms
                        Console.Beep(880, 150); // Second tone: C6 (1046 Hz), duration: 150 ms
                    }

                } catch (Exception) {
                    MessageBox.Show("Failed to modify the memory. Please use 'Refuel current vehicle' tool instead.", "Error");
                }
            });
            return new SaveEditTask {
                name = "Refuel now!",
                run = run,
                description = "Refuel the current vehicle 'in the running ETS2'. Not supported for ATS yet. Only supports ETS2 1.53."
            };
        }
        public SaveEditTask FixEverything() { // Needs rework due to binary sii support
            var run = new Action(() => {
                foreach (var e in saveGame.Reader) {
                    foreach (var f in e) {
                        if (f.Contains("_wear", StringComparison.Ordinal) || f.Contains("integrity_odometer", StringComparison.Ordinal)) {
                            e[f] = new RawDataValue2("0");
                        }
                    }
                }
                saveFile.Save(saveGame);
                MessageBox.Show("Repaired all truck/trailers.", "Done");
                // Old code
                //try {
                //    var lines = saveFile.content.Split('\n');
                //    var fs = new FileStream(saveFile.fullPath + @"\game.sii", FileMode.OpenOrCreate);
                //    var sw = new StreamWriter(fs, Encoding.UTF8);

                //    var p = new Regex(@"(([a-z_]*_wear(?:_unfixable)?(?:\[\d*\])?|integrity_odometer(?:_float_part)?):) (.*)\b", RegexOptions.Compiled);
                //    for (int i = 0; i < lines.Length; i++) {
                //        var str = lines[i];

                //        if (str.Contains("disco") || str.Contains("unlock") || str.Contains("{") || str.Contains("}") || (!str.Contains("wear") && !str.Contains("integrity_odometer")) || !p.IsMatch(str)) {
                //            sw.Write(str);
                //            sw.Write('\n');
                //            continue;
                //        }

                //        sw.Write(p.Replace(str, "$1 0"));
                //        sw.Write('\n');
                //    }
                //    sw.Close();
                //    fs.Close();

                //    MessageBox.Show("Repaired all truck/trailers.", "Done");
                //} catch (Exception e) {
                //    MessageBox.Show("An unknown error occured.", "Error");
                //    Console.WriteLine(e);
                //    throw;
                //}
            });
            return new SaveEditTask {
                name = "Repair All",
                run = run,
                description = "Repairs all trucks and trailers in the current savegame.\n\n1.49 Warning - This will even repair trucks in used truck dealer!"
            };
        }

        public SaveEditTask ShareNavigation() {
            var run = new Action(() => {
                try {
                    var economy = saveGame.EntityType("economy")!;
                    var waypointsBehind = economy.GetAllPointers("stored_gps_behind_waypoints");
                    var waypointsAhead = economy.GetAllPointers("stored_gps_ahead_waypoints");
                    var avoids = economy.GetAllPointers("stored_gps_avoid_waypoints");

                    byte DirectionToByte(string direction) { // enum<any=2, backward=1, forward=0>
                        if (direction == "forward") {
                            return 0;
                        } else if (direction == "backward") {
                            return 1;
                        } else {
                            return 2;
                        }
                    }

                    List<(byte, int, int, int)> readPointStorage(Entity2[] units) {
                        List<(byte, int, int, int)> res = [];
                        // Parse '(0, 0, 0)' format
                        foreach (var unit in units) {
                            byte direction = DirectionToByte(unit.GetValue("direction"));
                            var pos = unit.GetValue("nav_node_position");
                            var posArr = pos[1..^1].Split(',');
                            // trim spaces
                            for (int i = 0; i < posArr.Length; i++) {
                                posArr[i] = posArr[i].Trim();
                            }

                            //MessageBox.Show(int.Parse(posArr[0]) + " " + int.Parse(posArr[1]) + " " + int.Parse(posArr[2]), "Debug");

                            res.Add((direction, int.Parse(posArr[0]), int.Parse(posArr[1]), int.Parse(posArr[2])));
                        }
                        return res;
                    }

                    if (waypointsBehind is null || waypointsAhead is null || avoids is null) {
                        // Corrupt save.
                        MessageBox.Show("The navigation data is corrupted. Please remove all waypoints and try again.", "Error");
                        return;
                    }

                    if (waypointsBehind.Length > 256 || waypointsAhead.Length > 256 || avoids.Length > 256) { // Never happens because of in-game limit. Just in case.
                        MessageBox.Show("The number of waypoints exceeds the limit of 256. Please remove some waypoints and try again.", "Error");
                        return;
                    }

                    NavigationData data = new() {
                        WaypointBehind = readPointStorage(waypointsBehind),
                        WaypointAhead = readPointStorage(waypointsAhead),
                        Avoid = readPointStorage(avoids)
                    };

                    string encodedData = NavigationDataEncoder.EncodeNavigationCode(data);
                    Clipboard.SetText(encodedData);
                    MessageBox.Show($"Successfully encoded {waypointsBehind.Length} / {waypointsAhead.Length} waypoint(s) and {avoids.Length} avoid point(s)!\n\nThe navigation data was copied to clipboard.", "Complete!");
                } catch (Exception e) {
                    if (e.Message == "incompatible version") {
                        MessageBox.Show("Data version doesn't match the current version.", "Error");
                    } else {
                        if (e.Message.Contains("Clipboard", StringComparison.Ordinal)) {
                            MessageBox.Show($"There was an error while copying the position code. However, it may have already worked. Please check your clipboard.", "Complete!");
                        } else {
                            MessageBox.Show($"An error occured.\n{e.GetType().FullName}: {e.Message}\nPlease contact the developer.", "Error");
                        }
                    }
                    Console.WriteLine(e);
                }
            });
            return new SaveEditTask {
                name = "Share Navigation",
                run = run,
                description = "Shares the current navigation waypoints with others by copying it to the clipboard."
            };
        }
        public SaveEditTask ImportNavigation() {
            var run = new Action(() => {
                try {
                    var economy = saveGame.EntityType("economy")!;

                    var navigationData = NavigationDataEncoder.DecodeNavigationCode(Clipboard.GetText().Trim());

                    string ByteToDirection(byte b) {
                        return b switch {
                            0 => "forward",
                            1 => "backward",
                            _ => "any"
                        };
                    }

                    (string direction, string nav_node_position) EntryToStr((byte, int, int, int) entry) {
                        return (ByteToDirection(entry.Item1), $"({entry.Item2}, {entry.Item3}, {entry.Item4})");
                    }

                    Entity2 AppendGps((byte, int, int, int) entry) {
                        var newGps = saveGame.CreateNewUnit("gps_waypoint_storage");
                        newGps.Set("nav_node_position", EntryToStr(entry).nav_node_position);
                        newGps.Set("direction", EntryToStr(entry).direction);
                        return newGps;
                    }

                    // Erase navigation data first
                    foreach (var item in economy.GetAllPointers("stored_gps_behind_waypoints")) {
                        CommonEdits.DeleteUnitRecursively(item, []);
                    }
                    economy.Set("stored_gps_behind_waypoints", []);

                    foreach (var item in economy.GetAllPointers("stored_gps_ahead_waypoints")) {
                        CommonEdits.DeleteUnitRecursively(item, []);
                    }
                    economy.Set("stored_gps_ahead_waypoints", []);

                    foreach (var item in economy.GetAllPointers("stored_gps_avoid_waypoints")) {
                        CommonEdits.DeleteUnitRecursively(item, []);
                    }
                    economy.Set("stored_gps_avoid_waypoints", []);

                    // Write new navigation data
                    foreach (var item in navigationData.WaypointBehind) {
                        economy.ArrayAppend("stored_gps_behind_waypoints", AppendGps(item).Id);
                    }
                    foreach (var item in navigationData.WaypointAhead) {
                        economy.ArrayAppend("stored_gps_ahead_waypoints", AppendGps(item).Id);
                    }
                    // Avoid points
                    foreach (var item in navigationData.Avoid) {
                        economy.ArrayAppend("stored_gps_avoid_waypoints", AppendGps(item).Id);
                    }

                    // Set registry data[0] to '5' which means custom route
                    var registry = saveGame.EntityType("economy")!.GetPointer("registry");
                    var arr = registry.GetArray("data");
                    arr[0] = navigationData.WaypointAhead.Count > 0 ? "5" : "0";
                    registry.Set("data", arr);

                    saveFile.Save(saveGame);
                    MessageBox.Show($"Successfully imported the navigation data!\n\n{navigationData.WaypointBehind.Count} / {navigationData.WaypointAhead.Count} waypoint(s) and {navigationData.Avoid.Count} avoid point(s) were imported.", "Complete!");
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
                name = "Import Navigation",
                run = run,
                description = "Imports the shared navigation waypoints from clipboard."
            };
        }

        public SaveEditTask SharePosition() {
            var run = new Action(() => {
                try {
                    var player = saveGame.EntityType("player")!;

                    List<float[]> positions = [];

                    string truckPlacement = player.GetValue("truck_placement");
                    positions.Add(SCSSpecialString.DecodeSCSPosition(truckPlacement));

                    var trailerAssigned = player.GetValue("assigned_trailer") != "null";
                    if (trailerAssigned) {
                        string trailerPlacement = player.GetValue("trailer_placement");
                        positions.Add(SCSSpecialString.DecodeSCSPosition(trailerPlacement));
                    }

                    if (player.TryGetArray("slave_trailer_placements", out var slaveTrailers)) {
                        foreach (var slave in slaveTrailers) {
                            positions.Add(SCSSpecialString.DecodeSCSPosition(slave));
                        }
                    }

                    var trailerConnected = player.GetValue("assigned_trailer_connected") == "true";
                    if (positions.Count == 1) trailerConnected = true;

                    string encodedData = PositionCodeEncoder.EncodePositionCode(new PositionData {
                        TrailerConnected = trailerConnected,
                        MinifiedOrientation = false,
                        Positions = positions
                    });
                    Clipboard.SetText(encodedData);
                    MessageBox.Show($"The location of your truck, trailer was copied to the clipboard.\nNumber of vehicles in the code: {positions.Count}, Connected to trailer: {(trailerConnected ? "Yes" : "No")}", "Complete!");
                } catch (Exception e) {
                    if (e.Message == "incompatible version") {
                        MessageBox.Show("Data version doesn't match the current version.", "Error");
                    } else {
                        if (e.Message.Contains("Clipboard", StringComparison.Ordinal)) {
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
                    var player = saveGame.EntityType("player")!;

                    var positionData = PositionCodeEncoder.DecodePositionCode(Clipboard.GetText().Trim());
                    var decoded = (from a in positionData.Positions select SCSSpecialString.EncodeSCSPosition(a)).ToArray();
                    if (decoded.Length >= 1) {
                        player.Set("truck_placement", decoded[0]);
                    }
                    if (decoded.Length >= 2) {
                        player.Set("trailer_placement", decoded[1]);
                    } else {
                        player.Set("trailer_placement", decoded[0]);
                    }
                    if (decoded.Length > 2) {
                        player.Set("slave_trailer_placements", [.. decoded.Skip(2)]);
                    } else {
                        player.Set("slave_trailer_placements", "0");
                    }

                    player.Set("assigned_trailer_connected", positionData.TrailerConnected ? "true" : "false");

                    DestroyNavigationData(saveGame.EntityType("economy")!);

                    saveFile.Save(saveGame);
                    MessageBox.Show($"Successfully imported the position code!\nNumber of vehicles in the code: {decoded.Length}, Connected to trailer: {(positionData.TrailerConnected ? "Yes" : "No")}", "Complete!");
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
                    positionData.Positions = [
                        a
                    ];

                    positionData.TrailerConnected = true; // Always true because we're erasing where trailer was connected. If it's not connected, the game wouldn't know where to place the trailer.
                    positionData.MinifiedOrientation = false; // Remove X and Z components of the quaternion in orientation. This reduces the length of the code without affecting the accuracy.

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
                    for (int i = 0; i < decoded.Length; i++) {
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
                    var economy = saveGame.EntityType("economy")!;
                    var player = saveGame.EntityType("player")!;

                    var truckPlacement = player.GetValue("truck_placement");
                    player.Set("trailer_placement", truckPlacement);
                    player.Set("slave_trailer_placements", "0");
                    player.Set("assigned_trailer_connected", "true");

                    foreach (var item in economy.GetAllPointers("stored_gps_behind_waypoints")) {
                        item.DeleteSelf();
                    }
                    foreach (var item in economy.GetAllPointers("stored_gps_ahead_waypoints")) {
                        item.DeleteSelf();
                    }
                    economy.Set("stored_gps_behind_waypoints", "0");
                    economy.Set("stored_gps_ahead_waypoints", "0");

                    var registry = economy.GetPointer("registry");
                    var regData = registry.GetArray("data");
                    regData[0] = "0";
                    registry.Set("data", regData);

                    saveFile.Save(saveGame);
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
                    var choice = ListInputBox.Show("Vehicle Sharing", "WARNING: Importing an imcompatible vehicle (DLC, MOD, or incompatible version) will break your save. Click 'Cancel' to close this window.", ["Share Truck", "Share Trailer", "Import Vehicle", "Don't use this"]);
                    if (choice == -1) break;

                    if (choice == 3) { // SII2 reader test
                        // Measure the execution time
                        var sw = new Stopwatch();
                        sw.Start();

                        SII2 reader = SIIParser2.Parse(saveFile.content);

                        sw.Stop();

                        Console.WriteLine("Total entities: " + reader.Count);
                        Console.WriteLine("First 5 units:");
                        for (int i = 0; i < Math.Min(reader.Count, 5); i++) {
                            var unit = reader[i];
                            Console.WriteLine($"[{i}] {unit.Type} : {unit.Id} ({unit.Count} entries) {{");
                            for (int j = 0; j < Math.Min(unit.Count, 5); j++) {
                                Console.WriteLine($"  [{j}] {unit[j]}: {unit[unit[j]]}");
                            }
                            Console.WriteLine("}\n");
                        }

                        Console.WriteLine("SII2 Parsing complete. Elapsed: " + sw.ElapsedMilliseconds + "ms");
                        Console.WriteLine("Trying to export the parsed SII to output.sii");

                        sw.Restart();

                        FileStream sr = new("output.sii", FileMode.Create);
                        StreamWriter swr = new(sr, BetterThanStupidMS.UTF8);
                        reader.WriteTo(swr);
                        swr.Close();

                        Console.WriteLine("Writing complete. Elapsed: " + sw.ElapsedMilliseconds + "ms");
                    }
                    if (choice == 0) { // Export truck
                        Stopwatch sw = new();
                        sw.Start();
                        Console.WriteLine($"[{sw.Elapsed.TotalNanoseconds}ns] Sharing truck...");

                        var player = saveGame.EntityType("player")!;

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
                        Console.WriteLine($"[{sw.Elapsed.TotalNanoseconds}ns] Wrote the header");
                        var vehicleStr = UnitSerializer.SerializeUnit(assignedTruck, UnitSerializer.KNOWN_PTR_ITEMS_TRUCK);

                        Console.WriteLine($"[{sw.Elapsed.TotalNanoseconds}ns] Serialized the truck. Opening the dialog...");

                        var d = new SaveFileDialog() {
                            Title = "Save Truck",
                            Filter = "ASE Vehicle Data (*.asv)|*.asv|All files (*.*)|*.*",
                            FilterIndex = 0,
                        };
                        if (d.ShowDialog() != true) continue;

                        File.WriteAllText(d.FileName, headerStr + vehicleStr, Encoding.UTF8);
                    }
                    if (choice == 1) { // Export trailer
                        var player = saveGame.EntityType("player")!;

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
                        var vehicleStr = UnitSerializer.SerializeUnit(assignedTrailer, UnitSerializer.KNOWN_PTR_ITEMS_TRAILER);

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

                        var player = saveGame.EntityType("player")!;
                        var entities = UnitSerializer.DeserializeUnit(text, saveGame);

                        // Add entities to the savegame
                        foreach (var entity in entities) {
                            saveGame.Add(entity);
                        }

                        // Finish up necessary things to prevent crash
                        if (isTruck) {
                            // Add vehicle to truck list
                            // Add empty profit log entry
                            // Remind the user to assign it to a garage to prevent bugs that are confirmed to exist

                            if (entities[0].Unit.Type != "vehicle") {
                                MessageBox.Show($"Unexpected root node. Expected 'vehicle' but got '{entities[0].Unit.Type}'.", "Error");
                                continue;
                            }

                            string truckId = entities[0].Unit.Id;
                            string profitLogId = entities[0].Unit.Id + "0";
                            player.ArrayAppend("trucks", truckId, true);

                            var newLog = saveGame.CreateNewUnit("profit_log", profitLogId);
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

                            if (entities[0].Unit.Type != "trailer") {
                                MessageBox.Show($"Unexpected root node. Expected 'trailer' but got '{entities[0].Unit.Type}'.", "Error");
                                continue;
                            }

                            string trailerId = entities[0].Unit.Id;
                            string trailerDefId = entities[0].GetValue("trailer_definition");

                            player.ArrayAppend("trailers", trailerId, true);
                            if (trailerDefId.StartsWith('_')) {
                                player.ArrayAppend("trailer_defs", trailerDefId, true);
                            }

                            MessageBox.Show("Successfully imported the trailer!\n\nPlease assign the trailer to a garage to prevent possible bugs.", "Complete!");
                        }

                        saveFile.Save(saveGame);
                    }
                }
            });
            return new SaveEditTask {
                name = "[NEW] Vehicle Sharing",
                run = run,
                description = "You can use this tool to easily share and import trucks and trailers without worrying about collisions and errors."
            };
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
                    var res = ListInputBox.Show("ASE CC Tool", document, ["---- Application ----", "Apply CC Data to this profile", "Delete all CC saves in this profile", "---- Generation ----", "Export Active Vehicle", "Compile CC Data"]);
                    if (res == -1) return;
                    if (res == 4) { // Export active vehicle
                        try {
                            var economy = saveGame.EntityType("economy")!;
                            var player = saveGame.EntityType("player")!;

                            var sb = new StringBuilder();
                            sb.Append("ASE_VEHICLE\n");

                            Entity2 assignedTruck;
                            if (!player.TryGetPointer("assigned_truck", out assignedTruck!)) {
                                MessageBox.Show("You need to have an assigned truck.");
                                return;
                            }

                            player.TryGetPointer("assigned_trailer", out Entity2? assignedTrailer);

                            // Encode the vehicle into binary data
                            MemoryStream memory = new();
                            memory.WriteByte((byte)(assignedTrailer is not null ? 1 : 0));

                            // Encode truck
                            var vehicleStr = UnitSerializer.SerializeUnit(assignedTruck, UnitSerializer.KNOWN_PTR_ITEMS_TRUCK);
                            byte[] buf = Encoding.UTF8.GetBytes(vehicleStr);
                            memory.Write(ByteEncoder.EncodeUInt32((uint)buf.Length, ByteOrder.BigEndian));
                            memory.Write(buf);

                            // Encode trailer
                            if (assignedTrailer is not null) {
                                var trailerStr = UnitSerializer.SerializeUnit(assignedTrailer, UnitSerializer.KNOWN_PTR_ITEMS_TRAILER);
                                buf = Encoding.UTF8.GetBytes(trailerStr);
                                memory.Write(ByteEncoder.EncodeUInt32((uint)buf.Length, ByteOrder.BigEndian));
                                memory.Write(buf);
                            }

                            // Encode the data
                            var finalData = AESEncoder.InstanceA.Encode(memory.ToArray());
                            sb.Append(HexEncoder.ByteArrayToHexString(AESEncoder.GetDataChecksum(finalData))[0..6]);
                            sb.AppendLine(HexEncoder.ByteArrayToHexString(finalData));

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
                        var srcPlayer = saveGame.EntityType("player")!;
                        if (srcPlayer.GetValue("current_job") != "null") {
                            MessageBox.Show("You can't run this action with an active job in your save.");
                            return;
                        }

                        var vehicleCount = r.ReadInt32();

                        // Parsing data
                        // 1. vehicle data
                        var vehicles = new List<(string[] keys, Entity2[] units)>();
                        for (int k = 0; k < vehicleCount; k++) {
                            // Decode the vehicle data
                            var toRead = r.ReadUInt32();
                            uint bytesRead = 0;
                            var buf = new byte[toRead];
                            while (toRead > 0) {
                                uint read = (uint)r.Read(buf, (int)bytesRead, (int)toRead);
                                bytesRead += read;
                                toRead -= read;
                                if (read == 0 && toRead > 0) throw new Exception("Unexpected end of stream while reading vehicle data.");
                            }
                            var r2 = new MemoryStream(AESEncoder.InstanceA.Decode(buf));
                            bool hasTrailer = r2.ReadByte() > 0;

                            List<Entity2> units = [];
                            string[] keys = new string[hasTrailer ? 2 : 1];

                            for (int i = 0; i < (hasTrailer ? 2 : 1); i++) {
                                buf = new byte[4];
                                r2.Read(buf, 0, 4);
                                int len = (int)ByteEncoder.DecodeUInt32(buf, ByteOrder.BigEndian);

                                buf = new byte[len];
                                r2.Read(buf, 0, len);
                                var a = UnitSerializer.DeserializeUnit(Encoding.UTF8.GetString(buf));
                                units.AddRange(a);
                                keys[i] = a[0].Unit.Id;
                            }

                            vehicles.Add((keys, [.. units]));
                        }

                        // 2. position data - inject as progresses
                        var positionCount = r.ReadInt32();

                        // 3. do it
                        var saveToClone = saveGame.ToString();
                        // loading info.sii too
                        var infoSiiPath = saveFile.fullPath + @"\info.sii";
                        var infoContent = File.ReadAllBytes(infoSiiPath);
                        var reader = SIIParser2.Parse(infoContent);
                        var infoGame = new Game2(reader);

                        var startTime = DateTime.Now;
                        for (int k = 0; k < positionCount; k++) {
                            var targetEditedDate = startTime.AddMinutes(k + 1);
                            var saveName = r.ReadString();
                            var (keys, units) = vehicles[r.ReadInt32()];
                            var positionData = PositionCodeEncoder.DecodePositionCode(r.ReadString());

                            // output path
                            int i = 1;
                            while (Directory.Exists(saveFile.fullPath + @"\..\" + i)) i++;
                            var newPath = saveFile.fullPath + @"\..\" + i;
                            Directory.CreateDirectory(newPath);

                            // info.sii
                            infoGame.EntityType("save_container")!.Set("name", $@"""{SCSSaveHexEncodingSupport.GetEscapedSaveName(saveName)}""");
                            File.WriteAllText(newPath + @"\info.sii", infoGame.ToString());
                            File.SetLastWriteTime(newPath + @"\info.sii", targetEditedDate);

                            // game.sii
                            SII2 clonedReader = SII2SiiNDecoder.Decode(saveToClone);
                            Game2 cloned = new(clonedReader);

                            foreach (var vehicle in units) {
                                vehicle.Unit.___detach_do_not_use(); // In this function, same units are attached to multiple saves. Since old save files aren't used after saving, we can safely move units to new save.
                            }

                            cloned.AddAll(units);

                            Entity2 player = cloned.EntityType("player")!;

                            // Add the vehicle to truck list
                            player.ArrayAppend("trucks", keys[0]);

                            // Activate the truck
                            player.Set("assigned_truck", keys[0]);
                            player.Set("my_truck", keys[0]);

                            // Prevent game CTD bug
                            string dummyPfLogId = cloned.GenerateNewID();
                            player.ArrayAppend("truck_profit_logs", dummyPfLogId);
                            var u = cloned.CreateNewUnit("profit_log", dummyPfLogId);
                            u.Set("stats_data", "0");
                            u.Set("acc_distance_free", "0");
                            u.Set("acc_distance_on_job", "0");
                            u.Set("history_age", "nil");

                            if (keys.Length == 2) { // Add the vehicle to trailer list
                                player.ArrayAppend("trailers", keys[1], true);

                                player.Set("assigned_trailer", keys[1]);
                                player.Set("my_trailer", keys[1]);

                                string trailerDefId = (from a in units where a.Unit.Id == keys[1] select a).First().GetValue("trailer_definition");

                                if (trailerDefId.StartsWith('_')) {
                                    player.ArrayAppend("trailer_defs", trailerDefId, true);
                                }
                            } else {
                                player.Set("assigned_trailer", "null");
                                player.Set("my_trailer", "null");
                            }

                            // copy paste of InjectLocation
                            {
                                var decoded = (from a in positionData.Positions select SCSSpecialString.EncodeSCSPosition(a)).ToArray();
                                if (decoded.Length >= 1) {
                                    player.Set("truck_placement", decoded[0]);
                                }
                                if (decoded.Length >= 2) {
                                    player.Set("trailer_placement", decoded[1]);
                                } else {
                                    player.Set("trailer_placement", decoded[0]);
                                }
                                if (decoded.Length > 2) {
                                    player.Set("slave_trailer_placements", [.. decoded.Skip(2)]);
                                } else {
                                    player.Set("slave_trailer_placements", "0");
                                }

                                player.Set("assigned_trailer_connected", positionData.TrailerConnected ? "true" : "false");
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
                                            content += lines[j][..25] + "...";
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

                            var vehicles = new List<byte[]>();
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
                                    var checksum = lines[i][0..6];
                                    var vehicleDataHex = lines[i][6..];
                                    if (HexEncoder.ByteArrayToHexString(AESEncoder.GetDataChecksum(HexEncoder.HexStringToByteArray(vehicleDataHex)))[0..6] != checksum) {
                                        WrongFormat("Corrupted vehicle data. Checksum failed.");
                                        return;
                                    }

                                    vehicles.Add(HexEncoder.HexStringToByteArray(vehicleDataHex)); // Exclude the checksum because there will be one for the whole data

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
                                var w = new BinaryWriter(zs, Encoding.UTF8);

                                w.Write(vehicles.Count);
                                for (int k = 0; k < vehicles.Count; k++) {
                                    w.Write((uint)vehicles[k].Length);
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
                    var economy = saveGame.EntityType("economy")!;
                    var player = saveGame.EntityType("player")!;

                    // Current job unit
                    if (!player.TryGetPointer("current_job", out Entity2? job)) {
                        MessageBox.Show("You don't have any job now.", "Error");
                        return;
                    }

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
                        // Append profit log entry
                        var newLog = saveGame.CreateNewUnit("profit_log");
                        newLog.Set("stats_data", "0");
                        newLog.Set("acc_distance_free", "0");
                        newLog.Set("acc_distance_on_job", "0");
                        newLog.Set("history_age", "nil");

                        player.ArrayAppend("trucks", currentTruckId, true);
                        player.ArrayAppend("truck_profit_logs", newLog.Unit.Id, true);
                    } else if (isCurrentTruckStealable) { // Delete unused job truck
                        var s = saveGame[currentTruckId];

                        CommonEdits.DeleteUnitRecursively(s, UnitSerializer.KNOWN_PTR_ITEMS_TRUCK);
                    }

                    // Return to last position of owned truck, if exists
                    player.Set("assigned_truck", player.GetValue("my_truck"));
                    if (player.GetValue("my_truck_placement_valid") == "true") {
                        player.Set("truck_placement", player.GetValue("my_truck_placement"));
                        if (player.Contains("my_trailer_placement")) {
                            player.Set("trailer_placement", player.GetValue("my_trailer_placement"));
                        }
                        if (player.Contains("my_slave_trailer_placements")) {
                            player.Set("slave_trailer_placements", player.GetArray("my_slave_trailer_placements"));
                        }
                    }

                    if (stealTruck && stealTrailer)
                        stealTrailer = MessageBox.Show("Do you want to steal the trailer?", "Own Job Vehicle", MessageBoxButton.YesNo) == MessageBoxResult.Yes;

                    if (stealTrailer) { // Trailer steal logic
                        player.ArrayAppend("trailers", currentTrailerId, true);

                        // Adding a dummy accessory with path to "/def/vehicle/trailer_owned/scs.box/data.sii" fixes N/A in trailer listings
                        var trailer = saveGame[currentTrailerId];

                        string accPath = "/def/vehicle/trailer_owned/scs.box/data.sii";

                        var newUnit = saveGame.CreateNewUnit("vehicle_accessory");
                        newUnit.Set("data_path", $"\"{accPath}\"");

                        trailer.ArrayAppend("accessories", newUnit.Unit.Id, true);
                    } else if (isCurrentTrailerStealable) { // The user decided to only steal the truck and abandon the trailer. Delete the unused job trailer.
                        var s = saveGame[currentTrailerId];

                        CommonEdits.DeleteUnitRecursively(s, UnitSerializer.KNOWN_PTR_ITEMS_TRAILER);
                    }

                    if (player.GetValue("my_trailer_attached") == "true") {
                        player.Set("assigned_trailer", player.GetValue("my_trailer"));
                        player.Set("assigned_trailer_connected", "true");
                    } else {
                        player.Set("assigned_trailer", "null");
                        player.Set("assigned_trailer_connected", "false");
                    }

                    // Special transport
                    if (job.TryGetPointer("special", out Entity2? special)) {
                        CommonEdits.DeleteUnitRecursively(economy.GetPointer("stored_special_job"), ["AUTO"]);
                        economy.Set("stored_special_job", "null");
                        special.DeleteSelf();
                    }

                    // Delete the job and reset navigation
                    CommonEdits.DeleteUnitRecursively(job, []);
                    player.Set("current_job", "null");
                    DestroyNavigationData(economy);

                    saveFile.Save(saveGame);
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

        private void DestroyNavigationData(Entity2 economy) {
            var i = economy.GetAllPointers("stored_gps_ahead_waypoints");
            foreach (var t in i) {
                t.DeleteSelf();
            }
            economy.Set("stored_gps_ahead_waypoints", "0");

            var registry = economy.GetPointer("registry")!;
            var regData = registry.GetArray("data");
            if (regData.Count >= 3) regData[0] = "0";
            registry.Set("data", regData);
        }

        public SaveEditTask ChangeCargoMass() {
            var run = new Action(() => {
                try {
                    var player = saveGame.EntityType("player")!;

                    var assignedTrailerId = player.GetValue("assigned_trailer");
                    if (assignedTrailerId == "null") {
                        MessageBox.Show("You don't have an assigned trailer.", "Error");
                        return;
                    }

                    var specifiedMass = NumberInputBox.Show("Set mass", "How heavy would you like your trailer cargo to be in kilograms? Too high values will result in physics glitch. Set this to 0 if you want to remove the cargo.");

                    if (specifiedMass == -1) {
                        return;
                    }

                    var trailer = saveGame[assignedTrailerId];
                    trailer.Set("cargo_mass", $"{specifiedMass}");

                    saveFile.Save(saveGame);
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
