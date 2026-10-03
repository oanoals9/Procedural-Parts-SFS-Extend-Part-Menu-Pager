# Part Menu Pager

A small patch for **Spaceflight Simulator 1.6** that **splits a long part properties panel into pages**,
with Previous / Next buttons at the top.

## The problem it solves

The panel the game pops up next to a selected part (the one listing Mass, Angle, X Size, Liquid fuel, …)
is a plain vertical list: it has no scrolling and no clipping, so it just grows downwards. Stock parts
have two or three rows, which is why nobody ever noticed. The procedural parts of a parts pack such as
`Procedural Bundle` expose twenty or thirty editable values at once, so the panel ends up taller than the
screen and its lower half cannot be reached at all.

When the panel gets long, this mod splits it into pages and puts two buttons at the top:

```
┌──────────────────────────────┐
│ Procedural Fuel Tank   1 / 3 │   <- the page number follows the panel's own title
│ ◀   Previous page            │   <- greyed out on page 1
│ Next page   ▶                │   <- greyed out on the last page
│ Mass                     5t  │
│ Density                 1.2  │
│ …                            │
└──────────────────────────────┘
```

A panel that fits on one page is **left completely alone**: what you see is the panel the game built,
untouched.

## Why not a scroll bar

The panel is laid out with the game's own `SFS.UI.NewElement` system, there is no reusable scrolling
container in it, and UITools ships no icon assets that could be reused. So the mod takes the robust route
instead: on a page change it lets the game **rebuild the panel with only that page's rows in it**. The
group backgrounds, the panel's own height and the arrow that points at the part are therefore all computed
by the game itself; the mod never has to move or resize a single row.

## Installing

1. Requires **[UITools](https://github.com/cucumber-sp/UITools) 1.1.6 or later**.
2. Put `Part Menu Pager.dll` at `<game>\Mods\Part Menu Pager\Part Menu Pager.dll` (the folder and the
   file must share their name - that is how the game's loader finds a mod).
3. Start the game and press `F1` for the console; you should see one line, `[SFSPMP] loaded (v0.1.1)`.

## Settings

Under **Mods Settings → Part Menu Pager**:

| Setting | Meaning |
| --- | --- |
| **Paginate long part menus** | The master switch. Off gives you the game's unmodified panel. |
| **Rows per page** | How many rows go on a page, 4-40, default 14. The panel's rows are not all the same height (one with a slider is roughly twice as tall as a plain value row), so this counts rows, not pixels. |
| **Plain < > instead of arrow buttons** | Use `<` and `>` instead of an arrow character. Off by default: the mod asks the font the game's buttons use for `◀ ▶`, then `← →`, then `« »`, and falls back to `<` and `>` when it has none of them (see "About the arrow icons"). |
| **Debug output** | Prints the details of every pagination (total rows, rows per page, current page) to the console. |

Settings live in `<game>\Mods\Part Menu Pager\settings.txt`.

## About the arrow icons

- **UITools ships no icon assets at all** (every type in it was checked; there is nothing like an
  "icons" holder).
- The game's own arrow icons exist only as **sprites inside prefabs**, and only as *pairs* - the
  "◀▶" and "▲▼" symmetry icons. There is no standalone left or right arrow, and none of them is
  reachable from code.
- The game bundles no font file (it uses a system font), so the "Geometric Shapes" triangles are
  very likely missing. At run time the mod asks the font - fallbacks included - for `◀ ▶`, then
  `← →`, then `« »`, and uses `<` and `>` only if it has none of them. With Debug output on, the
  console says which one was chosen and why.

## Known limitations

- The buttons only appear when the panel is longer than one page; short panels are untouched.
- "The same part" is decided by the panel's title (the part's display name), so two identically named
  parts share their page position.
- The panel's title is rewritten to `Title   2 / 3`. A panel that has no title of its own (the shop
  panels, for example) gets a `Page 2 / 3` row at the top instead.
- Changing the page makes the game rebuild the panel (deliberately), so the pop-up animation does not
  replay.

## Licence

**GNU General Public License v3.0** (GPL-3.0) - the full text is in [LICENSE](LICENSE).

```
Part Menu Pager - a paged part properties panel for Spaceflight Simulator
Copyright (C) 2026 oanoals9

This program is free software: you can redistribute it and/or modify it under the terms of the
GNU General Public License as published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without
even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
General Public License for more details.

You should have received a copy of the GNU General Public License along with this program.
If not, see <https://www.gnu.org/licenses/>.
```

> Because the licence is the GPL, **the source goes out with the binary**: besides `-source.zip`,
> the complete source ships with the repository, and it rebuilds a byte-for-byte identical
> `Part Menu Pager.dll` with the build script that is included (no .NET SDK needed).
> **What is needed to build it, and which assemblies it references: see
> [BUILDING.md](BUILDING.md) in the source package.**

## Version history

| | |
| --- | --- |
| **0.1.1** | First release: Previous / Next buttons appear when the panel is longer than one page, with the page number after the panel's title; short panels are left completely alone; the page size, the arrow style and a master switch live in Mods Settings; the arrow character is picked from `◀ ▶` → `← →` → `« »` by asking the font, with `<` and `>` as the last resort |
| 0.1.0 | Test build: paging, page turning, page number, short panels untouched, master switch |

## Author

oanoals9
