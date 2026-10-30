public interface IInputBindingStorage
{
    bool TryLoad(out string json);
    void Save(string json);
    void Delete();
}
