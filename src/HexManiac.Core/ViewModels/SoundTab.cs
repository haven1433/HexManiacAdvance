using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Sound;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// The "Sound" tab: a Pokémon cry editor (play / export / import WAV) and a music tool (view, export and insert Sappy/mid2agb .s songs and MIDI files).
   /// </summary>
   public class SoundTab : ViewModelCore, ITabContent {
      public const string CryTable = "sound.pokemon.cry.normal";
      public const string GrowlTable = "sound.pokemon.cry.growl";
      public const string SongTable = "sound.tracks";
      public const string SongNamesList = "songnames";
      public const string SongFileDescription = "Song assembly (Sappy / mid2agb)";
      /// <summary>The name of the insert-song dialog's file filter: it lists the .s files and the MIDI files together.</summary>
      public const string SongInsertDescription = "Songs (*.s, *.mid, ...)";
      /// <summary>The anchor of the "All Instruments" voicegroup (instruments of many games), which MIDI files made for the GBA are usually meant for. A ROM may not have it.</summary>
      public const string AllInstrumentsAnchor = VoicegroupCatalog.AnchorPrefix + "all_instruments";
      /// <summary>A GBA song has at most this many tracks (the engine's limit); a MIDI file that uses more channels cannot be a song.</summary>
      public const int MaxSongTracks = 16;
      /// <summary>The music player for songs (gMPlayTable[0]) of the unmodified game plays this many tracks of a song, whatever the song has. Hacks change it: NUM_TRACKS_BGM in pokeemerald-expansion.</summary>
      public const int UnmodifiedBgmTracks = 10;
      /// <summary>The name a song added with 'Add new song' gets, followed by a number: song names can't have spaces, so this is "New song 1" with an underscore.</summary>
      public const string NewSongNamePrefix = "New_song_";
      /// <summary>The extensions the insert-song dialog offers, in order. The first is the dialog's default filter, so .s files are listed right away; the MIDI files are in the same entry.</summary>
      public static readonly string[] SongFileExtensions = { "s", "mid", "midi", "asm", "inc", "txt" };

      /// <summary>MIDI files are converted to a song (like mid2agb does) before they are inserted. Everything else is read as the song's assembly text.</summary>
      public static bool IsMidiFile(string fileName) => fileName != null && (fileName.EndsWith(".mid", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".midi", StringComparison.OrdinalIgnoreCase));

      private readonly IFileSystem fileSystem;
      private readonly ViewPort viewPort;
      private readonly IDataModel model;
      private readonly StubCommand close = new();

      public static bool IsSupported(IDataModel model) => model.GetTable(CryTable) != null || model.GetTable(SongTable) != null;

      #region ITabContent

      public string Name => "Sound";
      public bool SpartanMode { get; set; }
      public IDataModel Model => model;
      public bool IsMetadataOnlyChange => false;
      public ICommand Save { get; }
      public ICommand SaveAs { get; }
      public ICommand ExportBackup { get; } = new StubCommand();
      public ICommand Undo { get; }
      public ICommand Redo { get; }
      public ICommand Copy { get; } = new StubCommand();
      public ICommand DeepCopy { get; } = new StubCommand();
      public ICommand Diff => null;
      public ICommand DiffLeft => null;
      public ICommand DiffRight => null;
      public ICommand Clear { get; } = new StubCommand();
      public ICommand SelectAll { get; } = new StubCommand();
      public ICommand Goto { get; } = new StubCommand();
      public ICommand ResetAlignment { get; } = new StubCommand();
      public ICommand Back { get; } = new StubCommand();
      public ICommand Forward { get; } = new StubCommand();
      public ICommand Close => close;
      public bool CanDuplicate => false;
      public void Duplicate() { }

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler Closed;
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
      event EventHandler ITabContent.ClearMessage { add { } remove { } }
      event EventHandler<Action> ITabContent.RequestDelayedWork { add { } remove { } }
      event EventHandler ITabContent.RequestMenuClose { add { } remove { } }
      event EventHandler<Direction> ITabContent.RequestDiff { add { } remove { } }
      event EventHandler<CanDiffEventArgs> ITabContent.RequestCanDiff { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCanCreatePatch { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCreatePatch { add { } remove { } }
      event EventHandler ITabContent.RequestRefreshGotoShortcuts { add { } remove { } }

      public bool CanIpsPatchRight => false;
      public bool CanUpsPatchRight => false;
      public void IpsPatchRight() { }
      public void UpsPatchRight() { }
      void ITabContent.Refresh() => Reload();
      public bool TryImport(LoadedFile file, IFileSystem fileSystem) {
         if (file == null) return false;
         if (file.Name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) { ImportCryFromFile(file); return true; }
         // a dropped file is only taken when its type says it is a song: .asm, .inc and .txt files are offered by the insert dialog, but a dropped one may be anything
         if (file.Name.EndsWith(".s", StringComparison.OrdinalIgnoreCase) || IsMidiFile(file.Name)) { InsertSongFile(file); return true; }
         return false;
      }

      #endregion

      /// <summary>
      /// Raised when the user wants to hear a cry. The payload is a complete RIFF/WAVE file. The view plays it.
      /// </summary>
      public event EventHandler<byte[]> RequestPlayWav;

      private string status = string.Empty;
      public string Status { get => status; private set => Set(ref status, value); }

      public bool HasCries => model.GetTable(CryTable) != null;
      public bool HasSongs => model.GetTable(SongTable) != null;

      public SoundTab(IFileSystem fileSystem, ViewPort viewPort) {
         this.fileSystem = fileSystem;
         this.viewPort = viewPort;
         model = viewPort.Model;
         Save = viewPort.Save;
         SaveAs = viewPort.SaveAs;
         Undo = ReloadAfter(viewPort.Undo);
         Redo = ReloadAfter(viewPort.Redo);
         close.CanExecute = arg => true;
         close.Execute = arg => Closed?.Invoke(this, EventArgs.Empty);
         Reload();
      }

      /// <summary>
      /// Undo and redo change the ROM behind this tab's back: the lists (songs, voicegroups, slots) have to be read again afterwards.
      /// </summary>
      private ICommand ReloadAfter(ICommand command) {
         var wrapper = new StubCommand {
            CanExecute = arg => command.CanExecute(arg),
            Execute = arg => { command.Execute(arg); Reload(); },
         };
         command.CanExecuteChanged += (sender, e) => wrapper.RaiseCanExecuteChanged();
         return wrapper;
      }

      private StubCommand refresh;
      public ICommand Refresh => StubCommand(ref refresh, Reload, () => true);

      public void Reload() {
         var selectedCry = SelectedCry?.Index ?? -1;
         var selectedSong = SelectedSong?.Index ?? -1;
         LoadCries();
         LoadSongs();
         NotifyPropertyChanged(nameof(HasCries));
         NotifyPropertyChanged(nameof(HasSongs));
         if (selectedCry >= 0 && selectedCry < Cries.Count) SelectedCry = Cries[selectedCry];
         if (selectedSong >= 0 && selectedSong < Songs.Count) SelectedSong = Songs[selectedSong];
         Status = $"{Cries.Count} cries, {Songs.Count} songs, {Voicegroups.Count} voicegroups";
      }

      #region Cries

      public ObservableCollection<CryItem> Cries { get; } = new();

      private string cryFilter = string.Empty;
      public string CryFilter {
         get => cryFilter;
         set {
            if (!TryUpdate(ref cryFilter, value)) return;
            foreach (var item in Cries) item.MatchToFilter(cryFilter);
         }
      }

      private CryItem selectedCry;
      public CryItem SelectedCry {
         get => selectedCry;
         set {
            if (selectedCry == value) return;
            if (selectedCry != null) selectedCry.Selected = false;
            selectedCry = value;
            if (selectedCry != null) selectedCry.Selected = true;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(HasSelectedCry));
            UpdateCryDetails();
            playCry?.RaiseCanExecuteChanged();
            exportCry?.RaiseCanExecuteChanged();
            importCry?.RaiseCanExecuteChanged();
            gotoCry?.RaiseCanExecuteChanged();
         }
      }
      public bool HasSelectedCry => selectedCry?.Sample != null;

      private string cryDetails = string.Empty;
      public string CryDetails { get => cryDetails; private set => Set(ref cryDetails, value); }

      private IPixelViewModel cryWaveform = RenderWaveform(Array.Empty<sbyte>(), 320, 72);
      public IPixelViewModel CryWaveform { get => cryWaveform; private set { cryWaveform = value; NotifyPropertyChanged(); } }

      private bool compressImportedCries = true;
      /// <summary>Store imported cries with DPCM compression (half the size, slightly lossy). This is what the games use for all cries.</summary>
      public bool CompressImportedCries { get => compressImportedCries; set => Set(ref compressImportedCries, value); }

      private int importSampleRateLimit = 22050;
      /// <summary>WAV files with a higher sample rate are resampled down to this rate on import (0 = keep the file's rate).</summary>
      public int ImportSampleRateLimit { get => importSampleRateLimit; set => Set(ref importSampleRateLimit, Math.Max(0, value)); }

      private StubCommand playCry, exportCry, importCry, gotoCry;
      public ICommand PlayCry => StubCommand(ref playCry, ExecutePlayCry, () => HasSelectedCry);
      public ICommand ExportCry => StubCommand(ref exportCry, ExecuteExportCry, () => HasSelectedCry);
      public ICommand ImportCry => StubCommand(ref importCry, ExecuteImportCry, () => selectedCry != null);
      public ICommand GotoCry => StubCommand(ref gotoCry, ExecuteGotoCry, () => selectedCry != null);

      private void LoadCries() {
         Cries.Clear();
         var table = model.GetTable(CryTable);
         if (table == null) return;
         var names = BuildCryNames(table);
         for (int i = 0; i < table.ElementCount; i++) {
            var entry = table.Start + table.ElementLength * i;
            var sampleAddress = model.ReadPointer(entry + 4);
            GbaSample.TryRead(model, sampleAddress, out var sample);
            var name = i < names.Count ? names[i] : string.Empty;
            Cries.Add(new CryItem(i, name, entry, sampleAddress, sample));
         }
         foreach (var item in Cries) item.MatchToFilter(cryFilter);
      }

      /// <summary>
      /// Work out a display name for each cry.
      /// Expansion ROMs name the cry table with a list. Vanilla ROMs map species to cries: species-1 for the first 251, then the hoenn conversion table.
      /// Decomp ROMs with a 'cry' field in the species table map through that field.
      /// </summary>
      private IReadOnlyList<string> BuildCryNames(ITableRun cryTable) {
         var options = model.GetOptions(CryTable);
         if (options != null && options.Count >= cryTable.ElementCount) return options;
         var names = new string[cryTable.ElementCount];
         var species = model.GetTable(HardcodeTablesModel.PokemonNameTable) ?? model.GetTable("data.pokemon.stats");
         var speciesNames = model.GetOptions(HardcodeTablesModel.PokemonNameTable);
         if (speciesNames == null || speciesNames.Count == 0) speciesNames = model.GetOptions("data.pokemon.stats");
         if (speciesNames == null || speciesNames.Count == 0) return names;
         var stats = model.GetTable("data.pokemon.stats");
         var crySegment = stats?.ElementContent.FirstOrDefault(segment => segment.Name == "cry");
         if (stats != null && crySegment != null) {
            int offset = stats.ElementContent.Until(segment => segment == crySegment).Sum(segment => segment.Length);
            int valueOffset = crySegment is ArrayRunEnumSegment enumSegment ? enumSegment.ValueOffset : 1;
            for (int s = 0; s < stats.ElementCount && s < speciesNames.Count; s++) {
               var cry = model.ReadMultiByteValue(stats.Start + stats.ElementLength * s + offset, crySegment.Length) - valueOffset;
               if (cry >= 0 && cry < names.Length && names[cry] == null) names[cry] = speciesNames[s];
            }
         } else {
            for (int s = 1; s <= 251 && s < speciesNames.Count && s - 1 < names.Length; s++) names[s - 1] = speciesNames[s];
            var conversion = model.GetTable("sound.pokemon.cry.hoennconversion");
            if (conversion != null) {
               for (int i = 0; i < conversion.ElementCount; i++) {
                  var cry = model.ReadMultiByteValue(conversion.Start + conversion.ElementLength * i, conversion.ElementContent[0].Length);
                  var s = 277 + i;
                  if (cry >= 0 && cry < names.Length && s < speciesNames.Count && names[cry] == null) names[cry] = speciesNames[s];
               }
            }
         }
         for (int i = 0; i < names.Length; i++) names[i] ??= string.Empty;
         return names;
      }

      private void UpdateCryDetails() {
         if (selectedCry == null) { CryDetails = string.Empty; CryWaveform = RenderWaveform(Array.Empty<sbyte>(), 320, 72); return; }
         var sb = new StringBuilder();
         sb.AppendLine($"Cry {selectedCry.Index}{(string.IsNullOrEmpty(selectedCry.Name) ? "" : ": " + selectedCry.Name)}");
         sb.AppendLine($"Table entry: {CryTable}/{selectedCry.Index} ({selectedCry.EntryAddress:X6})");
         var type = model[selectedCry.EntryAddress];
         sb.AppendLine($"Entry type: {type:X2} ({((type & 0x20) != 0 ? "compressed" : "uncompressed")}{((type & 0x10) != 0 ? ", reversed" : "")}), key {model[selectedCry.EntryAddress + 1]}, length {model[selectedCry.EntryAddress + 2]}, pan {model[selectedCry.EntryAddress + 3]}");
         var growl = model.GetTable(GrowlTable);
         if (growl != null && selectedCry.Index < growl.ElementCount) {
            var growlSample = model.ReadPointer(growl.Start + growl.ElementLength * selectedCry.Index + 4);
            sb.AppendLine(growlSample == selectedCry.SampleAddress ? "Growl (reverse) entry: same sample" : $"Growl (reverse) entry: different sample at {growlSample:X6}");
         }
         if (selectedCry.Sample == null) {
            sb.AppendLine($"Sample at {selectedCry.SampleAddress:X6}: not a valid DirectSound sample.");
            CryDetails = sb.ToString();
            CryWaveform = RenderWaveform(Array.Empty<sbyte>(), 320, 72);
            return;
         }
         var sample = selectedCry.Sample;
         sb.AppendLine($"Sample: {sample.Address:X6}, {(sample.IsCompressed ? "DPCM compressed" : "8-bit PCM")}{(sample.IsLooped ? $", loops from {sample.LoopStart}" : "")}");
         sb.AppendLine($"Sample rate: {sample.SampleRate:0} Hz, {sample.SampleCount} samples ({sample.DurationSeconds:0.00} s), {sample.TotalLength} bytes");
         CryDetails = sb.ToString();
         try {
            CryWaveform = RenderWaveform(sample.Decode(model), 320, 72);
         } catch (Exception) {
            CryWaveform = RenderWaveform(Array.Empty<sbyte>(), 320, 72);
         }
      }

      public static IPixelViewModel RenderWaveform(sbyte[] samples, int width, int height) {
         var pixels = new short[width * height];
         const short background = 0x0C63, midline = 0x294A, wave = 0x7F80; // dark gray, gray, bright cyan-ish (BGR555)
         for (int i = 0; i < pixels.Length; i++) pixels[i] = background;
         for (int x = 0; x < width; x++) pixels[(height / 2) * width + x] = midline;
         if (samples.Length == 0) return new ReadonlyPixelViewModel(width, height, pixels);
         for (int x = 0; x < width; x++) {
            int start = (int)((long)x * samples.Length / width);
            int end = (int)((long)(x + 1) * samples.Length / width);
            if (end <= start) end = start + 1;
            int min = 127, max = -128;
            for (int i = start; i < end && i < samples.Length; i++) { if (samples[i] < min) min = samples[i]; if (samples[i] > max) max = samples[i]; }
            int yTop = (height / 2) - max * (height / 2 - 1) / 128;
            int yBottom = (height / 2) - min * (height / 2 - 1) / 128;
            for (int y = Math.Clamp(yTop, 0, height - 1); y <= Math.Clamp(yBottom, 0, height - 1); y++) pixels[y * width + x] = wave;
         }
         return new ReadonlyPixelViewModel(width, height, pixels);
      }

      private void ExecutePlayCry() {
         if (selectedCry?.Sample == null) return;
         try {
            var pcm = selectedCry.Sample.Decode(model);
            RequestPlayWav?.Invoke(this, GbaSample.ToWav(pcm, (int)Math.Round(selectedCry.Sample.SampleRate)));
         } catch (Exception e) {
            OnError?.Invoke(this, "Could not decode the cry: " + e.Message);
         }
      }

      private void ExecuteExportCry() {
         if (selectedCry?.Sample == null) return;
         var defaultName = string.IsNullOrEmpty(selectedCry.Name) ? $"cry_{selectedCry.Index}" : selectedCry.Name.ToLower().Replace(' ', '_');
         var name = fileSystem.RequestNewName(defaultName + ".wav", "Wave audio", "wav");
         if (string.IsNullOrEmpty(name)) return;
         var pcm = selectedCry.Sample.Decode(model);
         var wav = GbaSample.ToWav(pcm, (int)Math.Round(selectedCry.Sample.SampleRate));
         if (fileSystem.Save(new LoadedFile(name, wav))) Status = $"Exported cry {selectedCry.Index} to {name}";
      }

      private void ExecuteImportCry() {
         if (selectedCry == null) return;
         var file = fileSystem.OpenFile("Wave audio", "wav");
         if (file == null) return;
         ImportCryFromFile(file);
      }

      private void ImportCryFromFile(LoadedFile file) {
         if (selectedCry == null) { OnError?.Invoke(this, "Select a cry to replace first."); return; }
         if (!GbaSample.TryReadWav(file.Contents, out var samples, out var rate, out var error)) { OnError?.Invoke(this, error); return; }
         if (samples.Length == 0) { OnError?.Invoke(this, "The WAV file has no audio."); return; }
         if (ImportSampleRateLimit > 0 && rate > ImportSampleRateLimit) {
            samples = GbaSample.Resample(samples, rate, ImportSampleRateLimit);
            rate = ImportSampleRateLimit;
         }
         var pcm = GbaSample.Quantize(samples);
         var bytes = GbaSample.Build(pcm, rate, CompressImportedCries);
         var token = viewPort.CurrentChange;
         if (!VoicegroupEditor.TryAllocate(model, token, bytes.Length, out var newAddress, out var allocationError)) { OnError?.Invoke(this, allocationError); return; }
         token.ChangeData(model, newAddress, bytes);

         var oldSample = selectedCry.SampleAddress;
         int index = selectedCry.Index;
         RepointCry(CryTable, index, newAddress, CompressImportedCries, token);
         RepointCry(GrowlTable, index, newAddress, CompressImportedCries, token);

         // free the old sample if nothing else uses it
         if (GbaSample.TryRead(model, oldSample, out var old) && !AnyCryUses(oldSample)) {
            model.ClearFormatAndData(token, old.Address, old.TotalLength);
         }
         viewPort.ChangeHistory.ChangeCompleted();
         viewPort.Refresh();
         LoadCries();
         SelectedCry = index < Cries.Count ? Cries[index] : null;
         Status = $"Imported {file.Name} as cry {index}: {pcm.Length} samples at {rate} Hz, {bytes.Length} bytes at {newAddress:X6}.";
         OnMessage?.Invoke(this, Status);
      }

      private void RepointCry(string tableName, int index, int sampleAddress, bool compressed, ModelDelta token) {
         var table = model.GetTable(tableName);
         if (table == null || index >= table.ElementCount) return;
         var element = new ModelArrayElement(model, table.Start, index, () => token, table);
         var type = model[table.Start + table.ElementLength * index];
         type = (byte)((type & 0x10) | (compressed ? 0x20 : 0x00));
         element.SetValue("type", type);
         element.SetAddress("p", sampleAddress);
      }

      private bool AnyCryUses(int sampleAddress) {
         foreach (var tableName in new[] { CryTable, GrowlTable }) {
            var table = model.GetTable(tableName);
            if (table == null) continue;
            for (int i = 0; i < table.ElementCount; i++) {
               if (model.ReadPointer(table.Start + table.ElementLength * i + 4) == sampleAddress) return true;
            }
         }
         return false;
      }

      private void ExecuteGotoCry() {
         if (selectedCry == null) return;
         viewPort.Goto.Execute($"{CryTable}/{selectedCry.Index}");
         RequestTabChange?.Invoke(this, new TabChangeRequestedEventArgs(viewPort));
      }

      #endregion

      #region Songs

      public ObservableCollection<SongItem> Songs { get; } = new();
      public ObservableCollection<VoicegroupItem> Voicegroups { get; } = new();
      public ObservableCollection<string> MusicPlayers { get; } = new() { "0: Music (BGM)", "1: Sound effect 1", "2: Sound effect 2", "3: Sound effect 3" };

      private string songFilter = string.Empty;
      public string SongFilter {
         get => songFilter;
         set {
            if (!TryUpdate(ref songFilter, value)) return;
            foreach (var item in Songs) item.MatchToFilter(songFilter);
         }
      }

      private SongItem selectedSong;
      public SongItem SelectedSong {
         get => selectedSong;
         set {
            if (selectedSong == value) return;
            if (selectedSong != null) selectedSong.Selected = false;
            selectedSong = value;
            if (selectedSong != null) selectedSong.Selected = true;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(HasSelectedSong));
            NotifyPropertyChanged(nameof(SelectedSongName));
            NotifyPropertyChanged(nameof(CanRenameSong));
            UpdateSongDetails();
            exportSong?.RaiseCanExecuteChanged();
            gotoSong?.RaiseCanExecuteChanged();
            playSong?.RaiseCanExecuteChanged();
            exportSongWav?.RaiseCanExecuteChanged();
         }
      }
      public bool HasSelectedSong => selectedSong?.Header != null;

      /// <summary>Songs can be given a name whether or not their header is readable (a custom track inserted by hand has none yet).</summary>
      public bool CanRenameSong => selectedSong != null && model.TryGetList(SongNamesList, out _);

      /// <summary>
      /// The selected song's name, as the song name list (shown in the music dropdowns and the table) has it.
      /// Editing it updates that list in the ROM's metadata (.toml); the ROM itself doesn't store song names.
      /// </summary>
      public string SelectedSongName {
         get => selectedSong?.Name ?? string.Empty;
         set => RenameSelectedSong(value);
      }

      private void RenameSelectedSong(string name) {
         if (selectedSong == null) return;
         if (!model.TryGetList(SongNamesList, out var list)) {
            OnError?.Invoke(this, "This ROM's metadata has no song name list to update.");
            return;
         }
         var cleaned = CleanSongName(name);
         var index = selectedSong.Index;
         if (string.IsNullOrEmpty(cleaned)) cleaned = index.ToString();
         var names = list.ToList();
         while (names.Count <= index) names.Add(names.Count.ToString());
         if (names[index] == cleaned) {
            NotifyPropertyChanged(nameof(SelectedSongName)); // show the cleaned-up text again
            return;
         }
         names[index] = cleaned;
         model.SetList(viewPort.CurrentChange, SongNamesList, names, list.Comments, StoredList.GenerateHash(names));
         model.ClearCacheScope();
         viewPort.ChangeHistory.ChangeCompleted();
         selectedSong.Name = cleaned;
         NotifyPropertyChanged(nameof(SelectedSongName));
         UpdateSongDetails();
         Status = $"Song {index} is now called {cleaned}.";
      }

      /// <summary>
      /// List entries can't have spaces (they are used as identifiers in dropdowns and scripts), and the metadata file they are saved in is made of lines:
      /// '#' starts a comment there, ''' ends a value, and a line break would split the name (and move every name after it by one).
      /// </summary>
      public static string CleanSongName(string name) {
         var cleaned = new string((name ?? string.Empty).Trim().Select(c => c == ' ' || c == '#' || char.IsControl(c) ? '_' : c).ToArray());
         while (cleaned.Contains("'''")) cleaned = cleaned.Replace("'''", "''");
         return cleaned;
      }

      private string songDetails = string.Empty;
      public string SongDetails { get => songDetails; private set => Set(ref songDetails, value); }

      private string songText = string.Empty;
      /// <summary>The selected song, disassembled into mid2agb .s format.</summary>
      public string SongText { get => songText; private set => Set(ref songText, value); }

      private VoicegroupItem selectedVoicegroup;
      private bool voicegroupPickedByUser;
      /// <summary>
      /// The voicegroup songs are inserted with. Selecting a song and reading the lists choose it too (the song's own voicegroup, or All Instruments at first);
      /// a MIDI file only follows that automatic choice when the ROM has no All Instruments voicegroup (see ChooseVoicegroupForMidi).
      /// </summary>
      public VoicegroupItem SelectedVoicegroup {
         get => selectedVoicegroup;
         set {
            if (value != null && !ReferenceEquals(value, selectedVoicegroup)) voicegroupPickedByUser = true;
            selectedVoicegroup = value;
            NotifyPropertyChanged();
         }
      }

      /// <summary>Chooses the voicegroup without it counting as a choice of the user.</summary>
      private void SelectVoicegroupAutomatically(VoicegroupItem item) {
         selectedVoicegroup = item;
         NotifyPropertyChanged(nameof(SelectedVoicegroup));
      }

      private bool overrideVoicegroup;
      /// <summary>When set, inserted songs always use the selected voicegroup, even if the .s file names one.</summary>
      public bool OverrideVoicegroup { get => overrideVoicegroup; set => Set(ref overrideVoicegroup, value); }

      private bool insertAsNewSong;
      /// <summary>When set, inserted songs are appended to the song table as a new entry instead of replacing the selected song.</summary>
      public bool InsertAsNewSong { get => insertAsNewSong; set => Set(ref insertAsNewSong, value, old => NotifyPropertyChanged(nameof(InsertReplacesSong))); }
      /// <summary>The opposite of InsertAsNewSong, for binding a pair of radio buttons.</summary>
      public bool InsertReplacesSong { get => !insertAsNewSong; set => InsertAsNewSong = !value; }

      private bool insertWithNewVoicegroup;
      /// <summary>
      /// When set, an inserted song gets its own new voicegroup (a copy of the one it would have used) so any instrument of the game can be put in it,
      /// instead of being tied to a voicegroup other songs share.
      /// </summary>
      public bool InsertWithNewVoicegroup { get => insertWithNewVoicegroup; set => Set(ref insertWithNewVoicegroup, value); }

      private bool newSongsGetOwnVoicegroup = true;
      /// <summary>
      /// When set (it starts set), a song put into an empty slot (one that 'Add new song' made, or any song without tracks) gets a voicegroup of its own, made from the instruments the song uses. A MIDI file's General MIDI programs, or the voices a .s file selects, are taken from the voicegroup the song would have used, and every other slot is filled with a blank instrument.
      /// Replacing a song that has tracks never does this on its own (InsertWithNewVoicegroup is the way to ask for it).
      /// </summary>
      public bool NewSongsGetOwnVoicegroup { get => newSongsGetOwnVoicegroup; set => Set(ref newSongsGetOwnVoicegroup, value); }

      /// <summary>The size the ROM may grow to when a song needs more room than the free space has: the size of a GBA cartridge.</summary>
      public int MaxRomLength { get; set; } = VoicegroupEditor.MaxRomLength;

      private int insertMusicPlayer;
      public int InsertMusicPlayer { get => insertMusicPlayer; set => Set(ref insertMusicPlayer, value.LimitToRange(0, 3)); }

      private StubCommand exportSong, insertSong, gotoSong, playSong, exportSongWav, stopSong;
      public ICommand ExportSong => StubCommand(ref exportSong, ExecuteExportSong, () => HasSelectedSong);
      /// <summary>Render the selected song with a software version of the game's music engine and play it.</summary>
      public ICommand PlaySong => StubCommand(ref playSong, ExecutePlaySong, () => HasSelectedSong);
      public ICommand ExportSongWav => StubCommand(ref exportSongWav, ExecuteExportSongWav, () => HasSelectedSong);
      public ICommand StopSong => StubCommand(ref stopSong, () => RequestStopPlayback?.Invoke(this, EventArgs.Empty), () => true);

      public event EventHandler RequestStopPlayback;

      private byte[] RenderSelectedSong() {
         if (selectedSong?.Header == null) return null;
         if (selectedSong.Header.TrackCount == 0) { OnError?.Invoke(this, "This song has no tracks."); return null; }
         try {
            var renderer = new M4aRenderer(model);
            return renderer.RenderWav(selectedSong.Header);
         } catch (Exception e) {
            OnError?.Invoke(this, "Could not render the song: " + e.Message);
            return null;
         }
      }

      private void ExecutePlaySong() {
         var wav = RenderSelectedSong();
         if (wav == null) return;
         Status = $"Playing song {selectedSong.Index}{(string.IsNullOrEmpty(selectedSong.Name) ? "" : " (" + selectedSong.Name + ")")}: {(wav.Length - 44) / 4.0 / M4aRenderer.OutputRate:0.#} seconds (one loop, then a fade). This is a preview: the real game may sound a little different.";
         RequestPlayWav?.Invoke(this, wav);
      }

      private void ExecuteExportSongWav() {
         if (selectedSong?.Header == null) return;
         var defaultName = string.IsNullOrEmpty(selectedSong.Name) ? $"song_{selectedSong.Index}" : selectedSong.Name.ToLower();
         var name = fileSystem.RequestNewName(defaultName + ".wav", "Wave audio", "wav");
         if (string.IsNullOrEmpty(name)) return;
         var wav = RenderSelectedSong();
         if (wav == null) return;
         if (fileSystem.Save(new LoadedFile(name, wav))) Status = $"Exported song {selectedSong.Index} to {name}";
      }
      public ICommand InsertSong => StubCommand(ref insertSong, ExecuteInsertSong, () => HasSongs);
      public ICommand GotoSong => StubCommand(ref gotoSong, ExecuteGotoSong, () => selectedSong != null);

      private void LoadSongs() {
         var keepInsertVoicegroup = voicegroupPickedByUser ? selectedVoicegroup?.Address : null; // a voicegroup chosen by the song selection or by default is chosen again below
         Songs.Clear();
         Voicegroups.Clear();
         var table = model.GetTable(SongTable);
         if (table == null) { SelectedSong = null; UpdateAllInstruments(); return; }
         IReadOnlyList<string> names = model.GetOptions(SongTable);
         if ((names == null || names.Count == 0) && model.TryGetList(SongNamesList, out var songNames)) names = songNames;
         var usages = new List<(int voicegroup, string song)>();
         for (int i = 0; i < table.ElementCount; i++) {
            var entry = table.Start + table.ElementLength * i;
            var headerAddress = model.ReadPointer(entry);
            SongHeader.TryRead(model, headerAddress, out var header);
            var name = names != null && i < names.Count ? names[i] : string.Empty;
            var musicPlayer = model.ReadMultiByteValue(entry + 4, 2);
            Songs.Add(new SongItem(i, name, entry, headerAddress, header, musicPlayer));
            if (header != null && header.TrackCount > 0) usages.Add((header.Voicegroup, string.IsNullOrEmpty(name) ? i.ToString() : name));
         }
         foreach (var info in VoicegroupCatalog.Scan(model, usages)) Voicegroups.Add(new VoicegroupItem(model, info));
         UpdateAllInstruments();
         foreach (var item in Songs) item.MatchToFilter(songFilter);
         // a voicegroup the user picked for inserting songs stays picked when the lists are read again; otherwise it is the All Instruments voicegroup
         // if the ROM has one (songs made from MIDI files are meant for it), or the first one
         SelectVoicegroupAutomatically((keepInsertVoicegroup == null ? null : FindVoicegroupItem(keepInsertVoicegroup.Value)) ?? FindAllInstrumentsVoicegroup() ?? DefaultVoicegroup());
         // the selected song belongs to the old list: drop it (callers that want a selection pick one again)
         if (selectedSong != null && !Songs.Contains(selectedSong)) SelectedSong = null;
      }

      private void UpdateSongDetails() {
         if (selectedSong == null) { SongDetails = string.Empty; SongText = string.Empty; SyncVoicegroupSelection(); return; }
         var sb = new StringBuilder();
         sb.AppendLine($"Song {selectedSong.Index}{(string.IsNullOrEmpty(selectedSong.Name) ? "" : ": " + selectedSong.Name)}");
         sb.AppendLine($"Table entry: {SongTable}/{selectedSong.Index} ({selectedSong.EntryAddress:X6}), music player {selectedSong.MusicPlayer}");
         if (selectedSong.Header == null) {
            sb.AppendLine($"Header at {selectedSong.HeaderAddress:X6} is not a valid song header.");
            SongDetails = sb.ToString();
            SongText = string.Empty;
            SyncVoicegroupSelection();
            return;
         }
         var header = selectedSong.Header;
         if (header.TrackCount == 0) {
            // an empty slot: nothing to play, and its header is shared with the other unused slots, so there is no voicegroup to show or change
            sb.AppendLine($"Header: {header.Address:X6}, no tracks: this slot is empty and plays nothing.");
            sb.AppendLine(NewSongsGetOwnVoicegroup
               ? "Insert a song file into it ('Insert song…'): it then gets its own voicegroup, made from the instruments the song uses."
               : "Insert a song file into it ('Insert song…').");
            SongDetails = sb.ToString();
            SyncVoicegroupSelection();
            SongText = "(no tracks)";
            return;
         }
         sb.AppendLine($"Header: {header.Address:X6}, {header.TrackCount} tracks, priority {header.Priority}, reverb {header.Reverb}");
         sb.AppendLine($"Voicegroup: {DescribeVoicegroup(header.Voicegroup)}");
         SongDetails = sb.ToString();
         SyncVoicegroupSelection();
         try {
            var name = string.IsNullOrEmpty(selectedSong.Name) ? $"song_{selectedSong.Index}" : selectedSong.Name.ToLower();
            SongText = new SongDisassembler(model).Disassemble(header, name);
            var match = Voicegroups.FirstOrDefault(group => group.Address == header.Voicegroup);
            if (match != null) SelectVoicegroupAutomatically(match);
         } catch (Exception e) {
            SongText = "Could not disassemble this song: " + e.Message;
         }
      }

      private void ExecuteExportSong() {
         if (selectedSong?.Header == null) return;
         // the text shown for a song without tracks is a label, not a song: a file with it could not be put back in a ROM
         if (selectedSong.Header.TrackCount == 0) { OnError?.Invoke(this, "This song has no tracks, so there is nothing to export."); return; }
         var defaultName = string.IsNullOrEmpty(selectedSong.Name) ? $"song_{selectedSong.Index}" : selectedSong.Name.ToLower();
         var name = fileSystem.RequestNewName(defaultName + ".s", SongFileDescription, "s");
         if (string.IsNullOrEmpty(name)) return;
         if (fileSystem.Save(new LoadedFile(name, Encoding.UTF8.GetBytes(SongText)))) Status = $"Exported song {selectedSong.Index} to {name}";
      }

      private void ExecuteInsertSong() {
         var files = fileSystem.OpenFiles(SongInsertDescription, SongFileExtensions);
         if (files == null || files.Count == 0) return;
         if (files.Count == 1) InsertSongFile(files[0]);
         else InsertSongFiles(files);
      }

      private void InsertSongFile(LoadedFile file) {
         if (IsMidiFile(file.Name)) InsertMidiFromFile(file);
         else InsertSongFromFile(file);
      }

      /// <summary>What happened to a song put into the ROM.</summary>
      private class InsertedSong {
         public int Index { get; init; }
         public int Address { get; init; }
         public int Size { get; init; }
         public int Tracks { get; init; }
         /// <summary>The label of the song header in the assembly text.</summary>
         public string AssembledName { get; init; }
         /// <summary>The name the song has in the song name list now, or null if it was not named.</summary>
         public string Name { get; init; }
         public string VoicegroupMessage { get; init; } = string.Empty;
         /// <summary>The title of the voicegroup a song made from a MIDI file was made for (null for songs from text).</summary>
         public string PlayedWith { get; set; }
         /// <summary>The new address of the song table if inserting the song made it move (to get room for the new entry), otherwise -1.</summary>
         public int TableMovedTo { get; init; } = -1;
         /// <summary>A problem that did not stop the insertion (the song is in, but something extra could not be done), or null.</summary>
         public string Warning { get; init; }
      }

      /// <summary>A reminder when a song has more tracks than the music player of the original game plays, or an empty string.</summary>
      private static string TrackLimitNote(int tracks, int musicPlayer)
         => musicPlayer == 0 && tracks > UnmodifiedBgmTracks
            ? $" Note: it has {tracks} tracks, but the music player of the original game plays only {UnmodifiedBgmTracks}. A game with a bigger music player (NUM_TRACKS_BGM in pokeemerald-expansion) plays more."
            : string.Empty;

      private void InsertSongFromFile(LoadedFile file) {
         var asNew = InsertAsNewSong;
         var musicPlayer = asNew ? InsertMusicPlayer : selectedSong?.MusicPlayer ?? 0;
         var song = InsertSongFromText(DecodeSongText(file.Contents), SongNameFromFileName(file.Name), asNew, true, null, null, out var error);
         if (song == null) { OnError?.Invoke(this, error); return; }
         if (song.Warning != null) OnError?.Invoke(this, song.Warning);
         if (song.TableMovedTo >= 0) OnMessage?.Invoke(this, $"The song table was moved to {song.TableMovedTo:X6} to make room.");
         Status = $"Inserted {file.Name} ({song.AssembledName}, {song.Size} bytes) at {song.Address:X6} as song {song.Index}.{song.VoicegroupMessage}";
         OnMessage?.Invoke(this, Status + (asNew ? $" Play it with 'playbgm {song.Index}' / 'playse {song.Index}'." : string.Empty) + TrackLimitNote(song.Tracks, musicPlayer));
      }

      /// <summary>
      /// Assembles song text (a mid2agb .s file) into free space and puts it in the song table, as a new song or over the selected one.
      /// This is the one place that writes songs into the ROM: .s files and MIDI files (converted to .s text first) both come through here.
      /// </summary>
      /// <param name="songName">The name a new song gets in the song name list (null: none). Replaced songs keep their name.</param>
      /// <param name="finish">Complete the change (one undo step) and read the lists again. A batch passes false until the last file.</param>
      /// <param name="knownSymbols">Symbols the text uses that are already known (a MIDI conversion knows its voicegroup), or null.</param>
      /// <param name="voicegroup">The voicegroup the song is inserted with, or null for the selected one.</param>
      /// <returns>The inserted song, or null with the reason in 'error'; the ROM is not changed when null is returned for a reason found before the data is written.</returns>
      private InsertedSong InsertSongFromText(string text, string songName, bool asNewSong, bool finish, IReadOnlyDictionary<string, int> knownSymbols, VoicegroupItem voicegroup, out string error) {
         error = null;
         voicegroup ??= selectedVoicegroup;
         var table = model.GetTable(SongTable);
         if (table == null) { error = $"This ROM has no {SongTable} table."; return null; }
         if (!asNewSong && selectedSong == null) { error = "Select a song to replace, or choose 'add as new song'."; return null; }

         var makesOwnVoicegroup = !InsertWithNewVoicegroup && NewSongsGetOwnVoicegroup && !asNewSong && IsEmptySlot(selectedSong);
         // a new song that names a voicegroup this ROM has (voicegroup_littleroot for sound.voicegroups.littleroot) is made for that one, unless the user picked a voicegroup to insert with
         VoicegroupItem voicegroupNamedInFile(string symbol) {
            if (!makesOwnVoicegroup || knownSymbols != null || OverrideVoicegroup || voicegroupPickedByUser) return null;
            var info = VoicegroupEditor.FindByName(Voicegroups.Select(group => group.Info), symbol);
            return info == null ? null : Voicegroups.FirstOrDefault(group => group.Address == info.Address);
         }

         // first pass: find out how big the song is and which symbols it needs
         var externals = new Dictionary<string, int>();
         if (knownSymbols != null) foreach (var pair in knownSymbols) externals[pair.Key] = pair.Value;
         var result = SongAssembler.Assemble(text, BaseModel.PointerOffset, externals);
         if (!result.Success && result.UndefinedSymbols.Count > 0) {
            if (voicegroup == null) { error = $"The song needs these symbols, but no voicegroup is selected: {string.Join(", ", result.UndefinedSymbols)}"; return null; }
            var unresolved = new List<string>();
            foreach (var symbol in result.UndefinedSymbols) {
               // the voicegroup is the symbol of the header's voicegroup word, or one that is named like a voicegroup. Any other open symbol (the label of a GOTO or PATT,
               // for example) is a mistake in the file: binding it to the voicegroup would make the song jump into the instruments.
               if (symbol == result.VoicegroupSymbol || symbol.Contains("grp", StringComparison.OrdinalIgnoreCase) || symbol.Contains("voice", StringComparison.OrdinalIgnoreCase)) {
                  externals[symbol] = (voicegroupNamedInFile(symbol) ?? voicegroup).Address + BaseModel.PointerOffset;
               } else {
                  unresolved.Add(symbol);
               }
            }
            if (unresolved.Count > 0) {
               error = $"The song uses symbols this ROM doesn't know: {string.Join(", ", unresolved)}.";
               if (unresolved.Any(symbol => symbol.EndsWith("_B0", StringComparison.Ordinal))) error += " mid2agb writes a jump to a loop that does not exist when a MIDI file has a loop end marker ']' before any loop start marker '['.";
               return null;
            }
            result = SongAssembler.Assemble(text, BaseModel.PointerOffset, externals);
         }
         if (!result.Success) { error = "Could not assemble the song: " + result.Error; return null; }
         if (!HeaderPointsIntoTheRom(result, !OverrideVoicegroup, out var pointerError)) { error = pointerError; return null; }

         if (asNewSong && !HasRoomForNewEntry(table, result.Bytes.Length, out var roomError)) { error = roomError; return null; }

         // second pass: assemble for real at the chosen address
         var token = viewPort.CurrentChange;
         if (!VoicegroupEditor.TryAllocate(model, token, result.Bytes.Length + 4, out var address, out var allocationError)) { error = allocationError; return null; }
         while (address % 4 != 0) address++;
         result = SongAssembler.Assemble(text, address + BaseModel.PointerOffset, externals);
         if (!result.Success) { error = "Could not assemble the song: " + result.Error; return null; }
         var bytes = result.Bytes;
         if (OverrideVoicegroup && voicegroup != null) {
            var pointer = voicegroup.Address + BaseModel.PointerOffset;
            for (int i = 0; i < 4; i++) bytes[result.HeaderOffset + 4 + i] = (byte)(pointer >> (8 * i));
         }
         token.ChangeData(model, address, bytes);
         var headerAddress = address + result.HeaderOffset;

         // a song put into an empty slot (and a song the user asked a voicegroup of its own for) gets one, so every instrument of the game can be used in it
         var voicegroupMessage = string.Empty;
         string warning = null;
         if (InsertWithNewVoicegroup) {
            SongHeader.TryRead(model, headerAddress, out var insertedHeader);
            var sourceAddress = insertedHeader?.Voicegroup ?? voicegroup?.Address ?? -1;
            var source = FindVoicegroupItem(sourceAddress)?.Info ?? VoicegroupCatalog.Inspect(model, sourceAddress);
            var voicegroupSongName = !string.IsNullOrEmpty(result.SongName) ? result.SongName : selectedSong?.Name;
            if (VoicegroupEditor.TryCreateCopy(model, token, source, voicegroupSongName, out var voicegroupAddress, out var voicegroupAnchor, out var voicegroupError)) {
               VoicegroupEditor.AssignToSong(model, token, headerAddress, voicegroupAddress);
               voicegroupMessage = $" It has its own new voicegroup at {voicegroupAddress:X6}{(voicegroupAnchor == null ? string.Empty : " (" + voicegroupAnchor + ")")}.";
            } else {
               warning = "The song is inserted, but it could not get its own voicegroup: " + voicegroupError;
            }
         } else if (makesOwnVoicegroup) {
            var voicegroupSongName = !string.IsNullOrEmpty(result.SongName) ? result.SongName : !string.IsNullOrEmpty(songName) ? songName : selectedSong?.Name;
            voicegroupMessage = CreateVoicegroupFromSongInstruments(token, headerAddress, address, bytes.Length, voicegroupSongName, FindVoicegroupHint(text), out warning);
         }

         int index;
         var tableMovedTo = -1;
         if (asNewSong) {
            table = AppendSongTableEntry(token, table, out index, out tableMovedTo);
         } else {
            index = selectedSong.Index;
         }
         WriteSongTableEntry(token, table, index, headerAddress, asNewSong ? InsertMusicPlayer : -1);
         // a song put into a slot that only had a placeholder name (New_song_1, unnamed, ...) is named after the song
         var givenName = asNewSong || IsPlaceholderSongName(selectedSong?.Name, selectedSong?.Index ?? -1) ? TryNameNewSong(token, index, songName) : null;
         if (finish) {
            viewPort.ChangeHistory.ChangeCompleted();
            viewPort.Refresh();
            LoadSongs();
            SelectedSong = index < Songs.Count ? Songs[index] : null;
         }
         return new InsertedSong {
            Index = index,
            Address = address,
            Size = bytes.Length,
            Tracks = bytes[result.HeaderOffset],
            AssembledName = result.SongName,
            Name = givenName,
            VoicegroupMessage = voicegroupMessage,
            Warning = warning,
            TableMovedTo = tableMovedTo,
         };
      }

      /// <summary>
      /// A number in the song header (a track or voicegroup pointer written as a number, not a label) is written as it is. If it isn't an address of this ROM
      /// the song can't be played, and the Sound tab can't read its header: -1 or 0 from a typing mistake, for example. Refused before anything is written.
      /// </summary>
      private bool HeaderPointsIntoTheRom(SongAssembler.Result assembled, bool checkVoicegroup, out string error) {
         error = null;
         var bytes = assembled.Bytes;
         var limit = (long)model.Count + bytes.Length;
         int trackCount = bytes[assembled.HeaderOffset];
         for (int i = -1; i < trackCount; i++) {
            if (i == -1 && (!checkVoicegroup || trackCount == 0)) continue;
            var at = assembled.HeaderOffset + (i == -1 ? 4 : 8 + 4 * i);
            var pointer = (long)(uint)(bytes[at] | bytes[at + 1] << 8 | bytes[at + 2] << 16 | bytes[at + 3] << 24) - BaseModel.PointerOffset;
            if (pointer >= 0 && pointer < limit) continue;
            error = $"The song header's {(i == -1 ? "voicegroup" : "track " + (i + 1))} points to 0x{pointer + BaseModel.PointerOffset:X8}, which is no address of this ROM, so the game could not play it.";
            return false;
         }
         return true;
      }

      /// <summary>
      /// Whether a new song of 'songBytes' and one more entry in the song table fit. The table grows where it is when the bytes behind it are free, otherwise it moves
      /// to free space, and without any it would make the ROM grow: past the size of a cartridge, if the song already used the last room. This is asked before anything is written.
      /// </summary>
      private bool HasRoomForNewEntry(ITableRun table, int songBytes, out string error) {
         error = null;
         var grownLength = table.Length + table.ElementLength;
         var tableEnd = table.Start + table.Length;
         var behind = model.GetNextRun(tableEnd);
         if (behind.Start == tableEnd && behind.Length == 0) behind = model.GetNextRun(tableEnd + 1);
         var staysInPlace = tableEnd + table.ElementLength < model.Count && behind.Start >= tableEnd + table.ElementLength;
         for (int i = tableEnd; staysInPlace && i < tableEnd + table.ElementLength; i++) staysInPlace = model[i] == 0xFF || model[i] == 0x00;
         if (staysInPlace) return true; // only the song needs room, and allocating it says when there is none
         if (model.FindFreeSpace(model.FreeSpaceStart, songBytes + 4 + grownLength + 0x80) >= 0) return true;
         var end = (model.Count + 3) / 4 * 4;
         if (end + songBytes + 4 + grownLength + 4 <= MaxRomLength) return true; // the ROM can grow for both
         error = $"There is no room for the song ({songBytes} bytes) and the bigger song table ({grownLength} bytes), and the ROM can't grow past {(MaxRomLength % 0x100000 == 0 ? (MaxRomLength / 0x100000) + "MB" : MaxRomLength + " bytes")}.";
         return false;
      }

      #region Adding songs

      private StubCommand addNewSong;
      /// <summary>Adds an empty song at the end of the song table and selects it. The song plays nothing until a song file is inserted into it (that gives it its own voicegroup).</summary>
      public ICommand AddNewSong => StubCommand(ref addNewSong, ExecuteAddNewSong, () => HasSongs);

      /// <summary>A slot without a song: its header is missing or has no tracks (the game's unused slots all share one such header).</summary>
      private static bool IsEmptySlot(SongItem song) => song != null && (song.Header == null || song.Header.TrackCount == 0);

      /// <summary>Whether a name is only a stand-in for a song that was not named: nothing, its number, 'unnamed12', 'dummy_song_header' or a name made by 'Add new song'.</summary>
      public static bool IsPlaceholderSongName(string name, int index) {
         if (string.IsNullOrWhiteSpace(name)) return true;
         if (name == index.ToString()) return true;
         if (name.Equals("dummy_song_header", StringComparison.OrdinalIgnoreCase)) return true;
         foreach (var prefix in new[] { NewSongNamePrefix, "unnamed" }) {
            if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.Skip(prefix.Length).All(c => c >= '0' && c <= '9')) return true;
         }
         return false;
      }

      /// <summary>"New_song_1", or the first of New_song_2, New_song_3... that no song is called yet. Null when the ROM has no song name list.</summary>
      private string NextNewSongName() {
         if (!model.TryGetList(SongNamesList, out var list)) return null;
         var taken = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
         for (int n = 1; ; n++) {
            var name = NewSongNamePrefix + n;
            if (!taken.Contains(name)) return name;
         }
      }

      /// <summary>
      /// The header an empty song points at: the one the game itself uses for the slots that have no song (an 8 byte header without tracks that many entries of the song table share).
      /// The most shared one is taken, so a new slot costs the entry in the table and nothing else. Returns -1 when the song table has none.
      /// </summary>
      private int FindEmptySongHeader(ITableRun table) {
         var counts = new Dictionary<int, int>();
         for (int i = 0; i < table.ElementCount; i++) {
            var headerAddress = model.ReadPointer(table.Start + table.ElementLength * i);
            if (headerAddress < 0 || !SongHeader.TryRead(model, headerAddress, out var header) || header.TrackCount != 0) continue;
            counts[headerAddress] = counts.TryGetValue(headerAddress, out var count) ? count + 1 : 1;
         }
         if (counts.Count == 0) return -1;
         return counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).First().Key;
      }

      /// <summary>A song header without tracks (4 zero bytes, then a voicegroup pointer for tools that look at it), for a ROM whose song table has none to share.</summary>
      private bool TryCreateEmptySongHeader(ModelDelta token, out int address, out string error) {
         if (!VoicegroupEditor.TryAllocate(model, token, 12, out address, out error)) return false;
         while (address % 4 != 0) address++;
         token.ChangeData(model, address, new byte[8]);
         var voicegroup = Voicegroups.FirstOrDefault(group => !group.IsCustom);
         if (voicegroup != null) {
            model.WritePointer(token, address + 4, voicegroup.Address);
            model.ObserveRunWritten(token, new PointerRun(address + 4));
         }
         return true;
      }

      /// <summary>Makes room for one more entry at the end of the song table (it moves when the bytes behind it are in use) and adds the entry. Returns the table, which may be somewhere else now.</summary>
      private ITableRun AppendSongTableEntry(ModelDelta token, ITableRun table, out int index, out int tableMovedTo) {
         var originalStart = table.Start;
         table = model.RelocateForExpansion(token, table, table.Length + table.ElementLength);
         table = table.Append(token, 1);
         model.ObserveRunWritten(token, table);
         index = table.ElementCount - 1;
         tableMovedTo = table.Start != originalStart ? table.Start : -1;
         return table;
      }

      /// <summary>Points entry 'index' of the song table at a song header. A music player of 0 or more is written too (new entries); -1 leaves the entry's player as it is.</summary>
      private void WriteSongTableEntry(ModelDelta token, ITableRun table, int index, int headerAddress, int musicPlayer) {
         var element = new ModelArrayElement(model, table.Start, index, () => token, table);
         element.SetAddress("pointer", headerAddress);
         if (musicPlayer >= 0) {
            element.SetValue("musicplayer", musicPlayer);
            element.SetValue("unknown", musicPlayer);
         }
      }

      private void ExecuteAddNewSong() {
         var table = model.GetTable(SongTable);
         if (table == null) { OnError?.Invoke(this, $"This ROM has no {SongTable} table."); return; }
         var headerAddress = FindEmptySongHeader(table);
         if (!HasRoomForNewEntry(table, headerAddress >= 0 ? 0 : 12, out var roomError)) { OnError?.Invoke(this, roomError); return; }
         var token = viewPort.CurrentChange;
         if (headerAddress < 0 && !TryCreateEmptySongHeader(token, out headerAddress, out var headerError)) { OnError?.Invoke(this, headerError); return; }
         table = AppendSongTableEntry(token, table, out var index, out var tableMovedTo);
         WriteSongTableEntry(token, table, index, headerAddress, InsertMusicPlayer);
         var name = TryNameNewSong(token, index, NextNewSongName());
         viewPort.ChangeHistory.ChangeCompleted();
         viewPort.Refresh();
         SongFilter = string.Empty; // the new song is at the end of the list: a filter could hide it
         LoadSongs();
         SelectedSong = index < Songs.Count ? Songs[index] : null;
         InsertAsNewSong = false; // the next file goes into this song (Insert song, or a file dropped on the tab), not to the end of the table
         var player = InsertMusicPlayer == 0 ? "playbgm" : "playse";
         Status = $"Added song {index}{(string.IsNullOrEmpty(name) ? string.Empty : " (" + name + ")")}: an empty song that plays nothing. Use 'Insert song…' to put a MIDI or .s file in it{(NewSongsGetOwnVoicegroup ? "; it then gets a voicegroup of its own, made from the instruments the song uses" : string.Empty)}. Scripts play it with '{player} {index}'.";
         OnMessage?.Invoke(this, Status + (tableMovedTo >= 0 ? $" The song table was moved to {tableMovedTo:X6} to make room." : string.Empty));
      }

      /// <summary>
      /// Gives the song that was just written (its header is at 'headerAddress', the whole song at 'songAddress') a voicegroup of its own that holds only the instruments it uses.
      /// The voices are what the song's VOICE commands select (a MIDI file's General MIDI programs, or what the .s file asks for); each one is copied from the same slot of the voicegroup the song was written with.
      /// Every other slot is the games' blank instrument. Returns the sentence for the status line. 'warning' says why it could not be made (the song then keeps the voicegroup it was written with).
      /// </summary>
      private string CreateVoicegroupFromSongInstruments(ModelDelta token, int headerAddress, int songAddress, int songLength, string songName, string voicegroupHint, out string warning) {
         warning = null;
         if (!SongHeader.TryRead(model, headerAddress, out var header) || header.TrackCount == 0) return string.Empty;
         // a song file that defines its voicegroup inside its own data has one already
         if (header.Voicegroup >= songAddress && header.Voicegroup < songAddress + songLength) return " It brings its own voicegroup, so none was made.";
         var voices = new SongDisassembler(model).FindVoices(header);
         if (voices.Count == 0) return " It selects no instrument, so it keeps the voicegroup it was written with.";
         var sourceItem = FindVoicegroupItem(header.Voicegroup);
         var source = sourceItem?.Info ?? VoicegroupCatalog.Inspect(model, header.Voicegroup);
         var sourceTitle = sourceItem?.Title ?? source.Name ?? header.Voicegroup.ToString("X6");
         string caution = null;
         // The file names its voicegroup in two ways: an address (a song exported by this tool, right only for the ROM it came from, and a ROM changes its addresses every time it is built again)
         // and a name. A name that this ROM knows wins over an address that is not that voicegroup, so a song saved from another version of the ROM still finds its instruments.
         var named = string.IsNullOrEmpty(voicegroupHint) ? null : VoicegroupEditor.FindByName(Voicegroups.Select(group => group.Info), voicegroupHint);
         var namedItem = named == null ? null : Voicegroups.FirstOrDefault(group => group.Address == named.Address);
         if (namedItem != null && (sourceItem == null || sourceItem.Address != namedItem.Address)) {
            sourceItem = namedItem;
            source = namedItem.Info;
            sourceTitle = namedItem.Title;
            caution = $" The file's voicegroup address ({header.Voicegroup + BaseModel.PointerOffset:X8}) is not that voicegroup in this ROM (the file was probably saved from another version of it), so the voicegroup it names, {voicegroupHint}, was used.";
         } else if (sourceItem == null) {
            var real = source.IsCustom ? 0 : VoicegroupCatalog.CountRealInstruments(model, header.Voicegroup, source.EntryCount);
            if (real < 2) {
               // the address is not a voicegroup of this ROM and nothing else says which one was meant
               var fallback = selectedVoicegroup ?? DefaultVoicegroup();
               if (fallback != null) {
                  sourceItem = fallback;
                  source = fallback.Info;
                  sourceTitle = fallback.Title;
                  caution = $" The file's voicegroup address ({header.Voicegroup + BaseModel.PointerOffset:X8}) is not a voicegroup of this ROM, so the instruments were taken from {sourceTitle}: they may not be the ones the song was written for.";
               }
            }
         }
         if (!VoicegroupEditor.TryCreateForVoices(model, token, source, voices, songName, out var address, out var anchorName, out var missing, out var error)) {
            warning = "The song is inserted, but it could not get its own voicegroup: " + error;
            return string.Empty;
         }
         VoicegroupEditor.AssignToSong(model, token, headerAddress, address);
         var message = $" It has its own new voicegroup at {address:X6}{(anchorName == null ? string.Empty : " (" + anchorName + ")")}: the {voices.Count} instrument{(voices.Count == 1 ? string.Empty : "s")} the song uses (slot{(voices.Count == 1 ? " " : "s ")}{DescribeNumbers(voices)}) taken from {sourceTitle}, the other slots blank.";
         if (missing.Count > 0) message += $" {sourceTitle} has no instrument in slot{(missing.Count == 1 ? " " : "s ")}{DescribeNumbers(missing)}, so {(missing.Count == 1 ? "that slot stays" : "those slots stay")} blank.";
         if (caution != null) { message += caution; warning = caution.Trim(); }
         return message;
      }

      /// <summary>The voicegroup name that a song exported by this tool gives next to its voicegroup address (".equ song_grp, 0x0909C7E8   @ voicegroup vs_gym_leader"), or null.</summary>
      public static string FindVoicegroupHint(string songText) {
         if (string.IsNullOrEmpty(songText)) return null;
         var match = Regex.Match(songText, @"^[ \t]*\.equ[ \t]+\S+_grp[ \t]*,[^@\r\n]*@[ \t]*voicegroup[ \t]+([A-Za-z0-9_.]+)", RegexOptions.Multiline);
         return match.Success ? match.Groups[1].Value : null;
      }

      private static string DescribeNumbers(IEnumerable<int> numbers) {
         var list = numbers.ToList();
         return string.Join(", ", list.Take(8)) + (list.Count > 8 ? ", ..." : string.Empty);
      }

      #endregion

      #region Song names

      /// <summary>
      /// The text of a song file. Editors save text in different ways: UTF-8 with or without a byte order mark (Notepad adds one), and UTF-16 (the "Unicode" of Notepad, and the > of Windows PowerShell).
      /// The assembler needs plain text, so the mark is taken off and UTF-16 is converted. Anything else is read as UTF-8.
      /// </summary>
      public static string DecodeSongText(byte[] contents) {
         if (contents == null || contents.Length == 0) return string.Empty;
         if (contents.Length >= 3 && contents[0] == 0xEF && contents[1] == 0xBB && contents[2] == 0xBF) return Encoding.UTF8.GetString(contents, 3, contents.Length - 3);
         if (contents.Length >= 2 && contents[0] == 0xFF && contents[1] == 0xFE) return Encoding.Unicode.GetString(contents, 2, contents.Length - 2);
         if (contents.Length >= 2 && contents[0] == 0xFE && contents[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(contents, 2, contents.Length - 2);
         // UTF-16 without a mark: plain text then has a zero in every other byte
         int zerosEven = 0, zerosOdd = 0, length = Math.Min(contents.Length, 256);
         for (int i = 0; i < length; i++) if (contents[i] == 0) { if (i % 2 == 0) zerosEven++; else zerosOdd++; }
         if (length >= 8 && zerosOdd * 4 >= length && zerosEven == 0) return Encoding.Unicode.GetString(contents);
         if (length >= 8 && zerosEven * 4 >= length && zerosOdd == 0) return Encoding.BigEndianUnicode.GetString(contents);
         return Encoding.UTF8.GetString(contents);
      }

      /// <summary>
      /// Turns a file name (with or without its folder and extension) into something that works as a song name: the song name list can't hold spaces
      /// (names are identifiers in dropdowns and scripts), so anything but letters, digits and underscores becomes an underscore.
      /// </summary>
      public static string SongNameFromFileName(string fileName) {
         var name = fileName ?? string.Empty;
         var slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
         if (slash >= 0) name = name.Substring(slash + 1);
         var dot = name.LastIndexOf('.');
         if (dot > 0) name = name.Substring(0, dot);
         var sb = new StringBuilder();
         foreach (var c in name.Trim()) {
            var keep = c < 128 && (char.IsLetterOrDigit(c) || c == '_');
            if (keep) sb.Append(c);
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
         }
         return sb.ToString().Trim('_');
      }

      /// <summary>
      /// Writes the name of a new song into the song name list of the metadata, as part of the change being made (so undo takes it back).
      /// The name is made unique: a second "Route 101" becomes "Route_101_2". Returns the name used, or null if the ROM has no name list or there is no usable name.
      /// </summary>
      private string TryNameNewSong(ModelDelta token, int index, string name) {
         if (string.IsNullOrEmpty(name) || !model.TryGetList(SongNamesList, out var list)) return null;
         var names = list.ToList();
         while (names.Count <= index) names.Add(names.Count.ToString());
         var unique = name;
         for (int i = 2; names.Where((other, otherIndex) => otherIndex != index).Any(other => string.Equals(other, unique, StringComparison.OrdinalIgnoreCase)); i++) unique = name + "_" + i;
         names[index] = unique;
         model.SetList(token, SongNamesList, names, list.Comments, StoredList.GenerateHash(names));
         model.ClearCacheScope();
         return unique;
      }

      #endregion

      #region MIDI files

      private int midiVolume = 80;
      /// <summary>
      /// The master volume (0-127) a MIDI file's song is made with. MIDI files are usually mixed for loudness: at 127 most of them crackle on the GBA,
      /// so the default is lower, as the music packs for the All Instruments voicegroup advise.
      /// </summary>
      public int MidiVolume { get => midiVolume; set => SetClamped(ref midiVolume, value, 0, 127); }

      private bool midiReverbOn = true;
      /// <summary>Add reverb to songs made from MIDI files. Off leaves the song dry, which is what mid2agb does unless it is told otherwise.</summary>
      public bool MidiReverbOn { get => midiReverbOn; set => Set(ref midiReverbOn, value); }

      private int midiReverb = 50;
      /// <summary>The reverb amount (0-127) when it is on. The songs of the original games use 50.</summary>
      public int MidiReverb { get => midiReverb; set => SetClamped(ref midiReverb, value, 0, 127); }

      private int midiPriority;
      /// <summary>The priority of a song made from a MIDI file (0-255): when the engine runs out of channels, tracks of a song with a lower priority give way first.</summary>
      public int MidiPriority { get => midiPriority; set => SetClamped(ref midiPriority, value, 0, 255); }

      private bool midiExactGateTime = true;
      /// <summary>Keep the exact length of every note (like the songs of the original games do) instead of rounding them to the lengths the sound engine has a command for.</summary>
      public bool MidiExactGateTime { get => midiExactGateTime; set => Set(ref midiExactGateTime, value); }

      private void SetClamped(ref int field, int value, int lower, int upper, [CallerMemberName] string propertyName = null) {
         var limited = value.LimitToRange(lower, upper);
         if (field != limited) {
            field = limited;
            NotifyPropertyChanged(propertyName);
         } else if (limited != value) {
            NotifyPropertyChanged(propertyName); // show the limit instead of the number that was typed
         }
      }

      private AllInstrumentsStatus allInstruments;
      /// <summary>Whether the ROM has the All Instruments patch, said in a sentence that is shown under the description of the music section.</summary>
      public string AllInstrumentsNote => allInstruments?.Summary ?? string.Empty;
      /// <summary>What the patch is and what was found, for a tooltip.</summary>
      public string AllInstrumentsDetails => allInstruments?.Details ?? string.Empty;
      /// <summary>True when the ROM has the All Instruments voicegroup.</summary>
      public bool HasAllInstruments => allInstruments?.IsApplied ?? false;

      private void UpdateAllInstruments() {
         allInstruments = model.GetTable(SongTable) == null ? null : AllInstrumentsPatch.Inspect(model, Voicegroups.Select(group => group.Info));
         NotifyPropertyChanged(nameof(AllInstrumentsNote));
         NotifyPropertyChanged(nameof(AllInstrumentsDetails));
         NotifyPropertyChanged(nameof(HasAllInstruments));
      }

      /// <summary>
      /// The voicegroup that is picked for inserting songs when the ROM has no All Instruments one: of the voicegroups that hold a real set of instruments
      /// (the small voicegroups that 'Add new song' makes for one song each, with a few instruments and filler in every other slot, are not a good start for the next song),
      /// the one that the most songs of the game use (the first of them when several are used equally often), or else the first voicegroup of the list.
      /// </summary>
      private VoicegroupItem DefaultVoicegroup() {
         var real = Voicegroups.Where(group => !group.Info.IsCustom && VoicegroupCatalog.CountRealInstruments(model, group.Address, group.Info.EntryCount) >= 16).ToList();
         VoicegroupItem best = null;
         foreach (var group in real) if (best == null || group.Info.UsedBy.Count > best.Info.UsedBy.Count) best = group;
         return best ?? Voicegroups.FirstOrDefault();
      }

      /// <summary>What the Sound tab says when the ROM has no General MIDI instrument bank (no All Instruments voicegroup).</summary>
      public const string NoGeneralMidiBankNote = AllInstrumentsPatch.NoBankNote;

      /// <summary>The All Instruments voicegroup (anchored as sound.voicegroups.all_instruments), if the ROM has it.</summary>
      private VoicegroupItem FindAllInstrumentsVoicegroup() {
         // the same verdict as the note under the description: a voicegroup that only carries the name (an old toml whose anchor now points at other data) is not a bank
         if (allInstruments == null || !allInstruments.IsApplied) return null;
         return Voicegroups.FirstOrDefault(group => group.Address == allInstruments.Address && !group.Info.IsCustom);
      }

      /// <summary>
      /// The voicegroup a MIDI file is played with. The instruments of a MIDI file are General MIDI programs, which the All Instruments voicegroup is laid out for,
      /// but selecting a song selects the voicegroup of that song. So the All Instruments voicegroup (if the ROM has one) is used,
      /// unless the user picked another voicegroup, or ticked 'always use this one'.
      /// </summary>
      private VoicegroupItem ChooseVoicegroupForMidi() {
         if (voicegroupPickedByUser || OverrideVoicegroup) return selectedVoicegroup;
         // no bank in this ROM: the voicegroup most songs use, not the voicegroup of whichever song happens to be selected
         return FindAllInstrumentsVoicegroup() ?? DefaultVoicegroup() ?? selectedVoicegroup;
      }

      /// <summary>The options of the conversion, from the settings of the tab and the voicegroup the song is made for.</summary>
      private MidiToAgbOptions BuildMidiOptions(string fileName, VoicegroupItem voicegroup) {
         var voicegroupName = voicegroup?.Info.Name;
         var symbolName = string.IsNullOrEmpty(voicegroupName) ? "000" : "_" + new string(voicegroupName.Select(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_') ? c : '_').ToArray());
         return new MidiToAgbOptions {
            Label = MidiToAgb.LabelFromFileName(fileName),
            VoiceGroup = symbolName,
            MasterVolume = MidiVolume,
            Priority = MidiPriority,
            Reverb = MidiReverbOn ? MidiReverb : -1,
            ExactGateTime = MidiExactGateTime,
            AddMissingVoice = true, // a channel without a program change would be silent in the game
         };
      }

      private void InsertMidiFromFile(LoadedFile file) {
         var asNew = InsertAsNewSong;
         var musicPlayer = asNew ? InsertMusicPlayer : selectedSong?.MusicPlayer ?? 0;
         var general = !voicegroupPickedByUser && !OverrideVoicegroup && FindAllInstrumentsVoicegroup() != null; // ChooseVoicegroupForMidi takes All Instruments then
         var song = ConvertAndInsertMidi(file, asNew, true, out var error);
         if (song == null) { OnError?.Invoke(this, error); return; }
         if (song.Warning != null) OnError?.Invoke(this, song.Warning);
         if (song.TableMovedTo >= 0) OnMessage?.Invoke(this, $"The song table was moved to {song.TableMovedTo:X6} to make room.");
         Status = $"Converted {file.Name}: {song.Tracks} track{(song.Tracks == 1 ? string.Empty : "s")}, {ToneData.FormatSize(song.Size)}; inserted as song {song.Index}{(string.IsNullOrEmpty(song.Name) ? string.Empty : " (" + song.Name + ")")} at {song.Address:X6}, instruments from {song.PlayedWith}.{song.VoicegroupMessage}";
         // A MIDI file is read as General MIDI when nothing else was chosen. A MIDI file from a game's own source folder (pokeemerald's sound/songs/midi, for example) is not:
         // its programs are the slots of that game's voicegroup, so the General MIDI instruments make it sound like another song.
         if (!general && !voicegroupPickedByUser && !OverrideVoicegroup && allInstruments != null && !allInstruments.IsApplied) {
            Status += $" {NoGeneralMidiBankNote} ('Insert with voicegroup'): this song uses {song.PlayedWith}, the voicegroup most songs of the game use, so its instruments may not be the ones the MIDI file was written for. Undo, pick another voicegroup and insert the file again if it sounds wrong.";
         }
         if (general) {
            Status += " Its instruments are General MIDI programs. If the file comes from a game's own folder (its programs are slots of that game's voicegroup), undo, pick that voicegroup under 'Insert with voicegroup' and insert the file again.";
         }
         OnMessage?.Invoke(this, Status + (asNew ? $" Play it with 'playbgm {song.Index}' / 'playse {song.Index}'." : string.Empty) + TrackLimitNote(song.Tracks, musicPlayer));
      }

      /// <summary>Converts a MIDI file to song text with the settings of the tab and puts the song in the ROM, like a .s file.</summary>
      private InsertedSong ConvertAndInsertMidi(LoadedFile file, bool asNewSong, bool finish, out string error) {
         error = null;
         var voicegroup = ChooseVoicegroupForMidi();
         if (voicegroup == null) { error = "There is no voicegroup to play the MIDI file with. Pick one under 'Insert with voicegroup'."; return null; }
         var options = BuildMidiOptions(file.Name, voicegroup);
         MidiConversionResult conversion;
         try {
            conversion = MidiToAgb.Convert(file.Contents, options);
         } catch (MidiConversionException e) {
            error = $"Could not convert {file.Name}: {e.Message}";
            return null;
         }
         if (conversion.TrackCount == 0) { error = $"{file.Name} has no notes to play."; return null; }
         if (conversion.TrackCount > MaxSongTracks) {
            error = $"{file.Name} plays notes on {conversion.TrackCount} channels, but a GBA song can have at most {MaxSongTracks} tracks. Remove or merge some of its tracks in a MIDI editor and try again.";
            return null;
         }
         var symbols = new Dictionary<string, int> { ["voicegroup" + options.VoiceGroup] = voicegroup.Address + BaseModel.PointerOffset };
         var song = InsertSongFromText(conversion.Text, SongNameFromFileName(file.Name), asNewSong, finish, symbols, voicegroup, out var insertError);
         if (song == null) error = $"Could not insert {file.Name}: {insertError}";
         else song.PlayedWith = voicegroup.Title;
         return song;
      }

      #endregion

      /// <summary>Inserts several songs (.mid and .s files) at once, every one as a new song at the end of the song table. A file that can't be inserted is skipped and named in the result.</summary>
      private void InsertSongFiles(IReadOnlyList<LoadedFile> files) {
         if (model.GetTable(SongTable) == null) { OnError?.Invoke(this, $"This ROM has no {SongTable} table."); return; }
         var tableStart = model.GetTable(SongTable).Start;
         var failures = new List<string>();
         var inserted = new List<InsertedSong>();
         var warnings = new List<string>();
         for (int i = 0; i < files.Count; i++) {
            var file = files[i];
            Status = $"Inserting song {i + 1} of {files.Count}: {file.Name}";
            InsertedSong song;
            string error;
            if (IsMidiFile(file.Name)) song = ConvertAndInsertMidi(file, true, false, out error);
            else song = InsertSongFromText(DecodeSongText(file.Contents), SongNameFromFileName(file.Name), true, false, null, null, out error);
            if (song == null) failures.Add($"{file.Name}: {error}");
            else {
               inserted.Add(song);
               if (song.Warning != null) warnings.Add($"{file.Name}: {song.Warning}");
            }
         }
         // the whole batch is one undo step, and the lists are read once
         viewPort.ChangeHistory.ChangeCompleted();
         viewPort.Refresh();
         LoadSongs();
         if (inserted.Count > 0) SelectedSong = Songs.Count > inserted[inserted.Count - 1].Index ? Songs[inserted[inserted.Count - 1].Index] : null;
         var size = inserted.Sum(song => song.Size);
         Status = inserted.Count == 0
            ? $"None of the {files.Count} files could be inserted."
            : $"Inserted {inserted.Count} of {files.Count} songs as songs {inserted.Min(song => song.Index)} to {inserted.Max(song => song.Index)} ({ToneData.FormatSize(size)} in all).{(failures.Count > 0 ? $" {failures.Count} could not be inserted." : string.Empty)}";
         var problems = failures.Concat(warnings).ToList();
         if (problems.Count > 0) OnError?.Invoke(this, string.Join(Environment.NewLine, problems.Take(6)) + (problems.Count > 6 ? $"{Environment.NewLine}... and {problems.Count - 6} more." : string.Empty));
         var tableEnd = model.GetTable(SongTable).Start;
         if (tableEnd != tableStart) OnMessage?.Invoke(this, $"The song table was moved to {tableEnd:X6} to make room.");
         var tooManyTracks = InsertMusicPlayer == 0 ? inserted.Count(song => song.Tracks > UnmodifiedBgmTracks) : 0;
         var trackNote = tooManyTracks == 0 ? string.Empty : $" {tooManyTracks} of them have more than {UnmodifiedBgmTracks} tracks, the most the music player of the original game plays (a game with a bigger music player, NUM_TRACKS_BGM in pokeemerald-expansion, plays more).";
         if (inserted.Count > 0) OnMessage?.Invoke(this, Status + " Undo (Ctrl+Z) takes them all back." + trackNote);
      }

      private void ExecuteGotoSong() {
         if (selectedSong == null) return;
         viewPort.Goto.Execute($"{SongTable}/{selectedSong.Index}");
         RequestTabChange?.Invoke(this, new TabChangeRequestedEventArgs(viewPort));
      }

      #endregion

      #region Voicegroups of the selected song

      private bool updatingVoicegroupSelection;

      private VoicegroupItem songVoicegroup;
      /// <summary>
      /// The voicegroup in the selected song's header. Choosing another one rewrites the header's pointer (undoable);
      /// the next Play uses it.
      /// </summary>
      public VoicegroupItem SongVoicegroup {
         get => songVoicegroup;
         set {
            if (value == songVoicegroup) return;
            if (updatingVoicegroupSelection) { songVoicegroup = value; NotifyPropertyChanged(); return; }
            if (value == null || !CanChangeVoicegroup) return; // the list was reloaded or the selection cleared: nothing to apply
            ChangeSongVoicegroup(value);
         }
      }

      /// <summary>The voicegroup choices are only meaningful for a song with a readable header and tracks (an empty slot shares its header with the game's other unused slots).</summary>
      public bool CanChangeVoicegroup => HasSelectedSong && selectedSong.Header.TrackCount > 0;

      private string voicegroupNote = string.Empty;
      /// <summary>Says whether other songs share the selected song's voicegroup.</summary>
      public string VoicegroupNote { get => voicegroupNote; private set => Set(ref voicegroupNote, value); }

      private VoicegroupItem newVoicegroupSource;
      /// <summary>The voicegroup a new voicegroup is copied from (it starts out as the selected song's own).</summary>
      public VoicegroupItem NewVoicegroupSource { get => newVoicegroupSource; set => UpdateReference(ref newVoicegroupSource, value); }

      private StubCommand newVoicegroup, gotoVoicegroup, previewSlot, previewInstrument, useInstrument;
      /// <summary>Gives the selected song a new voicegroup of its own: a copy of NewVoicegroupSource with room for all 128 instruments.</summary>
      public ICommand NewVoicegroup => StubCommand(ref newVoicegroup, ExecuteNewVoicegroup, () => CanChangeVoicegroup);
      public ICommand GotoVoicegroup => StubCommand(ref gotoVoicegroup, ExecuteGotoVoicegroup, () => songVoicegroup != null && HasSelectedSong);
      /// <summary>Plays the instrument in the chosen slot of the song's voicegroup.</summary>
      public ICommand PreviewSlot => StubCommand(ref previewSlot, ExecutePreviewSlot, () => selectedSlot != null && HasSelectedSong);
      /// <summary>Plays the instrument chosen in the picker.</summary>
      public ICommand PreviewInstrument => StubCommand(ref previewInstrument, ExecutePreviewInstrument, () => selectedInstrument != null);
      /// <summary>Copies the instrument chosen in the picker into the chosen slot of the song's voicegroup.</summary>
      public ICommand UseInstrument => StubCommand(ref useInstrument, ExecuteUseInstrument, () => selectedInstrument != null && selectedSlot != null && CanChangeVoicegroup);

      /// <summary>TryUpdate for view model objects, which are compared by reference.</summary>
      private bool UpdateReference<T>(ref T field, T value, [CallerMemberName] string propertyName = null) where T : class {
         if (ReferenceEquals(field, value)) return false;
         field = value;
         NotifyPropertyChanged(propertyName);
         return true;
      }

      private void RaiseVoicegroupCommandsChanged() {
         newVoicegroup?.RaiseCanExecuteChanged();
         gotoVoicegroup?.RaiseCanExecuteChanged();
         previewSlot?.RaiseCanExecuteChanged();
         previewInstrument?.RaiseCanExecuteChanged();
         useInstrument?.RaiseCanExecuteChanged();
      }

      private int previewKey = 60;
      /// <summary>The note (0-127, 60 is middle C) played by the instrument previews. Drum kits and key splits answer with a different instrument for every note.</summary>
      public int PreviewKey { get => previewKey; set => Set(ref previewKey, value.LimitToRange(0, 127)); }

      #region Slots of the song's voicegroup

      public ObservableCollection<InstrumentItem> SongInstruments { get; } = new();

      private InstrumentItem selectedSlot;
      /// <summary>The slot (the number a song's VOICE command asks for) that UseInstrument writes to.</summary>
      public InstrumentItem SelectedSlot {
         get => selectedSlot;
         set {
            if (!UpdateReference(ref selectedSlot, value)) return;
            previewSlot?.RaiseCanExecuteChanged();
            useInstrument?.RaiseCanExecuteChanged();
         }
      }

      #endregion

      #region The instrument picker

      public ObservableCollection<InstrumentItem> InstrumentChoices { get; } = new();

      private VoicegroupItem instrumentSourceGroup;
      /// <summary>The voicegroup the picker lists the instruments of.</summary>
      public VoicegroupItem InstrumentSourceGroup {
         get => instrumentSourceGroup;
         set {
            if (!UpdateReference(ref instrumentSourceGroup, value)) return;
            RebuildInstrumentChoices();
         }
      }

      private InstrumentItem selectedInstrument;
      public InstrumentItem SelectedInstrument {
         get => selectedInstrument;
         set {
            if (!UpdateReference(ref selectedInstrument, value)) return;
            previewInstrument?.RaiseCanExecuteChanged();
            useInstrument?.RaiseCanExecuteChanged();
         }
      }

      #endregion

      private VoicegroupItem FindVoicegroupItem(int address) => Voicegroups.FirstOrDefault(group => group.Address == address);

      private VoicegroupItem EnsureVoicegroupItem(int address) {
         var item = FindVoicegroupItem(address);
         if (item != null) return item;
         // a voicegroup nothing else knows about (for example a song whose header points somewhere odd): list it as a custom one
         item = new VoicegroupItem(model, VoicegroupCatalog.Inspect(model, address));
         Voicegroups.Add(item);
         return item;
      }

      private string DescribeVoicegroup(int address) {
         var item = FindVoicegroupItem(address);
         return item != null ? $"{item.Title} ({address:X6})" : $"{address:X6}";
      }

      private int syncedSongIndex = -1;

      /// <summary>
      /// Shows the selected song's voicegroup in every voicegroup control and rebuilds the slot and instrument lists.
      /// The "copy from" and picker voicegroups follow the song when another song is selected, but stay as the user set them while the same song is edited.
      /// </summary>
      private void SyncVoicegroupSelection() {
         var header = selectedSong?.Header;
         var sameSong = selectedSong != null && selectedSong.Index == syncedSongIndex;
         var keepNewSource = sameSong ? newVoicegroupSource?.Address : null;
         var keepPickerSource = sameSong ? instrumentSourceGroup?.Address : null;
         syncedSongIndex = selectedSong?.Index ?? -1;
         updatingVoicegroupSelection = true;
         try {
            var item = header == null || header.TrackCount == 0 ? null : EnsureVoicegroupItem(header.Voicegroup);
            SongVoicegroup = item;
            NewVoicegroupSource = (keepNewSource == null ? null : FindVoicegroupItem(keepNewSource.Value)) ?? item;
            InstrumentSourceGroup = (keepPickerSource == null ? null : FindVoicegroupItem(keepPickerSource.Value)) ?? item;
         } finally {
            updatingVoicegroupSelection = false;
         }
         RebuildSongInstruments();
         RebuildInstrumentChoices();
         UpdateVoicegroupNote();
         NotifyPropertyChanged(nameof(CanChangeVoicegroup));
         RaiseVoicegroupCommandsChanged();
      }

      private void UpdateVoicegroupNote() {
         var item = songVoicegroup;
         if (item == null || selectedSong?.Header == null) { VoicegroupNote = string.Empty; return; }
         var others = OtherSongsUsing(item, selectedSong);
         var sb = new StringBuilder();
         if (item.IsCustom) {
            sb.Append($"The data at {item.Address:X6} doesn't look like a voicegroup. A new voicegroup can still be made as a copy of it.");
         } else if (others.Count > 0) {
            sb.Append($"{others.Count} other song{(others.Count == 1 ? " uses" : "s use")} this voicegroup too ({string.Join(", ", others.Take(4).Select(SongLabel))}{(others.Count > 4 ? ", ..." : "")}). Changing an instrument gives this song its own copy first, so they stay as they are.");
         } else {
            sb.Append("Only this song uses this voicegroup, so changing an instrument changes it in place.");
         }
         if (!item.IsCustom && item.EntryCount < VoicegroupCatalog.MaxEntries) {
            sb.Append($" It holds {item.EntryCount} instruments; a new voicegroup has room for all {VoicegroupCatalog.MaxEntries}.");
         }
         VoicegroupNote = sb.ToString().Trim();
      }

      private static string SongLabel(SongItem song) => string.IsNullOrEmpty(song.Name) ? song.Index.ToString() : song.Name;

      /// <summary>The songs, other than 'song', whose header points at the voicegroup.</summary>
      private List<SongItem> OtherSongsUsing(VoicegroupItem group, SongItem song)
         => Songs.Where(other => other != song && other.Header != null && other.Header.TrackCount > 0 && other.Header.Voicegroup == group.Address).ToList();

      private void RebuildSongInstruments() {
         var keepSlot = selectedSlot?.Slot ?? -1;
         SongInstruments.Clear();
         if (songVoicegroup != null && HasSelectedSong) {
            for (int slot = 0; slot < VoicegroupCatalog.MaxEntries; slot++) {
               var address = songVoicegroup.Address + slot * VoicegroupCatalog.EntrySize;
               var beyondEnd = !songVoicegroup.IsCustom && slot >= songVoicegroup.EntryCount;
               SongInstruments.Add(new InstrumentItem(model, slot, address, beyondEnd));
            }
         }
         SelectedSlot = keepSlot >= 0 && keepSlot < SongInstruments.Count ? SongInstruments[keepSlot] : SongInstruments.FirstOrDefault();
      }

      private int choicesSourceAddress = -1;

      private void RebuildInstrumentChoices() {
         var keepSlot = selectedInstrument?.Slot ?? -1;
         var keepBytes = selectedInstrument?.BytesKey;
         var keepSource = choicesSourceAddress;
         InstrumentChoices.Clear();
         var source = instrumentSourceGroup;
         choicesSourceAddress = source?.Address ?? -1;
         var choices = new List<InstrumentItem>();
         if (source != null && !source.IsCustom) {
            var byBytes = new Dictionary<string, InstrumentItem>();
            for (int slot = 0; slot < Math.Min(source.CopyCount, VoicegroupCatalog.MaxEntries); slot++) {
               var address = source.Address + slot * VoicegroupCatalog.EntrySize;
               var item = new InstrumentItem(model, slot, address, false);
               if (!item.IsValid) continue;
               // many slots hold the very same instrument (the unused ones especially): list each distinct instrument once
               if (byBytes.TryGetValue(item.BytesKey, out var first)) { first.AddSameSlot(slot); continue; }
               byBytes[item.BytesKey] = item;
               choices.Add(item);
            }
         }
         foreach (var choice in choices) InstrumentChoices.Add(choice);
         // the same instrument stays selected when the list is rebuilt for the same voicegroup (after one of its slots was edited, the instrument may have moved to another slot)
         var keep = keepSource != choicesSourceAddress || keepSlot < 0 ? null
            : choices.FirstOrDefault(choice => choice.BytesKey == keepBytes) ?? choices.FirstOrDefault(choice => choice.Slot == keepSlot);
         SelectedInstrument = keep ?? choices.FirstOrDefault();
      }

      /// <summary>After the ROM changed: read the selected song's header again (so the music engine sees the new pointer) and refresh what depends on it.</summary>
      private void RefreshSelectedSongHeader() {
         if (selectedSong == null) return;
         SongHeader.TryRead(model, selectedSong.HeaderAddress, out var header);
         selectedSong.SetHeader(header);
         RecomputeVoicegroupUsage();
         UpdateSongDetails();
      }

      private void RecomputeVoicegroupUsage() {
         var users = new Dictionary<int, List<string>>();
         foreach (var song in Songs) {
            if (song.Header == null || song.Header.TrackCount == 0) continue;
            if (!users.TryGetValue(song.Header.Voicegroup, out var list)) users[song.Header.Voicegroup] = list = new List<string>();
            list.Add(SongLabel(song));
         }
         foreach (var group in Voicegroups) group.UsedBy = users.TryGetValue(group.Address, out var list) ? list : (IReadOnlyList<string>)Array.Empty<string>();
      }

      /// <summary>Re-reads every song and voicegroup (a voicegroup was added) and selects the same song and slot again.</summary>
      private void ReloadSongsKeepingSelection() {
         var index = selectedSong?.Index ?? -1;
         var slot = selectedSlot?.Slot ?? -1;
         var pickerSource = instrumentSourceGroup?.Address;
         LoadSongs();
         SelectedSong = index >= 0 && index < Songs.Count ? Songs[index] : null;
         if (slot >= 0 && slot < SongInstruments.Count) SelectedSlot = SongInstruments[slot];
         var keptPicker = pickerSource == null ? null : FindVoicegroupItem(pickerSource.Value);
         if (keptPicker != null) InstrumentSourceGroup = keptPicker;
      }

      /// <summary>
      /// Voicegroups are found through the songs that use them (and through named anchors). When the song stops using a voicegroup nothing else uses,
      /// give that voicegroup a name, so it stays in the list and the song can go back to it.
      /// </summary>
      private void KeepVoicegroupListed(SongItem song, VoicegroupItem old, ModelDelta token) {
         if (old == null || old.IsCustom || old.Info.AnchorName != null) return;
         if (OtherSongsUsing(old, song).Count > 0) return;
         if (!string.IsNullOrEmpty(model.GetAnchorFromAddress(-1, old.Address))) return; // it has some other name already: that keeps it
         var songName = string.IsNullOrEmpty(song.Name) ? $"song_{song.Index}" : song.Name;
         VoicegroupEditor.NameVoicegroup(model, token, old.Address, VoicegroupEditor.UniqueAnchorName(model, songName + "_original"));
      }

      private void ChangeSongVoicegroup(VoicegroupItem chosen) {
         var song = selectedSong;
         var header = song.Header;
         if (header.Voicegroup == chosen.Address) return;
         var token = viewPort.CurrentChange;
         KeepVoicegroupListed(song, FindVoicegroupItem(header.Voicegroup), token);
         VoicegroupEditor.AssignToSong(model, token, header.Address, chosen.Address);
         viewPort.ChangeHistory.ChangeCompleted();
         viewPort.Refresh();
         RefreshSelectedSongHeader();
         Status = $"{SongLabel(song)} now uses voicegroup {chosen.Title} ({chosen.Address:X6}). Press Play to hear it.";
      }

      private void ExecuteNewVoicegroup() {
         var song = selectedSong;
         if (song?.Header == null || song.Header.TrackCount == 0) return;
         var source = newVoicegroupSource ?? songVoicegroup;
         var token = viewPort.CurrentChange;
         if (!CreatePrivateVoicegroup(song, source, token, out var address, out var anchorName)) { viewPort.ChangeHistory.ChangeCompleted(); return; }
         viewPort.ChangeHistory.ChangeCompleted();
         viewPort.Refresh();
         ReloadSongsKeepingSelection();
         Status = $"{SongLabel(song)} now has its own voicegroup at {address:X6}{(anchorName == null ? string.Empty : " (" + anchorName + ")")}, a copy of {source?.Title ?? "nothing"}. It sounds the same for now: change its instruments below.";
         OnMessage?.Invoke(this, Status);
      }

      /// <summary>Allocates a new voicegroup (a copy of 'source') and assigns it to the song, all in 'token'. Reports the problem and changes nothing when there is no room.</summary>
      private bool CreatePrivateVoicegroup(SongItem song, VoicegroupItem source, ModelDelta token, out int address, out string anchorName) {
         var name = string.IsNullOrEmpty(song.Name) ? $"song_{song.Index}" : song.Name;
         if (!VoicegroupEditor.TryCreateCopy(model, token, source?.Info, name, out address, out anchorName, out var error)) {
            OnError?.Invoke(this, "Could not make a new voicegroup: " + error);
            return false;
         }
         KeepVoicegroupListed(song, FindVoicegroupItem(song.Header.Voicegroup), token);
         VoicegroupEditor.AssignToSong(model, token, song.Header.Address, address);
         return true;
      }

      private void ExecuteGotoVoicegroup() {
         if (songVoicegroup == null) return;
         viewPort.Goto.Execute(songVoicegroup.Info.AnchorName ?? songVoicegroup.Address.ToString("X6"));
         RequestTabChange?.Invoke(this, new TabChangeRequestedEventArgs(viewPort));
      }

      private void ExecutePreviewSlot() {
         if (selectedSlot == null) return;
         PlayTone(selectedSlot.Address, $"slot {selectedSlot.Slot}");
      }

      private void ExecutePreviewInstrument() {
         if (selectedInstrument == null) return;
         PlayTone(selectedInstrument.Address, $"{instrumentSourceGroup?.Title} #{selectedInstrument.Slot}");
      }

      private void PlayTone(int toneAddress, string what) {
         byte[] wav;
         try {
            wav = new M4aRenderer(model).RenderPreviewNote(toneAddress, PreviewKey, 100);
         } catch (Exception e) {
            OnError?.Invoke(this, "Could not play the instrument: " + e.Message);
            return;
         }
         if (IsSilent(wav)) { Status = $"Nothing to hear for {what} at note {PreviewKey}: the instrument is empty, or has no sound for that note."; return; }
         Status = $"Playing {what} at note {PreviewKey}.";
         RequestPlayWav?.Invoke(this, wav);
      }

      private static bool IsSilent(byte[] wav) {
         for (int i = 44; i + 1 < wav.Length; i += 2) {
            if (Math.Abs((short)(wav[i] | (wav[i + 1] << 8))) > 80) return false;
         }
         return true;
      }

      private void ExecuteUseInstrument() {
         var song = selectedSong;
         if (song?.Header == null || song.Header.TrackCount == 0 || selectedSlot == null || selectedInstrument == null || songVoicegroup == null) return;
         var slot = selectedSlot.Slot;
         var instrument = selectedInstrument;
         var group = songVoicegroup;
         var token = viewPort.CurrentChange;

         // an instrument may only change a voicegroup that belongs to this song and has the slot
         var shared = OtherSongsUsing(group, song).Count > 0;
         var needsCopy = shared || group.IsCustom || slot >= group.EntryCount;
         var targetAddress = group.Address;
         var copyMessage = string.Empty;
         if (needsCopy) {
            if (!CreatePrivateVoicegroup(song, group, token, out targetAddress, out var anchorName)) { viewPort.ChangeHistory.ChangeCompleted(); return; }
            copyMessage = shared
               ? $" {SongLabel(song)} shared its voicegroup with other songs, so it got its own copy ({anchorName ?? targetAddress.ToString("X6")}) first."
               : $" {SongLabel(song)} now has its own voicegroup ({anchorName ?? targetAddress.ToString("X6")}) with room for every slot.";
         }
         if (!VoicegroupEditor.TryCopyTone(model, token, targetAddress, slot, instrument.Address, out var error)) {
            viewPort.ChangeHistory.ChangeCompleted();
            OnError?.Invoke(this, error);
            return;
         }
         viewPort.ChangeHistory.ChangeCompleted();
         viewPort.Refresh();
         if (needsCopy) {
            ReloadSongsKeepingSelection();
         } else {
            // edited in place: the lists were read before the change
            RefreshSelectedSongHeader();
            RebuildSongInstruments();
            RebuildInstrumentChoices();
         }
         if (slot < SongInstruments.Count) SelectedSlot = SongInstruments[slot];
         Status = $"Slot {slot} now plays {instrument.Description} (from {instrumentSourceGroup?.Title} #{instrument.Slot}).{copyMessage} Press Play to hear it in the song.";
         OnMessage?.Invoke(this, Status);
      }

      #endregion
   }

   public class CryItem : ViewModelCore {
      public int Index { get; }
      public string Name { get; }
      public int EntryAddress { get; }
      public int SampleAddress { get; }
      public GbaSample Sample { get; }
      public string Label => string.IsNullOrEmpty(Name) ? Index.ToString() : $"{Index} {Name}";
      public string Summary => Sample == null ? "(invalid)" : $"{Sample.SampleRate:0} Hz, {Sample.DurationSeconds:0.00} s";

      private bool isFilteredOut;
      public bool IsFilteredOut { get => isFilteredOut; set => TryUpdate(ref isFilteredOut, value); }
      private bool selected;
      public bool Selected { get => selected; set => TryUpdate(ref selected, value); }

      public CryItem(int index, string name, int entryAddress, int sampleAddress, GbaSample sample) {
         (Index, Name, EntryAddress, SampleAddress, Sample) = (index, name ?? string.Empty, entryAddress, sampleAddress, sample);
      }

      public void MatchToFilter(string filter) {
         if (string.IsNullOrWhiteSpace(filter)) { IsFilteredOut = false; return; }
         var terms = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
         IsFilteredOut = !terms.All(term => Index.ToString() == term || Name.Contains(term, StringComparison.OrdinalIgnoreCase));
      }
   }

   public class SongItem : ViewModelCore {
      public int Index { get; }
      private string name;
      public string Name {
         get => name;
         set { if (!TryUpdate(ref name, value ?? string.Empty)) return; NotifyPropertyChanged(nameof(Label)); }
      }
      public int EntryAddress { get; }
      public int HeaderAddress { get; }
      public SongHeader Header { get; private set; }
      public int MusicPlayer { get; }
      public string Label => string.IsNullOrEmpty(Name) ? Index.ToString() : $"{Index} {Name}";

      /// <summary>The ROM was edited (for example the song got another voicegroup): use the header as it reads now.</summary>
      public void SetHeader(SongHeader header) {
         Header = header;
         NotifyPropertyChanged(nameof(Header));
         NotifyPropertyChanged(nameof(Summary));
      }

      public string Summary => Header == null ? "(invalid)" : Header.TrackCount == 0 ? "no tracks" : $"{Header.TrackCount} track{(Header.TrackCount == 1 ? "" : "s")}";

      private bool isFilteredOut;
      public bool IsFilteredOut { get => isFilteredOut; set => TryUpdate(ref isFilteredOut, value); }
      private bool selected;
      public bool Selected { get => selected; set => TryUpdate(ref selected, value); }

      public SongItem(int index, string name, int entryAddress, int headerAddress, SongHeader header, int musicPlayer) {
         (Index, this.name, EntryAddress, HeaderAddress, Header, MusicPlayer) = (index, name ?? string.Empty, entryAddress, headerAddress, header, musicPlayer);
      }

      public void MatchToFilter(string filter) {
         if (string.IsNullOrWhiteSpace(filter)) { IsFilteredOut = false; return; }
         var terms = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
         IsFilteredOut = !terms.All(term => Index.ToString() == term || Name.Contains(term, StringComparison.OrdinalIgnoreCase));
      }
   }

   /// <summary>A voicegroup in a drop-down: its name (from the metadata when there is one), size and the songs that use it.</summary>
   public class VoicegroupItem : ViewModelCore {
      private readonly IDataModel model;
      private IReadOnlyList<string> usedBy;

      public VoicegroupInfo Info { get; }
      public int Address => Info.Address;
      public int EntryCount => Info.EntryCount;
      public int CopyCount => Info.CopyCount;
      /// <summary>True when the address isn't recognizable as a voicegroup (there are no valid instruments).</summary>
      public bool IsCustom => Info.IsCustom;

      /// <summary>"route110" when the metadata names the voicegroup, otherwise "Voicegroup 1351990" or, for something that isn't a voicegroup, "Custom (1351990)".</summary>
      public string Title => Info.IsCustom ? $"Custom ({Info.Address:X6})" : Info.Name ?? $"Voicegroup {Info.Address:X6}";

      public IReadOnlyList<string> UsedBy {
         get => usedBy;
         set {
            usedBy = value ?? Array.Empty<string>();
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(Label));
            NotifyPropertyChanged(nameof(Description));
         }
      }

      public string Label {
         get {
            var sb = new StringBuilder(Title);
            if (!Info.IsCustom) sb.Append($" · {Info.EntryCount} instruments");
            sb.Append(usedBy.Count == 0 ? " · unused" : $" · used by {usedBy[0]}{(usedBy.Count > 1 ? $" +{usedBy.Count - 1}" : string.Empty)}");
            return sb.ToString();
         }
      }

      /// <summary>The tooltip: where it is, what its first instruments are and who uses it.</summary>
      public string Description {
         get {
            var sb = new StringBuilder($"{Title} at {Address:X6}");
            if (Info.AnchorName != null && Info.Name != Info.AnchorName) sb.Append($" ({Info.AnchorName})");
            sb.AppendLine();
            if (Info.IsCustom) sb.AppendLine("Not recognizable as a voicegroup.");
            else sb.AppendLine($"{Info.EntryCount} instruments, starting with: {VoicegroupCatalog.DescribeStart(model, Address, Info.EntryCount)}");
            sb.Append(usedBy.Count == 0 ? "No song uses it." : $"Used by: {string.Join(", ", usedBy.Take(8))}{(usedBy.Count > 8 ? $" and {usedBy.Count - 8} more" : string.Empty)}");
            return sb.ToString();
         }
      }

      public VoicegroupItem(IDataModel model, VoicegroupInfo info) {
         this.model = model;
         Info = info;
         usedBy = info.UsedBy ?? Array.Empty<string>();
      }

      public override string ToString() => Label;
   }

   /// <summary>One slot of a voicegroup (or one distinct instrument of it): the 12 byte ToneData entry a song's VOICE command selects.</summary>
   public class InstrumentItem : ViewModelCore {
      private readonly List<int> sameSlots = new();

      public int Slot { get; }
      public int Address { get; }
      public ToneData Tone { get; }
      public bool IsBeyondEnd { get; }
      public bool IsValid => Tone != null && Tone.IsValid;
      /// <summary>The 12 bytes as hex: two instruments with the same key sound the same.</summary>
      public string BytesKey { get; }
      /// <summary>What the instrument is: "Sample 5.2 KB, 13379 Hz, 0.39 s", "Square wave 1, 50% duty", ...</summary>
      public string Description { get; }

      public string Label {
         get {
            var also = sameSlots.Count == 0 ? string.Empty : $"  (also {string.Join(", ", sameSlots.Take(4).Select(slot => "#" + slot))}{(sameSlots.Count > 4 ? ", ..." : string.Empty)})";
            return $"#{Slot}  {Description}{(IsBeyondEnd ? "  (past the end of this voicegroup)" : string.Empty)}{also}";
         }
      }

      public InstrumentItem(IDataModel model, int slot, int address, bool beyondEnd) {
         (Slot, Address, IsBeyondEnd) = (slot, address, beyondEnd);
         Tone = ToneData.Read(model, address);
         Description = Tone == null ? "(outside the ROM)" : Tone.Describe(model);
         BytesKey = Tone == null ? string.Empty : string.Concat(Enumerable.Range(0, ToneData.Size).Select(i => model[address + i].ToString("X2")));
      }

      /// <summary>Another slot of the same voicegroup holds this very instrument.</summary>
      public void AddSameSlot(int slot) {
         sameSlots.Add(slot);
         NotifyPropertyChanged(nameof(Label));
      }

      public override string ToString() => Label;
   }
}
