using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETS2SaveAutoEditor.Utils {
    /// <summary>
    /// Provides methods for serializing and deserializing unit entities.
    /// Note that this class does not handle the file format itself or check the file format version.
    /// You should manage this externally, and it is recommended to store and verify the format version
    /// using a comment (+) at the top of the file.
    /// Serialized units are no longer tied to '_nameless' identifiers; new identifiers are generated upon deserialization.
    /// The simple syntax of this format allows for easy manual editing.
    /// </summary>
    public class UnitSerializer {
        /// <summary>
        /// Serializes a unit and its referenced subunits into a string representation.
        /// </summary>
        /// <param name="root">The root <see cref="UnitEntity"/> to serialize.</param>
        /// <param name="knownPtrItems">
        /// A set of known pointer items in the format "{unitType}:{key}".
        /// This must be specified to serialize nested units; otherwise, they will be omitted.
        /// For example, "economy:player" indicates that the 'player' unit should also be serialized.
        /// 
        /// There's one exception. If you pass knownPtrItems only with exactly 'AUTO', it will try to find pointers automatically by checking if the value starts with "_". But this is not recommended as it may cause issues.
        /// </param>
        /// <returns>A serialized string representing the unit and its subunits.</returns>
        public static string SerializeUnit(UnitEntity root, HashSet<string> knownPtrItems) {
            var builder = new StringBuilder();

            Dictionary<string, int> unitIdMapping = new();
            Stack<Tuple<UnitEntity, int>> serializationQueue = new();
            serializationQueue.Push(new(root, 0));
            unitIdMapping[root.Id] = 0;

            var findPointers = knownPtrItems.Count == 1 && knownPtrItems.Contains("AUTO");

            while (serializationQueue.Count > 0) {
                var it = serializationQueue.Pop();
                var unit = it.Item1;
                int serializedId = it.Item2;
                builder.Append($"UNIT {serializedId:D6} {unit.Type}\n");

                Stack<Tuple<UnitEntity, int>> nextQueue = new();
                int serializeSubunit(string id) {
                    int ptrId;
                    if (unitIdMapping.ContainsKey(id)) {
                        ptrId = unitIdMapping[id];
                    } else {
                        ptrId = unitIdMapping.Count;
                        unitIdMapping[id] = ptrId;
                        nextQueue.Push(new(unit.EntityIdAround(id), ptrId));
                    }

                    return ptrId;
                }

                foreach (var entries in unit.Read()) {
                    var key = entries.Item1;
                    var value = entries.Item2;
                    var isArray = value.array != null;
                    var isPointer = knownPtrItems.Contains($"{unit.Type}:{entries.Item1}"); // This is pointer. Serialize the unit with value of this entry if the value starts with "_"

                    if (isArray) {
                        builder.Append($"  LIST {key}\n");

                        for (int i = 0; i < value.array.Length; i++) {
                            var v = value.array[i];
                            if ((isPointer || findPointers) && v.StartsWith("_")) {
                                builder.Append($"    PTR {serializeSubunit(v):D6}\n");
                            } else {
                                builder.Append($"    VAL {v}\n");
                            }
                        }
                    } else {
                        builder.Append($"  ITEM {key}\n");

                        var v = value.value;
                        if ((isPointer || findPointers) && v.StartsWith("_")) {
                            builder.Append($"    PTR {serializeSubunit(v):D6}\n");
                        } else {
                            builder.Append($"    VAL {v}\n");
                        }
                    }
                }

                builder.Append($"\n");

                // Add all nextQueue items to serializationQueue
                while (nextQueue.Count > 0) {
                    serializationQueue.Push(nextQueue.Pop());
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Deserializes unit data from a string and inserts the units into the game after a specified unit.
        /// </summary>
        /// <param name="after">The <see cref="UnitEntity"/> after which the new units will be inserted.</param>
        /// <param name="data">The serialized data string representing units to be deserialized.</param>
        /// <returns>An array of <see cref="UnitEntity"/> objects that were deserialized and inserted.</returns>
        /// <exception cref="Exception">
        /// Thrown when the data format is invalid or an error occurs during deserialization.
        /// </exception>
        public static UnitEntity[] DeserializeUnit(UnitEntity after, string data) {
            var lines = data.Split('\n');
            if (lines.Length < 2) {
                throw new Exception("Invalid file format");
            }

            var save = after.Game;
            var player = save.EntityType("player");

            // Begin importing
            // First of all, let's assign unique IDs to all units in the file
            Random rnd = new();
            var idPrefix = $"_nameless.ase{rnd.NextInt64() % 65536:x4}.{DateTime.Now.Ticks / 65536 / 65536 % 65536:x4}";
            Dictionary<int, string> idMapping = new();
            try {
                for (int i = 0; i < lines.Length; i++) {
                    var words = lines[i].Trim().Split(' ');
                    var cmd = words[0];
                    if (cmd != "UNIT") continue;
                    int unitId = int.Parse(lines[i].Split(" ")[1]);
                    idMapping[unitId] = $"{idPrefix}.{unitId:x4}";
                }
            } catch {
                throw new Exception("Failed to iterate the units to import. (Error code 1)\n\nAre you trying to import a modified file?");
            }

            List<UnitEntity> deserializedUnits = new();

            UnitEntity currentUnit = null;
            List<string> currentArray = null;
            string currentKey = null;
            // Beging adding units
            for (int i = 0; i < lines.Length; i++) {
                var line = lines[i].Trim();
                var words = line.Trim().Split(' ');
                var cmd = words[0];

                if (cmd == "+") continue; // Comment
                if ((cmd == "LIST" || cmd == "ITEM" || cmd == "UNIT") && currentArray != null) { // End of array elements
                    // Insert the array to the current unit
                    if (currentKey == null)
                        throw new Exception($"This can't happen! If you see this message, please handle the file you're importing to dev! Line number {i + 1}. (Error code 4)");
                    currentUnit.Set(currentKey, currentArray.ToArray());
                    currentArray = null;
                }
                if (cmd == "UNIT") {
                    var unitId = int.Parse(words[1]);
                    var unitType = words[2];
                    var generatedId = idMapping[unitId];
                    currentUnit = (currentUnit ?? player).InsertAfter(unitType, generatedId); // This makes sure the units are inserted in the correct order
                    deserializedUnits.Add(currentUnit);
                    continue;
                }
                if (cmd == "LIST") {
                    currentArray = new();
                    currentKey = words[1];
                    continue;
                }
                if (cmd == "ITEM") {
                    currentArray = null;
                    currentKey = words[1];
                    continue;
                }
                if (cmd == "VAL") {
                    if (currentKey == null)
                        throw new Exception($"Value specified before the key at line {i + 1}. (Error code 2)\n\nAre you trying to import a modified file?");
                    if (currentArray != null) {
                        currentArray.Add(line.Substring(4));
                    } else {
                        currentUnit.Set(currentKey, line.Substring(4));
                    }
                    continue;
                }
                if (cmd == "PTR") {
                    if (currentKey == null)
                        throw new Exception($"Value specified before the key at line {i + 1}. (Error code 3)\n\nAre you trying to import a modified file?");
                    if (currentArray != null) {
                        currentArray.Add(idMapping[int.Parse(words[1])]);
                    } else {
                        currentUnit.Set(currentKey, idMapping[int.Parse(words[1])]);
                    }
                    continue;
                }
            }

            return deserializedUnits.ToArray();
        }
    }
}
