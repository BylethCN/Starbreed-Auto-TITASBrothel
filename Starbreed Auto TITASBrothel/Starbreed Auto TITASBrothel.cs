using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace StarbreedAutoTITASBrothel
{
    [BepInPlugin("com.yourname.starbreed.titasbrothel", "Starbreed Auto TITAS Brothel", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;

        public static ConfigEntry<bool> EnableRoom1;
        public static ConfigEntry<bool> EnableRoom2;
        public static ConfigEntry<bool> EnableRoom3;
        public static ConfigEntry<bool> InfiniteStamina;

        public static void Log(string msg)
        {
            if (Instance != null) Instance.Logger.LogInfo(msg);
        }

        private void Awake()
        {
            Instance = this;

            EnableRoom1 = Config.Bind("妓院小游戏", "房间1_速度词汇", true, "");
            EnableRoom2 = Config.Bind("妓院小游戏", "房间2_多人速度", true, "");
            EnableRoom3 = Config.Bind("妓院小游戏", "房间3_挤牛奶", true, "");
            InfiniteStamina = Config.Bind("妓院小游戏", "无限耐力", false, "");

            Logger.LogInfo("Starbreed Auto TITAS Brothel 已加载，版本 1.0.0");
        }

        private static float _staminaCheckTimer = 0f;

        private void Update()
        {
            if (Time.timeScale == 0f) return;

            if (InfiniteStamina.Value && SaveData.instance != null)
            {
                SaveData.instance.stamina = 100f;
            }

            // -------- 房间 2/3：每 3 秒检测一次耐力 --------
            if (!InfiniteStamina.Value && SaveData.instance != null)
            {
                _staminaCheckTimer += Time.deltaTime;
                if (_staminaCheckTimer >= 3.0f)
                {
                    _staminaCheckTimer = 0f;

                    if (SaveData.instance.stamina <= 0f)
                    {
                        var cs2 = GameObject.FindObjectOfType<ClientStatus2>();
                        var hub3 = GameObject.FindObjectOfType<Brothel3Mechanics>();
                        if (cs2 != null || hub3 != null)
                        {
                            TryClickEndButton();
                        }
                    }
                }
            }

            // -------- 房间 1：姿势界面时检测耐力 --------
            if (!InfiniteStamina.Value && SaveData.instance != null && SaveData.instance.stamina <= 0f)
            {
                var doggy = FindButtonByPath("Main Camera/Canvas/ACtiveUI/ProgressButtons/Stage1/Doggy/Button");
                bool inRoom1Stage1 = doggy != null && doggy.gameObject.activeInHierarchy;

                var cs = GameObject.FindObjectOfType<ClientStatus>();
                bool inRoom1Client = cs != null && cs.speedButtons != null && cs.speedButtons.activeInHierarchy;

                if (inRoom1Stage1 && !inRoom1Client)
                {
                    TryClickEndButton();
                }
            }

            if (EnableRoom1.Value)
            {
                AutoRoom1Flow();
                AutoRoom1Tick();
            }

            if (EnableRoom2.Value) AutoRoom2Tick();

            if (EnableRoom3.Value)
            {
                var hub = GameObject.FindObjectOfType<Brothel3Mechanics>();
                if (hub != null) AutoMilkingTick(hub);
            }
        }

        // ============================================================
        // 结束按钮
        // ============================================================
        private static bool _endClicked = false;

        private static void TryClickEndButton()
        {
            if (_endClicked) return;

            string[] paths = {
                "Main Camera/Canvas/ACtiveUI/Other/EndButton/Button",  // 房间 2
                "Main Camera/Canvas/ACitveUI/EndButton/Button",        // 房间 3
                "Main Camera/Canvas/ACtiveUI/End/Button",              // 房间 1
            };

            foreach (var p in paths)
            {
                var endBtn = FindButtonByPath(p);
                if (endBtn != null && endBtn.interactable)
                {
                    endBtn.onClick.Invoke();
                    _endClicked = true;
                    Log($"[Brothel] 耐力耗尽，已点结束: {p}");
                    return;
                }
            }
        }

        // ============================================================
        // 房间 1 流程
        // ============================================================
        private static bool _room1PoseSelected = false;
        private static float _room1PoseSelectTime = 0f;
        private static float _room1StartBtnFirstSeen = -1f;

        private static void AutoRoom1Flow()
        {
            var startBtn = FindButtonByPath("Main Camera/Canvas/ACtiveUI/ProgressButtons/Intro/Panel/StartButton/Button");
            if (startBtn != null && startBtn.gameObject.activeInHierarchy && startBtn.interactable)
            {
                if (_room1StartBtnFirstSeen < 0f)
                    _room1StartBtnFirstSeen = Time.time;

                if (Time.time - _room1StartBtnFirstSeen >= 1.0f)
                {
                    startBtn.onClick.Invoke();
                    _room1PoseSelected = false;
                    _room1StartBtnFirstSeen = -1f;
                    return;
                }
            }
            else
            {
                _room1StartBtnFirstSeen = -1f;
            }

            var doggy = FindButtonByPath("Main Camera/Canvas/ACtiveUI/ProgressButtons/Stage1/Doggy/Button");
            var missionary = FindButtonByPath("Main Camera/Canvas/ACtiveUI/ProgressButtons/Stage1/Missionary/Button");
            var bj = FindButtonByPath("Main Camera/Canvas/ACtiveUI/ProgressButtons/Stage1/BJ/Button");

            bool inStage1 = doggy != null && doggy.gameObject.activeInHierarchy;

            if (inStage1 && !_room1PoseSelected)
            {
                var poses = new List<Button>();
                if (doggy != null && doggy.interactable) poses.Add(doggy);
                if (missionary != null && missionary.interactable) poses.Add(missionary);
                if (bj != null && bj.interactable) poses.Add(bj);

                if (poses.Count > 0)
                {
                    var pick = poses[Random.Range(0, poses.Count)];
                    pick.onClick.Invoke();
                    _room1PoseSelected = true;
                    _room1PoseSelectTime = Time.time;
                }
                return;
            }

            var next = FindButtonByPath("Main Camera/Canvas/ACtiveUI/ProgressButtons/Stage1/HsceneButton");
            if (next != null && next.gameObject.activeInHierarchy && next.interactable
                && _room1PoseSelected && (Time.time - _room1PoseSelectTime >= 1.0f))
            {
                next.onClick.Invoke();
                _room1PoseSelected = false;
            }
        }

        // ============================================================
        // 房间 1：单人速度 + 词汇
        // ============================================================
        private static Dictionary<ClientStatus, float> _wordWindowStartTime = new Dictionary<ClientStatus, float>();

        private static void AutoRoom1Tick()
        {
            var allCs = Resources.FindObjectsOfTypeAll<ClientStatus>();
            foreach (var cs in allCs)
            {
                if (cs == null) continue;
                var traverse = Traverse.Create(cs);

                bool speedActive = cs.speedButtons != null && cs.speedButtons.activeInHierarchy;
                if (speedActive)
                {
                    int requested = traverse.Field("requestedState").GetValue<int>();
                    int current = traverse.Field("state").GetValue<int>();
                    if (requested != current)
                    {
                        ClickSpeedButton(cs, requested);
                        traverse.Field("state").SetValue(requested);
                    }
                }

                bool wordWindow = traverse.Field("wordWindow").GetValue<bool>();
                if (wordWindow)
                {
                    if (!_wordWindowStartTime.ContainsKey(cs))
                        _wordWindowStartTime[cs] = Time.time;

                    if (Time.time - _wordWindowStartTime[cs] >= 1.0f)
                    {
                        cs.WordCheck(cs.curDemeanor);
                        _wordWindowStartTime.Remove(cs);
                    }
                }
                else
                {
                    if (_wordWindowStartTime.ContainsKey(cs))
                        _wordWindowStartTime.Remove(cs);
                }
            }
        }

        private static void ClickSpeedButton(ClientStatus cs, int requestedState)
        {
            if (cs.speedButtons == null) return;

            string childName;
            switch (requestedState)
            {
                case 0: childName = "Stop"; break;
                case 1: childName = "Slow"; break;
                case 2: childName = "Fast"; break;
                default: return;
            }

            var buttons = cs.speedButtons.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b.gameObject.name.StartsWith(childName))
                {
                    if (b.interactable) b.onClick.Invoke();
                    return;
                }
            }
        }

        // ============================================================
        // 房间 2
        // ============================================================
        private static float _room2StartBtnFirstSeen = -1f;

        private static void AutoRoom2Tick()
        {
            TryClickRoom2Start();

            var allCs2 = Resources.FindObjectsOfTypeAll<ClientStatus2>();

            foreach (var cs2 in allCs2)
            {
                if (cs2 == null) continue;

                var traverse = Traverse.Create(cs2);
                var stateList = traverse.Field("state").GetValue<List<int>>();
                var requestedList = traverse.Field("requestedState").GetValue<List<int>>();

                if (stateList == null || requestedList == null) continue;

                int count = Mathf.Min(stateList.Count, requestedList.Count);
                for (int i = 0; i < count; i++)
                {
                    if (stateList[i] != requestedList[i])
                    {
                        ClickRoom2Button(i, requestedList[i]);
                    }
                }
            }
        }

        private static void TryClickRoom2Start()
        {
            var startBtn = FindButtonByPath("Main Camera/Canvas/ACtiveUI/Other/ProgressButtons/Intro/Panel/StartButton/Button");
            if (startBtn == null)
                startBtn = FindButtonByPath("Main Camera/Canvas/ACtiveUI/ProgressButtons/Intro/Panel/StartButton/Button");

            if (startBtn != null && startBtn.gameObject.activeInHierarchy && startBtn.interactable)
            {
                if (_room2StartBtnFirstSeen < 0f)
                    _room2StartBtnFirstSeen = Time.time;

                if (Time.time - _room2StartBtnFirstSeen >= 1.0f)
                {
                    startBtn.onClick.Invoke();
                    _room2StartBtnFirstSeen = -1f;
                }
            }
            else
            {
                _room2StartBtnFirstSeen = -1f;
            }
        }

        private static readonly string[] Room2Paths = {
            "Main Camera/Canvas/ACtiveUI/Other/ProgressButtons/Stage1/LHand/",
            "Main Camera/Canvas/ACtiveUI/Other/ProgressButtons/Stage1/RHand/",
            "Main Camera/Canvas/ACtiveUI/Other/ProgressButtons/Stage1/Dildo/",
        };

        private static readonly string[] Room2ButtonNames = { null, "SlowButton", "MidButton", "FastButton" };

        private static void ClickRoom2Button(int customerIndex, int requestedState)
        {
            if (customerIndex < 0 || customerIndex >= Room2Paths.Length) return;
            if (requestedState < 1 || requestedState > 3) return;

            string basePath = Room2Paths[customerIndex];
            string btnNamePrefix = Room2ButtonNames[requestedState];

            var allButtons = Resources.FindObjectsOfTypeAll<Button>();
            foreach (var b in allButtons)
            {
                if (b == null) continue;
                string path = GetPath(b.transform);
                if (path.StartsWith(basePath) && b.gameObject.name.StartsWith(btnNamePrefix))
                {
                    b.onClick.Invoke();
                    return;
                }
            }
        }

        // ============================================================
        // 工具
        // ============================================================
        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private static Button FindButtonByPath(string path)
        {
            var allButtons = Resources.FindObjectsOfTypeAll<Button>();
            foreach (var b in allButtons)
            {
                if (b == null) continue;
                if (GetPath(b.transform) == path) return b;
            }
            return null;
        }

        // ============================================================
        // 房间 3
        // ============================================================
        private static HoldButton _currentHeld = null;
        private static Aether3CustomerRoll _currentCustomer = null;
        private static float _room3StartBtnFirstSeen = -1f;

        private static void AutoMilkingTick(Brothel3Mechanics hub)
        {
            TryClickRoom3Start();

            var allCustomers = GameObject.FindObjectsOfType<Aether3CustomerRoll>();
            var activeCustomers = new List<Aether3CustomerRoll>();
            foreach (var c in allCustomers)
            {
                if (c == null) continue;
                var t = Traverse.Create(c);
                bool requesting = t.Field("requesting").GetValue<bool>();
                bool sellActive = c.SellButton != null && c.SellButton.gameObject.activeInHierarchy;
                if (requesting && sellActive) activeCustomers.Add(c);
            }

            if (_currentCustomer == null || !activeCustomers.Contains(_currentCustomer))
            {
                Aether3CustomerRoll next = null;
                int bestSlot = int.MaxValue;
                foreach (var c in activeCustomers)
                {
                    if (c.customerSlot < bestSlot)
                    {
                        bestSlot = c.customerSlot;
                        next = c;
                    }
                }
                _currentCustomer = next;
            }

            if (_currentCustomer == null)
            {
                ReleaseCurrentHeld();
                return;
            }

            var ct = Traverse.Create(_currentCustomer);
            float targetAether = ct.Field("aetherAmt").GetValue<float>();

            float curAether = hub.currentAetherCum;
            float curRooster = hub.currentRoosterCum;
            float total = curAether + curRooster;
            float maxCum = hub.maxMugCum;

            if (_currentCustomer.SellButton.interactable)
            {
                ReleaseCurrentHeld();
                _currentCustomer.SellButton.onClick.Invoke();
                _currentCustomer = null;
                return;
            }

            bool full = total >= maxCum - 0.01f;
            HoldButton wantHold = null;

            if (!full)
            {
                if (curAether < targetAether)
                    wantHold = hub.aetherFastState;
                else
                    wantHold = hub.roosterFastState;
            }
            else
            {
                float diff = curAether - targetAether;
                if (diff < -0.02f)
                    wantHold = hub.aetherButtonState;
                else if (diff > 0.02f)
                    wantHold = hub.roosterButtonState;
                else
                    wantHold = null;
            }

            if (wantHold != _currentHeld)
            {
                ReleaseCurrentHeld();
                if (wantHold != null)
                    Traverse.Create(wantHold).Method("HoldItem").GetValue();
                _currentHeld = wantHold;
            }
        }

        private static void TryClickRoom3Start()
        {
            var startBtn = FindButtonByPath("Main Camera/Canvas/ACtiveUI/Other/ProgressButtons/Intro/Panel/StartButton/Button");
            if (startBtn == null)
                startBtn = FindButtonByPath("Main Camera/Canvas/ACtiveUI/ProgressButtons/Intro/Panel/StartButton/Button");

            if (startBtn != null && startBtn.gameObject.activeInHierarchy && startBtn.interactable)
            {
                if (_room3StartBtnFirstSeen < 0f)
                    _room3StartBtnFirstSeen = Time.time;

                if (Time.time - _room3StartBtnFirstSeen >= 1.0f)
                {
                    startBtn.onClick.Invoke();
                    _room3StartBtnFirstSeen = -1f;
                }
            }
            else
            {
                _room3StartBtnFirstSeen = -1f;
            }
        }

        private static void ReleaseCurrentHeld()
        {
            if (_currentHeld != null)
            {
                Traverse.Create(_currentHeld).Method("ReleaseItem").GetValue();
                _currentHeld = null;
            }
        }
    }
}