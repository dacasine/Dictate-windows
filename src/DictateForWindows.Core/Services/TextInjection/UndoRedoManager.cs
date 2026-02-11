namespace DictateForWindows.Core.Services.TextInjection;

/// <summary>
/// Manages undo/redo history for text injections.
/// Since we can't hook into application undo stacks, we maintain our own history.
/// </summary>
public class UndoRedoManager : IUndoRedoManager
{
    private readonly Stack<TextOperation> _undoStack = new();
    private readonly Stack<TextOperation> _redoStack = new();
    private readonly int _maxHistory;

    public UndoRedoManager(int maxHistory = 50)
    {
        _maxHistory = maxHistory;
    }

    /// <summary>
    /// Whether there are operations to undo.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Whether there are operations to redo.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Number of operations in undo history.
    /// </summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>
    /// Number of operations in redo history.
    /// </summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>
    /// Record a new text operation.
    /// </summary>
    /// <param name="text">The text that was inserted.</param>
    /// <param name="previousText">The text that was replaced (if any).</param>
    public void RecordOperation(string text, string? previousText = null)
    {
        var operation = new TextOperation
        {
            InsertedText = text,
            ReplacedText = previousText,
            Timestamp = DateTime.UtcNow
        };

        _undoStack.Push(operation);
        _redoStack.Clear(); // New operation clears redo stack

        // Trim history if needed — keep the most recent items
        if (_undoStack.Count > _maxHistory)
        {
            var items = _undoStack.ToArray(); // top (newest) to bottom (oldest)
            _undoStack.Clear();
            for (int i = _maxHistory - 1; i >= 0; i--)
            {
                _undoStack.Push(items[i]);
            }
        }
    }

    /// <summary>
    /// Get the last operation for undo.
    /// </summary>
    public TextOperation? PeekUndo()
    {
        return _undoStack.Count > 0 ? _undoStack.Peek() : null;
    }

    /// <summary>
    /// Perform undo operation.
    /// </summary>
    /// <returns>The operation to undo, or null if no operations available.</returns>
    public TextOperation? Undo()
    {
        if (_undoStack.Count == 0)
        {
            return null;
        }

        var operation = _undoStack.Pop();
        _redoStack.Push(operation);
        return operation;
    }

    /// <summary>
    /// Perform redo operation.
    /// </summary>
    /// <returns>The operation to redo, or null if no operations available.</returns>
    public TextOperation? Redo()
    {
        if (_redoStack.Count == 0)
        {
            return null;
        }

        var operation = _redoStack.Pop();
        _undoStack.Push(operation);
        return operation;
    }

    /// <summary>
    /// Clear all history.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}

/// <summary>
/// Represents a text operation that can be undone/redone.
/// </summary>
public class TextOperation
{
    /// <summary>
    /// The text that was inserted.
    /// </summary>
    public string InsertedText { get; set; } = string.Empty;

    /// <summary>
    /// The text that was replaced (if any).
    /// </summary>
    public string? ReplacedText { get; set; }

    /// <summary>
    /// When the operation occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Length of the inserted text.
    /// </summary>
    public int InsertedLength => InsertedText.Length;

    /// <summary>
    /// Length of the replaced text.
    /// </summary>
    public int ReplacedLength => ReplacedText?.Length ?? 0;
}

/// <summary>
/// Interface for undo/redo manager.
/// </summary>
public interface IUndoRedoManager
{
    bool CanUndo { get; }
    bool CanRedo { get; }
    int UndoCount { get; }
    int RedoCount { get; }

    void RecordOperation(string text, string? previousText = null);
    TextOperation? PeekUndo();
    TextOperation? Undo();
    TextOperation? Redo();
    void Clear();
}
