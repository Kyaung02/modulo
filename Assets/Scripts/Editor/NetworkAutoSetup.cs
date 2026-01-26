using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using System.Collections.Generic;
using System.IO;

public class NetworkAutoSetup
{
    [MenuItem("Modulo/Setup Network Prefabs")]
    public static void SetupPrefabs()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/NetworkPrefabs");

        string prefabPath = "Assets/Resources/NetworkPrefabs/PortComponent.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            // Create temporary object
            GameObject go = new GameObject("PortComponent");
            go.AddComponent<PortComponent>();
            go.AddComponent<NetworkObject>();
            
            // Visuals (Quad)
            GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Quad);
            vis.transform.SetParent(go.transform);
            vis.transform.localScale = new Vector3(1.0f, 0.15f, 1.0f);
            vis.transform.localPosition = new Vector3(0, 0.425f, 0.01f);
            Object.DestroyImmediate(vis.GetComponent<Collider>());
            var ren = vis.GetComponent<Renderer>();
            ren.material = new Material(Shader.Find("Sprites/Default"));
            ren.material.color = Color.black; 
            ren.sortingOrder = 5;

            // Save as Prefab
            prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            
            Debug.Log("[NetworkAutoSetup] Created PortComponent prefab at " + prefabPath);
        }

        // Register with NetworkManager
        NetworkManager nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm != null)
        {
            var prefabList = nm.NetworkConfig.Prefabs.Prefabs;
            bool exists = false;
            foreach (var p in prefabList)
            {
                if (p.Prefab == prefab) { exists = true; break; }
            }

            if (!exists)
            {
                NetworkPrefab netPrefab = new NetworkPrefab();
                netPrefab.Prefab = prefab;
                nm.NetworkConfig.Prefabs.Add(netPrefab);
                EditorUtility.SetDirty(nm); // Force save
                Debug.Log("[NetworkAutoSetup] Registered PortComponent to NetworkManager.");
            }
        }
        else
        {
            Debug.LogError("[NetworkAutoSetup] Could not find NetworkManager in scene to register prefab.");
        }
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path);
            string newFolder = Path.GetFileName(path);
            // Simple logic assuming parent exists or is Assets
            if (parent == "Assets") AssetDatabase.CreateFolder("Assets", newFolder);
            else 
            {
                // Recursive check needed or just assume typical structure
                // For "Assets/Resources/NetworkPrefabs", assumes Assets/Resources might need creation
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "NetworkPrefabs");
            }
        }
    }
}
