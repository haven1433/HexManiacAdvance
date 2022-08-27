using System;
using System.Collections.Generic;
using HavenSoft.AutoImplement.Delegation;

// this file was created by AutoImplement
namespace HavenSoft.HexManiac.Core.Models
{
    public class StubDataModel : IDataModel
    {
        public Func<int, bool> HasChanged { get; set; }
        
        bool IDataModel.HasChanged(int index)
        {
            if (this.HasChanged != null)
            {
                return this.HasChanged(index);
            }
            else
            {
                return default(bool);
            }
        }
        
        public Action ResetChanges { get; set; }
        
        void IDataModel.ResetChanges()
        {
            if (this.ResetChanges != null)
            {
                this.ResetChanges();
            }
        }
        
        public delegate System.Collections.Generic.IEnumerable<T> AllDelegate_<T>() where T : Runs.IFormattedRun;
        private readonly Dictionary<Type[], object> AllDelegates_ = new Dictionary<Type[], object>(new EnumerableEqualityComparer<Type>());
        public void ImplementAll<T>(AllDelegate_<T> implementation) where T : Runs.IFormattedRun
        {
            var key = new Type[] { typeof(T) };
            AllDelegates_[key] = implementation;
        }
        public System.Collections.Generic.IEnumerable<T> All<T>() where T : Runs.IFormattedRun
        {
            var key = new Type[] { typeof(T) };
            object implementation;
            if (AllDelegates_.TryGetValue(key, out implementation))
            {
                return ((AllDelegate_<T>)implementation).Invoke();
            }
            else
            {
                return default(System.Collections.Generic.IEnumerable<T>);
            }
        }
        
        public Func<int, Runs.IFormattedRun> GetNextRun { get; set; }
        
        Runs.IFormattedRun IDataModel.GetNextRun(int dataIndex)
        {
            if (this.GetNextRun != null)
            {
                return this.GetNextRun(dataIndex);
            }
            else
            {
                return default(Runs.IFormattedRun);
            }
        }
        
        public Func<int, Runs.IFormattedRun> GetNextAnchor { get; set; }
        
        Runs.IFormattedRun IDataModel.GetNextAnchor(int dataIndex)
        {
            if (this.GetNextAnchor != null)
            {
                return this.GetNextAnchor(dataIndex);
            }
            else
            {
                return default(Runs.IFormattedRun);
            }
        }
        
        public delegate bool TryGetUsefulHeaderDelegate_int_string(int address, out string header);
        
        public TryGetUsefulHeaderDelegate_int_string TryGetUsefulHeader_int_string { get; set; }
        
        bool IDataModel.TryGetUsefulHeader(int address, out string header)
        {
            header = default(string);
            if (this.TryGetUsefulHeader_int_string != null)
            {
                return this.TryGetUsefulHeader_int_string(address, out header);
            }
            else
            {
                return default(bool);
            }
        }
        
        public delegate bool TryGetListDelegate_string_ValidationList(string name, out ValidationList nameArray);
        
        public TryGetListDelegate_string_ValidationList TryGetList_string_ValidationList { get; set; }
        
        bool IDataModel.TryGetList(string name, out ValidationList nameArray)
        {
            nameArray = default(ValidationList);
            if (this.TryGetList_string_ValidationList != null)
            {
                return this.TryGetList_string_ValidationList(name, out nameArray);
            }
            else
            {
                return default(bool);
            }
        }
        
        public delegate bool IsAtEndOfArrayDelegate_int_Runs_ITableRun(int dataIndex, out Runs.ITableRun tableRun);
        
        public IsAtEndOfArrayDelegate_int_Runs_ITableRun IsAtEndOfArray_int_Runs_ITableRun { get; set; }
        
        bool IDataModel.IsAtEndOfArray(int dataIndex, out Runs.ITableRun tableRun)
        {
            tableRun = default(Runs.ITableRun);
            if (this.IsAtEndOfArray_int_Runs_ITableRun != null)
            {
                return this.IsAtEndOfArray_int_Runs_ITableRun(dataIndex, out tableRun);
            }
            else
            {
                return default(bool);
            }
        }
        
        public Action<ModelDelta, Runs.IFormattedRun> ObserveRunWritten { get; set; }
        
