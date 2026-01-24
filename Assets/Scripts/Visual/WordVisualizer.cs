using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class WordVisualizer : MonoBehaviour
{
    private SpriteRenderer _renderer;
    
    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _renderer.enabled = false; // Hide by default
        _renderer.sortingOrder = 10; // Ensure it renders on top of modules
    }

    public void UpdateVisual(WordData word)
    {
        if (word == null)
        {
            _renderer.enabled = false;
        }
        else
        {
            _renderer.enabled = true;
            _renderer.sprite = word.wordIcon;
            _renderer.color = word.wordColor;
            
            // Optional: Scale down slightly to fit inside the module
            transform.localScale = Vector3.one * 0.6f;
        }
    }
}
