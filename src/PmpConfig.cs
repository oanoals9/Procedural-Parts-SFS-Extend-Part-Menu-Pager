using System;
using SFS.IO;
using SFS.Variables;
using UITools;
using UnityEngine;

namespace SFSPartMenuPager
{
    /// <summary>
    /// Everything the player can change. Every field is one of the game's serialisable observable
    /// types, so UITools' ModSettings base can load and save the whole class as JSON in the mod folder.
    /// </summary>
    [Serializable]
    public class PmpConfigData
    {
        /// <summary>Turn the paging on or off. Off leaves the panel exactly as the game builds it.</summary>
        public Bool_Local enabled = new Bool_Local { Value = true };

        /// <summary>
        /// How many entries of the panel go on one page. The panel's entries are not all the same
        /// height (a plain value row is about half as tall as one with a slider), so this is a count
        /// of rows, not of pixels.
        /// </summary>
        public Float_Local rowsPerPage = new Float_Local(PmpConfig.DefaultRowsPerPage);

        /// <summary>
        /// Use "&lt;" and "&gt;" for the page buttons instead of the arrow characters. The arrow
        /// characters are only used when the button font actually has them (checked on the first long
        /// panel), so this is normally left off.
        /// </summary>
        public Bool_Local asciiArrows = new Bool_Local { Value = false };

        public Bool_Local debug = new Bool_Local { Value = false };
    }

    /// <summary>Settings storage. Derives from UITools' ModSettings, which loads the file on startup,
    /// saves it again, and saves once more whenever one of the observable values changes.</summary>
    public class PmpConfig : ModSettings<PmpConfigData>
    {
        public const float DefaultRowsPerPage = 14f;
        public const float MinRowsPerPage = 4f;
        public const float MaxRowsPerPage = 40f;

        private static bool ready;

        public static bool Ready
        {
            get { return ready; }
        }

        protected override FilePath SettingsFile
        {
            get { return new FolderPath(Entrypoint.Instance.ModFolder).ExtendToFile("settings.txt"); }
        }

        protected override void RegisterOnVariableChange(Action action)
        {
            settings.enabled.OnChange += action;
            settings.rowsPerPage.OnChange += action;
            settings.asciiArrows.OnChange += action;
            settings.debug.OnChange += action;
            Application.quitting += action;
        }

        /// <summary>Call once, from the mod's Load. Never throws: if anything goes wrong the mod keeps
        /// working with the built in defaults.</summary>
        public static void Initialise()
        {
            if (ready)
                return;

            try
            {
                PmpConfig config = new PmpConfig();
                config.Initialize();
                ready = settings != null;
                PmpLog.Detail("settings loaded: " + Describe());
            }
            catch (Exception e)
            {
                ready = false;
                PmpLog.Warn("settings could not be loaded, the defaults are used: " + e);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static bool Missing(object value)
        {
            return settings == null || PmpLib.IsNull(value);
        }

        private static float Number(Float_Local value, float fallback, float min, float max)
        {
            if (Missing(value))
                return fallback;
            return Mathf.Clamp(value.Value, min, max);
        }

        private static bool Flag(Bool_Local value, bool fallback)
        {
            if (Missing(value))
                return fallback;
            return value.Value;
        }

        private static void SetNumber(Float_Local value, float v, float min, float max)
        {
            if (Missing(value))
                return;
            value.Value = Mathf.Clamp(v, min, max);
        }

        private static void SetFlag(Bool_Local value, bool v)
        {
            if (Missing(value))
                return;
            value.Value = v;
        }

        // ------------------------------------------------------------------ values

        public static bool Enabled
        {
            get { return Flag(settings == null ? null : settings.enabled, true); }
        }

        public static void SetEnabled(bool v)
        {
            SetFlag(settings == null ? null : settings.enabled, v);
        }

        public static int RowsPerPage
        {
            get
            {
                float value = Number(settings == null ? null : settings.rowsPerPage,
                                     DefaultRowsPerPage, MinRowsPerPage, MaxRowsPerPage);
                return Mathf.Clamp(Mathf.RoundToInt(value), (int)MinRowsPerPage, (int)MaxRowsPerPage);
            }
        }

        public static void SetRowsPerPage(float v)
        {
            SetNumber(settings == null ? null : settings.rowsPerPage, v, MinRowsPerPage, MaxRowsPerPage);
        }

        public static bool AsciiArrows
        {
            get { return Flag(settings == null ? null : settings.asciiArrows, false); }
        }

        public static void SetAsciiArrows(bool v)
        {
            SetFlag(settings == null ? null : settings.asciiArrows, v);
        }

        public static bool Debug
        {
            get { return Flag(settings == null ? null : settings.debug, false); }
        }

        public static void SetDebug(bool v)
        {
            SetFlag(settings == null ? null : settings.debug, v);

            if (v)
                PmpLog.Always("debug output enabled");
        }

        private static string Describe()
        {
            return "enabled " + (Enabled ? "on" : "off") +
                   ", rows per page " + RowsPerPage +
                   ", asciiArrows " + (AsciiArrows ? "on" : "off") +
                   ", debug " + (Debug ? "on" : "off");
        }
    }
}
