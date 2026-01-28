using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ComponentSnapshot
{
    public int prefabIndex;
    public Vector2Int gridPos;
    public int rotationIndex;
    public int flipIndex;
    
    // For RecursiveModules: The serialized JSON of the inner world
    public string innerWorldJson;
}

[Serializable]
public class ModuleSnapshot
{
    public List<ComponentSnapshot> components = new List<ComponentSnapshot>();
}
