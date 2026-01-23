using UnityEngine;

[CreateAssetMenu(fileName = "NewWord", menuName = "Modulo/Word Data")]
public class WordData : ScriptableObject
{
    public string wordName;
    public Sprite wordIcon;
}