using ETS2SaveAutoEditor.SII2Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETS2SaveAutoEditor.Utils {
    internal class CommonEdits {
        public static void DeleteUnitRecursively(Entity2 unit, IEnumerable<string> knownPtrItemsE) {
            HashSet<string> knownPtrItems = [.. knownPtrItemsE];
            LinkedList<Entity2> queue = new();
            queue.AddLast(unit);

            bool findPointers = knownPtrItems.Count == 1 && knownPtrItems.Contains("AUTO");

            while (queue.Count > 0) {
                Entity2 entity = queue.First(); queue.RemoveFirst();
                if(entity.Unit.IsDetached) continue; // Already deleted somehow
                foreach (string key in entity.Unit) {
                    bool isArray = entity.IsArray(key);
                    bool isPointer = knownPtrItems.Contains($"{entity.Unit.Type}:{key}") || knownPtrItems.Contains($":{key}"); // This is pointer. Serialize the unit with value of this entry if the value starts with "_"

                    if (isArray) {
                        var arr = entity.GetArray(key);
                        for (int i = 0; i < arr.Count; i++) {
                            var v = arr[i];
                            if ((isPointer || findPointers) && v.StartsWith("_")) {
                                queue.AddLast(new Entity2(unit.Unit.Parent[v]));
                            }
                        }
                    } else {
                        var v = entity.GetValue(key);
                        if ((isPointer || findPointers) && v.StartsWith("_")) {
                            queue.AddLast(new Entity2(unit.Unit.Parent[v]));
                        }
                    }
                }
                entity.DeleteSelf();
            }
        }
    }
}
