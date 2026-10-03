using System;
using SFS.UI.ModGUI;
using TMPro;
using UITools;
using UnityEngine;
using UnityEngine.Events;
using Type = SFS.UI.ModGUI.Type;

namespace SFSPartMenuPager
{
    /// <summary>
    /// The mod's page in UITools' "Mods Settings" window. UITools creates that window during its own
    /// Early_Load, and the loader runs every mod's Early_Load before any mod's Load, so calling
    /// Register() from Load always finds it.
    ///
    /// Keep this page short: the settings window is the same height whatever is on the page, and its
    /// right hand column cannot be scrolled (UITools Scroll only makes the left column of that window
    /// scroll), so anything below the fold would be unreachable.
    /// </summary>
    public static class PmpSettingsPage
    {
        private const string ModTitle = "Part Menu Pager";

        private static bool registered;
        private static Label rowsLabel;

        public static void Register()
        {
            if (registered)
                return;

            try
            {
                ConfigurationMenu.Add(ModTitle,
                    new ValueTuple<string, Func<Transform, GameObject>>[]
                    {
                        new ValueTuple<string, Func<Transform, GameObject>>(
                            "Part Menu Pager", new Func<Transform, GameObject>(CreatePage))
                    });

                registered = true;
                PmpLog.Detail("settings page added to the Mods Settings window");
            }
            catch (Exception e)
            {
                PmpLog.Warn("could not add the settings page yet: " + e.Message);
            }
        }

        /// <summary>Called every frame while the window is alive, so the row count follows the slider.</summary>
        public static void Tick()
        {
            if (rowsLabel == null)
                return;

            string text = "Rows per page: " + PmpConfig.RowsPerPage;
            try
            {
                if (rowsLabel.Text != text)
                    rowsLabel.Text = text;
            }
            catch
            {
            }
        }

        private static GameObject CreatePage(Transform parent)
        {
            Box box = MakePage(parent, "Part Menu Pager Settings");
            int width = RowWidth();

            AddToggle(box, width, "Paginate long part menus",
                      delegate { return PmpConfig.Enabled; },
                      delegate { PmpConfig.SetEnabled(!PmpConfig.Enabled); });

            Builder.CreateLabel(box, width, 44, 0, 0,
                "A part panel longer than this gets page buttons at the top.\n" +
                "Turn this off to get the game's unmodified panel back.");

            AddSection(box, width, "Page size");

            rowsLabel = Builder.CreateLabel(box, width, 26, 0, 0, "Rows per page: " + PmpConfig.RowsPerPage);
            rowsLabel.TextAlignment = TextAlignmentOptions.Left;

            Builder.CreateSlider(box, width, PmpConfig.RowsPerPage,
                                 new ValueTuple<float, float>(PmpConfig.MinRowsPerPage, PmpConfig.MaxRowsPerPage),
                                 true,
                                 new UnityAction<float>(delegate (float changed) { PmpConfig.SetRowsPerPage(changed); }),
                                 delegate (float value) { return value.ToString("0"); });

            AddToggle(box, width, "Plain < > instead of arrow buttons",
                      delegate { return PmpConfig.AsciiArrows; },
                      delegate { PmpConfig.SetAsciiArrows(!PmpConfig.AsciiArrows); });

            Builder.CreateLabel(box, width, 60, 0, 0,
                "The arrow characters are used only when the game's button font has them;\n" +
                "otherwise the buttons fall back to < and > on their own.");

            AddSection(box, width, "Debug");
            AddToggle(box, width, "Debug output",
                      delegate { return PmpConfig.Debug; },
                      delegate { PmpConfig.SetDebug(!PmpConfig.Debug); });

            return box.gameObject;
        }

        // ------------------------------------------------------------------ building blocks

        private static Box MakePage(Transform parent, string title)
        {
            Vector2Int content = ConfigurationMenu.ContentSize;
            Box box = Builder.CreateBox(parent, content.x, content.y, 0, 0, 0.3f);
            box.CreateLayoutGroup(Type.Vertical, TextAnchor.UpperCenter, 16f,
                                  new RectOffset(0, 0, 5, 5), true);

            Builder.CreateLabel(box, RowWidth(), 40, 0, 0, title);
            return box;
        }

        private static int RowWidth()
        {
            int width = ConfigurationMenu.ContentSize.x - 50;
            return width < 100 ? 100 : width;
        }

        private static void AddSection(Box box, int width, string title)
        {
            Builder.CreateSeparator(box, width, 0, 0);
            Label label = Builder.CreateLabel(box, width, 34, 0, 0, title);
            label.TextAlignment = TextAlignmentOptions.Left;
        }

        private static void AddToggle(Box box, int width, string title, Func<bool> get, Action toggle)
        {
            Builder.CreateToggleWithLabel(box, width, 34, get, toggle, 0, 0, title);
        }
    }

    /// <summary>
    /// A tiny object that survives scene changes, so the settings page can follow the slider while the
    /// window is open (including from the main menu, where the mod does nothing else).
    /// </summary>
    public class PmpSettingsAux : MonoBehaviour
    {
        private static PmpSettingsAux instance;

        public static void Ensure()
        {
            if (instance != null)
                return;

            try
            {
                GameObject holder = new GameObject("SFS Part Menu Pager (aux)");
                UnityEngine.Object.DontDestroyOnLoad(holder);
                instance = holder.AddComponent<PmpSettingsAux>();
            }
            catch (Exception e)
            {
                PmpLog.Warn("the settings helper could not be created: " + e.Message);
            }
        }

        private void Update()
        {
            try
            {
                PmpSettingsPage.Tick();
            }
            catch (Exception e)
            {
                PmpLog.Error("the settings helper failed: " + e);
            }
        }
    }
}
