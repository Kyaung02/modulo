using UnityEngine;

[CreateAssetMenu(fileName = "NewWord", menuName = "Modulo/Word Data")]
public class WordData : ScriptableObject
{
    public string id; // Unique identifier
    public string wordName;
    public string emoji; // Emoji representation
    public Color wordColor = Color.white;
    
    public Sprite wordIcon; 
}