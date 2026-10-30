using System;
using System.Linq;
using System.Runtime.Serialization;

namespace HavenSoft.AutoImplement.Delegation;

/// <summary>
/// When a caller Disposes of this object, it will call the constructor on the target object.
/// </summary>
public class ConstructionCompletion : IDisposable {
   private readonly object target;
   private readonly object[] args;

   private ConstructionCompletion(object incompleteObject, params object[] constructorArguments) {
      target = incompleteObject;
      args = constructorArguments;
   }

   /// <summary>
   /// Create an uninitialized object and returns a disposable scope for working with it. When the scope is closed, the object's constructor is called.
   /// </summary>
   /// <typeparam name="T"></typeparam>
   /// <param name="incompleteObject"></param>
   /// <param name="constructorArguments"></param>
   /// <returns></returns>
   public static IDisposable CreateObjectWithDeferredConstruction<T>(out T incompleteObject, params object[] constructorArguments) {
      incompleteObject = (T)FormatterServices.GetUninitializedObject(typeof(T));
      return new ConstructionCompletion(incompleteObject, constructorArguments);
   }

   /// <summary>
   /// Finishes constructing the target object.
   /// </summary>
   public void Dispose() {
      target.GetType().GetConstructor(args.Select(arg => arg.GetType()).ToArray()).Invoke(target, args);
   }
}
