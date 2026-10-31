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

    public string Name
    {
        get
        {
            return _name;
        }
    }

    public string Path
    {
        get
        {
            return _path;
        }
    }

    public string Interactions
    {
        get
        {
            return _interactions;
        }
    }

    public string Processors
    {
        get
        {
            return _processors;
        }
    }

    public string Groups
    {
        get
        {
            return _groups;
        }
    }

    public bool IsComposite
    {
        get
        {
            return _isComposite;
        }
    }

    public bool IsPartOfComposite
    {
        get
        {
            return _isPartOfComposite;
        }
    }

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
