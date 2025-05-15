using ASE.SII2Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASE.Utils {
    internal class CommonEdits {
        public static readonly string[] KNOWN_PTR_ITEMS_TRUCK = ["vehicle:accessories"];
        public static readonly string[] KNOWN_PTR_ITEMS_TRAILER = ["trailer:trailer_definition", "trailer:slave_trailer", "trailer:accessories"];

        public static void DeleteUnitRecursively(Entity2 unit, IEnumerable<string> knownPtrItemsE) {
            HashSet<string> knownPtrItems = [.. knownPtrItemsE];
            LinkedList<Entity2> queue = [];
            queue.AddLast(unit);

            var game = unit.Unit.Parent;

            bool findPointers = knownPtrItems.Count == 1 && knownPtrItems.Contains("AUTO");

            while (queue.Count > 0) {
                Entity2 entity = queue.First(); queue.RemoveFirst();
                if (entity.Unit.IsDetached) continue; // Already deleted somehow
                
                foreach (string key in entity.Unit) {
                    bool isArray = entity.IsArray(key);
                    bool isPointer = knownPtrItems.Contains($"{entity.Unit.Type}:{key}") || knownPtrItems.Contains($":{key}"); // This is pointer. Serialize the unit with value of this entry if the value starts with "_"

                    if (isArray) {
                        var arr = entity.GetArray(key);
                        for (int i = 0; i < arr.Count; i++) {
                            var v = arr[i];
                            if ((isPointer || findPointers) && v.StartsWith('_')) {
                                queue.AddLast(new Entity2(game[v]));
                            }
                        }
                    } else {
                        var v = entity.GetValue(key);
                        if ((isPointer || findPointers) && v.StartsWith('_')) {
                            queue.AddLast(new Entity2(game[v]));
                        }
                    }
                }
                entity.DeleteSelf();
            }
        }

        public static void DestroyJobData(Entity2 economy, Entity2 player) {
            DestroyNavigationData(economy);

            if (!player.TryGetPointer("current_job", out Entity2? job)) {
                return;
            }

            // Special transport
            if (job.TryGetPointer("special", out Entity2? special)) {
                DeleteUnitRecursively(economy.GetPointer("stored_special_job"), ["AUTO"]);
                economy.Set("stored_special_job", "null");
                special.DeleteSelf();
            }

            // Delete the job and reset navigation
            DeleteUnitRecursively(job, ["AUTO"]);
            player.Set("current_job", "null");
        }

        public static void DestroyNavigationData(Entity2 economy) {
            // Erase navigation data first
            foreach (var item in economy.GetAllPointers("stored_gps_behind_waypoints")) {
                DeleteUnitRecursively(item, []);
            }
            economy.Set("stored_gps_behind_waypoints", []);

            foreach (var item in economy.GetAllPointers("stored_gps_ahead_waypoints")) {
                DeleteUnitRecursively(item, []);
            }
            economy.Set("stored_gps_ahead_waypoints", []);

            foreach (var item in economy.GetAllPointers("stored_gps_avoid_waypoints")) {
                DeleteUnitRecursively(item, []);
            }
            economy.Set("stored_gps_avoid_waypoints", []);

            var registry = economy.GetPointer("registry")!;
            var regData = registry.GetArray("data");
            if (regData.Count >= 3) regData[0] = "0";
            registry.Set("data", regData);
        }
    }
}
