namespace FolderIconManager.GUI.Services;

/// <summary>
/// Simple undo service that tracks icon operations
/// </summary>
public class UndoService
{
    private readonly Stack<UndoOperation> _undoStack = new();
    private readonly int _maxUndoCount;

    public UndoService(int maxUndoCount = 20)
    {
        _maxUndoCount = maxUndoCount;
    }

    public event Action? UndoStackChanged;

    public bool CanUndo => _undoStack.Count > 0;
    
    public int UndoCount => _undoStack.Count;

    public string? NextUndoDescription => _undoStack.TryPeek(out var op) ? op.Description : null;

    public void RecordOperation(UndoOperation operation)
    {
        _undoStack.Push(operation);
        
        // Trim stack if too large
        if (_undoStack.Count > _maxUndoCount)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            foreach (var item in items.Take(_maxUndoCount).Reverse())
            {
                _undoStack.Push(item);
            }
        }
        
        UndoStackChanged?.Invoke();
    }

    public UndoOperation? Pop()
    {
        if (_undoStack.TryPop(out var operation))
        {
            UndoStackChanged?.Invoke();
            return operation;
        }
        return null;
    }

    public void Clear()
    {
        _undoStack.Clear();
        UndoStackChanged?.Invoke();
    }
}

/// <summary>
/// Represents an undoable operation
/// </summary>
public class UndoOperation
{
    public required string FolderPath { get; init; }
    public required UndoOperationType Type { get; init; }
    public required string Description { get; init; }
    
    // Data needed to undo
    public string? OriginalIconPath { get; init; }
    public int OriginalIconIndex { get; init; }
    public string? OriginalRawValue { get; init; }
    public bool HadBackupManifest { get; init; }
    public string? BackupManifestJson { get; init; }
    public byte[]? LocalIconData { get; init; }
}

public enum UndoOperationType
{
    SetIcon,
    RemoveIcon,
    MakeLocal,
    Restore
}

