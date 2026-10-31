public sealed class WordNoteHandle
{
    private readonly WordNoteUI _owner;
    private readonly int _id;

    internal WordNoteHandle(WordNoteUI owner, int id)
    {
        _owner = owner;
        _id = id;
    }

    public int Id
    {
        get
        {
            return _id;
        }
    }

    public bool IsValid
    {
        get
        {
            return _owner && _owner.Contains(_id);
        }
    }

    public bool Close()
    {
        return _owner && _owner.Close(_id);
    }
}
