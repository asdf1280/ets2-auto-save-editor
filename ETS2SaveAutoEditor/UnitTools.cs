using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ETS2SaveAutoEditor
{
    public class UnitTools
    {
        private static readonly string unitIdPattern = "[_\\-\\.a-zA-Z0-9]+";
        private static void ValidateUnitFile(string[] lines)
        {
            if (!lines[0].StartsWith("SiiNunit")) throw new ArgumentException("invalid unit file");
        }
        public static UnitSearchResult FindUnitWithType(string[] lines, string type, int offset = 0, int searchBegin = 0, int searchEnd = -1)
        {
            if(searchBegin < 0)
            {
                throw new ArgumentException("searchBegin < 0");
            } else if(offset < 0)
            {
                throw new ArgumentException("offset < 0");
            } else if(searchEnd < -1)
            {
                throw new ArgumentException("searchEnd < -1");
            }
            ValidateUnitFile(lines);

            if (searchEnd == -1)
            {
                searchEnd = lines.Length;
            }
            searchEnd = Math.Min(lines.Length, searchEnd);

            string startId = null;
            int start = -1;
            int offsetRemaining = offset;

            for (int i = searchBegin; i < searchEnd; i++)
            {
                string line = lines[i];
                if (line.StartsWith(type + " : "))
                {
                    if (offsetRemaining == 0)
                    {
                        start = i;
                        Console.WriteLine($"^{type} : (${unitIdPattern}) {{$");
                        startId = Regex.Match(line.Trim(), $"^{type} : ({unitIdPattern}) {{$").Groups[1].Value;
                    }
                    else
                        offsetRemaining--;
                }
                if (start != -1 && line == "}")
                {
                    UnitSearchResult range = new UnitSearchResult
                    {
                        id = startId,
                        start = start,
                        end = i
                    };
                    return range;
                }
            }
            return null;
        }

        public static UnitSearchResult FindUnitWithId(string[] lines, string id, int searchBegin = 0, int searchEnd = -1)
        {
            if (searchBegin < 0)
            {
                throw new ArgumentException("searchBegin < 0");
            }
            if (searchEnd < -1)
            {
                throw new ArgumentException("searchEnd < -1");
            }
            ValidateUnitFile(lines);

            if (searchEnd == -1)
            {
                searchEnd = lines.Length;
            }
            searchEnd = Math.Min(lines.Length, searchEnd);

            string startType = null;
            int start = -1;

            for (int i = searchBegin; i < searchEnd; i++)
            {
                string line = lines[i];
                var ma = Regex.Match(line, $"^([a-z]+) : {id} {{$");
                if (ma.Success)
                {
                    start = i;
                    startType = ma.Groups[1].Value;
                }
                if (start != -1 && line == "}")
                {
                    UnitSearchResult range = new UnitSearchResult
                    {
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

        public static UnitChildren SearchChildrenWithId(string[] lines, string name, UnitSearchResult unit)
        {
            ValidateUnitFile(lines);
            string headerData = null;
            List<string> array = new List<string>();
            for (int i = unit.start + 1; i < unit.end - 1; i++)
            {
                string line = lines[i];
                var ma1 = Regex.Match(line, $"^\\s+{name}: (.*)$");
                var ma2 = Regex.Match(line, $"^\\s+{name}\\[\\d*\\]: (.*)$");
                if(ma1.Success)
                {
                    headerData = ma1.Groups[1].Value;
                }
                if(ma2.Success)
                {
                    array.Add(ma2.Groups[1].Value);
                }
            }
            if (headerData == null && array.Count == 0) return null;
            if(headerData == null)
            {
                headerData = array.Count.ToString();
            }

            var arrayConv = array.ToArray();
            if(array.Count == 0)
            {
                arrayConv = null;
            }
            UnitChildren children = new UnitChildren
            {
                name = name,
                header = headerData,
                array = arrayConv
            };
            return children;
        }
    }

    public interface IUnitSelection
    {

    }

    public class UnitSearchResult : IUnitSelection
    {
        public string type;
        public string id;
        public int start;
        public int end;
    }

    public class UnitChildren : IUnitSelection
    {
        public string name;
        public string header;
        public string[] array;
    }
}