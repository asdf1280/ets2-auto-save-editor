using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Xml.Linq;
using Windows.Foundation.Metadata;

namespace ETS2SaveAutoEditor.SII2Parser {
    /// <summary>
    /// A next-generation SII file parser. The key difference is that this parser decodes the whole file to the memory, unlike the old parser which kept the data line by line in SII string.
    /// </summary>
    public class SII2 : IList<Unit2>, IReadOnlyDictionary<string, Unit2> {
        internal readonly List<Unit2> units = [];
        internal readonly Dictionary<string, Unit2> unitMap = [];

        public object? StructureData;

        public const string Indent = " ";
        private static bool countArrays = false;
        public static bool CountArrays {
            get => countArrays;
            set {
                countArrays = value;
            }
        }

        /// <summary>
        /// Creates a new empty SII2 instance. Use SII2Parser to parse a SII file.
        /// </summary>
        public SII2() {

        }

        #region IList<Unit2> and IReadOnlyDictionary<string, Unit2> implementation
        public Unit2 this[int index] {
            get {
                return units[index];
            }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                // remove the old item.
                RemoveAt(index);

                Insert(index, value);
            }
        }

        public Unit2 this[string key] { // Doesn't support set by key because the key is determined by the unit itself.
            get => unitMap[key]; // will throw KeyNotFoundException if the key doesn't exist.
        }

        public int Count => units.Count;

        public bool IsReadOnly => false;

        IEnumerable<string> IReadOnlyDictionary<string, Unit2>.Keys => throw new NotImplementedException();

        IEnumerable<Unit2> IReadOnlyDictionary<string, Unit2>.Values => throw new NotImplementedException();

        int IReadOnlyCollection<KeyValuePair<string, Unit2>>.Count => throw new NotImplementedException();

        public void Add(Unit2 unit) {
            if (!unit.IsDetached) {
                throw new InvalidOperationException("The unit is already attached to a SII2 instance.");
            }
            if (ContainsKey(unit.Id)) {
                throw new ArgumentException("An item with the same key has already been added.");
            }
            units.Add(unit);
            unitMap[unit.Id] = unit; // BUGGY. This can cause a serious issue on duplicate keys.
            unit.___attach_do_not_use(this);
        }

        /// <summary>
        /// Appends a unit to SII without checking if the unit is already attached to a SII2 instance. This method is used internally by the parser to speed up the parsing process. Do not use this method unless you're sure that there are no duplicate keys.
        /// </summary>
        /// <param name="unit"></param>
        public void UncheckedAdd(Unit2 unit) {
            units.Add(unit);
            unitMap[unit.Id] = unit; // BUGGY. This can cause a serious issue on duplicate keys.
            unit.___attach_do_not_use(this);
        }

        public void Clear() {
            foreach (var unit in units) {
                unit.___detach_do_not_use();
            }
            units.Clear();
            unitMap.Clear();
        }

        public bool Contains(Unit2 item) {
            return unitMap.ContainsValue(item);
        }

        public bool ContainsKey(string key) {
            return unitMap.ContainsKey(key);
        }

        public void CopyTo(Unit2[] array, int arrayIndex) {
            units.CopyTo(array, arrayIndex);
        }

        public IEnumerator<Unit2> GetEnumerator() {
            return units.GetEnumerator();
        }

        public int IndexOf(Unit2 item) {
            return units.IndexOf(item);
        }

        public void Insert(int index, Unit2 item) {
            if (!item.IsDetached) {
                throw new InvalidOperationException("The unit is already attached to a SII2 instance.");
            }
            if (ContainsKey(item.Id)) {
                throw new ArgumentException("An item with the same key has already been added.");
            }
            units.Insert(index, item);
            unitMap[item.Id] = item; // BUGGY. This can cause a serious issue on duplicate keys.
            item.___attach_do_not_use(this);
        }

        public bool Remove(Unit2 item) {
            if (!units.Contains(item)) {
                return false;
            }
            item.___detach_do_not_use();
            return units.Remove(item) || unitMap.Remove(item.Id);
        }

        public void RemoveAt(int index) {
            Remove(units[index]);
        }

