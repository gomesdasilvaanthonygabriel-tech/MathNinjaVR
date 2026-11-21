using UnityEditor;
using UnityEngine;

/// <summary>Configuracao de import dos assets do ambiente modelado no Blender (Assets/Art/Ambiente).</summary>
public class AmbienteImportSettings : AssetPostprocessor
{
    const string Root = "Assets/Art/Ambiente/";

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Root)) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.useFileScale = true;          // FBX exportado com apply_unit_scale: 1 unidade = 1 m
        mi.bakeAxisConversion = true;    // sem rotacao -90 nos filhos
        mi.addCollider = false;          // colisores feitos a mao na cena
        mi.isReadable = false;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importVisibility = false;
        mi.importBlendShapes = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.importAnimation = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab; // materiais remapeados para Assets/Art/Ambiente/Materials (menu MathNinja/Ambiente)
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.importNormals = ModelImporterNormals.Import;
        mi.generateSecondaryUV = false;
    }

    // estrutura, mobilia, decoracao e ninjas = Static; o professor (animado pelo RobotTeacher.cs) nao
    void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.StartsWith(Root)) return;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root.transform || t.name.StartsWith("RobotTeacher")) continue;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)~0);
        }
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;
        var ti = (TextureImporter)assetImporter;
        if (!ti.importSettingsMissing) return;
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = true;
        ti.mipmapEnabled = true;
        ti.maxTextureSize = 2048;
        ti.anisoLevel = 4;
        ti.wrapMode = assetPath.Contains("Piso") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
    }
}
