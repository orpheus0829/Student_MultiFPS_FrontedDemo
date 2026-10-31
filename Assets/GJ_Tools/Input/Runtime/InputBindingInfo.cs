public readonly struct InputBindingInfo
{
    public GJInputAction Action { get; }
    public int BindingIndex { get; }
    public string Name { get; }
    public string Path { get; }
    public string DisplayName { get; }
    public bool IsComposite { get; }
    public bool IsPartOfComposite { get; }
    public bool CanRebind
    {
        get
        {
            return !IsComposite;
        }
    }

    public InputBindingInfo(
        GJInputAction action,
        int bindingIndex,
        string name,
        string path,
        string displayName,
        bool isComposite,
        bool isPartOfComposite)
    {
        Action = action;
        BindingIndex = bindingIndex;
        Name = name;
        Path = path;
        DisplayName = displayName;
        IsComposite = isComposite;
        IsPartOfComposite = isPartOfComposite;
    }
}
