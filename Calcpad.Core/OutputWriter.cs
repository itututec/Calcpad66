﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Calcpad.Core
{
    internal abstract class OutputWriter
    {
        internal enum OutputFormat
        {
            Text,
            Html,
            Xml
        }

        private readonly StringBuilder _stringBuilder = new(200);
        protected static int PowerOrder = Calculator.OperatorOrder[Calculator.OperatorIndex['^']];

        internal string FormatUnitsText(string text)
        {
            _stringBuilder.Clear();
            var literal = string.Empty;
            var power = string.Empty;
            var isPower = false;
            byte sub = 0;
            var brackets = new Stack<int>();

            for (int i = 0, len = text.Length; i < len; ++i)
            {
                var c = text[i];
                if (c == '·')
                    c = '*';

                switch (c)
                {
                    case '/':
                    case '*':
                    {
                        AppendPower();
                        _stringBuilder.Append(FormatOperator(c));
                        break;
                    }
                    case '^':
                        isPower = true;
                        break;
                    case '(':
                        brackets.Push(isPower ? power.Length : _stringBuilder.Length);
                        break;
                    case ')' when !brackets.Any():
#if BG
                        throw new MathParser.MathParserException("Липсва лява скоба '('.");
#else
                        throw new MathParser.MathParserException("Missing left bracket '('.");
#endif
                    case ')':
                    {
                        var index = brackets.Pop();
                        if (isPower && index < power.Length)
                        {
                            if (index == 0)
                                power = this is TextWriter ? $"({power})" : power;
                            else
                            {
                                var s = power[index..];
                                power = power.Remove(index);
                                power += $"({s})";
                            }
                        }
                        else
                        {
                            AppendPower();
                            var length = _stringBuilder.Length - index;
                            var s = _stringBuilder.ToString(index, length);
                            _stringBuilder.Remove(index, length);
                            _stringBuilder.Append(AddBrackets(s));
                        }
                        break;
                    }
                    default:
                    {
                        if (isPower)
                            power += c;
                        else
                        {
                            if (
                                sub == 0 && c == '_' ||
                                sub == 1 && (c == 'U' || c == 'm' || c == 'f') ||
                                sub == 2 && (c == 'K' || c == 'S')
                            )
                                sub++;
                            else
                                sub = 0;
                            literal += c;
                        }
                        break;
                    }
                }
            }
            if (brackets.Any())
#if BG
                throw new MathParser.MathParserException("Липсва дясна скоба ')'.");
#else
                throw new MathParser.MathParserException("Missing right bracket ')'.");
#endif
            if (literal.Length > 0)
                AppendPower();

            return _stringBuilder.ToString();

            string FormatLocal(string s)
            {
                var n = sub;
                if (n == 2 || n == 3)
                {
                    sub = 0;
                    return FormatSubscript(FormatUnits(s[..^n]), " " + s[^(n - 1)..]);
                }           
                return FormatUnits(s);
            }

            void AppendPower()
            {
                literal = FormatLocal(literal);
                if (isPower)
                {
                    if (this is XmlWriter)
                        power = XmlWriter.Run(power);

                    _stringBuilder.Append(FormatPower(literal, power, 0, -1));
                }
                else
                    _stringBuilder.Append(literal);

                literal = string.Empty;
                power = string.Empty;
                isPower = false;
            }
        }

        internal abstract string UnitString(Unit units);
        internal abstract string FormatInput(string s, Unit units, int line, bool isCalculated);
        internal abstract string FormatSubscript(string sa, string sb);
        internal abstract string FormatVariable(string name, string value);
        internal abstract string FormatUnits(string s);
        internal abstract string FormatFunction(string s);
        internal abstract string FormatSwitch(string[] sa, int level = 0);
        internal abstract string FormatIf(string sc, string sa, string sb, int level = 0);
        internal abstract string FormatRoot(string s, bool formatEquations, int level = 0, string n = "2");
        internal abstract string FormatOperator(char c);
        internal abstract string FormatPower(string sa, string sb, int level, int order);
        internal abstract string FormatDivision(string sa, string sb, int level);
        internal abstract string FormatNary(string symbol, string sub, string sup, string expr); //Integral, sum, product
        internal abstract string FormatValue(in Value v, int decimals);
        internal abstract string AddBrackets(string s, int level = 0);
        internal abstract string FormatAbs(string s, int level = 0);
        internal abstract string FormatReal(double d, int decimals);
        internal abstract string FormatComplex(double re, double im, int decimals);

        protected static string FormatOperatorHelper(char c) => c switch
        {
            Calculator.NegChar => "-",
            '-' => " – ",
            '<' => " < ",
            '>' => " > ",
            '+' => " + ",
            '=' => " = ",
            '≡' => " ≡ ",
            '≠' => " ≠ ",
            '≤' => " ≤ ",
            '≥' => " ≥ ",
            '⦼' => " mod ",
            '∧' => " and ",
            '∨' => " or ",
            '⊕' => " xor ",
            '*' => "·",
            ';' => "; ",
            _ => c.ToString()
        };

        protected string FormatComplexHelper(double re, double im, int decimals)
        {
            var t = Complex.Type(re, im);
            if (t == Complex.Types.Real)
                return FormatReal(re, decimals);

            if (t == Complex.Types.Imaginary)
                return FormatReal(im, decimals) + 'i';

            var sReal = FormatReal(re, decimals);
            var sImaginary = FormatReal(Math.Abs(im), decimals);
            return im < 0 ?
                $"{sReal} – {sImaginary}i" :
                $"{sReal} + {sImaginary}i";
        }

        private const string Sharps = "################";

        internal static string FormatNumberHelper(double d, int decimals)
        {
            if (double.IsNaN(d))
                return " Undefined ";
            if (double.IsPositiveInfinity(d))
                return "+∞";
            if (double.IsNegativeInfinity(d))
                return "-∞";
            if (double.IsInfinity(d))
                return "∞";

            if (Math.Abs(d) < 1e16)
            {
                var i = decimals - GetDigits(Math.Abs(d));
                if (i <= 16)
                {
                    if (i < 0)
                        i = 0;
                    else if (i == 16)
                        i = 15;

                    var s = Math.Round(d, i).ToString(CultureInfo.InvariantCulture);
                    return s == "-0" ? "0" : s;
                }
            }
            var format = "{0:#." + Sharps[..decimals] + "E+0}";
            return string.Format(CultureInfo.InvariantCulture, format, d);
        }

        private static int GetDigits(double d)
        {
            return d switch
            {
                0 => 0,
                <= 1.0 => d switch
                {
                    <= 1e-8 => d switch
                    {
                        <= 1e-12 when d <= 1e-14 => -15,
                        <= 1e-12 when d <= 1e-13 => -14,
                        <= 1e-12 => -13,
                        <= 1e-11 => -12,
                        <= 1e-10 => -11,
                        <= 1e-9 => -10,
                        _ => -9
                    },
                    <= 1e-4 when d <= 1e-7 => -8,
                    <= 1e-4 when d <= 1e-6 => -7,
                    <= 1e-4 when d <= 1e-5 => -6,
                    <= 1e-4 => -5,
                    <= 1e-3 => -4,
                    <= 1e-2 => -3,
                    <= 1e-1 => -2,
                    _ => -1
                },
                <= 1e8 => d switch
                {
                    <= 1e4 => 0,
                    <= 1e5 => 1,
                    <= 1e6 => 2,
                    <= 1e7 => 3,
                    _ => 4
                },
                <= 1e11 => d switch
                {
                    <= 1e9 => 5,
                    <= 1e10 => 6,
                    _ => 7
                },
                <= 1e12 => 8,
                <= 1e13 => 9,
                <= 1e14 => 10,
                _ => d <= 1e15 ? 11 : 0
            };
        }
    }

    internal class TextWriter : OutputWriter
    {
        internal override string UnitString(Unit units) => units.Text;
        internal override string FormatInput(string s, Unit units, int line, bool isCalculated) =>
            units is null ? s : s + ' ' + units.Text;

        internal override string FormatSubscript(string sa, string sb) => sa + "_" + sb;
        internal override string FormatVariable(string name, string value) => name;
        internal override string FormatUnits(string s) => s;
        internal override string FormatFunction(string s) => s;
        internal override string FormatRoot(string s, bool formatEquations, int level = 0, string n = "2") =>
            n switch
            {

                "2" => $"√({s})",
                "3" => $"³√({s})",
                "4" => $"⁴√({s})",
                _ => $"root({s}; {n})"  
            };

        internal override string FormatOperator(char c) => FormatOperatorHelper(c);
        internal override string FormatPower(string sa, string sb, int level, int order) => sa + '^' + sb;
        internal override string FormatDivision(string sa, string sb, int level) => sa + '/' + sb;

        internal override string FormatValue(in Value v, int decimals)
        {
            var s = FormatComplex(v.Re, v.Im, decimals);
            if (v.Units is not null)
            {
                if (!v.IsReal)
                    s = AddBrackets(s);

                return s + v.Units.Text;
            }
            return s;
        }

        internal override string AddBrackets(string s, int level = 0) => $"({s})";

        internal override string FormatAbs(string s, int level = 0) => $"|{s}|";
        internal override string FormatReal(double d, int decimals) => FormatNumberHelper(d, decimals);
        internal override string FormatComplex(double re, double im, int decimals)
        {
            if (double.IsNaN(re) && double.IsNaN(im))
                return " Undefined ";

            return FormatComplexHelper(re, im, decimals);
        }

        internal override string FormatNary(string symbol, string sub, string sup, string expr) =>
            $"{symbol}{{{expr}; {sub}...{sup}}}";

        internal override string FormatSwitch(string[] sa, int level = 0)
        {
            string s = "switch(" + sa[0];
            for (int i = 1, len = sa.Length; i < len; ++i)
                s += "; " + sa[i]; 

            return s +")";
        }

        internal override string FormatIf(string sc, string sa, string sb, int level = 0) =>
            $"if({sc}; {sa}; {sb})";

    }

    internal class HtmlWriter : OutputWriter
    {
        private static readonly string[] SqrPad = {
                "&ensp;&hairsp;",
                "&ensp;&ensp;",
                "&emsp;&thinsp;",
                "&emsp;&ensp;"
            };
            
        private static string RootPad(int level, string n) => level switch
            {
                0 => $"&hairsp;<sup class=\"nth\">{n}</sup>&hairsp;",
                1 => $"&nbsp;<small class=\"nth\">{n}</small>&nbsp;&hairsp;",
                2 => $"&hairsp;&nbsp;<small class=\"nth\">{n}</small>&nbsp;&thinsp;",
                _ => $"&ensp;<small class=\"nth\">{n}</small>&ensp;"
            };

        internal override string UnitString(Unit units) => units.Html;

        internal static string OffsetDivision(string s, int offset)
        {
            var n = s.IndexOf("\"dvc\"", StringComparison.Ordinal) + 4;
            if (n < 0)
                return s;

            return offset switch
            {
                -1 => s[..n] + " down" + s[n..],
                1 => s[..n] + " up" + s[n..],
                _ => s
            };
        }

        internal override string FormatInput(string s, Unit units, int line, bool isCalculated)
        {
            string output;
            if (s == "?")
                output = $"<input type=\"text\" size=\"2\" name=\"Var\" class=\"input-{line}\">&#8202;";
            else if (isCalculated)
                output = $"<u class=\"input-{line}\">{s}</u>";
            else
                output = $"<input type=\"text\" size=\"2\" name=\"Var\" class=\"input-{line}\" value=\"{s}\">&#8202;";

            if (units is not null)
                return $"{output} <i>{units.Html}</i>";

            return output;
        }

        internal override string FormatSubscript(string sa, string sb) => $"{sa}<sub>{sb}</sub>";
        internal override string FormatVariable(string name, string value)
        {
            var s = string.IsNullOrEmpty(value) ?
                string.Empty :
                $" class=\"value\" data-value=\"{value}\"";
            var i = name.IndexOf('_');
            if (i > 0)
            {
                var i1 = i + 1;
                if (i1 < name.Length)
                    return FormatSubscript($"<var{s}>{name[..i]}</var>", name[i1..]);
            }

            return $"<var{s}>{name}</var>";
        }

        internal override string FormatUnits(string s) => $"<i>{s}</i>";
        internal override string FormatFunction(string s)
        {
            var i = s.IndexOf('_');
            if (i > 0)
            {
                var i1 = i + 1;
                if (i1 < s.Length)
                    return FormatSubscript($"<b>{s[..i]}</b>", s[i1..]);
            }
            return $"<b>{s}</b>";
        }

        internal override string FormatRoot(string s, bool formatEquations, int level = 0, string n = "2")
        {
            if (n != "2")
            {
                if (level > 0)
                    return $"{RootPad(level, n)}<span class=\"o{level}\"><span class=\"r{level}\"></span>{s}</span>";

                return $"{RootPad(0, n)}<span class=\"o0\"><span class=\"r\">√</span>&hairsp;{s}</span>";
            }
            if (level > 0)
                return $"{SqrPad[level]}<span class=\"o{level}\"><span class=\"r{level}\"></span>{s}</span>";

            return $"{SqrPad[0]}<span class=\"o0\"><span class=\"r\">√</span>&hairsp;{s}</span>";
        }

        internal override string FormatOperator(char c) => c switch
        {
            '<' => " &lt; ",
            '>' => " &gt; ",
            '≤' => " &le; ",
            '≥' => " &ge; ",
            _ => FormatOperatorHelper(c),
        };

        internal override string FormatPower(string sa, string sb, int level, int order)
        {
            var s = level > 0 ? "<sup class=\"raised\">" : "<sup>";

            if (order == PowerOrder)
                s += '∙';
            return sa + s + sb + "</sup>";
        }

        internal override string FormatDivision(string sa, string sb, int level)
        {
            if (level < 4)
                return $"<span class=\"dvc\">{sa}<span class=\"dvl\"></span>{sb}</span>";

            return sa + " ÷ " + sb;
        }

        internal override string FormatNary(string symbol, string sub, string sup, string expr) =>
            $"<span class=\"dvr\"><small>{sup}</small><span class=\"nary\">{symbol}</span><small>{sub}</small></span>{expr}";

        internal override string FormatValue(in Value v, int decimals)
        {
            var s = FormatComplex(v.Re, v.Im, decimals);
            if (v.Units is null)
                return s;

            if (!v.IsReal)
                s = AddBrackets(s);

            return s + "&#8202;" + v.Units.Html;
        }

        internal override string AddBrackets(string s, int level = 0) =>
            level switch
            {
                0 => $"({s})",
                < 2 => FormatBrackets('(', level) + s + FormatBrackets(')', level),
                _ => FormatBrackets('[', level) + s + FormatBrackets(']', level)
            };

        private static string FormatLeftCurl(int level) =>
            $"<span class=\"c{level}\">{{</span>";

        private static string FormatBrackets(char symbol, int level) =>
            $"<span class=\"b{level}\">{symbol}</span>";

        internal override string FormatAbs(string s, int level = 0) =>
            level switch
            {
                0 => $"<b class=\"b0\">|</b>&hairsp;{s}&hairsp;<b class=\"b0\">|</b>",
                _ => $"<span class=\"b{level}\">|</span>{s}<span class=\"b{level}\">|</span>"
            };

        internal override string FormatReal(double d, int decimals)
        {
            var s = FormatNumberHelper(d, decimals);
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                return $"<span class=\"err\">{s}</span>";
            }
            var i = s.LastIndexOf('E');
            if (i <= 0)
                return s;

            var i1 = i + 1;
            var sign = s[i1++];
            if (sign is '+' or '-' or '0')
            {
                if (s[i1] is '0')
                    i1++;
            }
            return sign is '-' ?
                $"{s[..i]}×10<sup>-{s[i1..]}</sup>" :
                $"{s[..i]}×10<sup>{s[i1..]}</sup>";
        }

        internal override string FormatComplex(double re, double im, int decimals)
        {
            if (double.IsNaN(re) && double.IsNaN(im))
                return "<span class=\"err\"> Undefined </span>";

            return FormatComplexHelper(re, im, decimals);
        }

        internal override string FormatSwitch(string[] sa, int level = 0)
        {
            var s = $"if {sa[0]}: {sa[1]}";
            var len = sa.Length;
            for (int i = 2; i < len; i+= 2) 
            {
                if (len - i == 1)
                    s += $"<br />else: {sa[i]}";
                else
                    s += $"<br />if {sa[i]}: {sa[i + 1]}";
            }
            if (level > 8)
                level = 8;

            return $"{FormatLeftCurl(level)}<span class=\"dvs\">{s}</span>";
        }

        internal override string FormatIf(string sc, string sa, string sb, int level = 0)
        {
            var s = $"if {sc}: {sa}<br />else: {sb}";
            return $"{FormatLeftCurl(level)}<span class=\"dvs\">{s}</span>";
        }
    }

    internal class XmlWriter : OutputWriter
    {
        internal override string UnitString(Unit units) => units.Xml;
        internal override string FormatInput(string s, Unit units, int line, bool isCalculated)
        {
            string output;
            if (s == "?")
                output = Run(s);//<w:rPr><w:color w:val=\"FF0000\" /><w:shd w:fill=\"FFFF88\" /></w:rPr>
            else if (isCalculated)
                output = Run(s);//<w:rPr><w:u /><w:shd w:fill=\"FFFF88\" /></w:rPr>
            else
                output = Run(s);//<w:rPr><w:bdr w:val=\"single\" w:space=\"0\" w:color=\"000000\"<w:shd w:fill=\"FFFF88\" /></w:rPr>

            return units is null ? output : output + units.Xml;
        }
        internal override string FormatSubscript(string sa, string sb) =>
            $"<m:sSub><m:e>{Run(sa)}</m:e><m:sub>{Run(sb)}</m:sub></m:sSub>";
        internal override string FormatVariable(string name, string value)
        {
            var i = name.IndexOf('_');
            if (i <= 0)
                return Run(name); //<w:rPr><w:color w:val=\"0000FF\" /></w:rPr>

            var i1 = i + 1;
            return i1 < name.Length ?
                FormatSubscript(name[..i], name[(i + 1)..]) :
                Run(name);
        }
        internal override string FormatUnits(string s) => Run(s);
        internal override string FormatFunction(string s)
        {
            var i = s.IndexOf('_');
            if (i <= 0) return
                Run(s);

            var i1 = i + 1;
            return i1 < s.Length ?
                FormatSubscript(s[..i], s[(i + 1)..]) :
                Run(s);
        }
        internal override string FormatRoot(string s, bool formatEquations, int level = 0, string n = "2") => n switch
        {
            "2" => $"<m:rad><m:radPr><m:degHide m:val=\"1\"/></m:radPr><m:deg/><m:e>{s}</m:e></m:rad>",
            "3" => $"<m:rad><m:deg>{Run(n)}</m:deg><m:e>{s}</m:e></m:rad>",
            _ => $"<m:rad><m:deg>{n}</m:deg><m:e>{s}</m:e></m:rad>"
        };


        internal override string FormatOperator(char c) => c switch
        {
            '<' => Run("&lt;"),
            '>' => Run("&gt;"),
            '≤' => Run("&le;"),
            '≥' => Run("&ge;"),
            '*' => Run("·"),
            '⦼' => Run(" mod "),
            '∧' => Run(" and "),
            '∨' => Run(" or "),
            '⊕' => Run(" xor "),
            Calculator.NegChar => Run("-"),
            _ => Run(c.ToString())
        };

        internal override string FormatPower(string sa, string sb, int level, int order)
        {
            const string se = "</m:sup></m:sSup>";
            if (order == PowerOrder)
                return sa[..^se.Length] + FormatOperator('*') + sb + se;

            return $"<m:sSup><m:e>{sa}</m:e><m:sup>{sb}{se}";
        }

        internal override string FormatDivision(string sa, string sb, int level) =>
            $"<m:f><m:num>{sa}</m:num><m:den>{sb}</m:den></m:f>";

        internal override string FormatNary(string symbol, string sub, string sup, string expr)
        {
            var sProp = $"<m:naryPr><m:chr m:val=\"{symbol}\"/><m:limLoc m:val=\"undOvr\"/></m:naryPr>";
            var sSubSup = $"<m:sub>{sub}</m:sub><m:sup>{sup}</m:sup>";
            var sExpr = $"<m:e>{expr}</m:e>";
            return $"<m:nary>{sProp}{sSubSup}{sExpr}</m:nary>";
        }

        internal override string FormatValue(in Value v, int decimals)
        {
            var s = FormatComplex(v.Re, v.Im, decimals);
            if (v.Units is null)
                return s;

            if (!v.IsReal)
                s = AddBrackets(s);

            return s + v.Units.Xml;
        }

        internal override string AddBrackets(string s, int level = 0) =>
            level > 1 ? Brackets('[', ']', s) : Brackets('(', ')', s);

        internal override string FormatAbs(string s, int level = 0) => Brackets('|', '|', s);

        internal override string FormatReal(double d, int decimals)
        {
            var s = FormatNumberHelper(d, decimals);
            if (double.IsNaN(d) || double.IsInfinity(d))
                return Run(s);//<w:rPr><w:color w:val=\"FF0000\" /></w:rPr>

            var i = s.LastIndexOf('E');
            if (i <= 0)
                return Run(s);

            var i1 = i + 1;
            if (s[i1] == '+')
                i1++;

            return $"{Run(s[..i] + "×")}<m:sSup><m:e>{Run("10")}</m:e><m:sup><m:r><m:t>{s[i1..]}</m:t></m:r></m:sup></m:sSup>";
        }

        internal override string FormatComplex(double re, double im, int decimals)
        {
            if (double.IsNaN(re) && double.IsNaN(im))
                return Run("Undefined");//<w:rPr><w:color w:val=\"FF0000\" /></w:rPr>

            var t = Complex.Type(re, im);
            if (t == Complex.Types.Real)
                return FormatReal(re, decimals);

            var sImaginary = FormatReal(Math.Abs(im), decimals) + Run("i");
            if (t == Complex.Types.Imaginary)
                return sImaginary;

            var sReal = FormatReal(re, decimals);
            return im < 0 ?
                sReal + Run("–") + sImaginary :
                sReal + Run("+") + sImaginary;
        }

        internal static string Run(string s) => $"<m:r><m:t>{s}</m:t></m:r>";
        internal static string Brackets(char opening, char closing, string s) =>
            $"<m:d><m:dPr><m:begChr m:val=\"{opening}\"/><m:endChr m:val=\"{closing}\"/></m:dPr><m:e>{s}</m:e></m:d>";

        internal override string FormatSwitch(string[] sa, int level = 0)
        {
            var s = FormatIfRow(sa[0], sa[1]);
            var len = sa.Length;
            for (int i = 2; i < len; i += 2)
            {
                if (len - i == 1)
                    s += FormatElseRow(sa[i]);
                else
                    s += FormatIfRow(sa[i], sa[i + 1]);
            }
            return $"<m:d><m:dPr><m:begChr m:val=\"{{\"/><m:endChr m:val=\" \"/></m:dPr><m:e><m:m>{mPr}{s}</m:m></m:e></m:d>";
        }

        internal override string FormatIf(string sc, string sa, string sb, int level = 0)
        {
            var s = FormatIfRow(sc, sa) + FormatElseRow(sb);
            return $"<m:d><m:dPr><m:begChr m:val=\"{{\"/><m:endChr m:val=\" \"/></m:dPr><m:e><m:m>{mPr}{s}</m:m></m:e></m:d>";
        }

        private const string mPr = "<m:mPr><m:mcs><m:mc><m:mcPr><m:count m:val=\"2\"/><m:mcJc m:val=\"left\"/></m:mcPr></m:mc></m:mcs></m:mPr>";

        private static string FormatIfRow(string sa, string sb) =>
            $"<m:mr><m:e>{Run(" if ")}{sa}{Run(":")}</m:e><m:e>{sb}</m:e></m:mr>";
        private static string FormatElseRow(string sa) =>
            $"<m:mr><m:e>{Run(" else:")}</m:e><m:e>{sa}</m:e></m:mr>";
    }
}
