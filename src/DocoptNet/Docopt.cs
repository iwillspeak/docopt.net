namespace DocoptNet
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    partial class Docopt
    {
        public event EventHandler<PrintExitEventArgs> PrintExit;

        public IDictionary<string, ValueObject> Apply(string doc)
        {
            return Apply(doc, new Tokens(Enumerable.Empty<string>(), typeof (DocoptInputErrorException)));
        }

        public IDictionary<string, ValueObject> Apply(string doc, ICollection<string> argv, bool help = true,
            object version = null, bool optionsFirst = false, bool exit = false)
        {
            return Apply(doc, new Tokens(argv, typeof (DocoptInputErrorException)), help, version, optionsFirst, exit);
        }

        protected IDictionary<string, ValueObject> Apply(string doc, Tokens tokens,
            bool help = true,
            object version = null, bool optionsFirst = false, bool exit = false)
        {
            try
            {
                SetDefaultPrintExitHandlerIfNecessary(exit);
                var usageSections = ParseSection("usage:", doc);
                if (usageSections.Length == 0)
                    throw new DocoptLanguageErrorException("\"usage:\" (case-insensitive) not found.");
                if (usageSections.Length > 1)
                    throw new DocoptLanguageErrorException("More that one \"usage:\" (case-insensitive).");
                var exitUsage = usageSections[0];
                var options = ParseDefaults(doc);
                var pattern = ParsePattern(FormalUsage(exitUsage), options);
                var arguments = ParseArgv(tokens, options, optionsFirst).AsReadOnly();
                var patternOptions = pattern.Flat<Option>().Distinct().ToList();
                // [default] syntax for argument is disabled
                foreach (OptionsShortcut optionsShortcut in pattern.Flat(typeof (OptionsShortcut)))
                {
                    var docOptions = ParseDefaults(doc);
                    optionsShortcut.Children = docOptions.Distinct().Except(patternOptions).ToList();
                }

                if (help && arguments.Any(o => o is { Name: "-h" or "--help", Value: { IsNullOrEmpty: false } }))
                    OnPrintExit(doc);

                if (version is not null && arguments.Any(o => o is { Name: "--version", Value: { IsNullOrEmpty: false } }))
                    OnPrintExit(version.ToString());

                if (pattern.Fix().Match(arguments) is (true, { Count: 0 }, var collected))
                {
                    var dict = new Dictionary<string, ValueObject>();
                    foreach (var p in pattern.Flat().OfType<LeafPattern>().Concat(collected))
                        dict[p.Name] = p.Value;
                    return dict;
                }
                throw new DocoptInputErrorException(exitUsage);
            }
            catch (DocoptBaseException e)
            {
                if (!exit)
                    throw;

                OnPrintExit(e.Message, e.ErrorCode);

                return null;
            }
        }

        private void SetDefaultPrintExitHandlerIfNecessary(bool exit)
        {
            if (exit && PrintExit == null)
                // Default behaviour is to print usage
                // and exit with error code 1
                PrintExit += (sender, args) =>
                {
                    Console.WriteLine(args.Message);
                    Environment.Exit(args.ErrorCode);
                };
        }

        public string GenerateCode(string doc)
        {
            var res = GetFlatPatterns(doc);
            res = res
                .GroupBy(pattern => pattern.Name)
                .Select(group => group.First());
            var sb = new StringBuilder();
            foreach (var p in res)
            {
                sb.AppendLine(p.GenerateCode());
            }
            return sb.ToString();
        }

        public IEnumerable<Node> GetNodes(string doc)
        {
            return GetFlatPatterns(doc)
                .Select(p => p.ToNode())
                .Where(p => p != null)
                .ToArray();
        }

        static IEnumerable<Pattern> GetFlatPatterns(string doc)
        {
            var usageSections = ParseSection("usage:", doc);
            if (usageSections.Length == 0)
                throw new DocoptLanguageErrorException("\"usage:\" (case-insensitive) not found.");
            if (usageSections.Length > 1)
                throw new DocoptLanguageErrorException("More that one \"usage:\" (case-insensitive).");
            var exitUsage = usageSections[0];
            var options = ParseDefaults(doc);
            var pattern = ParsePattern(FormalUsage(exitUsage), options);
            var patternOptions = pattern.Flat<Option>().Distinct().ToList();
            // [default] syntax for argument is disabled
            foreach (OptionsShortcut optionsShortcut in pattern.Flat(typeof (OptionsShortcut)))
            {
                var docOptions = ParseDefaults(doc);
                optionsShortcut.Children = docOptions.Distinct().Except(patternOptions).ToList();
            }
            return pattern.Fix().Flat();
        }

        protected void OnPrintExit(string doc, int errorCode = 0)
        {
            if (PrintExit == null)
            {
                throw new DocoptExitException(doc);
            }
            else
            {
                PrintExit(this, new PrintExitEventArgs(doc, errorCode));
            }
        }

        /// <summary>
        ///     Parse command-line argument vector.
        /// </summary>
        internal static IList<LeafPattern> ParseArgv(Tokens tokens, ICollection<Option> options,
            bool optionsFirst = false)
        {
            //    If options_first:
            //        argv ::= [ long | shorts ]* [ argument ]* [ '--' [ argument ]* ] ;
            //    else:
            //        argv ::= [ long | shorts | argument ]* [ '--' [ argument ]* ] ;

            var parsed = new List<LeafPattern>();
            while (tokens.Current() != null)
            {
                if (tokens.Current() == "--")
                {
                    parsed.AddRange(tokens.Select(v => new Argument(null, new ValueObject(v))));
                    return parsed;
                }

                if (tokens.Current().StartsWith("--"))
                {
                    parsed.AddRange(ParseLong(tokens, options));
                }
                else if (tokens.Current().StartsWith("-") && tokens.Current() != "-")
                {
                    parsed.AddRange(ParseShorts(tokens, options));
                }
                else if (optionsFirst)
                {
                    parsed.AddRange(tokens.Select(v => new Argument(null, new ValueObject(v))));
                    return parsed;
                }
                else
                {
                    parsed.Add(new Argument(null, new ValueObject(tokens.Move())));
                }
            }
            return parsed;
        }

        internal static string FormalUsage(string exitUsage)
        {
            var (_, _, section) = exitUsage.Partition(":"); // drop "usage:"
            var pu = section.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
            var join = new StringBuilder();
            join.Append("( ");
            for (var i = 1; i < pu.Length; i++)
            {
                var s = pu[i];
                if (i > 1) join.Append(" ");
                join.Append((s == pu[0]) ? ") | (" : s);
            }
            join.Append(" )");
            return join.ToString();
        }

        internal static Required ParsePattern(string source, ICollection<Option> options)
        {
            var tokens = Tokens.FromPattern(source);
            var result = ParseExpr(tokens, options);
            if (tokens.Current() != null)
                throw tokens.CreateException("unexpected ending: " + string.Join(" ", tokens.ToArray()));
            return new Required(result.ToArray());
        }

        private static IEnumerable<Pattern> ParseExpr(Tokens tokens, ICollection<Option> options)
        {
            // expr ::= seq ( '|' seq )* ;
            var seq = ParseSeq(tokens, options);
            if (tokens.Current() != "|")
                return seq;
            var result = new List<Pattern>();
            if (seq.Count() > 1)
            {
                result.Add(new Required(seq.ToArray()));
            }
            else
            {
                result.AddRange(seq);
            }
            while (tokens.Current() == "|")
            {
                tokens.Move();
                seq = ParseSeq(tokens, options);
                if (seq.Count() > 1)
                {
                    result.Add(new Required(seq.ToArray()));
                }
                else
                {
                    result.AddRange(seq);
                }
            }
            result = result.Distinct().ToList();
            if (result.Count > 1)
                return new[] {new Either(result.ToArray())};
            return result;
        }

        private static ICollection<Pattern> ParseSeq(Tokens tokens, ICollection<Option> options)
        {
            // seq ::= ( atom [ '...' ] )* ;
            var result = new List<Pattern>();
            while (!new[] {null, "]", ")", "|"}.Contains(tokens.Current()))
            {
                var atom = ParseAtom(tokens, options);
                if (tokens.Current() == "...")
                {
                    result.Add(new OneOrMore(atom.ToArray()));
                    tokens.Move();
                    return result;
                }
                result.AddRange(atom);
            }
            return result;
        }

        private static IEnumerable<Pattern> ParseAtom(Tokens tokens, ICollection<Option> options)
        {
            // atom ::= '(' expr ')' | '[' expr ']' | 'options'
            //  | long | shorts | argument | command ;

            var token = tokens.Current();
            var result = new List<Pattern>();
            switch (token)
            {
                case "[":
                case "(":
                {
                    tokens.Move();
                    string matching;
                    if (token == "(")
                    {
                        matching = ")";
                        result.Add(new Required(ParseExpr(tokens, options).ToArray()));
                    }
                    else
                    {
                        matching = "]";
                        result.Add(new Optional(ParseExpr(tokens, options).ToArray()));
                    }
                    if (tokens.Move() != matching)
                        throw tokens.CreateException("unmatched '" + token + "'");
                }
                    break;
                case "options":
                    tokens.Move();
                    result.Add(new OptionsShortcut());
                    break;
                default:
                    if (token.StartsWith("--") && token != "--")
                    {
                        return ParseLong(tokens, options);
                    }
                    if (token.StartsWith("-") && token != "-" && token != "--")
                    {
                        return ParseShorts(tokens, options);
                    }
                    if ((token.StartsWith("<") && token.EndsWith(">")) || token.All(c => char.IsUpper(c)))
                    {
                        result.Add(new Argument(tokens.Move()));
                    }
                    else
                    {
                        result.Add(new Command(tokens.Move()));
                    }
                    break;
            }
            return result;
        }

        private static IEnumerable<Option> ParseShorts(Tokens tokens, ICollection<Option> options)
        {
            // shorts ::= '-' ( chars )* [ [ ' ' ] chars ] ;

            var token = tokens.Move();
            Debug.Assert(token.StartsWith("-") && !token.StartsWith("--"));
            var left = token.TrimStart(new[] {'-'});
            var parsed = new List<Option>();
            while (left != "")
            {
                var shortName = "-" + left[0];
                left = left.Substring(1);
                var similar = options.Where(o => o.ShortName == shortName).ToList();
                Option option = null;
                if (similar.Count > 1)
                {
                    throw tokens.CreateException($"{shortName} is specified ambiguously {similar.Count} times");
                }
                if (similar.Count < 1)
                {
                    option = new Option(shortName, null, 0);
                    options.Add(option);
                    if (tokens.ThrowsInputError)
                    {
                        option = new Option(shortName, null, 0, new ValueObject(true));
                    }
                }
                else
                {
                    // why is copying necessary here?
                    option = new Option(shortName, similar[0].LongName, similar[0].ArgCount, similar[0].Value);
                    ValueObject value = null;
                    if (option.ArgCount != 0)
                    {
                        if (left == "")
                        {
                            if (tokens.Current() == null || tokens.Current() == "--")
                            {
                                throw tokens.CreateException(shortName + " requires argument");
                            }
                            value = new ValueObject(tokens.Move());
                        }
                        else
                        {
                            value = new ValueObject(left);
                            left = "";
                        }
                    }
                    if (tokens.ThrowsInputError)
                        option.Value = value ?? new ValueObject(true);
                }
                parsed.Add(option);
            }
            return parsed;
        }

        private static IEnumerable<Option> ParseLong(Tokens tokens, ICollection<Option> options)
        {
            // long ::= '--' chars [ ( ' ' | '=' ) chars ] ;
            var (longName, eq, value) = tokens.Move().Partition("=") switch
            {
                (var ln, "", _) => (ln, false, null),
                var (ln, _, vs) => (ln, true, new ValueObject(vs))
            };
            Debug.Assert(longName.StartsWith("--"));
            var similar = options.Where(o => o.LongName == longName).ToList();
            if (tokens.ThrowsInputError && similar.Count == 0)
            {
                // If not exact match
                similar =
                    options.Where(o => !string.IsNullOrEmpty(o.LongName) && o.LongName.StartsWith(longName)).ToList();
            }
            if (similar.Count > 1)
            {
                // Might be simply specified ambiguously 2+ times?
                throw tokens.CreateException($"{longName} is not a unique prefix: {string.Join(", ", similar.Select(o => o.LongName))}?");
            }
            Option option = null;
            if (similar.Count < 1)
            {
                var argCount = eq ? 1 : 0;
                option = new Option(null, longName, argCount);
                options.Add(option);
                if (tokens.ThrowsInputError)
                    option = new Option(null, longName, argCount, argCount != 0 ? value : new ValueObject(true));
            }
            else
            {
                option = new Option(similar[0].ShortName, similar[0].LongName, similar[0].ArgCount, similar[0].Value);
                if (option.ArgCount == 0)
                {
                    if (value != null)
                        throw tokens.CreateException(option.LongName + " must not have an argument");
                }
                else
                {
                    if (value == null)
                    {
                        if (tokens.Current() == null || tokens.Current() == "--")
                            throw tokens.CreateException(option.LongName + " requires an argument");
                        value = new ValueObject(tokens.Move());
                    }
                }
                if (tokens.ThrowsInputError)
                    option.Value = value ?? new ValueObject(true);
            }
            return new[] {option};
        }

        internal static ICollection<Option> ParseDefaults(string doc)
        {
            var defaults = new List<Option>();
            foreach (var s in ParseSection("options:", doc))
            {
                // FIXME corner case "bla: options: --foo"

                var (_, _, optionsText) = s.Partition(":"); // get rid of "options:"
                var a = Regex.Split("\n" + optionsText, @"\r?\n[ \t]*(-\S+?)");
                var split = new List<string>();
                for (var i = 1; i < a.Length - 1; i += 2)
                {
                    var s1 = a[i];
                    var s2 = a[i + 1];
                    split.Add(s1 + s2);
                }
                var options = split.Where(x => x.StartsWith("-")).Select(x => Option.Parse(x));
                defaults.AddRange(options);
            }
            return defaults;
        }

        internal static string[] ParseSection(string name, string source)
        {
            var pattern = new Regex(@"^([^\r\n]*" + Regex.Escape(name) + @"[^\r\n]*\r?\n?(?:[ \t].*?(?:\r?\n|$))*)",
                                    RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return (from Match match in pattern.Matches(source) select match.Value.Trim()).ToArray();
        }
    }

    partial class PrintExitEventArgs : EventArgs
    {
        public PrintExitEventArgs(string msg, int errorCode)
        {
            Message = msg;
            ErrorCode = errorCode;
        }

        public string Message { get; set; }
        public int ErrorCode { get; set; }
    }
}
