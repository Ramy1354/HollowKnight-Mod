using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using UnityEngine;

namespace HKmenu
{
    [BepInPlugin("com.kvox.hkmenu", "HK Mod Menu", "1.7.1")]
    public class HKModMenu : BaseUnityPlugin
    {
        private const float FrameWidth = 410f;
        private const float FrameHeight = 490f;

        private bool _showMenu;
        private bool _oneHitKill;
        private bool _infiniteSoul;
        private bool _godMode;
        private bool _godWasOn;
        private bool _f1Held;
        private bool _stylesReady;
        private Rect _window = new Rect(40, 40, FrameWidth, FrameHeight);
        private Texture2D _frame;
        private GUIStyle _title;
        private GUIStyle _toggle;
        private GUIStyle _button;
        private GUIStyle _note;
        private GUIStyle _empty;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private void Awake()
        {
            Logger.LogInfo("HK Mod Menu 1.7.1 loaded");
            WriteStatus("1.7.1 awake");
            LoadFrame();

            var host = new GameObject("HKMenuHost");
            DontDestroyOnLoad(host);
            host.AddComponent<MenuHost>().Owner = this;
        }

        private void Update()
        {
            bool f1 = (GetAsyncKeyState(0x70) & 0x8000) != 0; // F1
            if (f1 && !_f1Held)
            {
                _showMenu = !_showMenu;
                Cursor.visible = true;
                Cursor.lockState = _showMenu ? CursorLockMode.None : CursorLockMode.Locked;
                WriteStatus(_showMenu ? "opened by F1" : "closed by F1");
            }
            _f1Held = f1;

            try
            {
                if (_infiniteSoul && HeroController.instance != null)
                    HeroController.instance.AddMPCharge(99);

                ApplyGodMode();

                if (_oneHitKill)
                    WeakenEnemies();
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Cheat tick failed: " + ex.Message);
            }
        }

        private void OnGUI()
        {
            DrawMenu();
        }

        internal void DrawMenu()
        {
            var hint = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            hint.normal.textColor = Color.yellow;
            GUI.Label(new Rect(16, 16, 460, 30), _showMenu ? "HK MENU OPEN   [F1]" : "HK MENU   [F1]", hint);

            if (!_showMenu) return;

            InitStyles();
            _window = GUI.Window(914, _window, DrawWindow, "", _empty);
        }

        private void DrawWindow(int id)
        {
            if (_frame != null)
                GUI.DrawTexture(new Rect(0, 0, FrameWidth, FrameHeight), _frame, ScaleMode.StretchToFill, true);

            GUILayout.BeginArea(new Rect(105, 100, FrameWidth - 210, FrameHeight - 160));
            GUILayout.Label("HK TRAINER 1.7.1", _title);
            GUILayout.Space(6);

            _oneHitKill = GUILayout.Toggle(_oneHitKill, " One Hit Kill", _toggle);
            _infiniteSoul = GUILayout.Toggle(_infiniteSoul, " Infinite Soul", _toggle);
            _godMode = GUILayout.Toggle(_godMode, " God Mode", _toggle);

            GUILayout.Space(8);
            if (GUILayout.Button("+1,000 Geo", _button, GUILayout.Height(28)))
            {
                if (HeroController.instance != null)
                    HeroController.instance.AddGeo(1000);
            }

            GUILayout.Space(8);
            GUILayout.Label("[F1] to close", _note);
            GUILayout.EndArea();

            GUI.DragWindow(new Rect(0, 0, FrameWidth, 90));
        }

        private bool _loggedGodMode;
        private void ApplyGodMode()
        {
            var hero = HeroController.instance;
            if (hero == null) return;

            if (_godMode)
            {
                if (!SetMember(hero, "damageMode", "NO_DAMAGE") && !_loggedGodMode)
                {
                    _loggedGodMode = true;
                    Logger.LogWarning("damageMode field was not found. Health lock is still on.");
                    WriteStatus("godmode: damageMode missing, health lock only");
                }

                if (hero.cState != null)
                {
                    hero.cState.invulnerable = true;
                    hero.cState.recoiling = false;
                    hero.cState.recoilFrozen = false;
                }

                SetBool(hero, "isInvincible", true);

                if (PlayerData.instance != null)
                {
                    SetBool(PlayerData.instance, "isInvincible", true);
                    if (PlayerData.instance.maxHealth > 0)
                        PlayerData.instance.health = PlayerData.instance.maxHealth;
                }

                _godWasOn = true;
            }
            else if (_godWasOn)
            {
                SetMember(hero, "damageMode", "FULL_DAMAGE");
                SetBool(hero, "isInvincible", false);

                if (PlayerData.instance != null)
                    SetBool(PlayerData.instance, "isInvincible", false);

                if (hero.cState != null)
                    hero.cState.invulnerable = false;

                _godWasOn = false;
            }
        }

        private static bool SetMember(object obj, string name, string enumName)
        {
            if (obj == null) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = obj.GetType().GetField(name, flags);
            if (field == null) return false;

            try
            {
                if (field.FieldType.IsEnum)
                    field.SetValue(obj, Enum.Parse(field.FieldType, enumName));
                else
                    field.SetValue(obj, 2);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SetBool(object obj, string name, bool value)
        {
            if (obj == null) return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = obj.GetType().GetField(name, flags);
            if (field == null || field.FieldType != typeof(bool)) return;

            try { field.SetValue(obj, value); }
            catch { }
        }

        private static void WeakenEnemies()
        {
            HealthManager[] targets = UnityEngine.Object.FindObjectsOfType<HealthManager>();
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null && !targets[i].isDead && targets[i].hp > 1)
                    targets[i].hp = 1;
            }
        }

        private void LoadFrame()
        {
            string path = Path.Combine(Paths.PluginPath, "menu_frame.png");
            if (!File.Exists(path)) return;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (LoadPng(tex, File.ReadAllBytes(path)))
                _frame = tex;
        }

        private static bool LoadPng(Texture2D tex, byte[] data)
        {
            try
            {
                var load = typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
                if (load != null)
                    return (bool)load.Invoke(null, new object[] { tex, data, false });
            }
            catch { }
            return false;
        }

        private void InitStyles()
        {
            if (_stylesReady) return;

            _empty = new GUIStyle();
            _title = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = Color.white;

            _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 12 };
            _toggle.normal.textColor = new Color(0.8f, 0.8f, 0.85f);
            _toggle.onNormal.textColor = Color.white;

            _button = new GUIStyle(GUI.skin.button) { fontSize = 12 };

            _note = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _note.normal.textColor = new Color(0.7f, 0.7f, 0.75f);

            _stylesReady = true;
        }

        private static void WriteStatus(string line)
        {
            try
            {
                File.AppendAllText(Path.Combine(Paths.PluginPath, "hkmenu-status.txt"),
                    DateTime.Now.ToString("HH:mm:ss") + " " + line + Environment.NewLine);
            }
            catch { }
        }

        private sealed class MenuHost : MonoBehaviour
        {
            internal HKModMenu Owner;

            private void OnGUI()
            {
                if (Owner != null)
                    Owner.DrawMenu();
            }
        }
    }
}
