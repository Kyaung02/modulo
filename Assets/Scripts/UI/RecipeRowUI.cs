using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecipeRowUI : MonoBehaviour
{
    [Header("UI Elements")]
    public ItemIconUI iconInputA;
    public ItemIconUI iconInputB;
    public ItemIconUI iconOutput;
    
    public TMP_Text textInputA; // Optional: Display Name?
    public TMP_Text textInputB;
    public TMP_Text textOutput;

    public void SetRecipe(WordData inA, WordData inB, WordData outResult)
    {
        if (iconInputA != null) iconInputA.SetWord(inA);
        if (iconInputB != null) iconInputB.SetWord(inB);
        if (iconOutput != null) iconOutput.SetWord(outResult);

        // Optional: Text names
        if (textInputA != null) textInputA.text = inA != null ? inA.wordName : "?";
        if (textInputB != null) textInputB.text = inB != null ? inB.wordName : "?";
        if (textOutput != null) textOutput.text = outResult != null ? outResult.wordName : "?";
    }

    [Header("Visuals")]
    public Image backgroundImage;
    public Color highlightColor = new Color(1f, 1f, 0f, 0.5f); // Yellow transparent
    private Color _originalColor;
    private Coroutine _highlightCoroutine;

    private void Awake()
    {
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (backgroundImage != null) _originalColor = backgroundImage.color;
    }

    public void Highlight()
    {
        if (backgroundImage == null) return;
        
        if (_highlightCoroutine != null) StopCoroutine(_highlightCoroutine);
        _highlightCoroutine = StartCoroutine(HighlightRoutine());
    }

    private System.Collections.IEnumerator HighlightRoutine()
    {
        backgroundImage.color = highlightColor;
        
        float duration = 1.0f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Fade back to original
            backgroundImage.color = Color.Lerp(highlightColor, _originalColor, t);
            yield return null;
        }
        
        backgroundImage.color = _originalColor;
        _highlightCoroutine = null;
    }
}

