using System;

namespace HavenSoft.Gen3Hex.Core.Models {
   public class ErrorInfo : IEquatable<ErrorInfo> {
      public static ErrorInfo NoError { get; } = new ErrorInfo(null);
      public bool HasError { get; }
      public string ErrorMessage { get; }
      public ErrorInfo(string message) {
         ErrorMessage = message;
         HasError = !string.IsNullOrEmpty(message);
      }

      public bool Equals(ErrorInfo other) => ErrorMessage == other.ErrorMessage;

      public override bool Equals(object obj) => Equals(obj as ErrorInfo);
      public override int GetHashCode() => ErrorMessage?.GetHashCode() ?? 0;
      public override string ToString() => ErrorMessage ?? string.Empty;
   }
}
