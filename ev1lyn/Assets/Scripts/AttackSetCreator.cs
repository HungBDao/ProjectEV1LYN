using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > EV1LYN > Create Default Sword Attacks
/// Creates the 6 sword AttackData assets with starter numbers. 
/// If a GameObject with ComboLogger is selected, it also wires them into its slots. 
/// Existing assets are reused, never overwritten.
/// </summary>
public static class AttackSetCreator
{
    const string Folder = "Assets/_EV1LYN/Data/Attacks/Sword";

    [MenuItem("Tools/EV1LYN/Create Default Sword Attacks")]
    static void Create()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();

        // Format: name, startup, active, recovery, damage, energyGain, hitstop, knockback, offset, size
        var ba1 = Make("BA1", 0.06f, 0.08f, 0.14f, 8f, 4f, 0.04f, new(1.5f, 0f), new(0.8f, 0f), new(1.2f, 1f));
        var ba2 = Make("BA2", 0.07f, 0.08f, 0.16f, 10f, 4f, 0.05f, new(2f, 0f), new(0.9f, 0f), new(1.4f, 1f));
        var ba3 = Make("BA3", 0.12f, 0.10f, 0.28f, 18f, 6f, 0.09f, new(6f, 3f), new(1f, 0.1f), new(1.8f, 1.3f));
        var ex = Make("EX", 0.10f, 0.15f, 0.35f, 40f, 0f, 0.12f, new(8f, 5f), new(1.2f, 0.2f), new(2.6f, 1.6f));
        var jump = Make("JUMP_ATK", 0.05f, 0.12f, 0.20f, 12f, 4f, 0.05f, new(2f, 1f), new(0.6f, -0.3f), new(1.4f, 1.2f));
        var hold = Make("HOLD", 0.12f, 0.12f, 0.30f, 25f, 8f, 0.10f, new(7f, 4f), new(1f, 0f), new(2f, 1.2f));

        AssetDatabase.SaveAssets();

        var go = Selection.activeGameObject;
        var logger = go != null ? go.GetComponent<ComboLogger>() : null;
        if (logger != null)
        {
            var so = new SerializedObject(logger);
            var basics = so.FindProperty("basics");
            basics.arraySize = 3;
            basics.GetArrayElementAtIndex(0).objectReferenceValue = ba1;
            basics.GetArrayElementAtIndex(1).objectReferenceValue = ba2;
            basics.GetArrayElementAtIndex(2).objectReferenceValue = ba3;
            so.FindProperty("exAttack").objectReferenceValue = ex;
            so.FindProperty("jumpAttack").objectReferenceValue = jump;
            so.FindProperty("holdAttack").objectReferenceValue = hold;
            so.ApplyModifiedProperties();
            Debug.Log($"[EV1LYN] Attacks created in {Folder} and assigned to {go.name}.");
        }
        else
        {
            Debug.Log($"[EV1LYN] Attacks created in {Folder}. Select your player and run again to auto-assign.");
        }
    }

    static AttackData Make(string name, float startup, float active, float recovery, float damage,
                            float energy, float hitstop, Vector2 knockback, Vector2 offset, Vector2 size)
    {
        string path = $"{Folder}/Atk_Sword_{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        if (existing != null) return existing;

        var a = ScriptableObject.CreateInstance<AttackData>();
        a.displayName = name;
        a.startup = startup; a.active = active; a.recovery = recovery;
        a.damage = damage; a.energyGain = energy; a.hitstop = hitstop;
        a.knockback = knockback; a.hitboxOffset = offset; a.hitboxSize = size;
        AssetDatabase.CreateAsset(a, path);
        return a;
    }
}