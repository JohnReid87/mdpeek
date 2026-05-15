namespace EzMarkdownViewer.App;

/// <summary>
/// In-memory back/forward navigation history for viewed markdown files.
/// Session-only — not persisted between launches. Implements the standard
/// browser branching model: visiting a new entry after going back discards
/// the forward stack.
/// </summary>
public sealed class NavigationHistory
{
    private readonly Stack<string> _back = new();
    private readonly Stack<string> _forward = new();

    /// <summary>The path of the file currently being viewed, or <c>null</c> if none.</summary>
    public string? Current { get; private set; }

    public bool CanGoBack => _back.Count > 0;

    public bool CanGoForward => _forward.Count > 0;

    /// <summary>
    /// Records a new visit. Pushes the previous <see cref="Current"/> onto the
    /// back stack and clears the forward stack. No-op if <paramref name="path"/>
    /// equals the current entry, so repeated selection of the same file does
    /// not pollute history.
    /// </summary>
    public void Visit(string path)
    {
        if (string.Equals(path, Current, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Current is not null)
        {
            _back.Push(Current);
        }

        Current = path;
        _forward.Clear();
    }

    /// <summary>
    /// Moves one step back. Returns the new current path, or <c>null</c> when
    /// the back stack is empty.
    /// </summary>
    public string? Back()
    {
        if (_back.Count == 0)
        {
            return null;
        }

        if (Current is not null)
        {
            _forward.Push(Current);
        }

        Current = _back.Pop();
        return Current;
    }

    /// <summary>
    /// Moves one step forward. Returns the new current path, or <c>null</c>
    /// when the forward stack is empty.
    /// </summary>
    public string? Forward()
    {
        if (_forward.Count == 0)
        {
            return null;
        }

        if (Current is not null)
        {
            _back.Push(Current);
        }

        Current = _forward.Pop();
        return Current;
    }

    /// <summary>Discards all history. Used when the open folder changes.</summary>
    public void Clear()
    {
        _back.Clear();
        _forward.Clear();
        Current = null;
    }
}
