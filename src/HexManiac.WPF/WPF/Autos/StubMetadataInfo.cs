using System;
using System.Collections.Generic;
using HavenSoft.AutoImplement.Delegation;

// this file was created by AutoImplement
namespace HavenSoft.HexManiac.Core.Models
{
    public class StubMetadataInfo : IMetadataInfo
    {
        public PropertyImplementation<string> VersionNumber = new PropertyImplementation<string>();
        
        string IMetadataInfo.VersionNumber
        {
            get
            {
                return this.VersionNumber.get();
            }
        }
        public PropertyImplementation<bool> IsPublicRelease = new PropertyImplementation<bool>();
        
        bool IMetadataInfo.IsPublicRelease
        {
            get
            {
                return this.IsPublicRelease.get();
            }
        }
    }
}
