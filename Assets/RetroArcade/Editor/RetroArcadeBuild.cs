using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RetroArcade.EditorTools
{
    /// <summary>
    /// La "ricetta" per creare l'eseguibile, equivalente di <c>packaging/RetroArcadeCollection.spec</c> della
    /// versione Python. Un clic su <b>Tools ▸ Retro Arcade ▸ Crea eseguibile Windows</b> e basta:
    /// <list type="number">
    /// <item>imposta nome, versione, icona e finestra (ridimensionabile, 4:3, niente splash screen);</item>
    /// <item>controlla che la scena sia nelle Build Settings e che il modulo della piattaforma sia installato;</item>
    /// <item>compila in <c>Builds/&lt;piattaforma&gt;/</c>, crea uno .zip pronto da condividere e apre la cartella.</item>
    /// </list>
    /// Funziona anche da riga di comando (CI):
    /// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod RetroArcade.EditorTools.RetroArcadeBuild.BuildWindowsCli -logFile build.log</c>
    /// </summary>
    public static class RetroArcadeBuild
    {
        public const string ProductName = "Retro Arcade Collection";
        public const string CompanyName = "Sir Alfry";
        public const string ExeName = "RetroArcadeCollection";
        public const string IconPath = "Assets/RetroArcade/Art/icon.png";
        public const string OutputRoot = "Builds";

        // ───────────────────────────── Menu ─────────────────────────────

        [MenuItem("Tools/Retro Arcade/Crea eseguibile Windows", priority = 40)]
        public static void BuildWindowsMenu() => Build(BuildTarget.StandaloneWindows64, interactive: true);

        [MenuItem("Tools/Retro Arcade/Crea eseguibile Linux", priority = 41)]
        public static void BuildLinuxMenu() => Build(BuildTarget.StandaloneLinux64, interactive: true);

        [MenuItem("Tools/Retro Arcade/Crea app macOS", priority = 42)]
        public static void BuildMacMenu() => Build(BuildTarget.StandaloneOSX, interactive: true);

        [MenuItem("Tools/Retro Arcade/Applica solo le impostazioni del Player", priority = 50)]
        public static void ApplyPlayerSettingsMenu()
        {
            ApplyPlayerSettings();
            EditorUtility.DisplayDialog("Retro Arcade",
                "Impostazioni applicate:\n• nome \"" + ProductName + "\" v" + ArcadeInfo.Version +
                "\n• icona\n• finestra ridimensionabile 4:3\n• niente splash screen", "OK");
        }

        // ───────────────────────────── Riga di comando ─────────────────────────────

        /// <summary>Per la CI: esce con codice 1 se la build fallisce.</summary>
        public static void BuildWindowsCli() => ExitWith(Build(BuildTarget.StandaloneWindows64, interactive: false));
        public static void BuildLinuxCli() => ExitWith(Build(BuildTarget.StandaloneLinux64, interactive: false));
        public static void BuildMacCli() => ExitWith(Build(BuildTarget.StandaloneOSX, interactive: false));

        private static void ExitWith(bool ok)
        {
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        // ───────────────────────────── Build ─────────────────────────────

        public static bool Build(BuildTarget target, bool interactive)
        {
            string platform = PlatformName(target);

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                Fail(interactive,
                    $"Manca il modulo \"{platform} Build Support\".\n\n" +
                    "Aggiungilo da Unity Hub ▸ Installs ▸ ⚙ (accanto alla tua versione di Unity) ▸ Add modules, " +
                    "poi riapri il progetto e riprova.");
                return false;
            }

            if (interactive && EditorApplication.isPlaying)
            {
                Fail(true, "Ferma il Play mode (▶) prima di creare l'eseguibile.");
                return false;
            }

            RetroArcadeSetup.EnsureSceneInBuild();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail(interactive, "Nessuna scena nelle Build Settings: usa Tools ▸ Retro Arcade ▸ Ricrea scena e Build Settings.");
                return false;
            }

            ApplyPlayerSettings();

            string folder = Path.Combine(OutputRoot, platform);
            string location = target switch
            {
                BuildTarget.StandaloneWindows64 => Path.Combine(folder, ExeName + ".exe"),
                BuildTarget.StandaloneOSX => Path.Combine(folder, ExeName + ".app"),
                _ => Path.Combine(folder, ExeName + ".x86_64"),
            };

            // Si riparte da una cartella pulita: niente file vecchi mescolati a quelli nuovi.
            // Però i record e gli screenshot di chi ha giocato con la build precedente si salvano.
            string keep = SaveUserData(folder);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                RestoreUserData(keep, folder);
                Fail(interactive, $"Build {platform} non riuscita ({summary.result}, {summary.totalErrors} errori).\n" +
                                  "Apri la Console (Window ▸ General ▸ Console) per i dettagli.");
                return false;
            }

            RemoveDoNotShipFolders(folder);
            string zip = Path.Combine(OutputRoot, $"{ExeName}-{platform}-v{ArcadeInfo.Version}.zip");
            bool zipped = TryZip(folder, zip); // lo zip non contiene mai i TUOI record
            RestoreUserData(keep, folder);

            string sizeMb = (DirectorySize(folder) / (1024f * 1024f)).ToString("0.0");
            string msg = $"Build {platform} completata in {summary.totalTime.TotalSeconds:0} s ({sizeMb} MB).\n\n" +
                         $"Eseguibile: {location}\n" +
                         (zipped ? $"Pacchetto da condividere: {zip}\n" : "") +
                         "\nPer condividerla invia lo .zip (o tutta la cartella): l'eseguibile da solo non basta.";
            Debug.Log("[RetroArcade] " + msg);
            if (interactive)
            {
                EditorUtility.DisplayDialog("Retro Arcade ▸ Build", msg, "Apri la cartella");
                EditorUtility.RevealInFinder(zipped ? zip : location);
            }
            return true;
        }

        // ───────────────────────────── Impostazioni del Player ─────────────────────────────

        /// <summary>Nome, icona e finestra come la versione Python (800×600 scalabile, F11 per lo schermo intero).</summary>
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.bundleVersion = ArcadeInfo.Version;

            // Finestra ridimensionabile in 4:3. La prima volta ArcadeApp la porta a una misura comoda per lo schermo.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 800;
            PlayerSettings.defaultScreenHeight = 600;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true; // Alt+Invio, come nella versione Python
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.usePlayerLog = true;

            // Da Unity 6 lo splash "Made with Unity" si può togliere anche con la licenza Personal.
            try { PlayerSettings.SplashScreen.show = false; }
            catch (Exception e) { Debug.LogWarning("[RetroArcade] Splash screen non disattivabile: " + e.Message); }

            var icon = LoadIcon();
            if (icon != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            }
            else
            {
                Debug.LogWarning("[RetroArcade] Icona non trovata in " + IconPath + ": uso quella di Unity.");
            }

            AssetDatabase.SaveAssets();
        }

        private static Texture2D LoadIcon()
        {
            if (!File.Exists(IconPath)) return null;
            if (AssetImporter.GetAtPath(IconPath) is TextureImporter imp)
            {
                // Pixel-art: niente filtro né compressione, così i quadratini restano nitidi a ogni misura.
                bool changed = imp.filterMode != FilterMode.Point
                               || imp.textureCompression != TextureImporterCompression.Uncompressed
                               || imp.mipmapEnabled;
                if (changed)
                {
                    imp.textureType = TextureImporterType.Default;
                    imp.filterMode = FilterMode.Point;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    imp.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        // ───────────────────────────── Utilità ─────────────────────────────

        private static string PlatformName(BuildTarget t) => t switch
        {
            BuildTarget.StandaloneWindows64 => "Windows",
            BuildTarget.StandaloneOSX => "macOS",
            BuildTarget.StandaloneLinux64 => "Linux",
            _ => t.ToString(),
        };

        /// <summary>Cartelle di debug che Unity crea accanto alla build ma che non vanno distribuite.</summary>
        private static void RemoveDoNotShipFolders(string folder)
        {
            foreach (var dir in Directory.GetDirectories(folder))
            {
                string name = Path.GetFileName(dir);
                if (name.EndsWith("_DoNotShip", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("_ButDontShipItWithYourGame", StringComparison.OrdinalIgnoreCase))
                {
                    try { Directory.Delete(dir, true); } catch (IOException) { /* non indispensabile */ }
                }
            }
        }

        /// <summary>File creati GIOCANDO con la build (vedi ScoreStore e F12): da non perdere ricompilando.</summary>
        private static readonly string[] UserData = { "scores.json", "screenshots" };

        /// <summary>Sposta record e screenshot della build precedente in una cartella temporanea.</summary>
        private static string SaveUserData(string folder)
        {
            if (!Directory.Exists(folder)) return null;
            string temp = null;
            foreach (var name in UserData)
            {
                string src = Path.Combine(folder, name);
                if (!File.Exists(src) && !Directory.Exists(src)) continue;
                temp ??= Path.Combine(OutputRoot, ".keep-" + Path.GetFileName(folder));
                Directory.CreateDirectory(temp);
                string dst = Path.Combine(temp, name);
                if (File.Exists(src)) File.Copy(src, dst, true);
                else CopyDirectory(src, dst);
            }
            return temp;
        }

        private static void RestoreUserData(string temp, string folder)
        {
            if (temp == null || !Directory.Exists(temp)) return;
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var name in UserData)
                {
                    string src = Path.Combine(temp, name), dst = Path.Combine(folder, name);
                    if (File.Exists(src)) File.Copy(src, dst, true);
                    else if (Directory.Exists(src)) CopyDirectory(src, dst);
                }
                Directory.Delete(temp, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RetroArcade] Non sono riuscito a rimettere a posto record/screenshot: li trovi in {temp} ({e.Message})");
            }
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src)) File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src)) CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
        }

        private static bool TryZip(string folder, string zip)
        {
            try
            {
                if (File.Exists(zip)) File.Delete(zip);
                ZipFile.CreateFromDirectory(folder, zip, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: true);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RetroArcade] Zip non creato (la build è comunque pronta): " + e.Message);
                return false;
            }
        }

        private static long DirectorySize(string folder) =>
            Directory.Exists(folder)
                ? new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
                : 0;

        private static void Fail(bool interactive, string message)
        {
            Debug.LogError("[RetroArcade] " + message);
            if (interactive) EditorUtility.DisplayDialog("Retro Arcade ▸ Build", message, "OK");
        }
    }
}