        void IDataModel.ObserveRunWritten(ModelDelta changeToken, Runs.IFormattedRun run)
        {
            if (this.ObserveRunWritten != null)
            {
                this.ObserveRunWritten(changeToken, run);
            }
        }
        
        public Action<ModelDelta, string, Runs.IFormattedRun> ObserveAnchorWritten { get; set; }
        
        void IDataModel.ObserveAnchorWritten(ModelDelta changeToken, string anchorName, Runs.IFormattedRun run)
        {
            if (this.ObserveAnchorWritten != null)
            {
                this.ObserveAnchorWritten(changeToken, anchorName, run);
            }
        }
        
        public Action<System.Collections.Generic.IReadOnlyDictionary<int, Runs.IFormattedRun>, System.Collections.Generic.IReadOnlyDictionary<int, Runs.IFormattedRun>, System.Collections.Generic.IReadOnlyDictionary<int, string>, System.Collections.Generic.IReadOnlyDictionary<int, string>, System.Collections.Generic.IReadOnlyDictionary<int, string>, System.Collections.Generic.IReadOnlyDictionary<int, string>, System.Collections.Generic.IReadOnlyDictionary<int, string>, System.Collections.Generic.IReadOnlyDictionary<int, string>, System.Collections.Generic.IReadOnlyDictionary<int, int>, System.Collections.Generic.IReadOnlyDictionary<int, int>, System.Collections.Generic.IReadOnlyDictionary<string, int>, System.Collections.Generic.IReadOnlyDictionary<string, int>, System.Collections.Generic.IReadOnlyDictionary<string, ValidationList>, System.Collections.Generic.IReadOnlyDictionary<string, ValidationList>> MassUpdateFromDelta { get; set; }
        
        void IDataModel.MassUpdateFromDelta(System.Collections.Generic.IReadOnlyDictionary<int, Runs.IFormattedRun> runsToRemove, System.Collections.Generic.IReadOnlyDictionary<int, Runs.IFormattedRun> runsToAdd, System.Collections.Generic.IReadOnlyDictionary<int, string> namesToRemove, System.Collections.Generic.IReadOnlyDictionary<int, string> namesToAdd, System.Collections.Generic.IReadOnlyDictionary<int, string> unmappedPointersToRemove, System.Collections.Generic.IReadOnlyDictionary<int, string> unmappedPointersToAdd, System.Collections.Generic.IReadOnlyDictionary<int, string> matchedWordsToRemove, System.Collections.Generic.IReadOnlyDictionary<int, string> matchedWordsToAdd, System.Collections.Generic.IReadOnlyDictionary<int, int> offsetPointersToRemove, System.Collections.Generic.IReadOnlyDictionary<int, int> offsetPointersToAdd, System.Collections.Generic.IReadOnlyDictionary<string, int> unmappedConstantsToRemove, System.Collections.Generic.IReadOnlyDictionary<string, int> unmappedConstantsToAdd, System.Collections.Generic.IReadOnlyDictionary<string, ValidationList> listsToRemove, System.Collections.Generic.IReadOnlyDictionary<string, ValidationList> listsToAdd)
        {
            if (this.MassUpdateFromDelta != null)
            {
                this.MassUpdateFromDelta(runsToRemove, runsToAdd, namesToRemove, namesToAdd, unmappedPointersToRemove, unmappedPointersToAdd, matchedWordsToRemove, matchedWordsToAdd, offsetPointersToRemove, offsetPointersToAdd, unmappedConstantsToRemove, unmappedConstantsToAdd, listsToRemove, listsToAdd);
            }
        }
        
        public delegate T RelocateForExpansionDelegate_ModelDelta_T_int<T>(ModelDelta changeToken, T run, int minimumLength) where T : Runs.IFormattedRun;
        private readonly Dictionary<Type[], object> RelocateForExpansionDelegates_ModelDelta_T_int = new Dictionary<Type[], object>(new EnumerableEqualityComparer<Type>());
        public void ImplementRelocateForExpansion<T>(RelocateForExpansionDelegate_ModelDelta_T_int<T> implementation) where T : Runs.IFormattedRun
        {
            var key = new Type[] { typeof(T) };
            RelocateForExpansionDelegates_ModelDelta_T_int[key] = implementation;
        }
        public T RelocateForExpansion<T>(ModelDelta changeToken, T run, int minimumLength) where T : Runs.IFormattedRun
        {
            var key = new Type[] { typeof(T) };
            object implementation;
            if (RelocateForExpansionDelegates_ModelDelta_T_int.TryGetValue(key, out implementation))
            {
                return ((RelocateForExpansionDelegate_ModelDelta_T_int<T>)implementation).Invoke(changeToken, run, minimumLength);
            }
            else
            {
                return default(T);
            }
        }
        
