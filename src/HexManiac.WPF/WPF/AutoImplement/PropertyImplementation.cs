using System;
using System.Reflection;

namespace HavenSoft.AutoImplement.Delegation;

/// <example>
/// this.MyProperty = new PropertyImplementation&lt;int&gt;();
/// 
/// this.MyProperty.set = value => Console.WriteLine(value);
/// 
/// this.MyProperty.get = () => 7;
/// 
/// this.MyProperty.value = 4;
/// 
/// this.MyProperty = 2;
/// 
/// int number = this.MyProperty;
/// </example>
/// <remarks>
/// Implicitly casting a T to a new PropertyImplementation resets its delegates to the defaults.
/// </remarks>
public class PropertyImplementation<T> {
   public Func<T> get;

   public Action<T> set;

   public T value;

   public PropertyImplementation(T initialValue = default(T)) {
      value = initialValue;
      set = input => value = input;
      get = () => value;
   }

   /// <summary>
   /// Assigning a normal value from a PropertyImplementation will use any custom get delegate that you've setup.
   /// If you haven't setup a custom get delegate, then the PropertyImplementation's value is used.
   /// </summary>
   /// <example>
   /// var property = new PropertyImplementation&lt;int&gt;();
   /// property.get = () => 4;
   /// property.value = 7;
   /// 
   /// int x = property; // x is now 4.
   /// </example>
   public static implicit operator T(PropertyImplementation<T> cast) => cast.get();

   /// <summary>
   /// Assigning a normal value to a PropertyImplementation will remove any get/set delegates you've added to the property implementation.
   /// </summary>
   /// <example>
   /// var property = new PropertyImplementation&lt;int&gt;();
   /// property = 7;
   /// </example>
   public static implicit operator PropertyImplementation<T>(T cast) => new PropertyImplementation<T>(cast);
}
