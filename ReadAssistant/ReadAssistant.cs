using Harmony12;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace ReadAssistant
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool autoRead = true;
        public bool just50Hard = true;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }

    static class Main
    {
        public static bool enabled;
        public static int readNum = 0;
        public static Settings settings;
        public static int wen = 0;      // 已“温”次数
        public static int wenMax = 0;   // 最大“温”次数

        public static void GetReadNum()
        {//计算已读页数
            int bookId = int.Parse(DateFile.instance.GetItemDate(BuildingWindow.instance.readBookId, 32, true));
            int[] bookPages = (BuildingWindow.instance.studySkillTyp != 17)
                ? ((!DateFile.instance.skillBookPages.ContainsKey(bookId)) ? new int[10] : DateFile.instance.skillBookPages[bookId])
                : ((!DateFile.instance.gongFaBookPages.ContainsKey(bookId)) ? new int[10] : DateFile.instance.gongFaBookPages[bookId]);
            Main.readNum = 0;
            for (int i = 0; i < 10; i++)
            {
                if (bookPages[i] != 0)
                {
                    Main.readNum++;
                }
            }
        }

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            settings = Settings.Load<Settings>(modEntry);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginVertical("Box");
            settings.autoRead = GUILayout.Toggle(settings.autoRead, "<color=#008cf7>已读书籍自动刷历练</color>");
            settings.just50Hard = GUILayout.Toggle(settings.just50Hard, "<color=#008cf7>仅对50%研读难度的书籍生效</color>");
            GUILayout.Label("说明： ");
            GUILayout.Label("仅对已读完的书籍有效，最低悟性要求是上限足够放一个【温故知新】。");
            GUILayout.Label("根据玩家初始耐心选取最优刷历练方案并自动实施，刷历练期间请不要手动释放技能。");
            GUILayout.Label("研读难度较高的书籍历练收益可能低于预期甚至低于低品书。");
            GUILayout.Label("刷历练方案待优化……");
            GUILayout.EndVertical();
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }
    }

    [HarmonyPatch(typeof(ReadBook), "ShowReadBookWindow")]
    static class ReadBook_ShowReadBookWindow_Patch
    {
        static void Postfix()
        {
            if (!Main.enabled)
                return;

            Main.GetReadNum();

            if (Main.readNum == 10)
            {
                int maxPatience = ReadBook.instance.GetMaxPatience();
                Main.wenMax = (int)Math.Ceiling(Math.Log(maxPatience, 2));
                Main.wen = 0;
            }
        }
    }

    [HarmonyPatch(typeof(ReadBook), "UpdateRead")]
    static class ReadBook_UpdateRead_Patch
    {
        static void Postfix()
        {
            if (!Main.enabled)
                return;

            Main.GetReadNum();

            Type type = ReadBook.instance.GetType();
            int readPageIndex = (int)type.InvokeMember("readPageIndex", BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, ReadBook.instance, null);
            int readLevel = (int)type.InvokeMember("readLevel", BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, ReadBook.instance, null);
            int canUseInt = (int)type.InvokeMember("canUseInt", BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, ReadBook.instance, null);

            int actorValue = (int)type.InvokeMember("actorValue", BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, ReadBook.instance, null);
            int readSkillId = int.Parse(DateFile.instance.GetItemDate(BuildingWindow.instance.readBookId, 32, true));
            int needInt = BuildingWindow.instance.GetNeedInt(actorValue, readSkillId);

            if (Main.readNum == 10)
            {
                int cost3 = int.Parse(DateFile.instance.readBookDate[3][1]) * needInt / 100;
                int cost6 = int.Parse(DateFile.instance.readBookDate[6][1]) * needInt / 100;
                if (!Main.settings.autoRead || (Main.settings.just50Hard && needInt > 50) || DateFile.instance.BaseAttr(DateFile.instance.mianActorId, 4, 0) < cost6)
                    return;

                int patience = (int)type.InvokeMember("patience", BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, ReadBook.instance, null);
                List<int[]> pageState = (List<int[]>)type.InvokeMember("pageState", BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, ReadBook.instance, null);

                if (readLevel == 0 || readLevel == 50)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        canUseInt = (int)type.InvokeMember("canUseInt", BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, ReadBook.instance, null);
                        if (readPageIndex * 3 + i < 30 - Main.wenMax && pageState[readPageIndex][i] == 0 && canUseInt >= cost3)
                        {
                            ReadBook.instance.UseIntPower(3);
                            pageState[readPageIndex][i] = 3;
                            canUseInt = (int)type.InvokeMember("canUseInt", BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, ReadBook.instance, null);
                        }
                    }
                    if (Main.wen < Main.wenMax && readPageIndex == (9 - (Main.wen / 3)))
                    {
                        ReadBook.instance.UseIntPower(6);
                        Main.wen++;
                        canUseInt = (int)type.InvokeMember("canUseInt", BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, ReadBook.instance, null);
                    }
                }
            }
        }
    }
}