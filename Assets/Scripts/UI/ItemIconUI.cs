using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class ItemIconUI : MonoBehaviour
{
    private Button _button;
    private Image _image;
    private WordData _currentWord;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _image = GetComponent<Image>();

        _button.onClick.AddListener(OnClick);
    }

    public void SetWord(WordData word)
    {
        _currentWord = word;

        if (_image == null) _image = GetComponent<Image>();

        if (word != null && word.wordIcon != null)
        {
            _image.sprite = word.wordIcon;
            _image.color = Color.white;
            _image.enabled = true;
            // _button.interactable = true; // Always interactable?
        }
        else
        {
            _image.enabled = false; // Hide if no data
            _currentWord = null;
        }
    }

    private void OnClick()
    {
        if (_currentWord != null)
        {
            Debug.Log($"Icon Clicked: {_currentWord.wordName}");
            if (RecipeUI.Instance != null)
            {
                RecipeUI.Instance.ShowRecipe(_currentWord);
            }
        }
    }
}
