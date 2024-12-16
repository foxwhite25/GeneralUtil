using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using PotionCraft.Assemblies.AchievementsSystem;
using PotionCraft.Assemblies.DataBaseSystem.PreparedObjects;
using PotionCraft.DebugObjects.DebugWindows;
using PotionCraft.DebugObjects.DebugWindows.Buttons;
using PotionCraft.LocalizationSystem;
using PotionCraft.ManagersSystem;
using PotionCraft.ManagersSystem.Application;
using PotionCraft.ManagersSystem.Npc;
using PotionCraft.Npc.MonoBehaviourScripts;
using PotionCraft.ObjectBased;
using PotionCraft.ObjectBased.Garden;
using PotionCraft.ObjectBased.Haggle;
using PotionCraft.ObjectBased.InteractiveItem;
using PotionCraft.ObjectBased.Mortar;
using PotionCraft.ObjectBased.RecipeMap.RecipeMapItem.IndicatorMapItem;
using PotionCraft.ObjectBased.RecipeMap.RecipeMapItem.PotionEffectMapItem;
using PotionCraft.ObjectBased.RecipeMap.RecipeMapItem.SolventDirectionHint;
using PotionCraft.ObjectBased.RecipeMap.RecipeMapItem.VortexMapItem;
using PotionCraft.ObjectBased.UIElements;
using PotionCraft.ObjectBased.UIElements.Books.RecipeBook;
using PotionCraft.ObjectBased.UIElements.Dialogue;
using PotionCraft.SceneLoader;
using PotionCraft.ScriptableObjects;
using PotionCraft.ScriptableObjects.AlchemyMachineProducts;
using PotionCraft.ScriptableObjects.Ingredient;
using PotionCraft.ScriptableObjects.Potion;
using PotionCraft.ScriptableObjects.Salts;
using PotionCraft.Settings;
using TMPro;
using TooltipSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using static UnityEngine.Gizmos;
using Key = PotionCraft.LocalizationSystem.Key;

