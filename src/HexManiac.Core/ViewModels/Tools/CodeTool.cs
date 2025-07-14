using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public enum CodeMode { Thumb, Script, BattleScript, AnimationScript, TrainerAiScript, Raw }

   public class CodeTool : ViewModelCore, IToolViewModel {
      public string Name => "Code Tool";

      public CodeMode mode;
      public readonly Singletons singletons;
      public readonly ThumbParser thumb;
      public readonly ScriptParser script, battleScript, animationScript, battleAIScript;
      public readonly IDataModel model;
      public readonly Selection selection;
      public readonly ChangeHistory<ModelDelta> history;
      public readonly IRaiseMessageTab messageTab;
      public readonly IDelayWorkTimer recompileTimer;

      public event EventHandler<ErrorInfo> ModelDataChanged;
      public event EventHandler AttentionNewContent;

      public IDataInvestigator Investigator { get; set; }

      public bool isSelected;
      public bool insertAutoActive = true;
      public bool InsertAutoActive { get => insertAutoActive; set => Set(ref insertAutoActive, value); }

      public bool showErrorText;
      public bool ShowErrorText { get => showErrorText; set => TryUpdate(ref showErrorText, value); }

      public string errorText;
      public string ErrorText { get => errorText; set => TryUpdate(ref errorText, value); }

      public int fontSize = 12;
      public int FontSize { get => fontSize; set => TryUpdate(ref fontSize, value); }

      public TextEditorViewModel Editor { get; } = new();

      public ObservableCollection<CodeBody> Contents { get; } = new ObservableCollection<CodeBody>();

      public ThumbParser Parser => thumb;

      public ScriptParser ScriptParser => script;

      public ScriptParser BattleScriptParser => battleScript;

      public ScriptParser AnimationScriptParser => animationScript;

      public ScriptParser BattleAIScriptParser => battleAIScript;

      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved;

      // properties that exist solely so the UI can remember things when the tab switches
      public double SingleBoxVerticalOffset { get; set; }
      public double MultiBoxVerticalOffset { get; set; }

      public void SetupThumbKeywords(Singletons singletons) {
         Editor.LineCommentHeader = "@";
         Editor.MultiLineCommentHeader = "/*";
         Editor.MultiLineCommentFooter = "*/";

         Editor.Keywords.Clear();
         var set = new HashSet<string>();
         foreach (var template in singletons.ThumbInstructionTemplates) {
            if (template is Instruction instr) {
               set.Add(instr.Operator);
            }
         }
         set.AddRange(new[] { ".word", ".byte", ".hword", ".align" });
         set.AddRange("beq bne bhs blo bcs bcc bmi bpl bvs bvc bhi bls bge blt bgt ble bal bnv".Split(' '));
         Editor.Keywords.AddRange(set);

         Editor.Constants.Clear();
         for (int i = 0; i <= 15; i++) Editor.Constants.Add($"r{i}");
         Editor.Constants.AddRange(new[] { "lr", "sp", "pc" });
      }

      public void ClearConstantCache() {
         script.ClearConstantCache();
         battleScript.ClearConstantCache();
         animationScript.ClearConstantCache();
         battleAIScript.ClearConstantCache();
      }

      #region RepointThumb

      public bool CalculateCanRepointThumb() {
         int left = selection.Scroll.ViewPointToDataIndex(selection.SelectionStart);
         int right = selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd);
         if (left > right) (left, right) = (right, left);
         var length = right - left + 1;
         return Parser.CanRepoint(model, left, length) != -1;
      }

      public bool canRepointThumb;
      public bool CanRepointThumb { get => canRepointThumb; set => Set(ref canRepointThumb, value); }

      #endregion

      public void UpdateScriptHelpFromLine(object sender, HelpContext context) {
         var codeBody = (CodeBody)sender;
         string help;
         if (mode == CodeMode.Script) help = ScriptParser.GetHelp(model, codeBody, context);
         else if (mode == CodeMode.BattleScript) help = BattleScriptParser.GetHelp(model, codeBody, context);
         else if (mode == CodeMode.AnimationScript) help = AnimationScriptParser.GetHelp(model, codeBody, context);
         else if (mode == CodeMode.TrainerAiScript) help = BattleAIScriptParser.GetHelp(model, codeBody, context);
         else throw new NotImplementedException();
         codeBody.HelpContent = help;
      }

      public SERun Construct<SERun>(int start, SortedSpan<int> sources) where SERun : IScriptStartRun {
         if (typeof(SERun) == typeof(XSERun)) return (SERun)(IScriptStartRun)new XSERun(start, sources);
         if (typeof(SERun) == typeof(ASERun)) return (SERun)(IScriptStartRun)new ASERun(start, sources);
         if (typeof(SERun) == typeof(BSERun)) return (SERun)(IScriptStartRun)new BSERun(start, sources);
         if (typeof(SERun) == typeof(TSERun)) return (SERun)(IScriptStartRun)new TSERun(start, sources);
         throw new NotImplementedException();
      }

      bool ignoreContentUpdates;
      public IDisposable CreateRecursionGuard() {
         if (ignoreContentUpdates) return new StubDisposable();
         ignoreContentUpdates = true;
         return new StubDisposable { Dispose = () => ignoreContentUpdates = false };
      }

      public string RawParse(IDataModel model, int start, int length) {
         var builder = new StringBuilder();
         while (length > 0) {
            builder.Append(model[start].ToHexString());
            builder.Append(" ");
            length--;
            start++;
            if (start % 16 == 0) builder.AppendLine();
         }
         return builder.ToString();
      }
   }

   public class CodeTextFormatter : ITextPreProcessor {
      public TextFormatting[] Format(string content) {
         var result = new TextFormatting[content.Length];
         bool inText = false, inComment = false;
         for (int i = 0; i < content.Length; i++) {
            if (inComment && content[i] == '\n') inComment = false;
            if (content[i] == '#') inComment = true;
            if (inComment) continue;
            if (content[i] == '{') inText = true;
            else if (content[i] == '}') inText = false;
            else if (inText) result[i] = TextFormatting.Text;
         }
         return result;
      }
   }

   public record HelpContext(string Line, int Index, int ContentBoundaryCount = 0, int ContentBoundaryIndex = -1, bool IsSelection = false);
}
