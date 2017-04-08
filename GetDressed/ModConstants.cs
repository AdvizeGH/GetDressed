using System.IO;
using StardewModdingAPI;
using StardewValley;
namespace GetDressed
{
    /// <summary>Internal constants used throughout the mod code.</summary>
    internal static class ModConstants
    {
        /// <summary>The mod version number.</summary>
        public static string VersionNumber => "3.2";

        /// <summary>The relative path to the current per-save config file, or <c>null</c> if the save isn't loaded yet.</summary>
        public static string PerSaveConfigPath => Constants.SaveFolderName != null
            ? Path.Combine("psconfigs", $"{Constants.SaveFolderName}.json")
            : null;

        /// <summary>The game's current zoom level.</summary>
        public static float ZoomLevel = Game1.options.zoomLevel;
    }
}
