using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public sealed class ResourceIdAttribute : PropertyAttribute
{
    public Type ResourceType { get; }

    public ResourceIdAttribute(Type resourceType)
    {
        ResourceType = resourceType;
    }
}
