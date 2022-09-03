using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using static HavenSoft.HexManiac.Core.Models.HardcodeTablesModel;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class EggMoveRun : BaseRun, IStreamRun, IAppendToBuilderRun {
      public const int MagicNumber = 0x4E20; // anything above this number is a pokemon, anything below it is a move
      public const int EndStream = 0xFFFF;
      public const string GroupStart = "[";
      public const string GroupEnd = "]";
      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "egg" + AsciiRun.StreamDelimeter;
      private readonly IDataModel model;

      public override int Length { get; }
      public override string FormatString => SharedFormatString;

      public EggMoveRun(IDataModel dataModel, int dataIndex, SortedSpan<int> pointerSources = null) : base(dataIndex, pointerSources) {
         model = dataModel;
         Length = 0;
         if (Start < 0) return;
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
            if (run.PointerSources.Count < 2 || run.PointerSources.Count > 10) continue;

            // verify limiter
            var length = data.ReadMultiByteValue(run.PointerSources[1] - 4, 4);

            // we just read the 'length' from basically a random byte... verify that it could make sense as a length
            if (length < 1000 || length > 7000) continue;
            if (run.Start + length * 2 + 3 < 0) continue;
            if (run.Start + length * 2 + 3 > data.Count) continue;

            // verify content
            bool possibleMatch = true;
            int lastValue = -1;
            for (int i = 0; i < length - 2; i++) {
               var value = data.ReadMultiByteValue(run.Start + i * 2, 2);

               // if the same byte pairs are repeated multiple times, then this pokemon is listed twice or has the same egg move twice.
               // that seems unlikely... this is probably the wrong data.
               if (value == lastValue) { possibleMatch = false; break; }
               lastValue = value;

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

      public override IDataFormat CreateDataFormat(IDataModel data, int dataIndex) {
         Debug.Assert(data == model);

         var cache = ModelCacheScope.GetCache(data);
         var cachedPokenames = cache.GetOptions(PokemonNameTable);
         var cachedMovenames = cache.GetOptions(MoveNamesTable);

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

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new EggMoveRun(model, Start, newPointerSources);

      public int GetPokemonNumber(string input) {
         var cachedPokenames = model.GetOptions(PokemonNameTable);

         if (input.StartsWith(GroupStart)) input = input.Substring(1, input.Length - 2);
         input = input.ToLower();
         var names = cachedPokenames.Select(name => name.Trim('"').ToLower()).ToList();
         return names.IndexOfPartial(input);
      }

      public int GetMoveNumber(string input) {
         var cache = ModelCacheScope.GetCache(model);
         var cachedMovenames = cache.GetOptions(MoveNamesTable);

         input = input.Trim('"').ToLower();
         var names = cachedMovenames.Select(name => name.Trim('"').ToLower()).ToList();
         return names.IndexOfPartial(input);
      }

      public string SerializeRun() {
         var cache = ModelCacheScope.GetCache(model);
         var cachedPokenames = cache.GetOptions(PokemonNameTable);
         var cachedMovenames = cache.GetOptions(MoveNamesTable);

         var builder = new StringBuilder();
         for (int i = 0; i < Length - 2; i += 2) {
            var address = Start + i;
            var value = model.ReadMultiByteValue(address, 2);
            if (value >= MagicNumber) {
               value -= MagicNumber;
               var pokemonName = value < cachedPokenames.Count ? cachedPokenames[value] : value.ToString();
               builder.Append($"{GroupStart}{pokemonName.Trim('"')}{GroupEnd}");
            } else {
               var moveName = value < cachedMovenames.Count ? cachedMovenames[value] : value.ToString();
               builder.Append(moveName.Trim('"'));
            }
            if (i < Length - 4) builder.AppendLine();
         }
         return builder.ToString();
      }

      public IStreamRun DeserializeRun(string content, ModelDelta token, out IReadOnlyList<int> changedOffsets) {
         var changedAddresses = new List<int>();
         var cache = ModelCacheScope.GetCache(model);
         var cachedPokenames = cache.GetOptions(PokemonNameTable);
         var cachedMovenames = cache.GetOptions(MoveNamesTable);

         var data = new List<int>();
         var pokemonNames = cachedPokenames.Select(name => $"{GroupStart}{name.Trim('"').ToLower()}{GroupEnd}").ToList();
         var moveNames = cachedMovenames.Select(name => name.Trim('"').ToLower()).ToList();
         var lines = content.ToLower().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
         for (int i = 0; i < data.Count; i++) {
            var p = run.Start + i * 2;
            if (model.WriteMultiByteValue(p, 2, token, data[i])) changedAddresses.Add(p);
         }
         var end = run.Start + data.Count * 2;
         if (model.ReadMultiByteValue(end, 2) != EndStream) model.WriteMultiByteValue(end, 2, token, EndStream); // write the new end token (untracked)
         for (int i = data.Count + 1; i < Length / 2; i++) model.WriteMultiByteValue(run.Start + i * 2, 2, token, EndStream); // fill any remaining old space with FF (untracked)
         changedOffsets = changedAddresses;
         return new EggMoveRun(model, run.Start);
      }

      public IReadOnlyList<AutocompleteItem> GetAutoCompleteOptions(string line, int caretLineIndex, int caretCharacterIndex) {
         var result = new List<AutocompleteItem>();
         line = line.Trim();
         var nameTable = MoveNamesTable;
         Func<string, string> wrap = option => option;

         if (line.StartsWith("[")) {
            line = line.Substring(1);
            if (line.EndsWith("]")) line = line.Substring(0, line.Length - 1);
            nameTable = PokemonNameTable;
            wrap = option => $"[{option}]";
         }

         result.AddRange(
            model.GetOptions(nameTable).
            Where(option => option.MatchesPartial(line)).
            Select(option => option.Trim('"')).
            Select(wrap).
            Select(option => new AutocompleteItem(option, option)));

         return result;
      }

      public IReadOnlyList<IPixelViewModel> Visualizations => new List<IPixelViewModel>();
      public bool DependsOn(string anchorName) {
         return anchorName == PokemonNameTable || anchorName == MoveNamesTable;
      }

      public IEnumerable<(int, int)> Search(string parentArrayName, int index) {
         var groupStart = 0;
         if (parentArrayName == PokemonNameTable) groupStart = MagicNumber;
         if (parentArrayName == PokemonNameTable || parentArrayName == MoveNamesTable) {
            for (int i = 0; i < Length - 2; i += 2) {
               if (model.ReadMultiByteValue(Start + i, 2) == index + groupStart) {
                  yield return (Start + i, Start + i + 1);
               }
            }
         }
      }

      public IEnumerable<string> GetAutoCompleteOptions() {
         var cache = ModelCacheScope.GetCache(model);
         var cachedPokenames = cache.GetOptions(PokemonNameTable);
         var cachedMovenames = cache.GetOptions(MoveNamesTable);

         var pokenames = cachedPokenames.Select(name => $"{GroupStart}{name}{GroupEnd}"); // closing brace, so no space needed
         var movenames = cachedMovenames.Select(name => name + " "); // autocomplete needs to complete after selection, so add a space
         return pokenames.Concat(movenames);
      }

      public void AppendTo(IDataModel model, StringBuilder text, int start, int length, bool deep) {
         var cache = ModelCacheScope.GetCache(model);
         var cachedPokenames = cache.GetOptions(PokemonNameTable);
         var cachedMovenames = cache.GetOptions(MoveNamesTable);

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

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) changeToken.ChangeData(model, start + i, 0);
      }

      public bool UpdateLimiter(ModelDelta token) {
         if (PointerSources?.Count != 2) return false;
         var address = PointerSources.Last() - 4;
         var limiter = Length / 2 - 2;
         model.WriteMultiByteValue(address, 4, token, limiter);
         return true;
      }
   }
}
