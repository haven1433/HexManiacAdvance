using HavenSoft.HexManiac.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Markup;

namespace HavenSoft.HexManiac.WPF.Resources {
   public class MultiKeyGesture : KeyGesture {

      private readonly ModifierKeys mods;
      private readonly IReadOnlyList<Key> keys;
      private int nextKey;

      public MultiKeyGesture(ModifierKeys mods, params Key[] keys) : base(keys[0], mods) {
         this.mods = mods;
         this.keys = keys;
      }

      public override bool Matches(object targetElement, InputEventArgs inputEventArgs) {
         if (!(inputEventArgs is KeyEventArgs keyArgs)) return false;
         if (keyArgs.IsRepeat) return false;

         if (nextKey == 0) {
            if (new KeyGesture(keys[0], mods).Matches(targetElement, keyArgs)) {
               nextKey += 1;
            } else {
               nextKey = 0;
            }
            return false;
         } else if (nextKey == keys.Count - 1) {
            nextKey = 0;
            return keyArgs.Key == keys[keys.Count - 1];
         } else {
            nextKey = keyArgs.Key == keys[nextKey] ? nextKey + 1 : 0;
            return false;
         }
      }
   }

   public class MultiKeyGestureExtension : MarkupExtension {
      private static readonly KeyConverter keyConverter = new KeyConverter();
      private static readonly ModifierKeysConverter modifierConverter = new ModifierKeysConverter();
      private readonly ModifierKeys mods;
      private readonly Key[] keys;

      public MultiKeyGestureExtension(string first, string second) : this(new[] { first, second }) { }
      public MultiKeyGestureExtension(params string[] textPieces) {
         var text = ", ".Join(textPieces);
         if (text.Contains("+")) {
            var parts = text.Split('+');
            text = parts[1];
            mods = (ModifierKeys)modifierConverter.ConvertFromString(parts[0]);
         } else {
            mods = ModifierKeys.None;
         }

         keys = text.Split(',').Select(k => (Key)keyConverter.ConvertFromString(k.Trim())).ToArray();
      }

      public override object ProvideValue(IServiceProvider serviceProvider) {
         return new MultiKeyGesture(mods, keys);
      }
   }
}
