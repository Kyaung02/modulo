using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds a menu item "Tools/Play from Title Scene" to toggle starting the game from TitleScene regardless of current open scene.
/// </summary>
[InitializeOnLoad]
public class PlayFromTitleScene
{
    private const string MENU_NAME = "Tools/Play from Title Scene";
    private const string PREF_KEY = "PlayFromTitleScene_Enabled";
    private const string TITLE_SCENE_PATH = "Assets/Scenes/TitleScene.unity";

    static PlayFromTitleScene()
    {
        // Delay call ensures Editor is fully initialized before accessing preferences
        EditorApplication.delayCall += () =>
        {
            bool isEnabled = EditorPrefs.GetBool(PREF_KEY, false);
            Menu.SetChecked(MENU_NAME, isEnabled);
            SetPlayModeStartScene(isEnabled);
        };
    }

    [MenuItem(MENU_NAME)]
    private static void ToggleAction()
    {
        bool isEnabled = !Menu.GetChecked(MENU_NAME);
        Menu.SetChecked(MENU_NAME, isEnabled);
        EditorPrefs.SetBool(PREF_KEY, isEnabled);
        SetPlayModeStartScene(isEnabled);
        
        Debug.Log($"[PlayFromTitleScene] {(isEnabled ? "Enabled" : "Disabled")}");
    }

    private static void SetPlayModeStartScene(bool enabled)
    {
        if (enabled)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TITLE_SCENE_PATH);
            if (sceneAsset != null)
            {
                EditorSceneManager.playModeStartScene = sceneAsset;
            }
            else
            {
                Debug.LogError($"[PlayFromTitleScene] Could not find scene at path: {TITLE_SCENE_PATH}");
                // Disable if scene not found to prevent errors
                EditorSceneManager.playModeStartScene = null;
                Menu.SetChecked(MENU_NAME, false);
                EditorPrefs.SetBool(PREF_KEY, false);
            }
        }
        else
        {
            EditorSceneManager.playModeStartScene = null;
        }
    }
}