        public Func<int, int, int> FindFreeSpace { get; set; }
        
        int IDataModel.FindFreeSpace(int start, int length)
        {
            if (this.FindFreeSpace != null)
            {
                return this.FindFreeSpace(start, length);
            }
            else
            {
                return default(int);
            }
        }
        
        public Action<ModelDelta, int, int> ClearAnchor { get; set; }
        
        void IDataModel.ClearAnchor(ModelDelta changeToken, int start, int length)
        {
            if (this.ClearAnchor != null)
            {
                this.ClearAnchor(changeToken, start, length);
            }
        }
        
        public Action<ModelDelta, int, int> ClearFormat { get; set; }
        
        void IDataModel.ClearFormat(ModelDelta changeToken, int start, int length)
        {
            if (this.ClearFormat != null)
            {
                this.ClearFormat(changeToken, start, length);
            }
        }
        
        public Action<ModelDelta, int, int> ClearData { get; set; }
        
        void IDataModel.ClearData(ModelDelta changeToken, int start, int length)
        {
            if (this.ClearData != null)
            {
                this.ClearData(changeToken, start, length);
            }
        }
        
        public Action<ModelDelta, int, int> ClearFormatAndData { get; set; }
        
        void IDataModel.ClearFormatAndData(ModelDelta changeToken, int start, int length)
        {
            if (this.ClearFormatAndData != null)
            {
                this.ClearFormatAndData(changeToken, start, length);
            }
        }
        
        public Action<ModelDelta, string, System.Collections.Generic.IReadOnlyList<string>, string> SetList { get; set; }
        
        void IDataModel.SetList(ModelDelta changeToken, string name, System.Collections.Generic.IReadOnlyList<string> list, string hash)
        {
            if (this.SetList != null)
            {
                this.SetList(changeToken, name, list, hash);
            }
        }
        
        public Action<ModelDelta, int, int> ClearPointer { get; set; }
        
        void IDataModel.ClearPointer(ModelDelta currentChange, int source, int destination)
        {
            if (this.ClearPointer != null)
            {
                this.ClearPointer(currentChange, source, destination);
            }
        }
        
        public Func<System.Func<ModelDelta>, int, int, bool, string> Copy { get; set; }
        
        string IDataModel.Copy(System.Func<ModelDelta> changeToken, int start, int length, bool deep)
        {
            if (this.Copy != null)
            {
                return this.Copy(changeToken, start, length, deep);
            }
            else
            {
                return default(string);
            }
        }
        
        public Action<System.Byte[], StoredMetadata> Load { get; set; }
        
        void IDataModel.Load(System.Byte[] newData, StoredMetadata metadata)
        {
            if (this.Load != null)
            {
                this.Load(newData, metadata);
            }
        }
        
        public Action<ModelDelta, int> ExpandData { get; set; }
        
        void IDataModel.ExpandData(ModelDelta changeToken, int minimumLength)
        {
            if (this.ExpandData != null)
            {
                this.ExpandData(changeToken, minimumLength);
            }
        }
        
        public Action<ModelDelta, int> ContractData { get; set; }
        
        void IDataModel.ContractData(ModelDelta changeToken, int maximumLength)
        {
            if (this.ContractData != null)
            {
                this.ContractData(changeToken, maximumLength);
            }
        }
        
        public Func<ModelDelta, System.Int32[], Runs.SortedSpan<int>> SearchForPointersToAnchor { get; set; }
        
        Runs.SortedSpan<int> IDataModel.SearchForPointersToAnchor(ModelDelta changeToken, System.Int32[] addresses)
        {
            if (this.SearchForPointersToAnchor != null)
            {
                return this.SearchForPointersToAnchor(changeToken, addresses);
            }
            else
            {
                return default(Runs.SortedSpan<int>);
            }
        }
        
        public Func<ModelDelta, int, int, bool> WritePointer { get; set; }
        