        bool IReadOnlyDictionary<string, Unit2>.ContainsKey(string key) {
            return unitMap.ContainsKey(key);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return units.GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, Unit2>> IEnumerable<KeyValuePair<string, Unit2>>.GetEnumerator() {
            return unitMap.GetEnumerator();
        }

        bool IReadOnlyDictionary<string, Unit2>.TryGetValue(string key, [MaybeNullWhen(false)] out Unit2 value) {
            return unitMap.TryGetValue(key, out value);
        }
        #endregion

        public void WriteTo(TextWriter writer) {
            Stopwatch stopwatch = new();
            stopwatch.Start();

            writer.Write("SiiNunit\n{\n");

            foreach (var unit in units) {
                unit.WriteTo(writer);
                writer.Write("\n"); // extra line between units
            }

            writer.Write("}\n");

            Console.WriteLine($"[SII2] Saved SII with {Count} units. Elapsed time: {stopwatch.ElapsedMilliseconds} ms");
        }

        public override string ToString() {
            StringWriter writer = new();
            WriteTo(writer);
            return writer.ToString();
        }
    }

    public enum Type2 {
        Raw,
        RawArray
    }

    public class Unit2 : IReadOnlyList<string>, IDictionary<string, Value2>, ICloneable {
        private SII2? parent = null;
        private string type;
        private string id;

        internal readonly List<string> keys = [];
        internal readonly Dictionary<string, Value2> valueMap = [];

        internal Unit2(string type, string id) {
            this.type = type;
            this.id = id;
        }

        internal void ___detach_do_not_use() {
            parent = null;
        }

        /// <summary>
        /// Attaches this unit to the parent SII2 instance. This method is called by the parent SII2 instance. Note that it should be added to the parent SII2 instance first before running this method.
        /// 
        /// InvalidOperationException is thrown if this unit is already attached to a SII2 instance.
        /// </summary>
        /// <param name="parent"></param>
        /// <exception cref="InvalidOperationException"></exception>
        internal void ___attach_do_not_use(SII2 parent) {
            if (this.parent != null) {
                throw new InvalidOperationException("This unit is already attached to a SII2 instance.");
            }
            this.parent = parent;
        }

        #region IReadOnlyList<string> and IDictionary<string, Value2<object>> implementation
        public IEnumerator<string> GetEnumerator() {
            return keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return keys.GetEnumerator();
        }

        public void Add(string key, Value2 value) {
            if (valueMap.ContainsKey(key)) {
                throw new ArgumentException("An item with the same key has already been added.");
            }

            keys.Add(key);
            valueMap[key] = value;
        }

        public bool ContainsKey(string key) {
            return valueMap.ContainsKey(key);
        }

        public bool Remove(string key) {
            return keys.Remove(key) || valueMap.Remove(key);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out Value2 value) {
            return valueMap.TryGetValue(key, out value);
        }

        public void Add(KeyValuePair<string, Value2> item) {
            Add(item.Key, item.Value);
        }

        public void Clear() {
            keys.Clear();
            valueMap.Clear();
        }

        public bool Contains(KeyValuePair<string, Value2> item) {
            return valueMap.Contains(item);
        }

        [Deprecated("This method is not supported.", DeprecationType.Remove, 1)]
        public void CopyTo(KeyValuePair<string, Value2>[] array, int arrayIndex) {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, Value2> item) {
            if (valueMap.Contains(item)) {
                keys.Remove(item.Key);
                return valueMap.Remove(item.Key);
            } else {
                return false;
            }
        }

        IEnumerator<KeyValuePair<string, Value2>> IEnumerable<KeyValuePair<string, Value2>>.GetEnumerator() {
            return valueMap.GetEnumerator();
        }

        public Value2 this[string key] {
            get {
                if (valueMap.TryGetValue(key, out Value2? value)) {
                    return value;
                } else {
                    throw new KeyNotFoundException();
                }
            }
            set {
                ArgumentNullException.ThrowIfNull(value);

                if (valueMap.ContainsKey(key)) {
                    valueMap[key] = value;
                } else {
                    keys.Add(key);
                    valueMap[key] = value;
                }
            }
        }

