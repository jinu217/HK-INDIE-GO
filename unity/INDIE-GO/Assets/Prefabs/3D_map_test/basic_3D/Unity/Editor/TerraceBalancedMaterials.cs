#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Optional: place this file in an Editor folder, select the supplied OBJ,
// then use Tools > Terrace > Create materials and prefab.
public static class TerraceBalancedMaterials
{
    [Serializable] private class Entry
    {
        public string name;
        public float[] basecolor_srgb;
        public float metallic;
        public float roughness;
        public string basecolor_texture;
    }
    [Serializable] private class Manifest { public Entry[] materials; }

    [MenuItem("Tools/Terrace/Create materials and prefab")]
    public static void CreatePrefab()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!path.EndsWith("/Terrace_Balanced_NoFlags.obj", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Terrace", "Select Terrace_Balanced_NoFlags.obj in the Project window.", "OK");
            return;
        }
        string root = Path.GetDirectoryName(path).Replace('\\', '/');
        string manifestPath = root + "/material_manifest.json";
        if (!File.Exists(manifestPath)) throw new FileNotFoundException(manifestPath);
        Manifest manifest = JsonUtility.FromJson<Manifest>("{\"materials\":" + File.ReadAllText(manifestPath) + "}");
        var pipeline = GraphicsSettings.currentRenderPipeline;
        string shaderName = pipeline == null ? "Standard" :
            (pipeline.GetType().Name.Contains("HDRender") ? "HDRP/Lit" : "Universal Render Pipeline/Lit");
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Shader not installed: " + shaderName);
        string materialsDir = AssetDatabase.GenerateUniqueAssetPath(root + "/BalancedMaterials");
        AssetDatabase.CreateFolder(root, Path.GetFileName(materialsDir));
        var lookup = new Dictionary<string, Material>(StringComparer.Ordinal);
        foreach (Entry e in manifest.materials)
        {
            var m = new Material(shader) { name = e.name };
            Color color = new Color(e.basecolor_srgb[0], e.basecolor_srgb[1], e.basecolor_srgb[2], e.basecolor_srgb[3]);
            Texture2D texture = null;
            if (!string.IsNullOrEmpty(e.basecolor_texture))
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(root + "/textures/" + e.basecolor_texture);
                if (texture == null) throw new FileNotFoundException("Texture missing: " + e.basecolor_texture);
                color = Color.white;
            }
            foreach (string property in new[] { "_BaseColor", "_Color" })
                if (m.HasProperty(property)) m.SetColor(property, color);
            foreach (string property in new[] { "_BaseColorMap", "_BaseMap", "_MainTex" })
                if (m.HasProperty(property)) m.SetTexture(property, texture);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", e.metallic);
            foreach (string property in new[] { "_Smoothness", "_Glossiness" })
                if (m.HasProperty(property)) m.SetFloat(property, 1f - e.roughness);
            AssetDatabase.CreateAsset(m, materialsDir + "/" + e.name + ".mat");
            lookup[e.name] = m;
        }
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] assigned = renderer.sharedMaterials;
                for (int i = 0; i < assigned.Length; i++)
                {
                    string name = assigned[i] == null ? "" : assigned[i].name.Replace(" (Instance)", "");
                    Material replacement;
                    if (lookup.TryGetValue(name, out replacement)) assigned[i] = replacement;
                    else Debug.LogWarning("Material not mapped: " + name + " on " + renderer.name);
                }
                renderer.sharedMaterials = assigned;
            }
            string target = AssetDatabase.GenerateUniqueAssetPath(root + "/Terrace_Balanced_Unity.prefab");
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, target);
            AssetDatabase.SaveAssets();
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
            Debug.Log("Terrace prefab created: " + target + " | " + shaderName);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }
}
#endif
