using System;
using System.Collections.Generic;
using HarmonyLib;
using ModLoader;
using UnityEngine;

namespace SFSPartMenuPager
{
    /// <summary>
    /// Mod entry point. The game's built-in loader (ModLoader.Mod in Assembly-CSharp.dll) creates this
    /// class and calls Early_Load() then Load().
    ///
    /// The mod has one job: the part info panel ("StatsMenu") that the game builds for a selected part
    /// is a single vertical list with no scrolling, so a part that exposes a lot of editable values -
    /// the procedural parts in a parts pack such as "Procedural Bundle" - pushes that list off the
    /// bottom of the screen. This mod splits the list into pages and puts Previous / Next buttons at
    /// the top of the panel.
    /// </summary>
    public class Entrypoint : Mod
    {
        public static Entrypoint Instance { get; private set; }

        public Entrypoint()
        {
            Instance = this;
        }

        public override string ModNameID => "sfsmenupager";
        public override string DisplayName => "Part Menu Pager";
        public override string Author => "oanoals9";
        public override string MinimumGameVersionNecessary => "1.6.0.18";
        public override string ModVersion => "0.1.1";
        public override string Description =>
            "Splits a long part properties panel into pages with Previous / Next buttons, so that the " +
            "procedural parts of a parts pack stay usable.";
        public override string IconLink => null;
        public override Action LoadKeybindings => null;

        public override Dictionary<string, string> Dependencies
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { "UITools", "1.1.6" }
                };
            }
        }

        public override void Early_Load()
        {
            // Patch class by class, so that one failing patch (for example after a game update)
            // cannot stop the whole mod from loading.
            int applied = 0;
            int failed = 0;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            Harmony harmony = new Harmony(ModNameID);

            foreach (Type type in assembly.GetTypes())
            {
                try
                {
                    if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0 &&
                        type.GetCustomAttributes(typeof(HarmonyPatchAll), true).Length == 0)
                    {
                        continue;
                    }

                    harmony.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception e)
                {
                    failed++;
                    PmpLog.Error("patch class " + type.FullName + " failed: " + e);
                }
            }

            PmpLog.Detail("Early_Load finished (" + applied + " patch class(es) applied, " + failed + " failed)");
        }

        public override void Load()
        {
            PmpConfig.Initialise();
            PmpSettingsPage.Register();
            PmpSettingsAux.Ensure();

            PmpLog.Always("loaded (v" + ModVersion + ")");
        }
    }

    /// <summary>
    /// "Is this missing?" for the game's observable types. They overload the equality operators (and
    /// the game marks the old ones obsolete), so a plain == would either not compile or silently ask
    /// the wrong question. Unity's own "destroyed object" test is applied as well.
    /// </summary>
    public static class PmpLib
    {
        public static bool IsNull(object value)
        {
            if (ReferenceEquals(value, null))
                return true;

            UnityEngine.Object unity = value as UnityEngine.Object;
            if (ReferenceEquals(unity, null))
                return false;

            return unity == null;
        }
    }

    /// <summary>Small logging helper, so every line of this mod can be found in the F1 console with
    /// one grep. Write/Warn/Error always print; Detail only prints while the Debug setting is on.</summary>
    public static class PmpLog
    {
        private const string Prefix = "[SFSPMP] ";

        public static void Write(string message)
        {
            Debug.Log(Prefix + message);
        }

        public static void Always(string message)
        {
            Debug.Log(Prefix + message);
        }

        public static void Detail(string message)
        {
            if (!PmpConfig.Debug)
                return;

            Debug.Log(Prefix + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        public static void Error(string message)
        {
            Debug.LogError(Prefix + message);
        }

        /// <summary>Logs and swallows. Used around everything that touches game objects, so that an
        /// unexpected state can never break the build screen.</summary>
        public static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Error(what + " failed: " + e);
            }
        }
    }
}
