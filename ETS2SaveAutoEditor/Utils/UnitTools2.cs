using ASE.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Shapes;
using System.Xml.Linq;
using Windows.Devices.PointOfService;

namespace ASE.SII2Parser {

    /// <summary>
    /// The primary class for managing SII files, offering functionality to read, modify, and write these files. 
    /// The class supports operations at both the file and unit levels, with 'entities' serving as a higher-level abstraction 
    /// to handle individual units. These 'entities' allow for comprehensive manipulation of units and their properties.
    ///
    /// Most methods within this class handle low-level operations. For more advanced manipulation, 
    /// utilize the 'Entity' or 'EntityType' method to obtain an 'entity' object that provides a more streamlined experience.
    /// 
    /// The purpose of the UnitEntity is to facilitate all required modifications to the save game file, 
    /// thereby minimizing the need to directly interact with the SiiSaveGame class. The SiiSaveGame class 
    /// is fully functional and operates effectively in the background. Thus, <b>the recommended approach is to 
    /// primarily work with the UnitEntity class for any modifications.</b>
    /// </summary>
    /// <remarks>
    /// This is one of two functions you'll be using to manipulate SII files. The other one is 'Entity'.
    /// </remarks>
    /// <param name="lines">List of savegame lines in SII text format.</param>
    public class Game2(SII2 reader) {
        private readonly SII2 reader = reader;
        private readonly BloomFilter<string> generatedIdsFilter = new();
        public SII2 Reader => reader;

        public Entity2 this[string id] {
            get {
                return new(reader[id]);
            }
        }

        public Entity2? EntityId(string id) {
            try {
                return this[id];
            } catch (KeyNotFoundException) {
                return null;
            }
        }

        public Entity2? EntityType(string type, [Optional] Unit2? after) {
            int start = 0;
            if (after != null) {
                start = reader.IndexOf(after) + 1;
            }
            for (int i = start; i < reader.Count; i++) {
                if (reader[i].Type == type) {
                    return new Entity2(reader[i]);
                }
            }
            return null;
        }

        public Entity2 CreateNewUnit(string type, [Optional] string? id) {
            id ??= GenerateNewID();
            Unit2 unit = new(type, id);
            reader.UncheckedAdd(unit);
            return new(unit);
        }

        public string GenerateNewID() {
            const string allowedChars = "0123456789abcdef";
            var random = new Random();
            string id;
            do {
                id = "_nameless";
                for (int i = 0; i < 12; i++) {
                    if (i % 4 == 0) id += ".";
                    id += allowedChars[random.Next(0, 16)];
                }
            } while (reader.ContainsKey(id) || generatedIdsFilter.Contains(id));
            generatedIdsFilter.Add(id);
            return id;
        }

        public void Add(Entity2 unit) {
            reader.Add(unit.Unit);
        }

        public void AddAll(IEnumerable<Entity2> units) {
            foreach (var unit in units) {
                reader.Add(unit.Unit);
            }
        }

        public override string ToString() {
            return reader.ToString();
        }
    }

    /// <summary>
    /// Note that this class isn't linked to Game2. It's a standalone class.
    /// </summary>
    /// <param name="unit"></param>
    public class Entity2(Unit2 unit) {
        public readonly Unit2 Unit = unit;

        /// <summary>
        /// Nullable version of Unit2[].
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Value2? Get(string key) {
            return Unit[key];
        }

        public bool Contains(string key) {
            return Unit.ContainsKey(key);
        }

        /// <summary>
        /// Should only be used when the value is surely a string.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public string GetValue(string key) {
            var item = Unit[key];
            if (item is RawDataValue2 value) {
                return value.Value;
            } else {
                return new RawDataValue2(item).Value;
            }
        }

        public bool TryGetValue(string key, [NotNullWhen(true)] out string? result) {
            try {
                result = GetValue(key);
                return true;
            } catch (Exception) {
                result = null;
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ArgumentException">When the entry isn't array.</exception>
        public List<string> GetArray(string key) {
            var item = Unit[key];
            if(item is RawDataArrayValue2 array) {
                return array.Value;
            } else {
                return new RawDataArrayValue2(item).Value;
            }
        }

        public bool TryGetArray(string key, [NotNullWhen(true)] out List<string>? result) {
            try {
                result = GetArray(key);
                return true;
            } catch (Exception) {
                result = null;
                return false;
            }
        }

        // Shortcut methods for pointer manipulation
        /// <summary>
        /// Get the unit which the value of specified key points to.
        /// </summary>
        /// <param name="key">Key in the unit</param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the unit.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the target unit with found ID does not exist. This might also happen when the value of <paramref name="key"/> isn't pointer.</exception>
        public Entity2 GetPointer(string key) {
            string targetUnitID = GetValue(key);
            if(Unit.Parent.unitMap.TryGetValue(targetUnitID, out Unit2? value)) {
                return new(value);
            } else {
                throw new InvalidOperationException($"The target unit with ID '{targetUnitID}' from '{key}' does not exist.");
            }
        }

        public bool TryGetPointer(string key, [NotNullWhen(true)] out Entity2? result) {
            try {
                result = GetPointer(key);
                return true;
            } catch (Exception) {
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Get all units which the values of specified key points to.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown when the entry isn't array.</exception>
        public Entity2[] GetAllPointers(string key) {
            var arr = GetArray(key);
            return (from item in arr select new Entity2(Unit.Parent[item])).ToArray();
        }

        public bool TryGetAllPointers(string key, [NotNullWhen(true)] out Entity2[]? result) {
            try {
                result = GetAllPointers(key);
                return true;
            } catch (Exception) {
                result = null;
                return false;
            }
        }

        // Shortcut methods for array manipulation

        /// <summary>
        /// This method is used to check if the key is an array. If it is, you can use GetArray(key) to get the array. If it is not, you can use Get(key) to get the value.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public bool IsArray(string key) {
            return Unit[key] is RawDataArrayValue2;
        }

        public bool ArrayContains(string key, string value) {
            if(TryGetArray(key, out var arr)) {
                return arr!.Contains(value);
            } else {
                return false;
            }
        }

        public int ArrayCount(string key) {
            var array = GetArray(key);
            return array.Count;
        }

        public bool ArrayAppend(string key, string value, bool force = false) {
            if (force && !IsArray(key)) {
                Set(key, [value]);
                return true;
            }
            GetArray(key).Add(value);
            return true;
        }

        public bool ArrayRemove(string key, string value) {
            return GetArray(key).Remove(value);
        }

        public void Set(string key, IEnumerable<string> data) {
            Unit[key] = new RawDataArrayValue2(data);
        }

        public void Set(string key, string data) {
            Unit[key] = new RawDataValue2(data);
        }

        public void Delete(string key) {
            Unit.Remove(key);
        }

        public void DeleteSelf() {
            Unit.Parent.Remove(Unit);
        }

        public string Id { get => Unit.Id; }
        public string Type { get => Unit.Type; }
    }
}