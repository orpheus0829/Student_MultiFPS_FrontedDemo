public readonly struct InputRebindResult
{
    public bool IsSuccessful { get; }
    public string Path { get; }
    public bool HasConflict { get; }
    public InputBindingConflict Conflict { get; }

    private InputRebindResult(bool isSuccessful, string path, bool hasConflict, InputBindingConflict conflict)
    {
        IsSuccessful = isSuccessful;
        Path = path;
        HasConflict = hasConflict;
        Conflict = conflict;
    }

    public static InputRebindResult Success(string path)
    {
        return new InputRebindResult(true, path, false, default);
    }

    public static InputRebindResult Rejected(string path, InputBindingConflict conflict)
    {
        return new InputRebindResult(false, path, true, conflict);
    }
}
