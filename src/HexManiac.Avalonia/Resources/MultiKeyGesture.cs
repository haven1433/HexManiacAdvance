using Avalonia.Input;
using Avalonia.Markup.Xaml;
using HavenSoft.HexManiac.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace HavenSoft.HexManiac.Avalonia.Resources {
   public class MultiKeyGesture {

      private readonly KeyModifiers mods;
      private readonly IReadOnlyList<Key> keys;
      private int prevKey;
      private int nextKey;

      public MultiKeyGesture(KeyModifiers mods, params Key[] keys) {
         this.mods = mods;
         this.keys = keys;
      }

      public bool Matches(object targetElement, EventArgs inputEventArgs) {
         if (!(inputEventArgs is KeyEventArgs keyArgs)) return false;
         if (keyArgs.Key == (Key)prevKey) return false;

         if (nextKey == 0) {
            if (new KeyGesture(keys[0], mods).Matches(keyArgs)) {
               prevKey = (int)keyArgs.Key;
               nextKey += 1;
            } else {
               prevKey = 0;
               nextKey = 0;
            }
            return false;
         } else if (nextKey == keys.Count - 1) {
            prevKey = 0;
            nextKey = 0;
            return keyArgs.Key == keys[keys.Count - 1];
         } else {
            prevKey = (int)keyArgs.Key;
            nextKey = keyArgs.Key == keys[nextKey] ? nextKey + 1 : 0;
            return false;
         }
      }
   }

   public class MultiKeyGestureExtension : MarkupExtension {
      private static readonly KeyConverter keyConverter = new KeyConverter();
      private static readonly ModifierKeysConverter modifierConverter = new ModifierKeysConverter();
      private readonly KeyModifiers mods;
      private readonly Key[] keys;

      public MultiKeyGestureExtension(string first, string second) : this(new[] { first, second }) { }
      public MultiKeyGestureExtension(params string[] textPieces) {
         var text = ", ".Join(textPieces);
         if (text.Contains("+")) {
            var parts = text.Split('+');
            text = parts[1];
            mods = (KeyModifiers)modifierConverter.ConvertFromString(parts[0]);
         } else {
            mods = KeyModifiers.None;
         }

         keys = text.Split(',').Select(k => (Key)keyConverter.ConvertFromString(k.Trim())).ToArray();
      }

      public override object ProvideValue(IServiceProvider serviceProvider) {
         return new MultiKeyGesture(mods, keys);
      }

      internal class KeyConverter : TypeConverter {
         public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object source) {
            return base.ConvertFrom(context, culture, source);
         }
      }
      internal class ModifierKeysConverter : TypeConverter {
         public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object source) {
            return base.ConvertFrom(context, culture, source);
         }
      }
   }
}