        bool IDataModel.WritePointer(ModelDelta changeToken, int address, int pointerDestination)
        {
            if (this.WritePointer != null)
            {
                return this.WritePointer(changeToken, address, pointerDestination);
            }
            else
            {
                return default(bool);
            }
        }
        
        public Func<ModelDelta, int, int, bool> WriteValue { get; set; }
        
        bool IDataModel.WriteValue(ModelDelta changeToken, int address, int value)
        {
            if (this.WriteValue != null)
            {
                return this.WriteValue(changeToken, address, value);
            }
            else
            {
                return default(bool);
            }
        }
        
        public Func<int, int> ReadPointer { get; set; }
        
        int IDataModel.ReadPointer(int address)
        {
            if (this.ReadPointer != null)
            {
                return this.ReadPointer(address);
            }
            else
            {
                return default(int);
            }
        }
        
        public Func<int, int> ReadValue { get; set; }
        
        int IDataModel.ReadValue(int address)
        {
            if (this.ReadValue != null)
            {
                return this.ReadValue(address);
            }
            else
            {
                return default(int);
            }
        }
        
        public Func<string, Runs.SortedSpan<int>> GetUnmappedSourcesToAnchor { get; set; }
        
        Runs.SortedSpan<int> IDataModel.GetUnmappedSourcesToAnchor(string anchor)
        {
            if (this.GetUnmappedSourcesToAnchor != null)
            {
                return this.GetUnmappedSourcesToAnchor(anchor);
            }
            else
            {
                return default(Runs.SortedSpan<int>);
            }
        }
        
        public Action<ModelDelta, string, int> SetUnmappedConstant { get; set; }
        
        void IDataModel.SetUnmappedConstant(ModelDelta changeToken, string name, int value)
        {
            if (this.SetUnmappedConstant != null)
            {
                this.SetUnmappedConstant(changeToken, name, value);
            }
        }
        
        public delegate bool TryGetUnmappedConstantDelegate_string_int(string name, out int value);
        
        public TryGetUnmappedConstantDelegate_string_int TryGetUnmappedConstant_string_int { get; set; }
        
        bool IDataModel.TryGetUnmappedConstant(string name, out int value)
        {
            value = default(int);
            if (this.TryGetUnmappedConstant_string_int != null)
            {
                return this.TryGetUnmappedConstant_string_int(name, out value);
            }
            else
            {
                return default(bool);
            }
        }
        
        public Func<ModelDelta, int, string, int> GetAddressFromAnchor { get; set; }
        
        int IDataModel.GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor)
        {
            if (this.GetAddressFromAnchor != null)
            {
                return this.GetAddressFromAnchor(changeToken, requestSource, anchor);
            }
            else
            {
                return default(int);
            }
        }
        
        public Func<int, int, string> GetAnchorFromAddress { get; set; }
        
        string IDataModel.GetAnchorFromAddress(int requestSource, int destination)
        {
            if (this.GetAnchorFromAddress != null)
            {
                return this.GetAnchorFromAddress(requestSource, destination);
            }
            else
            {
                return default(string);
            }
        }
        
        public Func<string, int, System.Collections.Generic.IEnumerable<string>> GetAutoCompleteAnchorNameOptions { get; set; }
        
        System.Collections.Generic.IEnumerable<string> IDataModel.GetAutoCompleteAnchorNameOptions(string partial, int maxResults)
        {
            if (this.GetAutoCompleteAnchorNameOptions != null)
            {
                return this.GetAutoCompleteAnchorNameOptions(partial, maxResults);
            }
            else
            {
                return default(System.Collections.Generic.IEnumerable<string>);
            }
        }
        
        public Func<IMetadataInfo, StoredMetadata> ExportMetadata { get; set; }
        
        StoredMetadata IDataModel.ExportMetadata(IMetadataInfo metadataInfo)
        {
            if (this.ExportMetadata != null)
            {
                return this.ExportMetadata(metadataInfo);
            }
            else
            {
                return default(StoredMetadata);
            }
        }
        
        public Action<ModelDelta, Runs.ArrayRunElementSegment, System.Collections.Generic.IReadOnlyList<Runs.ArrayRunElementSegment>, int, int, int> UpdateArrayPointer { get; set; }
        
