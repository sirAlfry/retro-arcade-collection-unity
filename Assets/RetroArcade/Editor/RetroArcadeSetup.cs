using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RetroArcade.EditorTools
{
    /// <summary>
    /// Configurazione automatica alla prima apertura del progetto:
    /// <list type="number">
    /// <item>se il package URP è installato ma nessuna render pipeline è attiva (progetto aperto direttamente
    /// dallo zip), crea l'asset URP con il Universal Renderer e lo attiva;</item>
    /// <item>crea <c>Assets/RetroArcade/Scenes/RetroArcade.unity</c> con un GameObject + <see cref="ArcadeApp"/>,
    /// la mette in testa alle Build Settings e la apre.</item>
    /// </list>
    /// Niente da trascinare a mano. Menu: Tools ▸ Retro Arcade.
    /// </summary>
    [InitializeOnLoad]
    internal static class RetroArcadeSetup
    {
        public const string SceneFolder = "Assets/RetroArcade/Scenes";
        public const string ScenePath = SceneFolder + "/RetroArcade.unity";
        public const string SettingsFolder = "Assets/RetroArcade/Settings";
        public const string UrpAssetPath = SettingsFolder + "/RetroArcade_URP.asset";
        public const string UrpRendererPath = SettingsFolder + "/RetroArcade_URP_Renderer.asset";

        private enum SceneResult { Cancelled, CreatedAndOpen, CreatedInBackground }

        static RetroArcadeSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return; // ci riproverà al prossimo reload
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            // Solo se l'asset non è mai stato creato: se poi scegli di tornare alla Built-in, non lo rifacciamo.
            bool urpConfigured = !File.Exists(UrpAssetPath) && GraphicsSettings.defaultRenderPipeline == null && TryConfigureUrp(silent: true);

            if (File.Exists(ScenePath))
            {
                if (urpConfigured) Debug.Log("[RetroArcade] URP configurata: " + UrpAssetPath);
                return;
            }

            var result = CreateScene();
            if (result == SceneResult.Cancelled) return;
            AddToBuildSettings(ScenePath, moveToTop: true);

            string urpLine = urpConfigured ? "• attivato URP (Universal Render Pipeline)\n" : "";
            if (result == SceneResult.CreatedAndOpen)
            {
                EditorUtility.DisplayDialog("Retro Arcade Collection",
                    "Progetto pronto:\n" + urpLine +
                    "• creata e aperta la scena " + ScenePath + "\n" +
                    "• aggiunta alle Build Settings\n\nPremi ▶ Play!", "Evviva");
            }
            else if (EditorUtility.DisplayDialog("Retro Arcade Collection",
                         "Progetto pronto:\n" + urpLine +
                         "• creata la scena " + ScenePath + "\n" +
                         "• aggiunta alle Build Settings\n\nVuoi aprirla adesso? Poi basta premere ▶ Play.",
                         "Apri la scena", "Più tardi"))
            {
                OpenScene();
            }
        }

        // ───────────────────────────── Menu ─────────────────────────────

        [MenuItem("Tools/Retro Arcade/Apri la scena principale", priority = 1)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath) && CreateScene() == SceneResult.Cancelled) return;
            if (SceneManager.GetActiveScene().path == ScenePath) return; // già aperta
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Retro Arcade/Ricrea scena e Build Settings", priority = 2)]
        public static void Recreate()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).path != ScenePath) continue;
                EditorUtility.DisplayDialog("Retro Arcade", "La scena è aperta: chiudila (o aprine un'altra) e riprova.", "OK");
                return;
            }
            if (CreateScene() == SceneResult.Cancelled) return;
            AddToBuildSettings(ScenePath, moveToTop: true);
            Debug.Log("[RetroArcade] Scena ricreata e messa al primo posto nelle Build Settings.");
        }

        [MenuItem("Tools/Retro Arcade/Attiva URP", priority = 10)]
        public static void ConfigureUrpMenu()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                EditorUtility.DisplayDialog("Retro Arcade",
                    "È già attiva una render pipeline: " + GraphicsSettings.defaultRenderPipeline.name, "OK");
                return;
            }
            if (TryConfigureUrp(silent: false))
                EditorUtility.DisplayDialog("Retro Arcade", "URP attivata (" + UrpAssetPath + ").", "OK");
        }

        [MenuItem("Tools/Retro Arcade/Azzera i record salvati", priority = 20)]
        public static void ResetScores()
        {
            if (EditorUtility.DisplayDialog("Retro Arcade", "Cancellare tutti i record (PlayerPrefs) della raccolta?", "Cancella", "Annulla"))
                ScoreStore.ResetAll();
        }

        /// <summary>Usato dalla build: crea la scena se manca e si assicura che sia nelle Build Settings.</summary>
        internal static void EnsureSceneInBuild()
        {
            if (!File.Exists(ScenePath) && CreateScene() == SceneResult.Cancelled) return;
            AddToBuildSettings(ScenePath, moveToTop: false);
        }

        // ───────────────────────────── URP ─────────────────────────────

        /// <summary>
        /// Crea e attiva un asset URP con il Universal Renderer, come fa il menu
        /// Assets ▸ Create ▸ Rendering ▸ URP Asset (with Universal Renderer).
        /// Usa la reflection di proposito: così questo script compila anche nei progetti SENZA URP
        /// (o con versioni di URP dall'API diversa) e, nel caso peggiore, si resta sulla Built-in.
        /// </summary>
        private static bool TryConfigureUrp(bool silent)
        {
            try
            {
                Type assetType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
                Type rendererType = FindType("UnityEngine.Rendering.Universal.UniversalRendererData");
                if (assetType == null || rendererType == null)
                {
                    if (!silent)
                        EditorUtility.DisplayDialog("Retro Arcade",
                            "Il package Universal RP non è installato.\nWindow ▸ Package Manager ▸ Unity Registry ▸ Universal RP ▸ Install.", "OK");
                    return false;
                }

                MethodInfo create = assetType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Create" && m.GetParameters().Length == 1);
                if (create == null) throw new MissingMethodException(assetType.Name, "Create");

                if (!AssetDatabase.IsValidFolder(SettingsFolder))
                    AssetDatabase.CreateFolder("Assets/RetroArcade", "Settings");

                // 1) Renderer (Universal Renderer, Forward) con i dati di post-processing di default
                var renderer = ScriptableObject.CreateInstance(rendererType);
                var ppField = rendererType.GetField("postProcessData", BindingFlags.Public | BindingFlags.Instance);
                var pp = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                if (ppField != null && pp != null && ppField.FieldType.IsInstanceOfType(pp)) ppField.SetValue(renderer, pp);
                AssetDatabase.CreateAsset(renderer, UrpRendererPath);

                // 2) Pipeline asset che usa quel renderer
                var asset = (RenderPipelineAsset)create.Invoke(null, new object[] { renderer });
                AssetDatabase.CreateAsset(asset, UrpAssetPath);
                AssetDatabase.SaveAssets();

                // 3) Attivazione (i livelli di qualità senza override usano questo asset)
                GraphicsSettings.defaultRenderPipeline = asset;
                AssetDatabase.SaveAssets();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RetroArcade] Impossibile attivare URP automaticamente, resto sulla Built-in Render Pipeline " +
                                 "(il gioco funziona uguale). Dettagli: " + e);
                return false;
            }
        }

        /// <summary>
        /// Cerca un tipo per nome completo. I due tipi di URP che servono derivano da ScriptableObject, quindi basta
        /// l'indice dei tipi che l'editor tiene già pronto (TypeCache). Niente AppDomain.GetAssemblies(): in Unity
        /// può restituire assembly già scaricati dopo un ricaricamento del codice (avviso UAC0005).
        /// TypeCache esiste in ogni versione di Unity 6 (l'alternativa CurrentAssemblies solo dalla 6.4).
        /// </summary>
        private static Type FindType(string fullName)
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (t.FullName == fullName) return t;
            }
            return null;
        }

        // ───────────────────────────── Scena ─────────────────────────────

        private static SceneResult CreateScene()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/RetroArcade", "Scenes");

            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                // C'è solo una scena "Untitled" (es. prima apertura del progetto): Unity non permette di affiancarle
                // una scena additiva, quindi la sostituiamo (chiedendo conferma se contiene modifiche).
                if (active.isDirty && !EditorUtility.DisplayDialog("Retro Arcade",
                        "La scena aperta non è salvata. Sostituirla con la scena della Retro Arcade Collection?",
                        "Sostituisci", "Annulla"))
                    return SceneResult.Cancelled;
                var single = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("RetroArcade").AddComponent<ArcadeApp>();
                EditorSceneManager.SaveScene(single, ScenePath);
                AssetDatabase.Refresh();
                return SceneResult.CreatedAndOpen;
            }

            // Scena creata in modalità additiva e poi chiusa: non tocca quella che hai aperto.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var go = new GameObject("RetroArcade");
            go.AddComponent<ArcadeApp>();
            SceneManager.MoveGameObjectToScene(go, scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
            return SceneResult.CreatedInBackground;
        }

        private static void AddToBuildSettings(string path, bool moveToTop)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int index = scenes.FindIndex(s => s.path == path);
            if (index >= 0)
            {
                if (!moveToTop)
                {
                    if (scenes[index].enabled) return;
                    scenes[index] = new EditorBuildSettingsScene(path, true); // era disattivata: la riattiviamo
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
                scenes.RemoveAt(index);
            }
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
