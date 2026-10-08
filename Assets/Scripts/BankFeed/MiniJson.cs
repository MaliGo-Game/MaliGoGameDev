using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MaliGo.BankFeed
{
    /// <summary>
    /// A small, strict JSON reader: objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, strings
    /// string, numbers the raw number text (so money never goes through a float), true/false bool, null null.
    /// Pure C# with no reflection, so it behaves the same in the logic tests and under IL2CPP. Returns false on any
    /// malformed input instead of throwing.
    /// </summary>
    public static class MiniJson
    {
        public static bool TryParse(string json, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            var reader = new Reader(json);
            try
            {
                value = reader.ReadValue(0);
                reader.SkipWhitespace();
                return reader.AtEnd;
            }
            catch (FormatException)
            {
                value = null;
                return false;
            }
        }

        sealed class Reader
        {
            const int MaxDepth = 64;

            readonly string s;
            int i;

            public Reader(string json)
            {
                s = json;
            }

            public bool AtEnd => i >= s.Length;

            public void SkipWhitespace()
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r' || s[i] == '﻿'))
                {
                    i++;
                }
            }

            char Peek()
            {
                if (i >= s.Length)
                {
                    throw new FormatException("unexpected end");
                }

                return s[i];
            }

            public object ReadValue(int depth)
            {
                if (depth > MaxDepth)
                {
                    throw new FormatException("too deep");
                }

                SkipWhitespace();
                char c = Peek();
                switch (c)
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                        {
                            return ReadNumber();
                        }

                        throw new FormatException("unexpected character");
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                {
                    throw new FormatException("expected " + word);
                }

                i += word.Length;
            }

            Dictionary<string, object> ReadObject(int depth)
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                i++;
                SkipWhitespace();
                if (Peek() == '}')
                {
                    i++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"')
                    {
                        throw new FormatException("expected key");
                    }

                    string key = ReadString();
                    SkipWhitespace();
                    if (Peek() != ':')
                    {
                        throw new FormatException("expected :");
                    }

                    i++;
                    result[key] = ReadValue(depth + 1);
                    SkipWhitespace();
                    char c = Peek();
                    i++;
                    if (c == '}')
                    {
                        return result;
                    }

                    if (c != ',')
                    {
                        throw new FormatException("expected , or }");
                    }
                }
            }

            List<object> ReadArray(int depth)
            {
                var result = new List<object>();
                i++;
                SkipWhitespace();
                if (Peek() == ']')
                {
                    i++;
                    return result;
                }

                while (true)
                {
                    result.Add(ReadValue(depth + 1));
                    SkipWhitespace();
                    char c = Peek();
                    i++;
                    if (c == ']')
                    {
                        return result;
                    }

                    if (c != ',')
                    {
                        throw new FormatException("expected , or ]");
                    }
                }
            }

            string ReadString()
            {
                i++;
                var sb = new StringBuilder();
                while (true)
                {
                    char c = Peek();
                    i++;
                    if (c == '"')
                    {
                        return sb.ToString();
                    }

                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    char e = Peek();
                    i++;
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture, out int code))
                            {
                                throw new FormatException("bad \\u escape");
                            }

                            sb.Append((char)code);
                            i += 4;
                            break;
                        default:
                            throw new FormatException("bad escape");
                    }
                }
            }

            string ReadNumber()
            {
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
                {
                    i++;
                }

                string text = s.Substring(start, i - start);
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    throw new FormatException("bad number");
                }

                return text;
            }
        }
    }
}
