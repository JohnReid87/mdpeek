# ez-markdown-viewer

A lightweight Windows desktop app for reading local markdown files. Point it at a folder, browse the directory tree, and view rendered `.md` files side-by-side — no server, no setup, no editor required.

## Getting started

_TODO: instructions once the project is scaffolded._

## Navigating

### Back / Forward history

Each markdown file you open is recorded in a per-session history. Use the **←** and **→** buttons on the toolbar to step through previously viewed files. Moving back or forward does not record a new entry, so the history stays linear; opening a different file after going back trims the forward stack.

- **Alt+Left** — Back
- **Alt+Right** — Forward
- **Mouse XButton1 / XButton2** — Back / Forward (standard 5-button mouse)

History is cleared when you open a different folder, but is preserved when you use **Go Up** or **Set as Root** within the same browsing session.

### Tree filter

The text box above the directory tree filters visible nodes by name. Matching is case-insensitive substring against each node's display name. A folder is shown if it matches or if any descendant matches, and is force-expanded to reveal those descendants. Clearing the filter restores the tree to the expansion state you had before filtering — folders that were force-expanded by the filter collapse back.

- **Ctrl+P** — focus the filter box and select its contents

### Go Up

Re-roots the tree at the parent of the current folder, so you can browse one level higher without reopening from the folder picker. The currently selected file stays selected and the tree's expanded folders are preserved, with the new root expanded so the previous root is visible underneath. Go Up does not record a history entry, since the file you are viewing has not changed.

- **Alt+Up** — Go Up
- Also available from the **File → Go Up** menu and the **↑** toolbar button

Go Up is disabled when the current root has no parent (e.g. a drive root such as `C:\`).
