using System;
using UnityEngine;

[Serializable]
public class InputBindingData
{
    [SerializeField] private string _name;
    [SerializeField] private string _path;
    [SerializeField] private string _interactions;
    [SerializeField] private string _processors;
    [SerializeField] private string _groups;
    [SerializeField] private bool _isComposite;
    [SerializeField] private bool _isPartOfComposite;

    public string Name => _name;
    public string Path => _path;
    public string Interactions => _interactions;
    public string Processors => _processors;
    public string Groups => _groups;
    public bool IsComposite => _isComposite;
    public bool IsPartOfComposite => _isPartOfComposite;

    public InputBindingData(string name, string path, bool isComposite = false, bool isPartOfComposite = false)
    {
        _name = name;
        _path = path;
        _isComposite = isComposite;
        _isPartOfComposite = isPartOfComposite;
    }

    public void SetPath(string path)
    {
        _path = path;
    }
}
