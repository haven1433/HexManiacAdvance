using System;
using System.Collections.Generic;
using HavenSoft.AutoImplement.Delegation;

// this file was created by AutoImplement
namespace System
{
    public class StubDisposable : IDisposable
    {
        public Action Dispose { get; set; }
        
        void IDisposable.Dispose()
        {
            if (this.Dispose != null)
            {
                this.Dispose();
            }
        }
        
    }
}
