using System.IO;
using UnityEditor;
using UnityEngine;

public class PrefabToIconExporter : EditorWindow
{
    [MenuItem("Tools/Export Selected Prefab Icon")]
    public static void ExportPrefabIcon()
    {
        // Get the currently selected GameObject in the Project tab
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a Prefab in the Project window first!", "OK");
            return;
        }

        // Try to fetch Unity's built-in asset preview thumbnail
        Texture2D texture = AssetPreview.GetAssetPreview(selectedObject);

        if (texture == null)
        {
            EditorUtility.DisplayDialog("Notice", "Unity hasn't generated a preview for this yet, or it's loading. Try clicking the prefab once to load its preview, then run this again.", "OK");
            return;
        }

        // Convert the preview texture into PNG bytes
        byte[] pngBytes = texture.EncodeToPNG();

        // Define where to save the image (Assets/Prefab_Name_Icon.png)
        string folderPath = Application.dataPath;
        string fileName = selectedObject.name + "_Icon.png";
        string fullPath = Path.Combine(folderPath, fileName);

        // Save the file
        File.WriteAllBytes(fullPath, pngBytes);

        // Tell Unity to refresh the asset database so the new image appears immediately
        AssetDatabase.Refresh();

        // Automatically configure the new file as a Sprite (UI) asset
        string localPath = "Assets/" + fileName;
        TextureImporter textureImporter = AssetImporter.GetAtPath(localPath) as TextureImporter;
        if (textureImporter != null)
        {
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.SaveAndReimport();
        }

        EditorUtility.DisplayDialog("Success", $"Icon successfully exported to:\nAssets/{fileName} (and configured as a Sprite!)", "Finish");
    }
}