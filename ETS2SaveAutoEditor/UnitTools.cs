using System;
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

    public class UnitIdSelector : IUnitResolvable {
        public string id;

        public static UnitIdSelector Of(string id) {
            return new UnitIdSelector { id = id };
        }
    }

    public class UnitTypeSelector : IUnitResolvable {
        public string type;
        public int offset;

        public static UnitTypeSelector Of(string type, int offset = 0) {
            return new UnitTypeSelector { type = type, offset = offset };
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
            var workingKeyword = "";
            int workingKeywordAt = -1;
            for (int i = 0; i < lines.Count; i++) {
                var line = lines[i];
                if (workingKeyword.Length > 0 && Regex.IsMatch(line, $@"^\s+{workingKeyword}\[\d*\]")) {
                    if (workingKeywordAt != -1) {
                        lines.RemoveAt(workingKeywordAt);
                        i--;
                        workingKeywordAt = -1;
                    }
                    var m = Regex.Match(line, $@"^(\s+{workingKeyword})\[\d*\]:\s+(.*)$");
                    lines[i] = $"{m.Groups[1].Value}[]: {m.Groups[2].Value}";
                } else if (Regex.IsMatch(line, @"^\s+[a-z_]+: \d+\b")) {
                    workingKeyword = Regex.Match(line, @"^\s+([a-z_]+): \d+\b").Groups[1].Value;

                    workingKeywordAt = i;
                }
            }
        }

        public UnitRange ResolveUnit(IUnitResolvable target) {
            if (target is UnitRange range) {
                return range;
            } else if (target is UnitIdSelector selector) {
                return FindUnitWithId(selector.id);
            } else if (target is UnitTypeSelector selector1) {
                return FindUnitWithType(selector1.type, selector1.offset);
            } else {
                return null;
            }
        }

        public UnitRange FindUnitWithId(string id, int searchBegin = 0, int searchEnd = -1) {
            if (searchBegin < 0) {
                throw new ArgumentException("searchBegin < 0");
            }
            if (searchEnd < -1) {
                throw new ArgumentException("searchEnd < -1");
            }

            if (searchEnd == -1) {
                searchEnd = lines.Count;
            }
            searchEnd = Math.Min(lines.Count, searchEnd);

            string startType = null;
            int start = -1;

            for (int i = searchBegin; i < searchEnd; i++) {
                string line = lines[i];
                var ma = Regex.Match(line, $"^([a-z_]+) : {id} {{$");
                if (ma.Success) {
                    start = i;
                    startType = ma.Groups[1].Value;
                }
                if (start != -1 && line == "}") {
                    UnitRange range = new UnitRange {
                        id = id,
                        type = startType,
                        start = start,
                        end = i
                    };
                    return range;
                }
            }
            return null;
        }

        public UnitRange FindUnitWithType(string type, int offset = 0, int searchBegin = 0, int searchEnd = -1) {
            if (searchBegin < 0) {
                throw new ArgumentException("searchBegin < 0");
            } else if (offset < 0) {
                throw new ArgumentException("offset < 0");
            } else if (searchEnd < -1) {
                throw new ArgumentException("searchEnd < -1");
            }

            if (searchEnd == -1) {
                searchEnd = lines.Count;
            }
            searchEnd = Math.Min(lines.Count, searchEnd);

            string startId = null;
            int start = -1;
            int offsetRemaining = offset;

            for (int i = searchBegin; i < searchEnd; i++) {
                string line = lines[i];
                if (line.StartsWith(type + " : ")) {
                    if (offsetRemaining == 0) {
                        start = i;
                        Console.WriteLine($"^{type} : (${unitIdPattern}) {{$");
                        startId = Regex.Match(line.Trim(), $"^{type} : ({unitIdPattern}) {{$").Groups[1].Value;
                    } else
                        offsetRemaining--;
                }
                if (start != -1 && line == "}") {
                    UnitRange range = new UnitRange {
                        id = startId,
                        start = start,
                        end = i
                    };
                    return range;
                }
            }
            return null;
        }

        public void DeleteUnit(IUnitResolvable target) {
            var unit = ResolveUnit(target);
            for (int i = unit.start; i <= unit.end; i++) {
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
            for (int i = unit.start + 1; i < unit.end - 1; i++) {
                string line = lines[i + offset];
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
                } else if (data.value != null && data.value.Length > 0) {
                    lines.Insert(unit.end + offset, $" {data.name}: {data.value}");
                    offset += 1;
                }
            }
        }
    }
}