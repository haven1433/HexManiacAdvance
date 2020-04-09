using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Collections.Generic;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ArrayColumnHeaderTests {
      readonly List<string> errors = new List<string>();
      readonly List<string> messages = new List<string>();
      readonly byte[] data = new byte[0x200];
      readonly PokemonModel model;
      readonly ViewPort viewPort;

      public ArrayColumnHeaderTests() {
         model = new PokemonModel(data);
         viewPort = new ViewPort("test.gba", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[data1. data2.]8 ");
         viewPort.OnError += (sender, e) => { if (!string.IsNullOrEmpty(e)) errors.Add(e); };
         viewPort.OnMessage += (sender, e) => messages.Add(e);
      }

      [Fact]
      public void AnchorFormatAutoCompletesToSingleByte() {
         viewPort.AnchorText = "^array[data1. b data2.]8";
         var array = (ArrayRun)model.GetNextRun(0);
         Assert.Equal(3, array.ElementLength);
         Assert.Empty(errors);
      }

      [Fact]
      public void HeadersChangeWhenAnchorChanges() {
         viewPort.AnchorText = "^array[data1. b data2.]8";
         Assert.Equal(0, viewPort.ColumnHeaders[0].ColumnHeaders.Count % 3);

         viewPort.AnchorText = "^array[data1. bc data2.]8";
         Assert.Equal("bc", viewPort.ColumnHeaders[0].ColumnHeaders[1].ColumnTitle);
         Assert.Equal(1, viewPort.ColumnHeaders[0].ColumnHeaders[1].ByteWidth);
      }

      [Fact]
      public void TableToolUpdatesAfterAnchorChange() {
         viewPort.AnchorText = "^array[data1. b data2.]8";
         Assert.Equal(3, viewPort.Tools.TableTool.Children.Count);

         viewPort.AnchorText = "^array[data1. bc data2.]8";
         Assert.Equal("bc", ((FieldArrayElementViewModel)viewPort.Tools.TableTool.Children[1]).Name);
      }
   }
}