        public int Count => keys.Count;

        public bool IsReadOnly => false;

        public ICollection<string> Keys => valueMap.Keys;

        public ICollection<Value2> Values => valueMap.Values;

        public string this[int index] {
            get {
                return keys[index];
            }
        }

        #endregion

        public bool IsDetached {
            get {
                return parent == null;
            }
        }

        public SII2 Parent {
            get {
                if (IsDetached) throw new InvalidOperationException("This unit is detached.");
                return parent!;
            }
        }

        public string Type {
            get {
                return type;
            }
            set {
                ArgumentNullException.ThrowIfNull(value);

                type = value;
            }
        }

        public string Id {
            get {
                return id;
            }
            set {
                if (parent != null) {
                    parent.unitMap.Remove(id);
                    parent.unitMap[value] = this;
                }
                id = value;
            }
        }

        public void WriteTo(TextWriter writer) {
            writer.Write(type + " : " + id + " {\n");

            var keysInMap = valueMap.Keys.ToHashSet();
            List<string> keys = new(this.keys);
            foreach (var k in keysInMap) {
                if (!keys.Contains(k)) {
                    keys.Add(k);
                }
            }

            foreach (var key in keys) {
                if (valueMap.TryGetValue(key, out Value2? value)) {
                    value.WriteTo(writer, key);
                }
            }

            writer.Write("}\n");
        }

        public object Clone() {
            throw new NotImplementedException();
        }
    }

    public abstract class Value2 : ICloneable {
        public abstract void WriteTo(TextWriter writer, string key);
        public abstract object Clone();

        public abstract object Value { get; }
    }

    public abstract class Value2<T> : Value2 {
        public abstract T TypedValue { get; }
    }

    public abstract class ValueArray2<T> : Value2<List<T>> {
    }

    public class RawDataValue2 : Value2<string> {
        private string rawstr;

        public RawDataValue2(string value) {
            rawstr = value;
        }

        public RawDataValue2(Value2 value2) : this(value2.Value.ToString() ?? "null") { }

        public RawDataValue2() {
            rawstr = "null";
        }

        public override string Value => rawstr;

        public override string TypedValue => rawstr;

        public void SetValue(string value) {
            rawstr = value;
        }

        public override void WriteTo(TextWriter writer, string key) {
            writer.Write(SII2.Indent + key + ": " + rawstr + "\n");
        }

        public override string ToString() {
            return rawstr;
        }

        public override object Clone() {
            return new RawDataValue2(rawstr);
        }
    }

    public class RawDataArrayValue2 : ValueArray2<string> {
        private readonly List<string> data;

        public override List<string> Value => data;
        public override List<string> TypedValue => data;

        public RawDataArrayValue2(IEnumerable<string> d) {
            data = new(d);
        }

        public RawDataArrayValue2() : this([]) { }
        public RawDataArrayValue2(Value2 d) {
            if (d.Value is string and "0") {
                data = [];
            } else if (d is ValueArray2<string> d2) {
                data = d2.TypedValue.Select(x => x.ToString() ?? "null").ToList(); // <- HERE
            } else {
                throw new ArgumentException("The value is not an array.");
            }
        }

        public RawDataArrayValue2(ValueArray2<object> d) : this((Value2)d) { }

        public override void WriteTo(TextWriter writer, string key) {
            if (SII2.CountArrays || data.Count == 0) {
                writer.Write(SII2.Indent + key + ": " + data.Count + "\n");
            }
            for (int i = 0; i < data.Count; i++) {
                if (SII2.CountArrays)
                    writer.Write(SII2.Indent + key + "[" + i + "]: " + data[i] + "\n");
                else
                    writer.Write(SII2.Indent + key + "[]: " + data[i] + "\n");
            }
        }

        public override string ToString() {
            StringBuilder sb = new();
            sb.Append(nameof(RawDataArrayValue2));
            foreach (var d in data) {
                sb.Append("\n\t" + d);
            }
            return sb.ToString();
        }

        public override object Clone() {
            // clone the list then return a new RawDataArrayValue2
            return new RawDataArrayValue2(new List<string>(data));
        }
    }
}