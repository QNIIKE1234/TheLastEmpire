using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildExecCharacterRig
{
    private const string PartsFolder = "Assets/Resources_Addressable/Image/NPC/exec-42420d3b-136f-4af1-89a2-8fe7a49bc4ae_RigParts";
    private const string PrefabPath = PartsFolder + "/ExecCharacter_BoneRig.prefab";

    [MenuItem("Tools/NPC/Create Exec Character Bone Rig")]
    private static void CreateRig()
    {
        string[] spriteNames =
        {
            "Head.png",
            "Body_Legs.png",
            "Arm_Left_Holding.png",
            "Arm_Right.png",
            "Bat.png"
        };

        foreach (string spriteName in spriteNames)
        {
            string assetPath = PartsFolder + "/" + spriteName;
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("Could not find texture importer for " + assetPath);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 100f;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.Tight;
            importer.SetTextureSettings(textureSettings);
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();

        Sprite head = LoadSprite("Head.png");
        Sprite body = LoadSprite("Body_Legs.png");
        Sprite leftArm = LoadSprite("Arm_Left_Holding.png");
        Sprite rightArm = LoadSprite("Arm_Right.png");
        Sprite bat = LoadSprite("Bat.png");
        if (head == null || body == null || leftArm == null || rightArm == null || bat == null)
        {
            Debug.LogError("One or more separated character sprites could not be loaded.");
            return;
        }

        var character = new GameObject("ExecCharacter_BoneRig");
        Transform root = CreateBone("Root", character.transform, Vector3.zero);
        Transform hips = CreateBone("Hips", root, Vector3.zero);
        Transform spine = CreateBone("Spine", hips, new Vector3(0f, 3.5f, 0f));
        Transform chest = CreateBone("Chest", spine, new Vector3(0f, 2.6f, 0f));
        Transform neck = CreateBone("Neck", chest, new Vector3(0f, 0.9f, 0f));
        Transform headBone = CreateBone("Head", neck, Vector3.zero);
        Transform leftShoulder = CreateBone("Shoulder_Left", chest, new Vector3(-1.8f, 0f, 0f));
        Transform leftWrist = CreateBone("Wrist_Left", leftShoulder, new Vector3(-0.15f, 2.3f, 0f));
        Transform rightShoulder = CreateBone("Shoulder_Right", chest, new Vector3(1.8f, 0f, 0f));

        AddSprite("Body_Legs", body, spine, new Vector3(0f, 0.175f, 0f), 0);
        AddSprite("Head_Sprite", head, headBone, new Vector3(0f, 2.25f, 0f), 3);
        AddSprite("Arm_Left_Holding", leftArm, leftShoulder, new Vector3(-0.65f, 0.88f, 0f), 2);
        AddSprite("Arm_Right", rightArm, rightShoulder, new Vector3(0.925f, -1.945f, 0f), 2);
        AddSprite("Bat", bat, leftWrist, new Vector3(2.975f, 1.75f, 0f), 1);

        PrefabUtility.SaveAsPrefabAsset(character, PrefabPath);
        Object.DestroyImmediate(character);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        EditorGUIUtility.PingObject(Selection.activeObject);
        Debug.Log("Created cutout bone rig prefab at " + PrefabPath);
    }

    private static Sprite LoadSprite(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(PartsFolder + "/" + fileName);
    }

    private static Transform CreateBone(string name, Transform parent, Vector3 localPosition)
    {
        var bone = new GameObject(name);
        bone.transform.SetParent(parent, false);
        bone.transform.localPosition = localPosition;
        bone.transform.localRotation = Quaternion.identity;
        bone.transform.localScale = Vector3.one;
        return bone.transform;
    }

    private static void AddSprite(string name, Sprite sprite, Transform parent, Vector3 localPosition, int sortingOrder)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = Vector3.one;

        var renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
    }
}
