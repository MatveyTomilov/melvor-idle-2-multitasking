using System.Runtime.CompilerServices;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Components.Trackers;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.GameMenus.Footer;
using Godot;
using HarmonyLib;

namespace Multitasking;

internal sealed class FooterActionSelector
{
    private static readonly ConditionalWeakTable<GameFooter, FooterActionSelector> Selectors = new();

    private readonly GameFooter footer;
    private readonly ActiveActionManager manager;
    private readonly TrackerComponentFooterView trackerView;
    private readonly OptionButton dropdown;
    private ActionComponent[] options = [];
    private ActionComponent? pinnedAction;
    private ActionComponent? displayedPin;
    private TrackerComponent? passiveTracker;
    private bool optionsInitialized;
    private bool russian;

    private FooterActionSelector(GameFooter footer, ActiveActionManager manager, TrackerComponentFooterView trackerView, OptionButton period)
    {
        this.footer = footer;
        this.manager = manager;
        this.trackerView = trackerView;
        dropdown = new OptionButton
        {
            Name = "MultitaskingActionSelector",
            ThemeTypeVariation = period.ThemeTypeVariation,
            SizeFlagsVertical = period.SizeFlagsVertical,
            Visible = false,
        };
        dropdown.ItemSelected += SelectAction;
        Node row = period.GetParent();
        row.AddChild(dropdown);
        row.MoveChild(dropdown, period.GetIndex());
        RefreshOptions();
    }

    internal static void Patch(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(GameFooter), nameof(GameFooter.Bind)),
            postfix: new HarmonyMethod(typeof(FooterActionSelector), nameof(Attach)));
        harmony.Patch(AccessTools.Method(typeof(GameFooter), nameof(GameFooter.Unbind)),
            prefix: new HarmonyMethod(typeof(FooterActionSelector), nameof(Detach)));
        harmony.Patch(AccessTools.Method(typeof(GameFooter), nameof(GameFooter.Update)),
            prefix: new HarmonyMethod(typeof(FooterActionSelector), nameof(UpdateFooter)));
        harmony.Patch(AccessTools.Method(typeof(GameFooter), nameof(GameFooter.BindPassiveTracker)),
            postfix: new HarmonyMethod(typeof(FooterActionSelector), nameof(SetPassiveTracker)));
        harmony.Patch(AccessTools.Method(typeof(GameFooter), nameof(GameFooter.UnbindPassiveTracker)),
            postfix: new HarmonyMethod(typeof(FooterActionSelector), nameof(ClearPassiveTracker)));
    }

    private static void Attach(GameFooter __instance, Game game)
    {
        var tracker = __instance.Get(GameFooter.PropertyName.TrackerView).As<TrackerComponentFooterView>();
        var period = tracker?.Get(TrackerComponentFooterView.PropertyName.TimePeriodOptions).As<OptionButton>();
        if (tracker != null && period?.GetParent() is Container)
        {
            Selectors.Add(__instance, new FooterActionSelector(__instance, game.ActionManager, tracker, period));
        }
    }

    private static void Detach(GameFooter __instance)
    {
        if (Selectors.TryGetValue(__instance, out FooterActionSelector? selector))
        {
            Selectors.Remove(__instance);
            selector.dropdown.ItemSelected -= selector.SelectAction;
            selector.dropdown.GetParent()?.RemoveChild(selector.dropdown);
            selector.dropdown.QueueFree();
        }
    }

    private static bool UpdateFooter(GameFooter __instance) =>
        !Selectors.TryGetValue(__instance, out FooterActionSelector? selector) || !selector.ApplyPinnedAction();

    private static void SetPassiveTracker(GameFooter __instance, TrackerComponent tracker)
    {
        if (Selectors.TryGetValue(__instance, out FooterActionSelector? selector))
        {
            selector.passiveTracker = tracker;
            selector.ApplyPinnedAction(force: true);
        }
    }

    private static void ClearPassiveTracker(GameFooter __instance)
    {
        if (Selectors.TryGetValue(__instance, out FooterActionSelector? selector))
        {
            selector.passiveTracker = null;
            selector.ApplyPinnedAction(force: true);
        }
    }

    private bool ApplyPinnedAction(bool force = false)
    {
        RefreshOptions();
        if (pinnedAction?.Entity is not ITrackedEntity tracked)
        {
            if (displayedPin != null && passiveTracker != null)
            {
                trackerView.Bind(passiveTracker);
            }
            displayedPin = null;
            return false;
        }

        if (force || !ReferenceEquals(displayedPin, pinnedAction))
        {
            trackerView.Bind(tracked.Tracker);
            trackerView.BindAction(pinnedAction);
            displayedPin = pinnedAction;
        }
        return true;
    }

    private void RefreshOptions()
    {
        bool useRussian = ActionSlots.InRussian;
        var active = manager.ActiveActions;
        if (!optionsInitialized || russian != useRussian || !options.SequenceEqual(active))
        {
            optionsInitialized = true;
            russian = useRussian;
            options = active.ToArray();
            if (pinnedAction != null && !options.Contains(pinnedAction))
            {
                pinnedAction = null;
            }

            dropdown.Clear();
            dropdown.AddItem(russian ? "Авто (последнее)" : "Auto (latest)");
            foreach (ActionComponent action in options)
            {
                dropdown.AddItem(action.Entity.DisplayName.Text);
            }
            dropdown.TooltipText = russian ? "Действие для нижней панели" : "Action to show in the footer";
            dropdown.Select(pinnedAction == null ? 0 : Array.IndexOf(options, pinnedAction) + 1);
            dropdown.Visible = options.Length > 1;
        }
    }

    private void SelectAction(long index)
    {
        pinnedAction = index > 0 && index <= options.Length ? options[(int)index - 1] : null;
        footer.Update();
    }
}