namespace GeneralUtil;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    private static ManualLogSource _logger;
    private const string GoldIcon = "<sprite=\"CommonAtlas\" name=\"Gold Icon\">";

    private static ConfigEntry<bool> EnableAutoHaggle;

    private static ConfigEntry<bool> EnableConsole;
    private static ConfigEntry<bool> EnableAchievement;

    private static ConfigEntry<bool> EnableSolventDirectionLine;
    private static ConfigEntry<bool> EnablePriceTooltips;
    private static ConfigEntry<bool> EnableNpcPotionTips;
    private static ConfigEntry<bool> EnablePotionTranslucent;
    private static ConfigEntry<bool> EnableGrindStatus;
    private static ConfigEntry<bool> EnablePotionStatus;
    private static ConfigEntry<bool> EnableRainbowPotion;
    private static ConfigEntry<double> RainbowPotionThreshold;
    private static ConfigEntry<bool> EnablePathClosestStatus;
    private static ConfigEntry<double> HarvestMultiplier;
    private static ConfigEntry<bool> TransparentDebugWindow;

    private static Sprite SolventDirectionLine;

    private static DebugWindow GrindStatusDebugWindow;
    private static DebugWindow PotionAngelDebugWindow;
    private static DebugWindow PotionIncludedAngelDebugWindow;
    private static DebugWindow PotionDistanceDebugWindow;
    private static DebugWindow PotionHealthDebugWindow;
    private static DebugWindow PathClosestDebugWindow;
    private static DebugWindow PathClosestPotionDebugWindow;

    private static Room _lab;

    private void Awake() {
        // Plugin startup logic
        _logger = Logger;
        Harmony.CreateAndPatchAll(typeof(Plugin));
        Harmony.CreateAndPatchAll(typeof(PlantGathererPatch));

        EnableAutoHaggle = Config.Bind("AutoHaggle", "Enable", true);

        EnableConsole = Config.Bind("DevConsole", "Enable", true);
        EnableAchievement = Config.Bind("DevConsole", "EnableAchievement", true);

        EnableSolventDirectionLine = Config.Bind("MoreInformation", "EnableSolventDirectionLine", true);
        EnablePriceTooltips = Config.Bind("MoreInformation", "EnablePriceTooltips", true);
        EnableNpcPotionTips = Config.Bind("MoreInformation", "EnableNPCPotionTips", true);
        EnablePotionTranslucent = Config.Bind("MoreInformation", "EnablePotionTranslucent", true);
        EnableGrindStatus = Config.Bind("MoreInformation", "EnableGrindStatus", true);

        EnablePotionStatus = Config.Bind("GeneralUtils", "EnablePotionStatus", true);
        EnableRainbowPotion = Config.Bind("GeneralUtils", "EnableRainbowPotion", true);
        RainbowPotionThreshold = Config.Bind("GeneralUtils", "RainbowPotionThreshold", 0.01);
        EnablePathClosestStatus = Config.Bind("GeneralUtils", "EnablePathClosestStatus", true);
        HarvestMultiplier = Config.Bind("GeneralUtils", "HarvestMultiplier", 1.0);
        TransparentDebugWindow = Config.Bind("GeneralUtils", "TransparentDebugWindow", false);
        
        LocalizationManager.OnInitialize.AddListener(SetModLocalization);
        LocalizationManager.OnLocaleChanged.AddListener(UpdateDebugLocal);
        SolventDirectionLine = LoadSprite("Solvent Direction Line.png");

        Print($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private static void Print(string s) {
        _logger.LogInfo(s);
    }

    private static void RegisterLoc(string key, string en, string zh) {
        for (var localeIndex = 0; localeIndex <= 13; ++localeIndex) {
            var data = AccessTools.StaticFieldRefAccess<LocalizationData>(typeof(LocalizationManager),
                "localizationData");
            data.Add(localeIndex, key, localeIndex == 9 ? zh : en);
        }
    }

    private static void UpdateDebugLocal() {
        foreach (var debugWindow in GetDebugWindows()) {
            if (debugWindow == null) {
                continue;
            }

            debugWindow.captionText.text = LocalizationManager.GetText(debugWindow.AppendableText);
        }
    }

    private static IEnumerable<DebugWindow> GetDebugWindows() {
        return new[] {
            GrindStatusDebugWindow,
            PotionAngelDebugWindow,
            PotionDistanceDebugWindow,
            PathClosestDebugWindow,
            PathClosestPotionDebugWindow,
        };
    }

    private static void SetModLocalization() {
        RegisterLoc("#mod_moreinformation_value", "Value", "价值");
        RegisterLoc("#mod_moreinformation_cost", "Cost", "成本");
        RegisterLoc("#mod_moreinformation_has", "Has", "已拥有");
        RegisterLoc("#mod_moreinformation_nothas", "<color=red>Items not owned, recommended</color>",
            "<color=red>未拥有，建议购入</color>");

        RegisterLoc("#mod_moreinformation_grind_status", "Grind Status", "研磨进度");
        RegisterLoc("#mod_moreinformation_angle", "Rotation", "旋转差");
        RegisterLoc("#mod_moreinformation_included_angle", "Included Angle", "夹角差");
        RegisterLoc("#mod_moreinformation_distance", "Distance", "距离差");
        RegisterLoc("#mod_moreinformation_health", "HP", "血量");
        RegisterLoc("#mod_moreinformation_path_closest", "Path closest distance to potion", "路径最近点离药水距离");
        RegisterLoc("#mod_moreinformation_potion", "Target Potion", "目标药水");
    }


    private static Texture2D LoadResTexture2D(string name) {
        foreach (var x1 in Assembly.GetExecutingAssembly().GetManifestResourceNames()) Print(x1);
        var manifestResourceStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("GeneralUtil." + name);
        if (manifestResourceStream == null) {
            Print("Resources \"" + "GeneralUtil." + name + "\" Not Found");
            return null;
        }

        var length = (int) manifestResourceStream.Length;
        var numArray = new byte[length];
        var _ = manifestResourceStream.Read(numArray, 0, length);
        manifestResourceStream.Close();
        var tex = new Texture2D(2, 2);
        tex.LoadImage(numArray);
        return tex;
    }

    private static Sprite LoadSprite(string name) {
        var texture = LoadResTexture2D(name);
        return Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height),
            new Vector2(0.5f, 0.0f));
    }

    private static DebugWindow CreateClearDebugWindow(string title) {
        var clearDebugWindow = DebugWindow.Init(LocalizationManager.GetText(title), true);
        foreground_queue.Add(clearDebugWindow);
        clearDebugWindow.ToForeground();
        clearDebugWindow.AppendableText = title;
        clearDebugWindow.captionText.text = LocalizationManager.GetText(title);
        
        if (TransparentDebugWindow.Value) {
            clearDebugWindow.colliderBackground.enabled = false;
            clearDebugWindow.headTransform.gameObject.SetActive(false);
            clearDebugWindow.spriteScratches.gameObject.SetActive(false);
            clearDebugWindow.spriteBackground.color = Color.clear;
        }

        var rendererList = new List<Renderer>();
        rendererList.AddRange(clearDebugWindow.GetComponentsInChildren<SpriteRenderer>(true));
        rendererList.AddRange(clearDebugWindow.GetComponentsInChildren<TextMeshPro>()
            .Select(textMesh => textMesh.renderer));
        AccessTools.Field(typeof(Window), "_windowRenderers").SetValue(clearDebugWindow, rendererList.ToArray());
        return clearDebugWindow;
    }

    private static readonly List<DebugWindow> foreground_queue = new();

    [HarmonyPatch]
    private class PlantGathererPatch {
        public static MethodBase TargetMethod() {
            var type = AccessTools.TypeByName("PlantGatherer");
            return AccessTools.FirstMethod(type, method => method.Name.Contains("CalculateIngredientAmount"));
        }

        public static void Postfix(ref int __result) {
            __result = (int) (__result * HarvestMultiplier.Value);
        }
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Window), "ToForeground")]
    public static bool Window_ToForeground_Patch(Window __instance) {
        if (__instance is not DebugWindow dbg) {
            return true;
        }

        if (!foreground_queue.Contains(dbg)) return false;
        foreground_queue.Remove(dbg);
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MovableUIItem), "FixOutOfBoundsCase")]
    public static bool MovableUIItem_FixOutOfBoundsCase(MovableUIItem __instance) {
        return __instance is not DebugWindow;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HagglePointer), "Update")]
    public static void HagglePatch(HagglePointer __instance) {
        if (!EnableAutoHaggle.Value) return;
        if (HaggleWindow.Instance.IsPaused || __instance.State > 0 ||
            Managers.Trade.haggle.haggleCurrentBonuses == null || Managers.Trade.haggle.haggleCurrentBonuses.Count < 2)
            return;
        var flag = Managers.Trade.haggle.haggleCurrentBonuses
            .Any(
                currentBonus => currentBonus.haggleBonus.Position >= 0.1 &&
                                currentBonus.haggleBonus.Position <= 0.9 &&
                                Math.Abs(currentBonus.haggleBonus.Position - __instance.Position) <=
                                currentBonus.size / 2.0
            );
        if (!flag)
            return;
        HaggleWindow.Instance.bargainButton.OnButtonClicked();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CloseButton), "Start")]
    public static void CloseButtonStartPatch(CloseButton __instance) {
        __instance.gameObject.SetActive(false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MinimizeButton), "Start")]
    public static void MinimizeButtonStartPatch(CloseButton __instance) {
        __instance.transform.localPosition += new Vector3(0.4f, 0f, 0f);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SolventDirectionHint), "Awake")]
    public static void SolventDirectionHint_Awake_Patch(SolventDirectionHint __instance) {
        if (!EnableSolventDirectionLine.Value)
            return;
        var renderer =
            (SpriteRenderer) AccessTools.Field(typeof(SolventDirectionHint), "spriteRenderer").GetValue(__instance);
        renderer.sprite = SolventDirectionLine;
    }

    private static string GetPriceString(InventoryItem item, int count = 1) {
        return GoldIcon + " " + (item.GetPrice() * count).ToString("0.##");
    }

    private static void AddNormalPriceTooltip(
        TooltipContent tooltip,
        InventoryItem item,
        bool notHasTip = false) {
        var tooltipContent1 = tooltip;
        tooltipContent1.description2 = tooltipContent1.description2 +
                                       LocalizationManager.GetText("#mod_moreinformation_value") + "\t " +
                                       GetPriceString(item);
        var itemCount = Managers.Player.Inventory.GetItemCount(item);
        if (itemCount > 0) {
            var tooltipContent2 = tooltip;
            tooltipContent2.description2 = tooltipContent2.description2 +
                                           $"\n{LocalizationManager.GetText("#mod_moreinformation_has")} {itemCount}\t " +
                                           GetPriceString(item, itemCount);
        }
        else if (notHasTip) {
            var tooltipContent3 = tooltip;
            tooltipContent3.description2 = tooltipContent3.description2 + "\n" +
                                           LocalizationManager.GetText("#mod_moreinformation_nothas");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Ingredient), "GetTooltipContent")]
    public static void Ingredient_GetTooltipContent_Patch(
        Ingredient __instance,
        ref TooltipContent __result) {
        if (!EnablePriceTooltips.Value)
            return;
        AddNormalPriceTooltip(__result, __instance, true);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Potion), "GetTooltipContent")]
    public static void Potion_GetTooltipContent_Patch(
        Potion __instance,
        ref TooltipContent __result) {
        if (!EnablePriceTooltips.Value)
            return;
        AddNormalPriceTooltip(__result, __instance);
        var num = 0.0f;
        foreach (var usedComponent in __instance.usedComponents.GetSummaryComponents())
            if (usedComponent.Type == 0)
                num += ((InventoryItem) usedComponent.Component).GetPrice() * usedComponent.Amount;

        var tooltipContent = __result;
        tooltipContent.description2 = tooltipContent.description2 + "\n" +
                                      LocalizationManager.GetText("#mod_moreinformation_cost") + "\t " + GoldIcon +
                                      " " + num.ToString("0.##");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Salt), "GetTooltipContent")]
    public static void Salt_GetTooltipContent_Patch(Salt __instance, ref TooltipContent __result) {
        if (!EnablePriceTooltips.Value)
            return;
        AddNormalPriceTooltip(__result, __instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LegendarySaltPile), "GetTooltipContent")]
    public static void LegendarySaltPile_GetTooltipContent_Patch(
        LegendarySaltPile __instance,
        ref TooltipContent __result) {
        if (!EnablePriceTooltips.Value)
            return;
        AddNormalPriceTooltip(__result, __instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LegendarySubstance), "GetTooltipContent")]
    public static void LegendarySubstance_GetTooltipContent_Patch(
        LegendarySubstance __instance,
        ref TooltipContent __result) {
        if (!EnablePriceTooltips.Value)
            return;
        AddNormalPriceTooltip(__result, __instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DialogueBox), "UpdatePotionRequestText")]
    public static void DialogueBox_UpdatePotionRequestText_Patch(DialogueBox __instance) {
        if (!EnableNpcPotionTips.Value)
            return;
        var npcMono = (NpcMonoBehaviour) AccessTools.Field(typeof(NpcManager), "currentNpcMonoBehaviour")
            .GetValue(Managers.Npc);
        var currentQuest = npcMono.currentQuest;
        if (currentQuest == null) return;
        var str1 = currentQuest.desiredEffects.Aggregate("",
            (current, desiredEffect) => current + new Key("#effect_" + desiredEffect.name).GetText() + " ");
        var text = (TextMeshPro) AccessTools.Field(typeof(DialogueText), "text").GetValue(__instance.dialogueText);
        var str2 = text.text + "<color=#a39278>" + str1 + "</color>";
        text.text = str2;
        var sizeDelta = text.rectTransform.sizeDelta;
        sizeDelta = new Vector2(
            sizeDelta.x,
            sizeDelta.y + 0.3f);
        text.rectTransform.sizeDelta = sizeDelta;
        text.DeleteAllSubMeshes();
        __instance.dialogueText.seamlessWindowSkin.UpdateSize(text.rectTransform.sizeDelta);
        __instance.dialogueText.transform.localPosition =
            (__instance.dialogueText.minTextBoxY + 0.5f * text.rectTransform.sizeDelta.y) * Vector2.up;
    }

    private static void UpdatePotionStatus(IndicatorMapItem mapItem) {
        try {
            InitWindows(ref PotionAngelDebugWindow, "#mod_moreinformation_angle", new Vector3(-12f, -3.1f, -100.0f));
            InitWindows(ref PotionIncludedAngelDebugWindow, "#mod_moreinformation_included_angle",
                new Vector3(-12f, -4.3f, -100.0f));
            InitWindows(ref PotionDistanceDebugWindow, "#mod_moreinformation_distance", new Vector3(-12f, -5.5f, 0.0f));
            InitWindows(ref PotionHealthDebugWindow, "#mod_moreinformation_health", new Vector3(2f, 0.4f, 0.0f));
        }
        catch (Exception e) {
            Print(e.ToString());
            return;
        }

        var health = (float) AccessTools.Field(typeof(IndicatorMapItem), "health").GetValue(mapItem);
        PotionHealthDebugWindow.ShowText((health * 100f).ToString("f2") + "%");

        var indicatorContainer = Managers.RecipeMap.recipeMapObject.indicatorContainer;
        if (Managers.RecipeMap.currentPotionEffectMapItem == null) {
            PotionDistanceDebugWindow.ShowText("");
            PotionAngelDebugWindow.ShowText("");
            PotionIncludedAngelDebugWindow.ShowText("");
            return;
        }

        var transform = Managers.RecipeMap.currentPotionEffectMapItem.transform;
        var localPosition = indicatorContainer.localPosition;
        var position = transform.localPosition;
        var distance = Vector2.Distance(localPosition, position);
        var angle = Mathf.Abs(Mathf.DeltaAngle(Managers.RecipeMap.indicatorRotation.Value, transform.eulerAngles.z));

        Vector2 indicatorPosition = localPosition;
        Vector2 targetPosition = position;
        var origin = Vector2.zero;

        var fromOriginToIndicator = indicatorPosition - origin;
        var fromOriginToTarget = targetPosition - origin;

        var includedAngle = Vector2.Angle(fromOriginToIndicator, fromOriginToTarget);

        PotionDistanceDebugWindow.ShowText(distance.ToString("f4"));
        PotionAngelDebugWindow.ShowText(angle.ToString("f4"));
        PotionIncludedAngelDebugWindow.ShowText(includedAngle.ToString("f4"));
    }

    private static void UpdatePathStatus() {
        try {
            InitWindows(ref PathClosestDebugWindow, "#mod_moreinformation_path_closest", new Vector3(2f, -0.8f, 0.0f));
            InitWindows(ref PathClosestPotionDebugWindow, "#mod_moreinformation_potion", new Vector3(2f, -2.0f, 0.0f));
        }
        catch (Exception e) {
            Print(e.ToString());
            return;
        }

        var targets = GetNearestEffects();
        var path = Managers.RecipeMap.path.fixedPathHints;

        var minDistance = float.MaxValue;
        PotionEffectMapItem closestPoint = null;

        foreach (var point in path.Select(fixedHint => fixedHint.evenlySpacedPointsFixedPhysics.points)
                     .SelectMany(points => points)) {
            foreach (var potionEffectMapItem in targets) {
                if (Managers.RecipeMap.currentMap.referencesContainer.transform == null) {
                    break;
                }

                var fixed_point =
                    Managers.RecipeMap.currentMap.referencesContainer.transform.InverseTransformPoint(
                        Managers.RecipeMap.path.thisTransform.TransformPoint(point));

                var distance = Vector2.Distance(fixed_point, potionEffectMapItem.transform.localPosition);
                if (!(distance < minDistance)) continue;
                minDistance = distance;
                closestPoint = potionEffectMapItem;
            }
        }

        PathClosestDebugWindow.ShowText(minDistance < float.MaxValue ? minDistance.ToString("f4") : "");
        PathClosestPotionDebugWindow.ShowText(closestPoint != null ? closestPoint.Effect.GetLocalizedTitle() : "");
    }

    private static List<PotionEffectMapItem> GetNearestEffects() {
        Vector2 indicatorLocalPosition = Managers.RecipeMap.recipeMapObject.indicatorContainer.localPosition;
        var availableEffects = Managers.RecipeMap.currentMap.referencesContainer.potionEffectsOnMap
            .Where(effect => effect.Status != PotionEffectStatus.Collected);
        var nearestEffects = availableEffects
            .OrderBy(effect => ((Vector2) effect.thisTransform.localPosition - indicatorLocalPosition).sqrMagnitude)
            .ToList();
        return nearestEffects;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(IndicatorMapItem), "UpdateByCollection")]
    public static void IndicatorMapItem_UpdateByCollection_Patch(IndicatorMapItem __instance) {
        if (EnablePotionStatus.Value) {
            UpdatePotionStatus(__instance);
        }

        if (EnablePathClosestStatus.Value) {
            UpdatePathStatus();
        }

        if (!EnableRainbowPotion.Value) {
            return;
        }

        var indicatorContainer = Managers.RecipeMap.recipeMapObject.indicatorContainer;
        if (Managers.RecipeMap.currentPotionEffectMapItem == null) {
            return;
        }

        var transform = Managers.RecipeMap.currentPotionEffectMapItem.transform;
        var distance = Vector2.Distance(indicatorContainer.localPosition, transform.localPosition);
        if (distance > RainbowPotionThreshold.Value) {
            return;
        }

        var t = Mathf.PingPong(Time.time * 1.0f, 1);
        var color = Color.HSVToRGB(t, 1, 1);
        var animator = (LiquidColorChangeAnimator) AccessTools
            .Field(typeof(IndicatorMapItem), "liquidColorChangeAnimator")
            .GetValue(__instance);
        if (animator == null) {
            return;
        }

        var upper = (IndicatorLiquidSpritesContainer) AccessTools
            .Field(typeof(LiquidColorChangeAnimator), "upperContainer")
            .GetValue(animator);
        if (upper == null) {
            return;
        }

        var upper_renderer = (SpriteRenderer[]) AccessTools
            .Field(typeof(IndicatorLiquidSpritesContainer), "liquidRenderers")
            .GetValue(upper);
        if (upper_renderer == null) {
            return;
        }

        var lower = (IndicatorLiquidSpritesContainer) AccessTools
            .Field(typeof(LiquidColorChangeAnimator), "lowerContainer")
            .GetValue(animator);
        if (lower == null) {
            return;
        }

        var lower_renderer = (SpriteRenderer[]) AccessTools
            .Field(typeof(IndicatorLiquidSpritesContainer), "liquidRenderers")
            .GetValue(lower);
        if (lower_renderer == null) {
            return;
        }

        foreach (var t1 in upper_renderer) {
            if (t1 == null) {
                continue;
            }

            t1.color = color;
        }

        foreach (var t1 in lower_renderer) {
            if (t1 == null) {
                continue;
            }

            t1.color = color;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractiveItem), "Hover")]
    public static void InteractiveItem_UpdateHover_Patch(
        InteractiveItem __instance, bool hover) {
        if (!EnablePotionTranslucent.Value ||
            __instance is not IndicatorMapItem indicatorMapItem)
            return;
        var animator = (LiquidColorChangeAnimator) AccessTools
            .Field(typeof(IndicatorMapItem), "liquidColorChangeAnimator")
            .GetValue(indicatorMapItem);
        var upper = (IndicatorLiquidSpritesContainer) AccessTools
            .Field(typeof(LiquidColorChangeAnimator), "upperContainer")
            .GetValue(animator);
        var lower = (IndicatorLiquidSpritesContainer) AccessTools
            .Field(typeof(LiquidColorChangeAnimator), "lowerContainer")
            .GetValue(animator);
        if (hover) {
            indicatorMapItem.backgroundSpriteRenderer.enabled = false;
            upper.SetAlpha(0.1f);
            lower.SetAlpha(0.1f);
        }
        else {
            indicatorMapItem.backgroundSpriteRenderer.enabled = true;
            upper.SetAlpha(1f);
            lower.SetAlpha(1f);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Room), "Awake")]
    public static void Room_Awake_Patch(Room __instance) {
        if (__instance.roomIndex == RoomIndex.Laboratory) _lab = __instance;
    }

    private static void InitWindows(ref DebugWindow window, string title, Vector3 pos) {
        if (window != null || _lab == null) return;
        var transform = _lab.transform;

        window = CreateClearDebugWindow(title);
        Transform transform1;
        (transform1 = window.transform).SetParent(transform, false);
        transform1.localPosition = pos;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Mortar), "Update")]
    public static void Mortar_Update_Patch(Mortar __instance) {
        if (!EnableGrindStatus.Value)
            return;

        try {
            InitWindows(ref GrindStatusDebugWindow, "#mod_moreinformation_grind_status", new Vector3(4.5f, -5f, 0.0f));
        }
        catch (Exception e) {
            Print(e.ToString());
            return;
        }

        if (__instance.ContainedStack != null) {
            var num = Mathf.Clamp01(__instance.ContainedStack.overallGrindStatus);
            GrindStatusDebugWindow.ShowText((num * 100f).ToString("f2") + "%");
        }
        else {
            GrindStatusDebugWindow.ShowText("");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ObjectsLoader), "AddLast", typeof(string), typeof(Action))]
    private static void DebugManager_Awake_Patch(string name, ref Action action) {
        if (!EnableConsole.Value)
            return;

        if (name != "InitializeDeveloperMode") return;
        var tmp = action;
        action = () => {
            Settings<ApplicationManagerSettings>.Asset.developerModeOnStartInBuild = true;
            tmp();
        };
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AchievementsManager), "UnlockAchievement", typeof(Achievement), typeof(bool), typeof(bool))]
    private static void AchievementsManager_Unlock_Patch(ref bool isDeveloperMode) {
        if (!EnableAchievement.Value) return;
        isDeveloperMode = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AchievementsManager), "UnlockAchievement", typeof(string), typeof(bool), typeof(bool))]
    private static void AchievementsManager_Unlock_Patch2(ref bool isDeveloperMode) {
        if (!EnableAchievement.Value) return;
        isDeveloperMode = false;
    }
}