using System;
using System.Collections.Generic;
using HavenSoft.AutoImplement.Delegation;

// this file was created by AutoImplement
namespace HavenSoft.HexManiac.Core.Models
{
    public class StubFileSystem : IFileSystem
    {
        public Func<string, string, System.String[], string> RequestNewName { get; set; }
        
        string IFileSystem.RequestNewName(string currentName, string extensionDescription, System.String[] extensionOptions)
        {
            if (this.RequestNewName != null)
            {
                return this.RequestNewName(currentName, extensionDescription, extensionOptions);
            }
            else
            {
                return default(string);
            }
        }
        
        public Func<string, System.String[], LoadedFile> OpenFile { get; set; }
        
        LoadedFile IFileSystem.OpenFile(string extensionDescription, System.String[] extensionOptions)
        {
            if (this.OpenFile != null)
            {
                return this.OpenFile(extensionDescription, extensionOptions);
            }
            else
            {
                return default(LoadedFile);
            }
        }

        public Func<string> OpenFolder { get; set; }
        string IFileSystem.OpenFolder() => OpenFolder?.Invoke() ?? default;

        public Func<string, bool> Exists { get; set; }
        
        bool IFileSystem.Exists(string file)
        {
            if (this.Exists != null)
            {
                return this.Exists(file);
            }
            else
            {
                return default(bool);
            }
        }
        
        public Action<string> LaunchProcess { get; set; }
        
        void IFileSystem.LaunchProcess(string file)
        {
            if (this.LaunchProcess != null)
            {
                this.LaunchProcess(file);
            }
        }
        
        public Func<string, LoadedFile> LoadFile { get; set; }
        
        LoadedFile IFileSystem.LoadFile(string fileName)
        {
            if (this.LoadFile != null)
            {
                return this.LoadFile(fileName);
            }
            else
            {
                return default(LoadedFile);
            }
        }
        
        public Action<string, System.Action<IFileSystem>> AddListenerToFile { get; set; }
        
        void IFileSystem.AddListenerToFile(string fileName, System.Action<IFileSystem> listener)
        {
            if (this.AddListenerToFile != null)
            {
                this.AddListenerToFile(fileName, listener);
            }
        }
        
        public Action<string, System.Action<IFileSystem>> RemoveListenerForFile { get; set; }
        
        void IFileSystem.RemoveListenerForFile(string fileName, System.Action<IFileSystem> listener)
        {
            if (this.RemoveListenerForFile != null)
            {
                this.RemoveListenerForFile(fileName, listener);
            }
        }
        
        public Func<LoadedFile, bool> Save { get; set; }
        
        bool IFileSystem.Save(LoadedFile file)
        {
            if (this.Save != null)
            {
                return this.Save(file);
            }
            else
            {
                return default(bool);
            }
        }
        
        public Func<string, System.String[], bool> SaveMetadata { get; set; }
        
        bool IFileSystem.SaveMetadata(string originalFileName, System.String[] metadata)
        {
            if (this.SaveMetadata != null)
            {
                return this.SaveMetadata(originalFileName, metadata);
            }
            else
            {
                return default(bool);
            }
        }
        
        public Func<LoadedFile, System.Nullable<bool>> TrySavePrompt { get; set; }
        
        System.Nullable<bool> IFileSystem.TrySavePrompt(LoadedFile file)
        {
            if (this.TrySavePrompt != null)
            {
                return this.TrySavePrompt(file);
            }
            else
            {
                return default(System.Nullable<bool>);
            }
        }
        
        public Func<string, System.String[]> MetadataFor { get; set; }
        
        System.String[] IFileSystem.MetadataFor(string fileName)
        {
            if (this.MetadataFor != null)
            {
                return this.MetadataFor(fileName);
            }
            else
            {
                return default(System.String[]);
            }
        }
        
        public Func<string, System.ValueTuple<System.Int16[], int>> LoadImage { get; set; }
        
        System.ValueTuple<System.Int16[], int> IFileSystem.LoadImage(string fileName)
        {
            if (this.LoadImage != null)
            {
                return this.LoadImage(fileName);
            }
            else
            {
                return default(System.ValueTuple<System.Int16[], int>);
            }
        }
        
        public Action<System.Int16[], int, string> SaveImage { get; set; }
        
        void IFileSystem.SaveImage(System.Int16[] image, int width, string fileName = null)
        {
            if (this.SaveImage != null)
            {
                this.SaveImage(image, width, fileName);
            }
        }
        
        public Func<string, string, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<object>>, VisualOption[], int> ShowOptions { get; set; }
        
        int IFileSystem.ShowOptions(string title, string prompt, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<object>> additionalDetails, VisualOption[] options)
        {
            if (this.ShowOptions != null)
            {
                return this.ShowOptions(title, prompt, additionalDetails, options);
            }
            else
            {
                return default(int);
            }
        }
        
        public Func<string, string, string> RequestText { get; set; }
        
        string IFileSystem.RequestText(string title, string prompt)
        {
            if (this.RequestText != null)
            {
                return this.RequestText(title, prompt);
            }
            else
            {
                return default(string);
            }
        }
        
        public Func<string, bool, ProcessModel[], System.Nullable<bool>> ShowCustomMessageBox { get; set; }
        
        System.Nullable<bool> IFileSystem.ShowCustomMessageBox(string message, bool showYesNoCancel, ProcessModel[] links)
        {
            if (this.ShowCustomMessageBox != null)
            {
                return this.ShowCustomMessageBox(message, showYesNoCancel, links);
            }
            else
            {
                return default(System.Nullable<bool>);
            }
        }
        
        public PropertyImplementation<string> CopyText = new PropertyImplementation<string>();
        
        string IFileSystem.CopyText
        {
            get
            {
                return this.CopyText.get();
            }
            set
            {
                this.CopyText.set(value);
            }
        }
        public PropertyImplementation<System.ValueTuple<System.Int16[], int>> CopyImage = new PropertyImplementation<System.ValueTuple<System.Int16[], int>>();
        
        System.ValueTuple<System.Int16[], int> IFileSystem.CopyImage
        {
            get
            {
                return this.CopyImage.get();
            }
            set
            {
                this.CopyImage.set(value);
            }
        }
    }
}