        void IDataModel.UpdateArrayPointer(ModelDelta changeToken, Runs.ArrayRunElementSegment segment, System.Collections.Generic.IReadOnlyList<Runs.ArrayRunElementSegment> segments, int parentIndex, int address, int destination)
        {
            if (this.UpdateArrayPointer != null)
            {
                this.UpdateArrayPointer(changeToken, segment, segments, parentIndex, address, destination);
            }
        }
        
        public Func<System.Func<ModelDelta>, System.Collections.Generic.IReadOnlyList<int>, int> ConsiderResultsAsTextRuns { get; set; }
        
        int IDataModel.ConsiderResultsAsTextRuns(System.Func<ModelDelta> futureChange, System.Collections.Generic.IReadOnlyList<int> startLocations)
        {
            if (this.ConsiderResultsAsTextRuns != null)
            {
                return this.ConsiderResultsAsTextRuns(futureChange, startLocations);
            }
            else
            {
                return default(int);
            }
        }
        
        public Func<string, System.Collections.Generic.IEnumerable<string>> GetAutoCompleteByteNameOptions { get; set; }
        
        System.Collections.Generic.IEnumerable<string> IDataModel.GetAutoCompleteByteNameOptions(string text)
        {
            if (this.GetAutoCompleteByteNameOptions != null)
            {
                return this.GetAutoCompleteByteNameOptions(text);
            }
            else
            {
                return default(System.Collections.Generic.IEnumerable<string>);
            }
        }
        
        public Func<string, System.Collections.Generic.IReadOnlyList<int>> GetMatchedWords { get; set; }
        
        System.Collections.Generic.IReadOnlyList<int> IDataModel.GetMatchedWords(string name)
        {
            if (this.GetMatchedWords != null)
            {
                return this.GetMatchedWords(name);
            }
            else
            {
                return default(System.Collections.Generic.IReadOnlyList<int>);
            }
        }
        
        public Func<string, System.Collections.Generic.IReadOnlyList<TableGroup>> GetTableGroups { get; set; }
        
        System.Collections.Generic.IReadOnlyList<TableGroup> IDataModel.GetTableGroups(string tableName)
        {
            if (this.GetTableGroups != null)
            {
                return this.GetTableGroups(tableName);
            }
            else
            {
                return default(System.Collections.Generic.IReadOnlyList<TableGroup>);
            }
        }
        
        public Action<ModelDelta, string, System.Collections.Generic.IReadOnlyList<string>, string> AppendTableGroup { get; set; }
        
        void IDataModel.AppendTableGroup(ModelDelta token, string groupName, System.Collections.Generic.IReadOnlyList<string> tableNames, string hash)
        {
            if (this.AppendTableGroup != null)
            {
                this.AppendTableGroup(token, groupName, tableNames, hash);
            }
        }
        
        public PropertyImplementation<System.Threading.Tasks.Task> InitializationWorkload = new PropertyImplementation<System.Threading.Tasks.Task>();
        
        System.Threading.Tasks.Task IDataModel.InitializationWorkload
        {
            get
            {
                return this.InitializationWorkload.get();
            }
        }
        public PropertyImplementation<System.Byte[]> RawData = new PropertyImplementation<System.Byte[]>();
        
        System.Byte[] IDataModel.RawData
        {
            get
            {
                return this.RawData.get();
            }
        }
        public PropertyImplementation<Runs.ModelCacheScope> CurrentCacheScope = new PropertyImplementation<Runs.ModelCacheScope>();
        
        Runs.ModelCacheScope IDataModel.CurrentCacheScope
        {
            get
            {
                return this.CurrentCacheScope.get();
            }
        }
        public PropertyImplementation<int> ChangeCount = new PropertyImplementation<int>();
        
        int IDataModel.ChangeCount
        {
            get
            {
                return this.ChangeCount.get();
            }
        }
        public PropertyImplementation<int> FreeSpaceStart = new PropertyImplementation<int>();
        
        int IDataModel.FreeSpaceStart
        {
            get
            {
                return this.FreeSpaceStart.get();
            }
            set
            {
                this.FreeSpaceStart.set(value);
            }
        }
        public PropertyImplementation<int> FreeSpaceBuffer = new PropertyImplementation<int>();
        
        int IDataModel.FreeSpaceBuffer
        {
            get
            {
                return this.FreeSpaceBuffer.get();
            }
            set
            {
                this.FreeSpaceBuffer.set(value);
            }
        }
        public PropertyImplementation<int> NextExportID = new PropertyImplementation<int>();
        
