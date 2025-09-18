#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace mmzkworks.SimpleHttpServer.OpenApi
{
    public static class SimpleYamlEmitter
    {
        private const int IndentSize = 2;

        public static void WriteYaml(object? obj, TextWriter w) => WriteNode(obj, w, 0, topLevel:true);

        static void WriteNode(object? obj, TextWriter w, int indent, bool topLevel = false)
        {
            switch (obj)
            {
                case null:
                    w.WriteLine("null");
                    return;

                case string s:
                    WriteScalar(w, s);
                    w.WriteLine();
                    return;

                case bool b:
                    w.WriteLine(b ? "true" : "false");
                    return;

                case int or long or short or byte or float or double or decimal:
                    w.WriteLine(Convert.ToString(obj, CultureInfo.InvariantCulture));
                    return;

                case IDictionary dict:
                    WriteMap(dict, w, indent);
                    return;

                case IEnumerable list:
                    WriteSeq(list, w, indent);
                    return;

                default:
                    // POCO fallback
                    var dict2 = new Dictionary<string, object?>();
                    foreach (var p in obj.GetType().GetProperties())
                        if (p.CanRead) dict2[p.Name] = p.GetValue(obj);
                    WriteMap(dict2, w, indent);
                    return;
            }
        }

        static void WriteMap(IDictionary dict, TextWriter w, int indent)
        {
            if (dict.Count == 0) { w.WriteLine("{}"); return; }

            foreach (DictionaryEntry de in dict)
            {
                Indent(w, indent);
                WriteKey(w, de.Key?.ToString() ?? "null");
                w.Write(": ");

                if (IsScalar(de.Value))
                {
                    WriteScalar(w, de.Value);
                    w.WriteLine();
                }
                else
                {
                    w.WriteLine();
                    WriteNode(de.Value, w, indent + IndentSize);
                }
            }
        }

        static void WriteSeq(IEnumerable list, TextWriter w, int indent)
        {
            bool any = false;
            foreach (var item in list)
            {
                any = true;
                Indent(w, indent);
                w.Write("- ");
                if (IsScalar(item))
                {
                    WriteScalar(w, item);
                    w.WriteLine();
                }
                else
                {
                    w.WriteLine();
                    WriteNode(item, w, indent + IndentSize);
                }
            }
            if (!any) w.WriteLine("[]");
        }

        static bool IsScalar(object? v) =>
            v is null or string or bool
            || v is int or long or short or byte or float or double or decimal;

        static void WriteKey(TextWriter w, string key)
        {
            // 単純キーとして安全ならそのまま、必要ならクォート
            if (RequiresQuote(key)) w.Write(Quote(key));
            else w.Write(key);
        }

        static void WriteScalar(TextWriter w, object? v)
        {
            if (v is null) { w.Write("null"); return; }
            if (v is string s)
            {
                if (RequiresQuote(s)) w.Write(Quote(s));
                else w.Write(s);
                return;
            }
            if (v is bool b) { w.Write(b ? "true" : "false"); return; }

            // 数値
            w.Write(Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        static void Indent(TextWriter w, int n)
        {
            for (int i = 0; i < n; i++) w.Write(' ');
        }

        static bool RequiresQuote(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            if (char.IsWhiteSpace(s[0]) || char.IsWhiteSpace(s[^1])) return true;
            if (s.Contains('\n')) return true;
            // YAML的に特別扱いされる文字や見出し語
            if (s.IndexOfAny(new[] { ':', '-', '?', '#', ',', '[', ']', '{', '}', '&', '*', '!', '|', '>', '\'', '"', '%', '@', '`' }) >= 0) return true;
            // 数値や true/false と誤解されるもの
            if (bool.TryParse(s, out _)) return true;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return true;
            return false;
        }

        static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
