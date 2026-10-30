using System;

namespace HavenSoft.HexManiac.Core.Models {
   public class ErrorInfo : IEquatable<ErrorInfo> {
      public static ErrorInfo NoError { get; } = new ErrorInfo(null);
      public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
      public bool IsWarning { get; }
      public string ErrorMessage { get; }
      public ErrorInfo(string message, bool isWarningLevel = false) {
         ErrorMessage = message;
         IsWarning = isWarningLevel;
      }

      public bool Equals(ErrorInfo other) => ErrorMessage == other?.ErrorMessage && IsWarning == other?.IsWarning;

      public override bool Equals(object obj) => Equals(obj as ErrorInfo);
      public override int GetHashCode() => ErrorMessage?.GetHashCode() ?? 0;
      public override string ToString() => ErrorMessage ?? string.Empty;
   }
}
