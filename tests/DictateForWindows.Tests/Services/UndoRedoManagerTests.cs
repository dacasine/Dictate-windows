using DictateForWindows.Core.Services.TextInjection;
using Xunit;

namespace DictateForWindows.Tests.Services;

public class UndoRedoManagerTests
{
    [Fact]
    public void RecordOperation_AddsEntryToStack()
    {
        var manager = new UndoRedoManager();

        manager.RecordOperation("Hello");

        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void Undo_ReturnsLastEntry()
    {
        var manager = new UndoRedoManager();
        manager.RecordOperation("First");
        manager.RecordOperation("Second");
        manager.RecordOperation("Third");

        var result = manager.Undo();

        Assert.NotNull(result);
        Assert.Equal("Third", result.InsertedText);
        Assert.True(manager.CanUndo);
        Assert.True(manager.CanRedo);
    }

    [Fact]
    public void Undo_ReturnsNullWhenEmpty()
    {
        var manager = new UndoRedoManager();

        var result = manager.Undo();

        Assert.Null(result);
        Assert.False(manager.CanUndo);
    }

    [Fact]
    public void Redo_ReturnsUndoneEntry()
    {
        var manager = new UndoRedoManager();
        manager.RecordOperation("First");
        manager.RecordOperation("Second");
        manager.Undo();

        var result = manager.Redo();

        Assert.NotNull(result);
        Assert.Equal("Second", result.InsertedText);
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void Redo_ReturnsNullWhenEmpty()
    {
        var manager = new UndoRedoManager();
        manager.RecordOperation("First");

        var result = manager.Redo();

        Assert.Null(result);
    }

    [Fact]
    public void RecordOperation_ClearsRedoStack()
    {
        var manager = new UndoRedoManager();
        manager.RecordOperation("First");
        manager.RecordOperation("Second");
        manager.Undo();

        Assert.True(manager.CanRedo);

        manager.RecordOperation("Third");

        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var manager = new UndoRedoManager();
        manager.RecordOperation("First");
        manager.RecordOperation("Second");
        manager.Undo();

        manager.Clear();

        Assert.False(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void RecordOperation_RespectsMaxCapacity()
    {
        var manager = new UndoRedoManager(maxHistory: 3);

        manager.RecordOperation("1");
        manager.RecordOperation("2");
        manager.RecordOperation("3");
        manager.RecordOperation("4"); // Should remove "1"

        Assert.Equal("4", manager.Undo()?.InsertedText);
        Assert.Equal("3", manager.Undo()?.InsertedText);
        Assert.Equal("2", manager.Undo()?.InsertedText);
        Assert.Null(manager.Undo()); // "1" was removed
    }

    [Fact]
    public void MultipleUndoRedo_WorksCorrectly()
    {
        var manager = new UndoRedoManager();
        manager.RecordOperation("A");
        manager.RecordOperation("B");
        manager.RecordOperation("C");

        // Undo all
        Assert.Equal("C", manager.Undo()?.InsertedText);
        Assert.Equal("B", manager.Undo()?.InsertedText);
        Assert.Equal("A", manager.Undo()?.InsertedText);
        Assert.False(manager.CanUndo);

        // Redo all
        Assert.Equal("A", manager.Redo()?.InsertedText);
        Assert.Equal("B", manager.Redo()?.InsertedText);
        Assert.Equal("C", manager.Redo()?.InsertedText);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void Count_ReturnsCorrectNumber()
    {
        var manager = new UndoRedoManager();

        Assert.Equal(0, manager.UndoCount);

        manager.RecordOperation("1");
        Assert.Equal(1, manager.UndoCount);

        manager.RecordOperation("2");
        Assert.Equal(2, manager.UndoCount);

        manager.Undo();
        Assert.Equal(1, manager.UndoCount);
        Assert.Equal(1, manager.RedoCount);
    }

    [Fact]
    public void PeekUndo_ReturnsLastWithoutRemoving()
    {
        var manager = new UndoRedoManager();
        manager.RecordOperation("First");
        manager.RecordOperation("Second");

        var peeked = manager.PeekUndo();
        var undone = manager.Undo();

        Assert.Equal(peeked?.InsertedText, undone?.InsertedText);
        Assert.Equal("Second", peeked?.InsertedText);
    }

    [Fact]
    public void RecordOperation_TracksReplacedText()
    {
        var manager = new UndoRedoManager();
        manager.RecordOperation("New text", "Old text");

        var operation = manager.Undo();

        Assert.NotNull(operation);
        Assert.Equal("New text", operation.InsertedText);
        Assert.Equal("Old text", operation.ReplacedText);
    }

    [Fact]
    public void TextOperation_CalculatesLengths()
    {
        var operation = new TextOperation
        {
            InsertedText = "Hello",
            ReplacedText = "Hi"
        };

        Assert.Equal(5, operation.InsertedLength);
        Assert.Equal(2, operation.ReplacedLength);
    }

    [Fact]
    public void TextOperation_ReplacedLengthIsZeroWhenNull()
    {
        var operation = new TextOperation
        {
            InsertedText = "Hello",
            ReplacedText = null
        };

        Assert.Equal(0, operation.ReplacedLength);
    }
}
