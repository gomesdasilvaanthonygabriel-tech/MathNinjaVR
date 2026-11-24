using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Abre a cena Game ao iniciar o Editor (num clone novo a Unity abre uma cena vazia).
/// Roda so uma vez por sessao e nao mexe em cena que voce ja abriu.
/// </summary>
[InitializeOnLoad]
static class AbrirCenaGameAoIniciar
{
    const string CenaGame = "Assets/Scenes/Game.unity";
    const string CenaPadrao = "Assets/Scenes/SampleScene.unity";
    const string ChaveSessao = "MathNinja.AbrirCenaGameAoIniciar";

    static AbrirCenaGameAoIniciar()
    {
        // uma vez por sessao do editor (recompilacoes nao disparam de novo)
        if (SessionState.GetBool(ChaveSessao, false)) return;
        SessionState.SetBool(ChaveSessao, true);
        EditorApplication.delayCall += AbrirSePreciso;
    }

    static void AbrirSePreciso()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var cena = EditorSceneManager.GetActiveScene();
        bool cenaVaziaOuPadrao = string.IsNullOrEmpty(cena.path) || cena.path == CenaPadrao;
        if (!cenaVaziaOuPadrao || cena.isDirty) return;

        // so abre se o arquivo existir
        if (File.Exists(CenaGame))
            EditorSceneManager.OpenScene(CenaGame);
    }
}
