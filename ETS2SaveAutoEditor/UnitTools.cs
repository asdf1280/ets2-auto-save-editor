using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace ETS2SaveAutoEditor {
    public interface IUnitResolvable {

    }

    public class UnitRange : IUnitResolvable {
        public string type;
        public string id;
        public int start;
        public int end;
    }

    public interface IUnitTrackable : IUnitResolvable {
        int LastFoundStart {
            get;
        }

        int LastFoundEnd {
            get;
        }
    }

    public class UnitIdSelector : IUnitTrackable {
        public string id;

        public int lastFoundStart = 0;
        public int lastFoundEnd = 0;
        int IUnitTrackable.LastFoundStart {
            get {
                return lastFoundStart;
            }
        }

        int IUnitTrackable.LastFoundEnd {
            get {
                return lastFoundEnd;
            }
        }

        public static UnitIdSelector Of(string id, int lastFoundStart = 0, int lastFoundEnd = 0) {
            return new UnitIdSelector { id = id, lastFoundStart = lastFoundStart, lastFoundEnd = lastFoundEnd };
        }
    }

    public class UnitTypeSelector : IUnitTrackable {
        public string type;

        public int lastFoundStart = 0;
        public int lastFoundEnd = 0;
        int IUnitTrackable.LastFoundStart {
            get {
                return lastFoundStart;
            }
        }

        int IUnitTrackable.LastFoundEnd {
            get {
                return lastFoundEnd;
            }
        }

        public static UnitTypeSelector Of(string type, int lastFoundStart = 0, int lastFoundEnd = 0) {
            return new UnitTypeSelector { type = type, lastFoundStart = lastFoundStart, lastFoundEnd = lastFoundEnd };
        }
    }

    public class UnitItem {
        public string name;
        public string value;
        public string[] array;
    }

    public class SiiSaveGame {
        private static readonly string unitIdPattern = "[_\\-\\.a-zA-Z0-9]+";

        private readonly List<string> lines;
        public List<string> Lines {
            get {
                return lines;
            }
        }

        public SiiSaveGame(List<string> lines) {
            this.lines = lines;

            ValidateUnitFile();
            NormaliseSiiLists();
        }

        public SiiSaveGame(string saveGame) : this(saveGame.Replace("\r\n", "\n").Split('\n').ToList()) {

        }

        public string MergeResult() {
            return string.Join("\r\n", lines);
        }

        public override string ToString() {
            return MergeResult();
        }

        private void ValidateUnitFile() {
            if (!lines[0].StartsWith("SiiNunit")) throw new ArgumentException("invalid unit file");
        }

        private void NormaliseSiiLists() {
            var modifiedLines = new List<string>();
            var workingKeyword = "";
            int workingKeywordAt = -1;
            var p2 = new Regex(@"^\s+([a-z_]+): \d+\b", RegexOptions.Compiled);
            for (int i = 0; i < lines.Count; i++) {
                var line = lines[i];
                if (workingKeyword.Length > 0 && line.TrimStart().StartsWith($"{workingKeyword}[")) {
                    if (workingKeywordAt != -1) {
                        modifiedLines.RemoveAt(workingKeywordAt);
                        workingKeywordAt = -1;
                    }
                    var m = Regex.Match(line, $@"^(\s+{workingKeyword})\[\d*\]:\s+(.*)$");
                    modifiedLines.Add($"{m.Groups[1].Value}[]: {m.Groups[2].Value}");
                } else if (p2.IsMatch(line)) {
                    workingKeyword = p2.Match(line).Groups[1].Value;
                    workingKeywordAt = modifiedLines.Count;
                    modifiedLines.Add(line);
                } else {
                    workingKeyword = "";
                    modifiedLines.Add(line);
                }
            }
            lines.Clear();
            lines.AddRange(modifiedLines);
        }

        public UnitRange ResolveUnit(IUnitResolvable target) {
            if (target is UnitRange range) {
                return range;
            } else if (target is UnitIdSelector selector) {
                var result = FindUnitWithId(selector.id, selector.lastFoundStart);
                if (result != null) {
                    selector.lastFoundStart = result.start;
                    selector.lastFoundEnd = result.end;
                }
                return result;
            } else if (target is UnitTypeSelector selector1) {
                var result = FindUnitWithType(selector1.type, selector1.lastFoundStart);
                if (result != null) {
                    selector1.lastFoundStart = result.start;
                    selector1.lastFoundEnd = result.end;
                }
                return result;
            } else {
                return null;
            }
        }

        public UnitRange FindUnitWithId(string id, int searchFrom = 0) {
            if (searchFrom < 0 || searchFrom >= lines.Count) {
                throw new ArgumentException("searchFrom out of range");
            }

            var p = new Regex($"^([a-z_]+) : {id} {{$", RegexOptions.Compiled);

            UnitRange searchLine(int line) {
                string lineStr = lines[line];
                if (lineStr.StartsWith(" ")) return null;
                if (!p.IsMatch(lineStr)) return null;

                string type = p.Match(lineStr).Groups[1].Value;
                int end = lines.FindIndex(line + 1, s => s.Trim() == "}");
                if (end == -1) return null;

                return new UnitRange {
                    id = id,
                    type = type,
                    start = line,
                    end = end
                };
            }

            for (int i = 0; searchFrom + i < lines.Count || searchFrom - i - 1 >= 0; i++) {
                if (searchFrom + i < lines.Count) {
                    var a = searchLine(searchFrom + i);
                    if (a != null) { return a; }
                }

                if (searchFrom - i - 1 >= 0) {
                    var a = searchLine(searchFrom - i - 1);
                    if (a != null) { return a; }
                }
            }
            return null;
        }

        public UnitRange FindUnitWithType(string type, int searchFrom = 0) {
            if (searchFrom < 0 || searchFrom >= lines.Count) {
                throw new ArgumentException("searchFrom out of range");
            }

            UnitRange searchLine(int line) {
                string lineStr = lines[line];
                if (!lineStr.StartsWith(type + " : ")) return null;

                string unitId = Regex.Match(lineStr.Trim(), $"^{type} : ({unitIdPattern}) {{$").Groups[1].Value;
                int end = lines.FindIndex(line + 1, s => s.Trim() == "}");
                if (end == -1) return null;

                return new UnitRange {
                    id = unitId,
                    type = type,
                    start = line,
                    end = end
                };
            }

            for (int i = 0; searchFrom + i < lines.Count || searchFrom - i - 1 >= 0; i++) {
                if (searchFrom + i < lines.Count) {
                    var a = searchLine(searchFrom + i);
                    if (a != null) { return a; }
                }

                if (searchFrom - i - 1 >= 0) {
                    var a = searchLine(searchFrom - i - 1);
                    if (a != null) { return a; }
                }
            }
            return null;
        }

        public void DeleteUnit(IUnitResolvable target) {
            var unit = ResolveUnit(target);
            for (int i = unit.start; i <= unit.end; i++) {
                lines.RemoveAt(unit.start);
            }
            if (lines[unit.start].Trim() == "") {
                lines.RemoveAt(unit.start);
            }
        }

        public UnitItem GetUnitItem(IUnitResolvable target, string name) {
            var unit = ResolveUnit(target);

            string headerData = null;
            List<string> array = new List<string>();
            for (int i = unit.start + 1; i < unit.end - 1; i++) {
                string line = lines[i];
                var ma1 = Regex.Match(line, $"^\\s+{name}: (.*)$");
                var ma2 = Regex.Match(line, $"^\\s+{name}\\[\\d*\\]: (.*)$");
                if (ma1.Success) {
                    headerData = ma1.Groups[1].Value;
                }
                if (ma2.Success) {
                    array.Add(ma2.Groups[1].Value);
                }
            }
            if (headerData == null && array.Count == 0) return null;
            if (headerData == null) {
                headerData = array.Count.ToString();
            }

            var arrayConv = array.ToArray();
            if (array.Count == 0) {
                arrayConv = null;
            }
            UnitItem children = new UnitItem {
                name = name,
                value = headerData,
                array = arrayConv
            };
            return children;
        }

        public void SetUnitItem(IUnitResolvable target, UnitItem data) {
            var unit = ResolveUnit(target);

            List<string> array = new List<string>();
            var insertedAlready = false;
            var offset = 0;
            for (int i = unit.start + 1; i < unit.end; i++) {
                string line = lines[i + offset];
                if (data.name == "refund")
                    Console.WriteLine(line);
                var ma1 = Regex.Match(line, $@"^\s+{data.name}: (.*)$");
                var ma2 = Regex.Match(line, $@"^\s+{data.name}\[\d*\]: (.*)$");
                if (ma1.Success || ma2.Success) {
                    lines.RemoveAt(i + offset);
                    if (!insertedAlready) {
                        insertedAlready = true;
                        if (data.array != null && data.array.Length > 0) {
                            var ar = from d in data.array select $" {data.name}[]: {d}";
                            lines.InsertRange(i + offset, ar);
                            offset += ar.Count();

                            if (data.array.Length == 0) {
                                lines.Insert(i + offset, $" {data.name}: 0");
                                offset += 1;
                            }
                        } else if (data.value != null && data.value.Length > 0) {
                            lines.Insert(i + offset, $" {data.name}: {data.value}");
                            offset += 1;
                        }
                    }
                    offset--;
                }
            }
            if (!insertedAlready) {
                if (data.array != null && data.array.Length > 0) {
                    var ar = from d in data.array select $" {data.name}[]: {d}";
                    lines.InsertRange(unit.end + offset, ar);
                    offset += ar.Count();

                    if (data.array.Length == 0) {
                        lines.Insert(unit.end + offset, $" {data.name}: 0");
                        offset += 1;
                    }
                } else if (data.value != null && data.value.Length > 0) {
                    lines.Insert(unit.end + offset, $" {data.name}: {data.value}");
                    offset += 1;
                }
            }
        }

        public UnitEntity Entity(IUnitTrackable target) {
            return new UnitEntity(this, target);
        }

        public UnitEntity EntityId(string id, int searchFrom = 0) {
            return Entity(UnitIdSelector.Of(id, searchFrom));
        }

        public UnitEntity EntityType(string type, int searchFrom = 0) {
            return Entity(UnitTypeSelector.Of(type, searchFrom));
        }
    }

    public class UnitEntity {
        public readonly SiiSaveGame Game;
        public readonly IUnitTrackable Target;
        public UnitEntity(SiiSaveGame game, IUnitTrackable target) {
            Game = game;
            Target = target;
        }

        public string Id {
            get {
                return ResolvedUnit.id;
            }
        }

        public string Type {
            get {
                return ResolvedUnit.type;
            }
        }

        public UnitRange ResolvedUnit {
            get {
                return Game.ResolveUnit(Target);
            }
        }

        public void Delete() {
            Game.DeleteUnit(Target);
        }

        public UnitItem Get(string key) {
            return Game.GetUnitItem(Target, key);
        }

        public UnitEntity GetPointer(string key) {
            return Game.Entity(UnitIdSelector.Of(Get(key).value, Target.LastFoundEnd));
        }

        public UnitEntity[] GetAllPointers(string key) {
            return (from item in Get(key).array select Game.Entity(UnitIdSelector.Of(item, Target.LastFoundEnd))).ToArray();
        }

        public void Set(string key, IEnumerable<string> data) {
            Game.SetUnitItem(Target, new UnitItem { name = key, array = data.ToArray() });
        }

        public void Set(string key, string data) {
            Game.SetUnitItem(Target, new UnitItem { name = key, value = data });
        }

        public UnitEntity EntityIdAround(string id) {
            return Game.Entity(UnitIdSelector.Of(id, Target.LastFoundEnd));
        }

        public UnitEntity EntityTypeAround(string type) {
            return Game.Entity(UnitTypeSelector.Of(type, Target.LastFoundEnd));
        }

        public string GetFullString() {
            var sb = new StringBuilder();
            var range = ResolvedUnit;
            for (int i = range.start; i <= range.end; i++) {
                sb.AppendLine(Game.Lines[i]);
            }
            return sb.ToString();
        }
    }

    public class UnitEntityWrapper {
        protected readonly UnitEntity e;
        public UnitEntityWrapper(UnitEntity e) {
            this.e = e;
        }
    }

    public class UnitPlayerWrapper : UnitEntityWrapper {
        public UnitPlayerWrapper(UnitEntity e) : base(e) {
        }

        public void SetActiveTrailer(string id) {
            e.Set("my_trailer", id);
            e.Set("assigned_trailer", id);
        }
    }
}