        int IDataModel.NextExportID
        {
            get
            {
                return this.NextExportID.get();
            }
            set
            {
                this.NextExportID.set(value);
            }
        }
        public PropertyImplementation<Runs.Factory.IFormatRunFactory> FormatRunFactory = new PropertyImplementation<Runs.Factory.IFormatRunFactory>();
        
        Runs.Factory.IFormatRunFactory IDataModel.FormatRunFactory
        {
            get
            {
                return this.FormatRunFactory.get();
            }
        }
        public PropertyImplementation<ITextConverter> TextConverter = new PropertyImplementation<ITextConverter>();
        
        ITextConverter IDataModel.TextConverter
        {
            get
            {
                return this.TextConverter.get();
            }
        }
        public Func<int, byte> get_Item = (index) => default(byte);
        
        public Action<int, byte> set_Item = (index, value) => {};
        
        byte IDataModel.this[int index]
        {
            get
            {
                return get_Item(index);
            }
            set
            {
                set_Item(index, value);
            }
        }
        public PropertyImplementation<System.Collections.Generic.IReadOnlyList<string>> ListNames = new PropertyImplementation<System.Collections.Generic.IReadOnlyList<string>>();
        
        System.Collections.Generic.IReadOnlyList<string> IDataModel.ListNames
        {
            get
            {
                return this.ListNames.get();
            }
        }
        public PropertyImplementation<System.Collections.Generic.IReadOnlyList<Runs.ArrayRun>> Arrays = new PropertyImplementation<System.Collections.Generic.IReadOnlyList<Runs.ArrayRun>>();
        
        System.Collections.Generic.IReadOnlyList<Runs.ArrayRun> IDataModel.Arrays
        {
            get
            {
                return this.Arrays.get();
            }
        }
        public PropertyImplementation<System.Collections.Generic.IReadOnlyList<Runs.IStreamRun>> Streams = new PropertyImplementation<System.Collections.Generic.IReadOnlyList<Runs.IStreamRun>>();
        
        System.Collections.Generic.IReadOnlyList<Runs.IStreamRun> IDataModel.Streams
        {
            get
            {
                return this.Streams.get();
            }
        }
        public PropertyImplementation<System.Collections.Generic.IReadOnlyList<string>> Anchors = new PropertyImplementation<System.Collections.Generic.IReadOnlyList<string>>();
        
        System.Collections.Generic.IReadOnlyList<string> IDataModel.Anchors
        {
            get
            {
                return this.Anchors.get();
            }
        }
        public PropertyImplementation<System.Collections.Generic.IReadOnlyList<GotoShortcutModel>> GotoShortcuts = new PropertyImplementation<System.Collections.Generic.IReadOnlyList<GotoShortcutModel>>();
        
        System.Collections.Generic.IReadOnlyList<GotoShortcutModel> IDataModel.GotoShortcuts
        {
            get
            {
                return this.GotoShortcuts.get();
            }
        }
        byte System.Collections.Generic.IReadOnlyList<byte>.this[int index]
        {
            get
            {
                return get_Item(index);
            }
        }
        public PropertyImplementation<int> Count = new PropertyImplementation<int>();
        
        int System.Collections.Generic.IReadOnlyCollection<byte>.Count
        {
            get
            {
                return this.Count.get();
            }
        }
        public Func<System.Collections.Generic.IEnumerator<byte>> GetEnumerator { get; set; }
        
        System.Collections.Generic.IEnumerator<byte> System.Collections.Generic.IEnumerable<byte>.GetEnumerator()
        {
            if (this.GetEnumerator != null)
            {
                return this.GetEnumerator();
            }
            else
            {
                return default(System.Collections.Generic.IEnumerator<byte>);
            }
        }
        
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            if (this.GetEnumerator != null)
            {
                return this.GetEnumerator();
            }
            else
            {
                return default(System.Collections.IEnumerator);
            }
        }
        
        public Func<HavenSoft.HexManiac.Core.Models.IDataModel, bool> Equals { get; set; }
        
        bool System.IEquatable<IDataModel>.Equals(HavenSoft.HexManiac.Core.Models.IDataModel other)
        {
            if (this.Equals != null)
            {
                return this.Equals(other);
            }
            else
            {
                return default(bool);
            }
        }
        
    }
}
