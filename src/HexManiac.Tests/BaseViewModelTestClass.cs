using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Lots of view model tests have similar setups.
   /// Pull standard setup into a base-class to reduce code duplication
   /// </summary>
   public class BaseViewModelTestClass {
      public ViewPort ViewPort { get; }
      public PokemonModel Model { get; }
      public byte[] Data { get; } = new byte[0x200];
      public List<string> Messages { get; } = new List<string>();
      public List<string> Errors { get; } = new List<string>();

      public BaseViewModelTestClass() {
         Model = new PokemonModel(Data);
         ViewPort = new ViewPort("file.txt", Model) { Width = 0x10, Height = 0x10 };
         ViewPort.OnError += (sender, e) => Errors.Add(e);
         ViewPort.OnMessage += (sender, e) => Messages.Add(e);
      }

      /// <summary>
      /// Text Tables are pretty simple.
      /// Each entry is X bytes long, which is just a single field of text.
      /// </summary>
      public void CreateTextTable(string tableName, int address, params string[] entries) {
         var length = entries.Max(name => name.Length) + 1; // +1 for the end-string character
         ViewPort.Goto.Execute(address.ToString("X2"));
         ViewPort.Edit($"^{tableName}[name\"\"{length}]{entries.Length} ");
         foreach (var entry in entries) ViewPort.Edit(entry + "\"");
      }

      /// <summary>
      /// Enum tables are a bit more complex.
      /// Each entry is 2 bytes long, but the text that you see is an entry from another table.
      /// </summary>
      public void CreateEnumTable(string tableName, int address, string sourceTable, params int[] entries) {
         for (int i = 0; i < entries.Length; i++) {
            Data[address + i * 2 + 0] = (byte)(entries[i] >> 0);
            Data[address + i * 2 + 1] = (byte)(entries[i] >> 8);
         }

         ViewPort.Goto.Execute(address.ToString("X2"));
         ViewPort.Edit($"^{tableName}[data:{sourceTable}]{entries.Length} ");
      }

      /// <summary>
      /// BitArray tables are fairly confusing.
      /// Each entry is X bits long, where X is the length of a matched enum table. X is rounded up to the nearest 8.
      /// </summary>
      public void CreateBitArrayTable(string tableName, int address, string sourceTable, params int[] encoding) {
         var token = new ModelDelta();
         var sourceAddress = Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, sourceTable);
         var sourceRun = (ArrayRun)Model.GetNextRun(sourceAddress);
         var bytesToEncode = (int)Math.Ceiling(sourceRun.ElementCount / 8.0);

         for (int i = 0; i < encoding.Length; i++) {
            Model.WriteMultiByteValue(address + i * bytesToEncode, bytesToEncode, token, encoding[i]);
         }

         ViewPort.Goto.Execute(address.ToString("X2"));
         ViewPort.Edit($"^{tableName}[data{BitArray.SharedFormatString}{sourceTable}]{encoding.Length} ");
      }
   }
}
