using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class EggMoveRun : IFormattedRun {
      public const int MagicNumber = 0x4E20; // anything above this number is a pokemon, anything below it is a move
      public const int EndStream = 0xFFFF;
      public const string PokemonNameTable = "pokenames";
      public const string MoveNamesTable = "movenames";
      public const string GroupStart = "[";
      public const string GroupEnd = "]";
      private readonly IDataModel model;

      public int Start { get; }
      public int Length { get; }
      public IReadOnlyList<int> PointerSources { get; private set; }
      public string FormatString => AsciiRun.StreamDelimeter + "egg" + AsciiRun.StreamDelimeter;

      public EggMoveRun(IDataModel dataModel, int dataIndex) {
         model = dataModel;
         Start = dataIndex;
         Length = 0;
         for (int i = Start; i < model.Count; i += 2) {
            if (model[i] == 0xFF && model[i + 1] == 0xFF) {
               Length = i - Start + 2;
               break;
            }
         }
      }

      public static bool TrySearch(IDataModel data, ModelDelta token, out EggMoveRun eggmoves) {
         eggmoves = default;
         var pokenames = data.GetNextRun(data.GetAddressFromAnchor(token, -1, PokemonNameTable)) as ArrayRun;
         var movenames = data.GetNextRun(data.GetAddressFromAnchor(token, -1, MoveNamesTable)) as ArrayRun;
         if (pokenames == null || movenames == null) return false;

         for (var run = data.GetNextRun(0); run.Start < int.MaxValue; run = data.GetNextRun(run.Start + run.Length)) {
            if (run is ArrayRun || run is PCSRun || run.PointerSources == null) continue;

            // verify expected pointers to this
            if (run.PointerSources.Count != 2) continue;

            // verify limiter
            var length = data.ReadMultiByteValue(run.PointerSources[1] - 4, 4);

            // we just read the 'length' from basically a random byte... verify that it could make sense as a length
            if (length < 0) continue;
            if (run.Start + length * 2 + 3 < 0) continue;
            if (run.Start + length * 2 + 3 > data.Count) continue;
            var endValue = data.ReadMultiByteValue(run.Start + length * 2 + 2, 2);
            if (endValue != EndStream) continue;

            // verify content
            bool possibleMatch = true;
            for (int i = 0; i < length - 2; i++) {
               var value = data.ReadMultiByteValue(run.Start + i * 2, 2);
               if (value == EndStream) break; // early exit, the data was edited, but that's ok. Everything still matches up.
               if (value >= MagicNumber) {
                  value -= MagicNumber;
                  if (value < pokenames.ElementCount) continue;
               }
               if (value < movenames.ElementCount) continue;
               possibleMatch = false;
               break;
            }
            if (!possibleMatch) continue;

            // content is correct, length is correct: this is it!
            eggmoves = new EggMoveRun(data, run.Start);
            return true;
         }

         return false;
      }

      private IReadOnlyList<string> cachedPokenames, cachedMovenames;
      private int lastFormatRequest = int.MinValue;
      public IDataFormat CreateDataFormat(IDataModel data, int dataIndex) {
         Debug.Assert(data == model);
         if (dataIndex != lastFormatRequest + 1) {
            cachedPokenames = ArrayRunEnumSegment.GetOptions(model, PokemonNameTable) ?? new List<string>();
            cachedMovenames = ArrayRunEnumSegment.GetOptions(model, MoveNamesTable) ?? new List<string>();
         }
         lastFormatRequest = dataIndex;

         var position = dataIndex - Start;
         var groupStart = position % 2 == 1 ? position - 1 : position;
         position -= groupStart;
         var value = data.ReadMultiByteValue(Start + groupStart, 2);
         if (value >= MagicNumber) {
            value -= MagicNumber;
            string content = cachedPokenames.Count > value ? cachedPokenames[value] : value.ToString();
            if (value == EndStream - MagicNumber) content = string.Empty;
            if (content.StartsWith("\"")) content = content.Substring(1);
            if (content.EndsWith("\"")) content = content.Substring(0, content.Length - 1);
            return new EggSection(groupStart + Start, position, $"{GroupStart}{content}{GroupEnd}");
         } else {
            string content = cachedMovenames.Count > value ? cachedMovenames[value] : value.ToString();
            return new EggItem(groupStart + Start, position, content);
         }
      }

      public IFormattedRun MergeAnchor(IReadOnlyList<int> sources) {
         var newSources = new HashSet<int>();
         if (sources != null) newSources.AddRange(sources);
         if (PointerSources != null) newSources.AddRange(PointerSources);
         return new EggMoveRun(model, Start) { PointerSources = newSources.ToList() };
      }

      public IFormattedRun RemoveSource(int source) {
         var sources = PointerSources.ToList();
         sources.Remove(source);
         return new EggMoveRun(model, Start) { PointerSources = sources };
      }

      public int GetPokemonNumber(string input) {
         if (input.StartsWith(GroupStart)) input = input.Substring(1, input.Length - 2);
         var names = cachedPokenames.Select(name => name.Trim('"').ToLower()).ToList();
         return GetNumber(input.ToLower(), names);
      }

      public int GetMoveNumber(string input) {
         input = input.Trim('"').ToLower();
         var names = cachedMovenames.Select(name => name.Trim('"').ToLower()).ToList();
         return GetNumber(input, names);
      }

      public string SerializeForTool() {
         var builder = new StringBuilder();
         for (int i = 0; i < Length - 2; i += 2) {
            var address = Start + i;
            var value = model.ReadMultiByteValue(address, 2);
            if (value >= MagicNumber) {
               value -= MagicNumber;
               builder.Append($"{GroupStart}{cachedPokenames[value].Trim('"')}{GroupEnd}");
            } else {
               builder.Append(cachedMovenames[value].Trim('"'));
            }
            if (i < Length - 4) builder.AppendLine();
         }
         return builder.ToString();
      }

      public int DeserializeFromTool(string content, ModelDelta token) {
         var data = new List<int>();
         var pokemonNames = cachedPokenames.Select(name => $"{GroupStart}{name.Trim('"').ToLower()}{GroupEnd}").ToList();
         var moveNames = cachedMovenames.Select(name => name.Trim('"').ToLower()).ToList();
         var lines = content.ToLower().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         foreach (var line in lines) {
            var index = pokemonNames.IndexOf(line);
            if (index != -1) { data.Add(index + MagicNumber); continue; }
            index = moveNames.IndexOf(line);
            if (index != -1) { data.Add(index); continue; }

            // didn't find an exact match... look for a partial pokemon match
            var matchFound = false;
            for (int i = 0; i < pokemonNames.Count; i++) {
               if (pokemonNames[i].Contains(line)) { data.Add(i + MagicNumber); matchFound = true; break; }
            }
            if (matchFound) continue;

            // look for a partial move match
            for (int i = 0; i < moveNames.Count; i++) {
               if (moveNames[i].Contains(line)) { data.Add(i); break; }
            }
         }
         var run = model.RelocateForExpansion(token, this, data.Count * 2 + 2);
         for (int i = 0; i < data.Count; i++) model.WriteMultiByteValue(run.Start + i * 2, 2, token, data[i]);
         model.WriteMultiByteValue(run.Start + data.Count * 2, 2, token, EndStream); // write the new end token
         for (int i = data.Count + 2; i < Length / 2; i++) model.WriteMultiByteValue(run.Start + i * 2, 2, token, EndStream); // fill any remaining old space with FF
         return run.Start;
      }

      public IEnumerable<string> GetAutoCompleteOptions() {
         var pokenames = cachedPokenames.Select(name => $"{GroupStart}{name}{GroupEnd}");
         var movenames = cachedMovenames.Select(name => name + " ");
         return pokenames.Concat(movenames);
      }

      private static int GetNumber(string input, IList<string> names) {
         var matchIndex = names.IndexOf(input);
         if (matchIndex != -1) return matchIndex;
         var match = names.FirstOrDefault(name => name.Contains(input));
         if (match == null) return -1;
         return names.IndexOf(match);
      }

      public void AppendTo(StringBuilder text, int start, int length) {
         while (length > 0 && start < Start + Length) {
            var value = model.ReadMultiByteValue(start, 2);
            if (value == EndStream) {
               text.Append($"{GroupStart}{GroupEnd}");
            } else if (value >= MagicNumber) {
               value -= MagicNumber;
               if (value >= cachedPokenames.Count) text.Append($"{GroupStart}{value}{GroupEnd}");
               else text.Append($"{GroupStart}{cachedPokenames[value].Trim('"')}{GroupEnd}");
            } else {
               if (value >= cachedMovenames.Count) text.Append($"{value}");
               else text.Append($"{cachedMovenames[value]}");
            }
            start += 2;
            length -= 2;
            if (length > 0 && start < Start + Length) text.Append(" ");
         }
      }

      public bool UpdateLimiter(ModelDelta token) {
         if (PointerSources.Count != 2) return false;
         var address = PointerSources.Last() - 4;
         var limiter = Length / 2 - 2;
         model.WriteMultiByteValue(address, 4, token, limiter);
         return true;
      }
   }
}
