using System;
using System.IO;
using System.Text;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   /// <summary>Where the CUBE patch and the matching HexManiac metadata (.toml) come from.</summary>
   public interface ICubeResources {
      /// <summary>The bytes of the shipped patch (gzip-compressed BPS), or null if it is not there.</summary>
      byte[] ReadPatch();

      /// <summary>The lines of the shipped CUBE .toml, or null if it is not there.</summary>
      string[] ReadToml();

      /// <summary>What the older CUBE versions look like (gzip file written by CubeFingerprint.Write), or null if it is not there.</summary>
      byte[] ReadVersions();

      /// <summary>Where the files are expected, for error messages.</summary>
      string Location { get; }
   }

   /// <summary>
   /// The files that are copied next to the program by the build (see HexManiac.WPF.csproj):
   /// resources/cube_emerald.bps.gz and resources/cube_emerald.toml.
   /// </summary>
   public class ShippedCubeResources : ICubeResources {
      public const string PatchFileName = "cube_emerald.bps.gz";
      public const string TomlFileName = "cube_emerald.toml";
      public const string VersionsFileName = "cube_versions.fp";

      private readonly string folder;

      /// <param name="folder">Defaults to the 'resources' folder next to the program.</param>
      public ShippedCubeResources(string folder = null) {
         this.folder = folder ?? Path.Combine(AppContext.BaseDirectory, "resources");
      }

      public string Location => Path.GetFullPath(folder);

      public byte[] ReadPatch() {
         var path = Path.Combine(folder, PatchFileName);
         return File.Exists(path) ? File.ReadAllBytes(path) : null;
      }

      public byte[] ReadVersions() {
         var path = Path.Combine(folder, VersionsFileName);
         return File.Exists(path) ? File.ReadAllBytes(path) : null;
      }

      public string[] ReadToml() {
         var path = Path.Combine(folder, TomlFileName);
         return File.Exists(path) ? File.ReadAllLines(path, Encoding.UTF8) : null;
      }
   }
}
