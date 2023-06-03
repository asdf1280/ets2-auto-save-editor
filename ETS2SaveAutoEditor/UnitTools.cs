using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace ETS2SaveAutoEditor {
    public class UnitTools {
        private static readonly string unitIdPattern = "[_\\-\\.a-zA-Z0-9]+";
        private static void ValidateUnitFile(List<string> lines) {
            if (!lines[0].StartsWith("SiiNunit")) throw new ArgumentException("invalid unit file");
        }
        public static UnitSearchResult FindUnitWithType(List<string> lines, string type, int offset = 0, int searchBegin = 0, int searchEnd = -1) {
            if (searchBegin < 0) {
                throw new ArgumentException("searchBegin < 0");
            } else if (offset < 0) {
                throw new ArgumentException("offset < 0");
            } else if (searchEnd < -1) {
                throw new ArgumentException("searchEnd < -1");
            }
            ValidateUnitFile(lines);

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
                    UnitSearchResult range = new UnitSearchResult {
                        id = startId,
                        start = start,
                        end = i
                    };
                    return range;
                }
            }
            return null;
        }

        public static UnitSearchResult FindUnitWithId(List<string> lines, string id, int searchBegin = 0, int searchEnd = -1) {
            if (searchBegin < 0) {
                throw new ArgumentException("searchBegin < 0");
            }
            if (searchEnd < -1) {
                throw new ArgumentException("searchEnd < -1");
            }
            ValidateUnitFile(lines);

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
                    UnitSearchResult range = new UnitSearchResult {
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

        public static UnitChildren SearchChildrenWithId(List<string> lines, string name, UnitSearchResult unit) {
            ValidateUnitFile(lines);
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
            UnitChildren children = new UnitChildren {
                name = name,
                header = headerData,
                array = arrayConv
            };
            return children;
        }

        public static void NormalizeArrayNotation(List<string> lines) {
            var lastKeyword = "";
            int lastKeywordAt = -1;
            for (int i = 0; i < lines.Count; i++) {
                var line = lines[i];
                if (lastKeyword.Length > 0 && Regex.IsMatch(line, $@"^\s+{lastKeyword}\[\d*\]")) {
                    if(lastKeywordAt != -1) {
                        lines.RemoveAt(lastKeywordAt);
                        i--;
                        lastKeywordAt = -1;
                    }
                    var m = Regex.Match(line, $@"^(\s+{lastKeyword})\[\d*\]:\s+(.*)$");
                    lines[i] = $"{m.Groups[1].Value}[]: {m.Groups[2].Value}";
                } else if (Regex.IsMatch(line, @"^\s+[a-z_]+: \d+\b")) {
                    lastKeyword = Regex.Match(line, @"^\s+([a-z_]+): \d+\b").Groups[1].Value;

                    lastKeywordAt = i;
                }
            }
        }

        public static void DeleteUnit(List<string> lines, UnitSearchResult unit) {
            ValidateUnitFile(lines);
            for (int i = unit.start; i <= unit.end; i++) {
                lines.RemoveAt(unit.start);
            }
        }

        public static void InsertOrReplaceChildren(List<string> lines, UnitChildren data, UnitSearchResult unit) {
            ValidateUnitFile(lines);
            List<string> array = new List<string>();
            var insertedAlready = false;
            var offset = 0;
            for (int i = unit.start + 1; i < unit.end - 1; i++) {
                string line = lines[i + offset];
                var ma1 = Regex.Match(line, $@"^(\s+){data.name}: (.*)$");
                var ma2 = Regex.Match(line, $@"^(\s+){data.name}\[\d*\]: (.*)$");
                if (ma1.Success || ma2.Success) {
                    var whiteSpaces = (ma1.Success ? ma1 : ma2).Groups[1].Value;

                    lines.RemoveAt(i + offset);
                    offset--;
                    if (!insertedAlready) {
                        insertedAlready = true;
                        if (data.array != null && data.array.Length > 0) {
                            var ar = from d in data.array select $"{whiteSpaces}{data.name}[]: {d}";
                            lines.InsertRange(i + offset, ar);
                            offset += ar.Count();
                        } else {
                            lines.Insert(i + offset, $"{whiteSpaces}{data.name}: {data.header}");
                            offset += 1;
                        }
                    }
                }
            }
            if (!insertedAlready) {
                if (data.array != null && data.array.Length > 0) {
                    var ar = from d in data.array select $" {data.name}[]: {d}";
                    lines.InsertRange(unit.end + offset, ar);
                    offset += ar.Count();
                } else {
                    lines.Insert(unit.end + offset, $" {data.name}: {data.header}");
                    offset += 1;
                }
            }
        }
    }

    public interface IUnitSelection {

    }

    public class UnitSearchResult : IUnitSelection {
        public string type;
        public string id;
        public int start;
        public int end;
    }

    public class UnitChildren : IUnitSelection {
        public string name;
        public string header;
        public string[] array;
    }

    public interface ISiiElement {

    }

    public abstract class SiiContainer : ISiiElement {
        private string RawData;

        public SiiContainer(string savegame) {
            savegame = savegame.Replace("\r", "");
            if (savegame.StartsWith("SiiNunit\n")) {
                savegame = savegame.Substring(9);
            } else {
                throw new ArgumentException("Corrupted save data");
            }
            RawData = savegame;
        }

        public abstract SiiUnit FindUnitOfId(string id);

        public abstract SiiUnit FindUnitOfType(string id);
    }

    public abstract class SiiUnit : ISiiElement {
        private string RawData;
        private SiiContainer _parent;

        protected SiiUnit() {

        }

        public abstract SiiChildren FindChildrenOfId(string id);
    }

    public abstract class SiiChildren : ISiiElement {
        private string _rawData;
        private SiiUnit _parent;

        protected SiiChildren(SiiUnit parent) {
            _parent = parent;
        }

        public string Key {
            get {
                return "";
            }
        }

        public string RawValue {
            get {
                return "";
            }
        }

        public string RawData {
            get {
                return _rawData;
            }
        }
    }
}