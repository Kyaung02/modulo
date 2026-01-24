using UnityEngine;

[CreateAssetMenu(fileName = "NewWord", menuName = "Modulo/Word Data")]
public class WordData : ScriptableObject
{
    public string id; // Unique identifier for lookup and save/load
    public string wordName;
    public Sprite wordIcon;
    public Color wordColor = Color.white; // Default color for visualization
}