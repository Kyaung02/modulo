using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class BlueprintData
{
    public string name;
    public string snapshotJson; // The serialized ModuleSnapshot
    public int prefabIndex;     // The root component type index
    public Sprite previewSprite; // The static captured image for UI
}
