using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public enum GameAction
{
    Rotate,
    ExitModule,
    Interact,
    Flip,
    Copy,
    Paste,
    CancelBuild,
    ToggleMilestone,
    ToggleRecipe,
    ToggleMenu,
    QuickSlot1,
    QuickSlot2,
    QuickSlot3,
    QuickSlot4,
    QuickSlot5,
    QuickSlot6,
    QuickSlot7,
    QuickSlot8,
    QuickSlot9,
    QuickSlot0
}

public class KeybindingManager : MonoBehaviour
{
    private static KeybindingManager _instance;
    public static KeybindingManager Instance 
    {
        get 
        {
            if (_instance == null) 
            {
                _instance = FindFirstObjectByType<KeybindingManager>();
                if (_instance == null) 
                {
                    GameObject go = new GameObject("KeybindingManager");
                    _instance = go.AddComponent<KeybindingManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private Dictionary<GameAction, Key> bindings;

    private void Awake() 
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        LoadBindings();
    }

    private void LoadBindings()
    {
        // Default Bindings
        // TODO: Load from PlayerPrefs or JSON
        bindings = new Dictionary<GameAction, Key>
        {
            { GameAction.Rotate, Key.R },
            { GameAction.ExitModule, Key.Q },
            { GameAction.Interact, Key.E },
            { GameAction.Flip, Key.T },
            { GameAction.Copy, Key.C },
            { GameAction.Paste, Key.V },
            { GameAction.CancelBuild, Key.X },
            { GameAction.ToggleMilestone, Key.F },
            { GameAction.ToggleRecipe, Key.G },
            { GameAction.ToggleMenu, Key.Escape },
            { GameAction.QuickSlot1, Key.Digit1 },
            { GameAction.QuickSlot2, Key.Digit2 },
            { GameAction.QuickSlot3, Key.Digit3 },
            { GameAction.QuickSlot4, Key.Digit4 },
            { GameAction.QuickSlot5, Key.Digit5 },
            { GameAction.QuickSlot6, Key.Digit6 },
            { GameAction.QuickSlot7, Key.Digit7 },
            { GameAction.QuickSlot8, Key.Digit8 },
            { GameAction.QuickSlot9, Key.Digit9 },
            { GameAction.QuickSlot0, Key.Digit0 },
        };
    }
    
    public bool GetKeyDown(GameAction action)
    {
        if (bindings.TryGetValue(action, out Key key))
        {
             return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }
        return false;
    }
    
    public bool GetKey(GameAction action)
    {
        if (bindings.TryGetValue(action, out Key key))
        {
             return Keyboard.current != null && Keyboard.current[key].isPressed;
        }
        return false;
    }
    
    public Key GetBinding(GameAction action)
    {
        return bindings.ContainsKey(action) ? bindings[action] : Key.None;
    }
    
    public void Rebind(GameAction action, Key newKey)
    {
        if (bindings.ContainsKey(action)) bindings[action] = newKey;
        else bindings.Add(action, newKey);
        // SaveBindings();
    }
}
