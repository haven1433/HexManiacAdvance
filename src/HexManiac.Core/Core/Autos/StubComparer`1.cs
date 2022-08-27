using System;
using System.Collections.Generic;
using HavenSoft.AutoImplement.Delegation;

// this file was created by AutoImplement
namespace System.Collections.Generic
{
    public class StubComparer<T> : IComparer<T>
    {
        public Func<T, T, int> Compare { get; set; }
        
        int IComparer<T>.Compare(T x, T y)
        {
            if (this.Compare != null)
            {
                return this.Compare(x, y);
            }
            else
            {
                return default(int);
            }
        }
        
    }
}
