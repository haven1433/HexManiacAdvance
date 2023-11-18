using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Code {
   public interface IScriptLine {
      IReadOnlyList<IScriptArg> Args { get; }
      IReadOnlyList<byte> LineCode { get; }
      string LineCommand { get; }
      IReadOnlyList<string> Documentation { get; }
      string Usage { get; }

      bool IsEndingCommand { get; }

      bool MatchesGame(int gameCodeHash);
      int CompiledByteLength(IDataModel model, int start, IDictionary<int, int> destinationLengths); // compile from the bytes in the model, at that start location
      int CompiledByteLength(IDataModel model, string line); // compile from the line of code passed in
      bool Matches(int gameCodeHash, IReadOnlyList<byte> data, int index);
      string Decompile(IDataModel data, int start, DecompileLabelLibrary labels, IList<ExpectedPointerType> streamTypes);

      /// <summary>
      /// Returns true if the command looks correct, even if the arguments are incomplete.
      /// </summary>
      bool CanCompile(string line);

      /// <summary>
      /// Returns an error if the line cannot be compiled, or a set of tokens if it can be compiled.
      /// </summary>
      string ErrorCheck(string scriptLine, out string[] tokens);

      string Compile(IDataModel model, int start, string scriptLine, LabelLibrary labels, out byte[] result);

      void AddDocumentation(string content);

      public int CountShowArgs() {
         return Args.Sum(arg => {
            if (arg is ScriptArg) return 1;
            return 0;
            // something with array args?
         });
      }
   }

   public class MacroScriptLine : IScriptLine {
      private static readonly IReadOnlyList<byte> emptyByteList = new byte[0];
      private readonly List<string> documentation = new List<string>();

      private bool hasShortForm;
      private readonly Dictionary<int, int> shortIndexFromLongIndex = new();
      private readonly IReadOnlyList<int> matchingGames;

      public IReadOnlyList<IScriptArg> Args { get; }
      public IReadOnlyList<IScriptArg> ShortFormArgs {
         get {
            if (shortIndexFromLongIndex.Count == 0) {
               return Args.Where(arg => arg is not SilentMatchArg).ToList();
            }
            var args = new IScriptArg[shortIndexFromLongIndex.Values.Distinct().Count()];
            foreach (var pair in shortIndexFromLongIndex) {
               args[pair.Value] = Args[pair.Key];
            }
            return args;
         }
      }
      public IReadOnlyList<byte> LineCode => emptyByteList;
      public IReadOnlyList<string> Documentation => documentation;
      public string LineCommand { get; }
      public bool IsEndingCommand => false;
      public bool IsValid { get; } = true;
      public string Usage { get; private set; }

      public static bool IsMacroLine(string engineLine) {
         engineLine = engineLine.Trim();
         var tokens = engineLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
         if (tokens.Length == 0) return false;
         var token = tokens[0];
         if (token.StartsWith("[") && tokens.Length > 1) token = tokens[1];
         if (token.StartsWith("#")) return false;
         if (token.Length == 2 && token.TryParseHex(out _)) return false;
         return true;
      }

      public MacroScriptLine(string engineLine) {
         var docSplit = engineLine.Split(new[] { '#' }, 2);
         if (docSplit.Length > 1) documentation.Add('#' + docSplit[1]);
         engineLine = docSplit[0].Trim();
         matchingGames = ScriptLine.ExtractMatchingGames(ref engineLine);
         ExtractShortformInfo(ref engineLine);
         if (!hasShortForm) {
            Usage = " ".Join(engineLine.Split(' ').Where(token => token.Length != 2 || !token.TryParseHex(out _)));
         }
         var usageTokens = Usage.Split(" ", StringSplitOptions.RemoveEmptyEntries);
         Usage = usageTokens[0] + " " + " ".Join(usageTokens.Skip(1).Select(t => t.Split(".:|<".ToCharArray())[0]));

         var tokens = engineLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         var args = new List<IScriptArg>();
         LineCommand = tokens[0];

         for (int i = 1; i < tokens.Length; i++) {
            var token = tokens[i];
            if (token.Length == 2 && token.TryParseHex(out int number)) {
               args.Add(new SilentMatchArg((byte)number));
            } else if (ScriptArg.IsValidToken(token)) {
               args.Add(new ScriptArg(token));
            } else {
               IsValid = false;
            }
         }

         Args = args;
      }

      private void ExtractShortformInfo(ref string engineLine) {
         if (!engineLine.Contains("->")) return;
         var parts = engineLine.Split("->");
         if (parts.Length != 2) return;
         engineLine = parts[1];
         var shortTokens = parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
         var longTokens = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
         if (shortTokens[0] != longTokens[0]) return;
         shortTokens = shortTokens.Skip(1).ToArray();
         longTokens = longTokens.Skip(1).ToArray();

         // for each entry in long, it shows up somewhere in short
         // entries in long can appear multiple times
         for (int i = 0; i < longTokens.Length; i++) {
            var index = shortTokens.IndexOf(longTokens[i]);
            if (index == -1) continue;
            shortIndexFromLongIndex.Add(i, index);
         }

         hasShortForm = true;
         Usage = parts[0];
      }

      public bool MatchesGame(int game) => matchingGames?.Contains(game) ?? true;

      public int CompiledByteLength(IDataModel model, int start, IDictionary<int, int> destinationLengths) {
         var length = LineCode.Count;
         foreach (var arg in Args) {
            if (destinationLengths != null) {
               var argLength = ScriptParser.GetArgLength(model, arg, start + length, destinationLengths);
               if (argLength > 0) destinationLengths[model.ReadPointer(start + length)] = argLength;
            }
            length += arg.Length(default, -1);
         }
         return length;
      }

      public int CompiledByteLength(IDataModel model, string line) {
         if (!CanCompile(line)) return 0;
         var length = LineCode.Count;
         foreach (var arg in Args) {
            length += arg.Length(default, -1);
         }
         return length;
      }

      public bool Matches(int gameCodeHash, IReadOnlyList<byte> data, int index) {
         if (Args.Count == 0) return false;
         if (!MatchesGame(gameCodeHash)) return false;
         for (int i = 0; i < Args.Count; i++) {
            var arg = Args[i];
            if (arg is SilentMatchArg smarg) {
               if (data[index] != smarg.ExpectedValue) return false;
            } else if (arg is ScriptArg sarg) {
               // don't validate, this part is variable
            } else {
               throw new NotImplementedException();
            }
            index += arg.Length(default, -1);
         }
         return true;
      }

      public string Decompile(IDataModel data, int start, DecompileLabelLibrary labels, IList<ExpectedPointerType> streamTypes) {
         var builder = new StringBuilder(LineCommand);
         var streamContent = new List<string>();
         var args = new List<string>();
         foreach (var arg in Args) {
            if (arg is ScriptArg sarg) {
               var tempBuilder = new StringBuilder();
               sarg.Build(false, data, start, tempBuilder, streamContent, labels, streamTypes);
               args.Add(tempBuilder.ToString());
            }
            start += arg.Length(data, start);
         }
         if (args.Count > 0) {
            builder.Append(" ");
            builder.Append(" ".Join(ConvertLongFormToShortForm(args.ToArray())));
         }
         foreach (var content in streamContent) {
            builder.AppendLine();
            builder.AppendLine("{");
            builder.AppendLine(content);
            builder.Append("}");
         }
         return builder.ToString();
      }

      public bool CanCompile(string line) {
         var tokens = ScriptLine.Tokenize(line);
         if (tokens.Length == 0) return false;
         if (tokens[0] != LineCommand) return false;
         return true;
      }

      public string ErrorCheck(string scriptLine, out string[] tokens) {
         tokens = ScriptLine.Tokenize(scriptLine);
         for (int i = 1; i < scriptLine.Length - 1; i++) {
            if (scriptLine[i] != '"') continue;
            if (scriptLine[i - 1] != ' ' && scriptLine[i + 1] != ' ') return "Cannot have \"quotes\" in the middle of a name.";
         }
         if (tokens[0] != LineCommand) throw new ArgumentException($"Command {LineCommand} was expected, but received {tokens[0]} instead.");
         var args = tokens.Skip(1).ToArray();
         var shortArgs = args;
         args = ConvertShortFormToLongForm(args);
         var commandText = LineCommand;
         var specifiedArgs = Args.Where(arg => arg is ScriptArg).Count();
         if (specifiedArgs != args.Length) {
            return $"Command {commandText} expects {specifiedArgs} arguments, but received {shortArgs.Length} instead.";
         }
         return null;
      }

      public string Compile(IDataModel model, int start, string scriptLine, LabelLibrary labels, out byte[] result) {
         result = null;
         var error = ErrorCheck(scriptLine, out var tokens);
         if (error != null) return error;
         var args = tokens.Skip(1).ToArray();
         args = ConvertShortFormToLongForm(args);
         var results = new List<byte>();
         var specifiedArgIndex = 0;
         for (int i = 0; i < Args.Count; i++) {
            if (Args[i] is ScriptArg scriptArg) {
               var token = args[specifiedArgIndex];
               var message = scriptArg.Build(model, start + results.Count, token, results, labels);
               if (message != null) return message;
               specifiedArgIndex += 1;
            } else if (Args[i] is SilentMatchArg silentArg) {
               results.Add(silentArg.ExpectedValue);
            }
         }
         result = results.ToArray();
         return null;
      }

      public void AddDocumentation(string doc) => documentation.Add(doc);

      private string[] ConvertShortFormToLongForm(string[] args) {
         if (!hasShortForm) return args;
         // build long-form args from this short form
         var longForm = new List<string>();
         for (int i = 0; i < Args.Count; i++) {
            if (Args[i] is SilentMatchArg) continue;
            var shortIndex = shortIndexFromLongIndex[i];
            if (shortIndex < args.Length) longForm.Add(args[shortIndex]);
         }
         return longForm.ToArray();
      }

      private string[] ConvertLongFormToShortForm(string[] args) {
         if (!hasShortForm) return args;
         var shortForm = new Dictionary<int, string>();
         int j = 0;
         for (int i = 0; i < Args.Count; i++) {
            if (Args[i] is SilentMatchArg) continue;
            var shortIndex = shortIndexFromLongIndex[i];
            shortForm[shortIndex] = args[j];
            j += 1;
         }
         return shortForm.Count.Range(i => shortForm[i]).ToArray();
      }
   }

   public abstract class ScriptLine : IScriptLine {
      private readonly List<string> documentation = new List<string>();
      private readonly IReadOnlyList<int> matchingGames;

      public const string Hex = "0123456789ABCDEF";
      public IReadOnlyList<IScriptArg> Args { get; }
      public IReadOnlyList<byte> LineCode { get; }
      public string LineCommand { get; }
      public IReadOnlyList<string> Documentation => documentation;
      public string Usage { get; }

      public virtual bool IsEndingCommand { get; }

      /// <param name="destinationLengths">If this line contains pointers, calculate the pointer data's lengths and include here.</param>
      public int CompiledByteLength(IDataModel model, int start, IDictionary<int, int> destinationLengths) {
         var length = LineCode.Count;
         foreach (var arg in Args) {
            if (arg.Type == ArgType.Pointer) {
               var destination = model.ReadPointer(start + length);
               if (destinationLengths != null && !destinationLengths.ContainsKey(destination)) {
                  var argLength = ScriptParser.GetArgLength(model, arg, start + length, destinationLengths);
                  if (argLength > 0) destinationLengths[destination] = argLength;
               }
            }
            length += arg.Length(model, start + length);
         }
         return length;
      }
      public int CompiledByteLength(IDataModel model, string line) {
         var length = LineCode.Count;
         var tokens = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         for (var i = 0; i < Args.Count; i++) {
            if (Args[i] is ScriptArg sarg) length += sarg.Length(default, -1);
            if (Args[i] is ArrayArg aarg) length += aarg.ConvertMany(model, tokens.Skip(i + 1)).Count() * aarg.TokenLength + 1;
         }
         return length;
      }

      public ScriptLine(string engineLine) {
         var docSplit = engineLine.Split(new[] { '#' }, 2);
         if (docSplit.Length > 1) documentation.Add('#' + docSplit[1]);
         engineLine = docSplit[0].Trim();
         matchingGames = ExtractMatchingGames(ref engineLine);
         Usage = engineLine.Split(new[] { ' ' }, 2).Last();
         var usageTokens = Usage.Split(" ", StringSplitOptions.RemoveEmptyEntries);
         Usage = usageTokens[0] + " " + " ".Join(usageTokens.Skip(1).Select(t => t.Split(".:|<".ToCharArray())[0]));

         var tokens = engineLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         var lineCode = new List<byte>();
         var args = new List<IScriptArg>();

         foreach (var token in tokens) {
            if (token.Length == 2 && token.All(ViewPort.AllHexCharacters.Contains)) {
               lineCode.Add(byte.Parse(token, NumberStyles.HexNumber));
            } else if (token.StartsWith("[") && token.EndsWith("]")) {
               var content = token.Substring(1, token.Length - 2);
               args.Add(new ArrayArg(content));
            } else if (ScriptArg.IsValidToken(token)) {
               args.Add(new ScriptArg(token));
            } else {
               LineCommand = token;
            }
         }

         LineCode = lineCode;
         Args = args;
      }

      public static IReadOnlyList<int> ExtractMatchingGames(ref string line) {
         if (!line.StartsWith("[")) return null;
         var gamesEnd = line.IndexOf("]");
         if (gamesEnd == -1) return null;
         var games = line.Substring(1, gamesEnd - 1);
         line = line.Substring(gamesEnd + 1).TrimStart();
         return games.Split("_").Select(ConvertAscii).ToList();
      }

      public static IReadOnlyList<string> GetMatchingGames(IScriptLine line) {
         var names = new[] { "AXVE", "AXPE", "BPRE", "BPGE", "BPEE" };
         return names.Where(name => line.MatchesGame(ConvertAscii(name))).ToList();
      }

      public static int ConvertAscii(string letters) {
         return letters.Reverse().Aggregate(0, (current, letter) => (current << 8) | (byte)letter);
      }

      public bool MatchesGame(int game) => matchingGames?.Contains(game) ?? true;

      public void AddDocumentation(string doc) => documentation.Add(doc);

      public bool PartialMatchLine(string line) => LineCommand.MatchesPartial(line.Split(' ')[0]);

      public bool Matches(int gameCodeHash, IReadOnlyList<byte> data, int index) {
         if (index + LineCode.Count >= data.Count) return false;
         if (MatchesGame(gameCodeHash)) {
            var result = true;
            for (int i = 0; result && i < LineCode.Count; i++) result = data[index + i] == LineCode[i]; // avoid making lambda for performance
            return result;
         }
         return false;
      }

      public bool CanCompile(string line) {
         if (!(line + " ").StartsWith(LineCommand + " ", StringComparison.CurrentCultureIgnoreCase)) return false;
         if (LineCode.Count == 1) return true;
         var tokens = Tokenize(line).ToList();
         if (tokens.Count < LineCode.Count) return false;
         tokens.RemoveAt(0);
         for (int i = 1; i < LineCode.Count; i++) {
            if (!byte.TryParse(tokens[0], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var value)) return false;
            if (value != LineCode[i]) return false;
            tokens.RemoveAt(0);
         }
         return true;
      }

      public string ErrorCheck(string scriptLine, out string[] tokens) {
         tokens = Tokenize(scriptLine);
         if (!tokens[0].Equals(LineCommand, StringComparison.CurrentCultureIgnoreCase)) throw new ArgumentException($"Command {LineCommand} was expected, but received {tokens[0]} instead.");
         var commandText = LineCommand;
         for (int i = 1; i < LineCode.Count; i++) commandText += " " + LineCode[i].ToString("X2");
         var fillerCount = Args.Count(arg => arg.Name == "filler");
         for (int i = 0; i < fillerCount; i++) {
            if (tokens.Length < Args.Count + LineCode.Count) tokens = tokens.Append("0").ToArray();
         }
         if (Args.Count > 0 && Args.Last() is ArrayArg) {
            if (Args.Count > tokens.Length) {
               return $"Command {commandText} expects {Args.Count} arguments, but received {tokens.Length - LineCode.Count} instead.";
            }
         } else if (Args.Count != tokens.Length - LineCode.Count) {
            return $"Command {commandText} expects {Args.Count} arguments, but received {tokens.Length - LineCode.Count} instead.";
         }
         return null;
      }

      public string Compile(IDataModel model, int start, string scriptLine, LabelLibrary labels, out byte[] result) {
         result = null;
         var error = ErrorCheck(scriptLine, out var tokens);
         if (error != null) return error;
         var results = new List<byte>(LineCode);
         start += LineCode.Count;
         for (int i = 0; i < Args.Count; i++) {
            if (Args[i] is ScriptArg scriptArg) {
               var token = tokens[i + LineCode.Count];
               var message = scriptArg.Build(model, start, token, results, labels);
               if (message != null) return message;
               start += scriptArg.Length(model, start);
            } else if (Args[i] is ArrayArg arrayArg) {
               var values = arrayArg.ConvertMany(model, tokens.Skip(i + 1)).ToList();
               results.Add((byte)values.Count);
               start += 1;
               foreach (var value in values) {
                  if (Args[i].Type == ArgType.Byte) {
                     results.Add((byte)value);
                     start += 1;
                  } else if (Args[i].Type == ArgType.Short) {
                     results.Add((byte)value);
                     results.Add((byte)(value >> 8));
                     start += 2;
                  } else if (Args[i].Type == ArgType.Word) {
                     results.Add((byte)value);
                     results.Add((byte)(value >> 0x8));
                     results.Add((byte)(value >> 0x10));
                     results.Add((byte)(value >> 0x18));
                     start += 4;
                  } else {
                     throw new NotImplementedException();
                  }
               }
            }
         }
         result = results.ToArray();
         return null;
      }

      public string Decompile(IDataModel data, int start, DecompileLabelLibrary labels, IList<ExpectedPointerType> streamTypes) {
         for (int i = 0; i < LineCode.Count; i++) {
            if (LineCode[i] != data[start + i]) throw new ArgumentException($"Data at {start:X6} does not match the {LineCommand} command.");
         }
         var allFillerIsZero = IsAllFillerZero(data, start);
         start += LineCode.Count;
         var builder = new StringBuilder(LineCommand);
         for (int i = 1; i < LineCode.Count; i++) {
            builder.Append(" " + LineCode[i].ToHexString());
         }

         var streamContent = new List<string>();
         foreach (var arg in Args) {
            builder.Append(" ");
            if (arg is ScriptArg scriptArg) {
               if (scriptArg.Build(allFillerIsZero, data, start, builder, streamContent, labels, streamTypes)) continue;
            } else if (arg is ArrayArg arrayArg) {
               builder.Append(arrayArg.ConvertMany(data, start));
            } else {
               throw new NotImplementedException();
            }
            start += arg.Length(data, start);
         }
         foreach (var content in streamContent) {
            builder.AppendLine();
            builder.AppendLine("{");
            builder.AppendLine(content);
            builder.Append("}");
         }
         return builder.ToString();
      }

      private bool IsAllFillerZero(IDataModel data, int start) {
         start += LineCode.Count;
         foreach (var arg in Args) {
            if (arg.Name == "filler") {
               var value = data.ReadMultiByteValue(start, arg.Length(data, start));
               if (value != 0) return false;
            }
            start += arg.Length(data, start);
         }
         return true;
      }

      public static string ReadString(IDataModel data, int start) {
         var length = PCSString.ReadString(data, start, true);
         return data.TextConverter.Convert(data, start, length);
      }

      public static string[] Tokenize(string scriptLine) {
         var result = new List<string>();
         var quoteCut = scriptLine.Split('"');

         for (int i = 0; i < quoteCut.Length; i++) {
            if (i % 2 == 0 && quoteCut[i].Length == 0) continue;

            if (i % 2 == 1) result.Add($"\"{quoteCut[i]}\"");
            else result.AddRange(quoteCut[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
         }

         return result.ToArray();
      }

      public override string ToString() {
         return string.Join(" ", LineCode.Select(code => code.ToHexString()).Concat(Args.Select(arg => arg.Name)).ToArray());
      }
   }

   public class XSEScriptLine : ScriptLine {
      public XSEScriptLine(string engineLine) : base(engineLine) { }

      public override bool IsEndingCommand => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x02, 0x03, 0x05, 0x08, 0x0A, 0x0C, 0x0D);
   }

   public class BSEScriptLine : ScriptLine {
      public BSEScriptLine(string engineLine) : base(engineLine) { }

      public override bool IsEndingCommand => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x28, 0x3c, 0x3d, 0x3e, 0x3f);
   }

   public class ASEScriptLine : ScriptLine {
      public ASEScriptLine(string engineLine) : base(engineLine) { }

      public override bool IsEndingCommand => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x08, 0x0F, 0x11, 0x13);
   }

   public class TSEScriptLine : ScriptLine {
      public TSEScriptLine(string engineLine) : base(engineLine) { }
      public override bool IsEndingCommand => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x45, 0x47, 0x59, 0x5A);
   }

}
