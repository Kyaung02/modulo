using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class ItemIconUI : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text nameText; // Optional label
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

        if (word != null)
        {
            if (word.wordIcon != null)
            {
                _image.sprite = word.wordIcon;
                _image.color = Color.white;
                _image.enabled = true;
            }
            // else keep image state? Or hide? Standard is icon usually needed.
            
            if (nameText != null) nameText.text = word.wordName;
        }
        else
        {
            _image.enabled = false;
            _currentWord = null;
            if (nameText != null) nameText.text = "";
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
