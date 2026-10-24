using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace RetroArcade
{
    /// <summary>
    /// Dove finiscono record e impostazioni. Due "sezioni" come nel file Python:
    /// <c>{"version": 1, "scores": {...}, "settings": {...}}</c>. Tutti i valori sono numeri
    /// (i booleani si salvano come true/false ma si leggono come 1/0).
    /// </summary>
    public interface IScoreStorage
    {
        bool TryGet(string section, string key, out double value);
        void Set(string section, string key, double value);
        void Clear(string section);
        /// <summary>Descrizione per i messaggi (percorso del file, "PlayerPrefs"...).</summary>
        string Location { get; }
    }

    /// <summary>
    /// Record e impostazioni persistenti: porting di <c>scores.py</c>.
    /// <list type="bullet">
    /// <item><b>Mai perdere dati</b>: il file viene scritto in un file temporaneo e poi sostituito in un colpo solo.</item>
    /// <item><b>Mai bloccare il gioco</b>: un file danneggiato viene rinominato in <c>scores.json.corrupt</c>.</item>
    /// <item><b>Dove</b>: variabile d'ambiente <c>RETRO_ARCADE_SCORES</c>; altrimenti accanto all'eseguibile
    /// (nell'editor: nella cartella del progetto); se quella cartella non è scrivibile (es. <c>C:\Program Files</c>)
    /// in <c>Application.persistentDataPath</c>. Nel browser (WebGL) si usano i PlayerPrefs.</item>
    /// </list>
    /// </summary>
    public static class ScoreStore
    {
        public const string ScoresSection = "scores";
        public const string SettingsSection = "settings";
        public const string FileName = "scores.json";
        public const string EnvVar = "RETRO_ARCADE_SCORES";

        private static IScoreStorage _storage;

        /// <summary>Backend di salvataggio (sostituibile nei test con <see cref="MemoryScoreStorage"/>).</summary>
        public static IScoreStorage Storage
        {
            get => _storage ??= CreateDefault();
            set => _storage = value;
        }

        // ── Record ──

        public static double? Get(string key) => Storage.TryGet(ScoresSection, key, out double v) ? v : (double?)null;
        public static int GetInt(string key, int fallback = 0) => Storage.TryGet(ScoresSection, key, out double v) ? (int)Math.Round(v) : fallback;
        public static void Set(string key, double value) => Storage.Set(ScoresSection, key, value);
        public static void Add(string key, int amount = 1) => Set(key, GetInt(key) + amount);

        /// <summary>Salva solo se il valore supera il record. Restituisce il record aggiornato.</summary>
        public static int SetIfHigher(string key, int value)
        {
            int best = GetInt(key);
            if (value <= best) return best;
            Set(key, value);
            return value;
        }

        /// <summary>
        /// Salva <paramref name="value"/> se batte il record (come <c>scores.record_best</c>).
        /// Per tempi e mosse usa <paramref name="lowerIsBetter"/> = true.
        /// </summary>
        /// <returns>True se è un nuovo record.</returns>
        public static bool RecordBest(string key, double value, bool lowerIsBetter = false)
        {
            double? current = Get(key);
            bool better = current == null ? (value > 0 || lowerIsBetter)
                        : lowerIsBetter ? value < current.Value
                        : value > current.Value;
            if (better) Set(key, value);
            return better;
        }

        // ── Impostazioni ──

        public static double GetSetting(string key, double fallback) =>
            Storage.TryGet(SettingsSection, key, out double v) ? v : fallback;
        public static bool GetSetting(string key, bool fallback) =>
            Storage.TryGet(SettingsSection, key, out double v) ? v != 0 : fallback;
        public static void SetSetting(string key, double value) => Storage.Set(SettingsSection, key, value);
        public static void SetSetting(string key, bool value) => Storage.Set(SettingsSection, key, value ? 1 : 0);

        /// <summary>Cancella tutti i record (le impostazioni restano).</summary>
        public static void ResetAll() => Storage.Clear(ScoresSection);

        // ── Scelta del backend ──

        private static IScoreStorage CreateDefault()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new PlayerPrefsStorage();
#else
            try
            {
                return new JsonFileStorage(DefaultPath());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RetroArcade] scores.json non utilizzabile, uso i PlayerPrefs: " + e.Message);
                return new PlayerPrefsStorage();
            }
#endif
        }

        /// <summary>Stessa logica di <c>scores.default_path()</c>.</summary>
        public static string DefaultPath()
        {
            string overridePath = Environment.GetEnvironmentVariable(EnvVar);
            if (!string.IsNullOrEmpty(overridePath)) return overridePath;
            string primary = AppFolder();
            if (primary != null && IsWritable(primary)) return Path.Combine(primary, FileName);
            return Path.Combine(Application.persistentDataPath, FileName);
        }

        /// <summary>
        /// Cartella "del programma": quella del progetto nell'editor, quella dell'eseguibile nelle build
        /// desktop (Windows/Linux). Su macOS l'app è un pacchetto firmato: lì non si scrive mai.
        /// </summary>
        public static string AppFolder()
        {
            try
            {
                if (Application.isEditor) return Directory.GetCurrentDirectory();
                if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.LinuxPlayer)
                    return Path.GetDirectoryName(Application.dataPath); // <exe>_Data → cartella dell'exe
            }
            catch (Exception) { /* si ripiega sulla cartella utente */ }
            return null;
        }

        private static readonly Dictionary<string, bool> WritableCache = new Dictionary<string, bool>();

        public static bool IsWritable(string folder)
        {
            if (WritableCache.TryGetValue(folder, out bool ok)) return ok;
            try
            {
                Directory.CreateDirectory(folder);
                string probe = Path.Combine(folder, ".retro-write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                ok = true;
            }
            catch (Exception)
            {
                ok = false;
            }
            WritableCache[folder] = ok;
            return ok;
        }
    }

    // ───────────────────────────── Backend: file JSON ─────────────────────────────

    /// <summary><c>scores.json</c> con scrittura atomica (file temporaneo + sostituzione) e quarantena dei file rotti.</summary>
    public sealed class JsonFileStorage : IScoreStorage
    {
        private readonly string _path;
        private Dictionary<string, Dictionary<string, double>> _doc;

        public JsonFileStorage(string path) { _path = path; }

        public string Location => _path;

        public bool TryGet(string section, string key, out double value)
        {
            value = 0;
            return Load().TryGetValue(section, out var d) && d.TryGetValue(key, out value);
        }

        public void Set(string section, string key, double value)
        {
            var doc = Load();
            if (!doc.TryGetValue(section, out var d)) doc[section] = d = new Dictionary<string, double>();
            if (d.TryGetValue(key, out double old) && old.Equals(value)) return; // niente scritture inutili
            d[key] = value;
            Save();
        }

        public void Clear(string section)
        {
            Load().Remove(section);
            Save();
        }

        private Dictionary<string, Dictionary<string, double>> Load()
        {
            if (_doc != null) return _doc;
            _doc = NewDoc();
            try
            {
                if (!File.Exists(_path)) return _doc;
                string raw = File.ReadAllText(_path, Encoding.UTF8);
                var root = MiniJson.Parse(raw) as Dictionary<string, object>;
                if (root == null) throw new FormatException("la radice non è un oggetto");
                // Formato "vecchio" (dizionario piatto di record): lo consideriamo tutto record.
                if (!root.ContainsKey(ScoreStore.ScoresSection) && !root.ContainsKey(ScoreStore.SettingsSection))
                    root = new Dictionary<string, object> { [ScoreStore.ScoresSection] = root };
                foreach (string section in new[] { ScoreStore.ScoresSection, ScoreStore.SettingsSection })
                {
                    if (!(root.TryGetValue(section, out object obj) && obj is Dictionary<string, object> values)) continue;
                    foreach (var kv in values)
                        if (MiniJson.TryNumber(kv.Value, out double v)) _doc[section][kv.Key] = v;
                }
            }
            catch (Exception e) when (e is FormatException || e is InvalidCastException)
            {
                Debug.LogWarning("[RetroArcade] " + _path + " danneggiato: lo rinomino in .corrupt e riparto da zero.");
                Quarantine();
                _doc = NewDoc();
            }
            catch (IOException e)
            {
                Debug.LogWarning("[RetroArcade] Impossibile leggere " + _path + ": " + e.Message);
            }
            return _doc;
        }

        private static Dictionary<string, Dictionary<string, double>> NewDoc() => new Dictionary<string, Dictionary<string, double>>
        {
            [ScoreStore.ScoresSection] = new Dictionary<string, double>(),
            [ScoreStore.SettingsSection] = new Dictionary<string, double>(),
        };

        private void Quarantine()
        {
            try
            {
                string bad = _path + ".corrupt";
                if (File.Exists(bad)) File.Delete(bad);
                File.Move(_path, bad);
            }
            catch (Exception) { /* pazienza: il file verrà sovrascritto */ }
        }

        private void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(_path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string tmp = Path.Combine(dir ?? ".", ".scores-" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(tmp, Serialize(), new UTF8Encoding(false));
                if (File.Exists(_path)) File.Replace(tmp, _path, null);
                else File.Move(tmp, _path);
            }
            catch (Exception e)
            {
                // Disco pieno, permessi... si continua a giocare (come in Python).
                Debug.LogWarning("[RetroArcade] Impossibile salvare " + _path + ": " + e.Message);
            }
        }

        private string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"version\": 1");
            foreach (string section in new[] { ScoreStore.ScoresSection, ScoreStore.SettingsSection })
            {
                sb.Append(",\n  \"").Append(section).Append("\": {");
                var keys = new List<string>(_doc[section].Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    sb.Append(i == 0 ? "\n" : ",\n").Append("    ").Append(MiniJson.Quote(keys[i])).Append(": ");
                    sb.Append(MiniJson.Number(_doc[section][keys[i]]));
                }
                sb.Append(keys.Count > 0 ? "\n  }" : "}");
            }
            sb.Append("\n}\n");
            return sb.ToString();
        }
    }

    // ───────────────────────────── Backend: PlayerPrefs (Web) ─────────────────────────────

    /// <summary>Nel browser non c'è un file system vero: PlayerPrefs finisce in IndexedDB.</summary>
    internal sealed class PlayerPrefsStorage : IScoreStorage
    {
        private const string Prefix = "retro.";
        private const string IndexKey = "retro.__keys";

        public string Location => "PlayerPrefs";

        public bool TryGet(string section, string key, out double value)
        {
            string k = Prefix + section + "." + key;
            value = 0;
            if (!PlayerPrefs.HasKey(k)) return false;
            return double.TryParse(PlayerPrefs.GetString(k), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public void Set(string section, string key, double value)
        {
            string k = Prefix + section + "." + key;
            PlayerPrefs.SetString(k, value.ToString("R", CultureInfo.InvariantCulture));
            string index = PlayerPrefs.GetString(IndexKey, "");
            if (!("|" + index + "|").Contains("|" + k + "|"))
                PlayerPrefs.SetString(IndexKey, index.Length == 0 ? k : index + "|" + k);
            PlayerPrefs.Save(); // su WebGL serve per scrivere subito
        }

        public void Clear(string section)
        {
            string prefix = Prefix + section + ".";
            var keep = new List<string>();
            foreach (string k in PlayerPrefs.GetString(IndexKey, "").Split('|'))
            {
                if (k.Length == 0) continue;
                if (k.StartsWith(prefix, StringComparison.Ordinal)) PlayerPrefs.DeleteKey(k);
                else keep.Add(k);
            }
            PlayerPrefs.SetString(IndexKey, string.Join("|", keep));
            PlayerPrefs.Save();
        }
    }

    // ───────────────────────────── Backend: memoria (test, anteprime) ─────────────────────────────

    public sealed class MemoryScoreStorage : IScoreStorage
    {
        private readonly Dictionary<string, double> _data = new Dictionary<string, double>();
        public string Location => "memoria";
        public bool TryGet(string section, string key, out double value) => _data.TryGetValue(section + "." + key, out value);
        public void Set(string section, string key, double value) => _data[section + "." + key] = value;

        public void Clear(string section)
        {
            var remove = new List<string>();
            foreach (var k in _data.Keys) if (k.StartsWith(section + ".", StringComparison.Ordinal)) remove.Add(k);
            foreach (var k in remove) _data.Remove(k);
        }
    }

    // ───────────────────────────── Mini parser JSON ─────────────────────────────

    /// <summary>
    /// Un parser JSON minimo (oggetti, array, numeri, stringhe, true/false/null): JsonUtility di Unity
    /// non gestisce i dizionari, e per un file così piccolo non vale la pena aggiungere dipendenze.
    /// </summary>
    internal static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            object value = ParseValue(text, ref i);
            SkipWs(text, ref i);
            if (i != text.Length) throw new FormatException("caratteri in più alla fine");
            return value;
        }

        public static bool TryNumber(object o, out double v)
        {
            switch (o)
            {
                case double d: v = d; return true;
                case bool b: v = b ? 1 : 0; return true;
                default: v = 0; return false;
            }
        }

        public static string Number(double v) =>
            Math.Abs(v - Math.Round(v)) < 1e-9 && Math.Abs(v) < 1e15
                ? ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture)
                : v.ToString("R", CultureInfo.InvariantCulture);

        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("fine inattesa");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (Match(s, ref i, "true")) return true;
            if (Match(s, ref i, "false")) return false;
            if (Match(s, ref i, "null")) return null;
            return ParseNumber(s, ref i);
        }

        private static bool Match(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("chiave attesa");
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("':' atteso");
                i++;
                d[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return d; }
                throw new FormatException("',' o '}' atteso");
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return list; }
                throw new FormatException("',' o ']' atteso");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // "
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("\\u incompleto");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("stringa non chiusa");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException("valore non valido");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
