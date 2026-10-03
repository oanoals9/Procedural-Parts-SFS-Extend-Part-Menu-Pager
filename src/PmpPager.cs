using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace SFSPartMenuPager
{
    /// <summary>
    /// The whole mod.
    ///
    /// The game builds the panel a selected part shows (StatsMenu) in one pass:
    ///   Open() -> Close() -> the drawing callback appends one "draw" entry per value ->
    ///   every entry is instantiated as a row below the previous one -> the panel is resized.
    /// Nothing in there scrolls, clips or wraps, so a part with forty editable values produces a panel
    /// that runs off the bottom of the screen and its lower half cannot be reached at all.
    ///
    /// How the paging works:
    ///   1. The panel is opened normally, and every Draw() call is counted (the resulting panel is
    ///      then thrown away again).
    ///   2. If it is at most one page long, nothing else happens - the panel the player sees is the
    ///      one the game just built, untouched.
    ///   3. If it is longer, the panel is built a second time, with a filter in front of Draw() that
    ///      lets through only the entries belonging to the page being shown, and with two extra
    ///      buttons queued ahead of everything else.
    ///
    /// Building the page with the game's own code (rather than hiding rows afterwards) is what keeps
    /// the group backgrounds and the panel's own height exactly right: the game only ever sees the
    /// rows that are on the page.
    /// </summary>
    public static class PmpPager
    {
        /// <summary>The page buttons are queued with the highest possible priority: the game sorts
        /// the panel's entries by priority (descending) and then by insertion order, so they end up
        /// at the very top.</summary>
        private const int PagerPriority = int.MaxValue;

        // ------------------------------------------------------------------ state

        internal sealed class Row
        {
            public int index;
            public int priority;
        }

        internal sealed class MenuState
        {
            /// <summary>Position of the next Draw() call inside the panel that is being built.</summary>
            public int cursor;

            /// <summary>Every Draw() call of the current top level build pass, in call order.</summary>
            public readonly List<Row> rows = new List<Row>();

            public bool titleSeen;
            public string currentTitle;
            public string lastTitle;

            /// <summary>Which page is shown, and how many there are.</summary>
            public int page;
            public int pageCount = 1;

            /// <summary>One flag per row of the full panel: is this row on the page being built?</summary>
            public bool[] allowed;

            /// <summary>Rows outside the page being built are dropped.</summary>
            public bool filterActive;

            /// <summary>True while the page buttons are being queued, so that they are never filtered.</summary>
            public bool inPager;

            /// <summary>Set on the rebuild that this mod starts itself, so that it is not paged again.</summary>
            public bool suppress;

            /// <summary>Set for the rebuild that a page button asked for: the page must not be reset.</summary>
            public bool keepPageOnce;

            /// <summary>The arguments the panel was opened with, needed to build another page.</summary>
            public Func<bool> openHasControl;
            public Action<StatsMenu> openDraw;
            public bool openSkipAnimation;
            public Action openOnOpen;
            public Action openOnClose;
        }

        private static readonly ConditionalWeakTable<StatsMenu, MenuState> States =
            new ConditionalWeakTable<StatsMenu, MenuState>();

        private static readonly FieldInfo OnCloseField = typeof(StatsMenu).GetField(
            "onClose", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static MenuState State(StatsMenu menu)
        {
            if (PmpLib.IsNull(menu))
                return null;

            MenuState state;
            if (!States.TryGetValue(menu, out state))
            {
                state = new MenuState();
                States.Add(menu, state);
            }
            return state;
        }

        // ------------------------------------------------------------------ session

        /// <summary>Called before the game starts building a panel. The panel is always built by the
        /// game itself; this only resets the counters of the pass that is about to start.</summary>
        internal static void ResetPass(StatsMenu menu)
        {
            MenuState state = State(menu);
            if (state == null)
                return;

            state.cursor = 0;
            state.rows.Clear();
            state.titleSeen = false;
            state.currentTitle = null;
        }

        /// <summary>Called from the prefix of StatsMenu.Draw(int, Action, bool) - the one funnel every
        /// row of the panel goes through. Returning false drops the row.</summary>
        internal static bool OnDraw(StatsMenu menu, int priority)
        {
            MenuState state = State(menu);
            if (state == null)
                return true;

            int index = state.cursor;
            state.cursor = index + 1;

            // The page buttons are queued after the panel's own rows and must never be filtered out.
            if (state.inPager)
                return true;

            state.rows.Add(new Row { index = index, priority = priority });

            if (state.filterActive && state.allowed != null)
            {
                if (index >= state.allowed.Length || !state.allowed[index])
                    return false;
            }

            return true;
        }

        internal static void OnTitle(StatsMenu menu, string title)
        {
            MenuState state = State(menu);
            if (state == null)
                return;

            state.titleSeen = true;
            state.currentTitle = title;
        }

        /// <summary>Called once the game has finished building a panel.</summary>
        internal static void AfterOpen(StatsMenu menu, Func<bool> hasControl, Action<StatsMenu> draw,
                                       bool skipAnimation, Action onOpen, Action onClose,
                                       ref OpenTracker result)
        {
            MenuState state = State(menu);
            if (state == null)
                return;

            // The rebuild this mod starts itself ends here as well; it must not page itself again.
            if (state.suppress)
            {
                state.suppress = false;
                return;
            }

            try
            {
                int total = state.cursor;

                // Remember the arguments, so that a page button can build another page.
                state.openHasControl = hasControl;
                state.openDraw = draw;
                state.openSkipAnimation = skipAnimation;
                state.openOnOpen = onOpen;
                state.openOnClose = onClose;

                if (draw == null || !PmpConfig.Enabled)
                {
                    PmpLog.Detail("paging off, the panel is left as it is (" + total + " row(s))");
                    return;
                }

                int pageSize = PmpConfig.RowsPerPage;
                int pageCount = total <= 0 ? 1 : (total + pageSize - 1) / pageSize;

                if (pageCount <= 1)
                {
                    PmpLog.Detail("panel is short enough: " + total + " row(s), nothing paged");
                    return;
                }

                state.pageCount = pageCount;

                // A different part starts on page one. The same part keeps the page it was on, which
                // matters because the game rebuilds this panel whenever the part changes shape.
                bool samePart = state.currentTitle != null &&
                                string.Equals(state.currentTitle, state.lastTitle, StringComparison.Ordinal);

                if (!state.keepPageOnce && !samePart)
                    state.page = 0;

                state.lastTitle = state.currentTitle;
                state.keepPageOnce = false;

                if (state.page < 0)
                    state.page = 0;
                if (state.page >= pageCount)
                    state.page = pageCount - 1;

                // The panel is shown in the order the game sorts its entries in, not in the order they
                // were queued, so the page has to be taken from the sorted order.
                List<Row> sorted = new List<Row>(state.rows);
                sorted.Sort(CompareRows);

                bool[] allowed = new bool[total];
                int from = state.page * pageSize;
                int to = Math.Min(from + pageSize, total);
                for (int i = from; i < to; i++)
                {
                    Row row = sorted[i];
                    if (row.index >= 0 && row.index < total)
                        allowed[row.index] = true;
                }

                state.allowed = allowed;

                PmpLog.Detail("paging \"" + (state.currentTitle ?? "(untitled)") + "\": " + total +
                              " row(s), " + pageSize + " per page, " + pageCount + " page(s), showing " +
                              (state.page + 1));

                state.filterActive = true;
                state.suppress = true;

                OpenTracker inner = null;
                try
                {
                    inner = menu.Open(hasControl, Wrapped(state, draw), true, onOpen, onClose);
                }
                finally
                {
                    state.filterActive = false;
                    state.suppress = false;
                }

                KeepOuterTracker(menu, result, inner);
            }
            catch (Exception e)
            {
                // Whatever went wrong, the next build must not be filtered, or the panel would come
                // out empty.
                state.filterActive = false;
                state.suppress = false;
                PmpLog.Error("paging the part panel failed: " + e);
            }
        }

        /// <summary>
        /// The panel was rebuilt through a second call to Open(), which the caller never sees. The
        /// caller holds the OpenTracker of the first call, and the game asks that tracker whether the
        /// panel is open (SFS.Builds.BuildMenus.OpenAttachedPartsMenu and friends use it to decide
        /// between opening and closing). Rebuilding runs the game's Close() on the way, which marks
        /// every tracker as closed, so the caller's tracker is put back into the open state here, and
        /// it is given a callback that closes it again when the panel really does close.
        /// </summary>
        private static void KeepOuterTracker(StatsMenu menu, OpenTracker outer, OpenTracker inner)
        {
            try
            {
                if (outer == null)
                    return;

                outer.isOpen = inner == null || inner.isOpen;

                if (OnCloseField == null)
                    return;

                Action rearm = delegate
                {
                    try
                    {
                        outer.isOpen = false;
                    }
                    catch
                    {
                    }
                };

                Action current = OnCloseField.GetValue(menu) as Action;
                OnCloseField.SetValue(menu, (Action)Delegate.Combine(current, rearm));
            }
            catch (Exception e)
            {
                PmpLog.Warn("the panel's open state could not be restored: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ building a page

        private static Action<StatsMenu> Wrapped(MenuState state, Action<StatsMenu> draw)
        {
            return delegate (StatsMenu menu)
            {
                try
                {
                    draw(menu);
                }
                catch (Exception e)
                {
                    PmpLog.Error("drawing the part panel failed: " + e);
                }

                try
                {
                    AddPager(menu, state);
                }
                catch (Exception e)
                {
                    PmpLog.Error("adding the page buttons failed: " + e);
                }
            };
        }

        private static void AddPager(StatsMenu menu, MenuState state)
        {
            if (state.pageCount <= 1)
                return;

            string left;
            string right;
            Arrows(menu, out left, out right);

            int page = state.page;
            int pages = state.pageCount;

            state.inPager = true;
            try
            {
                if (state.titleSeen && menu.titleText != null)
                {
                    menu.titleText.Text = (state.currentTitle ?? "") + "   " + (page + 1) + " / " + pages;
                }
                else
                {
                    menu.DrawText(PagerPriority, "Page " + (page + 1) + " / " + pages);
                }

                menu.DrawFullButton(PagerPriority - 1, left + "   Previous page",
                                    delegate { ChangePage(menu, -1); }, page > 0);

                menu.DrawFullButton(PagerPriority - 2, "Next page   " + right,
                                    delegate { ChangePage(menu, +1); }, page < pages - 1);
            }
            finally
            {
                state.inPager = false;
            }
        }

        /// <summary>Rebuilds the panel on another page. The panel is opened exactly the way the game
        /// opened it, so the caller's arrow, position and close handling keep working.</summary>
        private static void ChangePage(StatsMenu menu, int delta)
        {
            MenuState state = State(menu);
            if (state == null || state.openDraw == null)
                return;

            int target = state.page + delta;
            if (target < 0 || target >= state.pageCount)
                return;

            state.page = target;
            state.keepPageOnce = true;

            try
            {
                PmpLog.Detail("showing page " + (target + 1) + " of " + state.pageCount);
                menu.Open(state.openHasControl, state.openDraw, state.openSkipAnimation,
                          state.openOnOpen, state.openOnClose);
            }
            catch (Exception e)
            {
                PmpLog.Error("changing the page failed: " + e);
            }
            finally
            {
                state.keepPageOnce = false;
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The game's own ordering of the panel's entries (StatsMenu.&lt;Open&gt;b__23_0):
        /// higher priority first, and among equal priorities the order they were queued in.</summary>
        private static int CompareRows(Row a, Row b)
        {
            if (a.priority != b.priority)
                return b.priority.CompareTo(a.priority);
            return a.index.CompareTo(b.index);
        }

        private static string arrowLeft;
        private static string arrowRight;

        /// <summary>
        /// The arrow characters, but only when the font the buttons use actually has them. The game's
        /// UI font is not guaranteed to carry "BLACK LEFT-POINTING TRIANGLE" (it carries no arrow
        /// characters in its own strings), and a missing glyph would leave an empty button behind, so
        /// the font is asked first and "&lt;" / "&gt;" are used when the answer is no.
        /// </summary>
        private static void Arrows(StatsMenu menu, out string left, out string right)
        {
            if (arrowLeft == null)
            {
                // Tried in this order; the first pair the font really has wins. The solid triangles are
                // the nicest, but they live in "Geometric Shapes", which most text fonts do not carry -
                // which is why the long arrows and then the guillemets are on the list as well.
                char[][] candidates =
                {
                    new[] { '\u25C0', '\u25B6' },   // left / right pointing solid triangle
                    new[] { '\u2190', '\u2192' },   // leftwards / rightwards arrow
                    new[] { '\u00AB', '\u00BB' },   // left / right guillemet
                };

                string l = "<";
                string r = ">";
                string why;

                if (PmpConfig.AsciiArrows)
                {
                    why = "the setting asks for plain characters";
                }
                else
                {
                    why = "no text component found on the button prefab";
                    try
                    {
                        RectTransform prefab = menu.fullButtonPrefab != null
                            ? menu.fullButtonPrefab
                            : menu.buttonPrefab;

                        if (prefab != null)
                        {
                            TMP_Text text = prefab.GetComponentInChildren<TMP_Text>(true);
                            if (text != null && text.font != null)
                            {
                                TMP_FontAsset font = text.font;
                                for (int i = 0; i < candidates.Length; i++)
                                {
                                    if (font.HasCharacter(candidates[i][0], true, true) &&
                                        font.HasCharacter(candidates[i][1], true, true))
                                    {
                                        l = candidates[i][0].ToString();
                                        r = candidates[i][1].ToString();
                                        why = "the font \"" + font.name + "\" has U+" +
                                              ((int)candidates[i][0]).ToString("X4") + "/U+" +
                                              ((int)candidates[i][1]).ToString("X4");
                                        break;
                                    }
                                }

                                if (l == "<")
                                    why = "the font \"" + font.name + "\" has none of the arrow characters";
                            }
                            else
                            {
                                UnityEngine.UI.Text legacy = prefab.GetComponentInChildren<UnityEngine.UI.Text>(true);
                                if (legacy != null && legacy.font != null)
                                {
                                    for (int i = 0; i < candidates.Length; i++)
                                    {
                                        if (legacy.font.HasCharacter(candidates[i][0]) &&
                                            legacy.font.HasCharacter(candidates[i][1]))
                                        {
                                            l = candidates[i][0].ToString();
                                            r = candidates[i][1].ToString();
                                            why = "the font \"" + legacy.font.name + "\" has U+" +
                                                  ((int)candidates[i][0]).ToString("X4") + "/U+" +
                                                  ((int)candidates[i][1]).ToString("X4");
                                            break;
                                        }
                                    }

                                    if (l == "<")
                                        why = "the font \"" + legacy.font.name +
                                              "\" has none of the arrow characters";
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        why = "the font could not be checked: " + e.Message;
                    }
                }

                arrowLeft = l;
                arrowRight = r;
                PmpLog.Detail("page buttons use \"" + arrowLeft + "\" and \"" + arrowRight + "\" (" + why + ")");
            }

            left = arrowLeft;
            right = arrowRight;
        }
    }

    // ---------------------------------------------------------------------- patches

    [HarmonyPatch(typeof(StatsMenu), "Open")]
    internal static class Patch_StatsMenu_Open
    {
        private static void Prefix(StatsMenu __instance)
        {
            try
            {
                PmpPager.ResetPass(__instance);
            }
            catch (Exception e)
            {
                PmpLog.Error("resetting the panel pass failed: " + e);
            }
        }

        private static void Postfix(StatsMenu __instance, Func<bool> hasControl, Action<StatsMenu> draw,
                                    bool skipAnimation, Action onOpen, Action onClose,
                                    ref OpenTracker __result)
        {
            try
            {
                PmpPager.AfterOpen(__instance, hasControl, draw, skipAnimation, onOpen, onClose, ref __result);
            }
            catch (Exception e)
            {
                PmpLog.Error("the panel postfix failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(StatsMenu), "Draw", new Type[] { typeof(int), typeof(Action), typeof(bool) })]
    internal static class Patch_StatsMenu_Draw
    {
        private static bool Prefix(StatsMenu __instance, int priority)
        {
            try
            {
                return PmpPager.OnDraw(__instance, priority);
            }
            catch (Exception e)
            {
                PmpLog.Error("filtering a panel row failed: " + e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(StatsMenu), "DrawTitle")]
    internal static class Patch_StatsMenu_DrawTitle
    {
        private static void Postfix(StatsMenu __instance, string title)
        {
            try
            {
                PmpPager.OnTitle(__instance, title);
            }
            catch
            {
            }
        }
    }
}
