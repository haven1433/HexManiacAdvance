using System;
using System.Reflection;

namespace HavenSoft.AutoImplement.Tool {
   /// <summary>
   /// Hidden requirement:
   /// An implementation of IPatternBuilder is expected to have a constructor that takes
   /// a StringWriter. The AppendBlah methods are assumed to append things to that StringWriter.
   /// </summary>
   public interface IPatternBuilder {
      /// <summary>
      /// the name of the file, including any generic modifiers after the type and the file extension.
      /// </summary>
      string GetDesiredOutputFileName(Type interfaceType);

      /// <summary>
      /// the full class declaration, including any extended types / implemented interfaces
      /// </summary>
      string ClassDeclaration(Type interfaceType);

      /// <summary>
      /// If the pattern implementation has any standard members unrelated to the interface, add them here.
      /// For exampe, your pattern may add a specific constructor or protected member.
      /// </summary>
      void AppendExtraMembers(Type interfaceType);

      void AppendMethod(MethodInfo info, MemberMetadata metadata);

      void AppendEvent(EventInfo info, MemberMetadata metadata);

      void AppendProperty(PropertyInfo info, MemberMetadata metadata);

      /// <summary>
      /// the 'Item' property in C# is special: it's exposed as this[]
      /// </summary>
      void AppendItemProperty(PropertyInfo info, MemberMetadata metadata);

      /// <summary>
      /// Called when there are no more methods and all the scopes have been closed. Gives the builder a final chance to append things.
      /// </summary>
      void BuildCompleted();
   }
}
