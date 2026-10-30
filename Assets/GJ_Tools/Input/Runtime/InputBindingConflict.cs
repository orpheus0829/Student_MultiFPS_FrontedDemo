public readonly struct InputBindingConflict
{
    public GJInputAction Action { get; }
    public int BindingIndex { get; }
    public string DisplayName { get; }

    public InputBindingConflict(GJInputAction action, int bindingIndex, string displayName)
    {
        Action = action;
        BindingIndex = bindingIndex;
        DisplayName = displayName;
    }
}
