using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "NewWordDatabase", menuName = "Modulo/Word Database")]
public class WordDatabase : ScriptableObject
{
    [Header("Word Data List")]
    [Tooltip("단어 데이터 리스트")]
    public List<WordData> words = new List<WordData>();

    /// <summary>
    /// 단어 이름으로 WordData를 찾습니다.
    /// </summary>
    /// <param name="wordName">찾을 단어 이름</param>
    /// <returns>찾은 WordData, 없으면 null</returns>
    public WordData GetWordByName(string wordName)
    {
        return words.FirstOrDefault(w => w.wordName == wordName);
    }

    /// <summary>
    /// 단어 이름으로 WordData를 찾습니다 (대소문자 무시).
    /// </summary>
    /// <param name="wordName">찾을 단어 이름</param>
    /// <returns>찾은 WordData, 없으면 null</returns>
    public WordData GetWordByNameIgnoreCase(string wordName)
    {
        return words.FirstOrDefault(w => w.wordName.Equals(wordName, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 인덱스로 WordData를 가져옵니다.
    /// </summary>
    /// <param name="index">인덱스</param>
    /// <returns>WordData, 범위를 벗어나면 null</returns>
    public WordData GetWordByIndex(int index)
    {
        if (index >= 0 && index < words.Count)
        {
            return words[index];
        }
        return null;
    }

    /// <summary>
    /// 데이터베이스에 단어가 있는지 확인합니다.
    /// </summary>
    /// <param name="wordName">확인할 단어 이름</param>
    /// <returns>존재하면 true</returns>
    public bool ContainsWord(string wordName)
    {
        return words.Any(w => w.wordName == wordName);
    }

    /// <summary>
    /// 데이터베이스에 단어가 있는지 확인합니다 (대소문자 무시).
    /// </summary>
    /// <param name="wordName">확인할 단어 이름</param>
    /// <returns>존재하면 true</returns>
    public bool ContainsWordIgnoreCase(string wordName)
    {
        return words.Any(w => w.wordName.Equals(wordName, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 단어 개수를 반환합니다.
    /// </summary>
    public int Count => words.Count;

    /// <summary>
    /// 모든 단어 이름을 반환합니다.
    /// </summary>
    public string[] GetAllWordNames()
    {
        return words.Select(w => w.wordName).ToArray();
    }

    /// <summary>
    /// 모든 WordData를 반환합니다.
    /// </summary>
    public List<WordData> GetAllWords()
    {
        return new List<WordData>(words);
    }
}
