using System.Reflection;
using System.Reflection.Emit;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Components.Shop;
using Godot;
using HarmonyLib;
using MelvorModLoader;

namespace Multitasking;

public sealed class MultitaskingMod : IMod
{
    public void OnLoad(ModContext context)
    {
        var harmony = new Harmony("multitasking.melvoridle2");
        harmony.Patch(
            AccessTools.Method(typeof(ActionComponent), nameof(ActionComponent.TryStart)),
            transpiler: new HarmonyMethod(typeof(MultitaskingMod), nameof(UsePurchasedSlots)));
        harmony.Patch(
            AccessTools.Method(typeof(Game), "OnDataLoaded"),
            prefix: new HarmonyMethod(typeof(ActionSlots), nameof(ActionSlots.AddShopPurchases)));
        harmony.Patch(
            AccessTools.Method(typeof(Game), "OnSaveLoaded"),
            postfix: new HarmonyMethod(typeof(ActionSlots), nameof(ActionSlots.EnforceLoadedLimit)));
        harmony.Patch(
            AccessTools.Method(typeof(ShopComponent), nameof(ShopComponent.ProcessPurchase)),
            prefix: new HarmonyMethod(typeof(ActionSlots), nameof(ActionSlots.SkipEmptyPurchase)));

        var panel = new ActiveActionsWindow(context.Tree);
        context.AddWindow("Multitasking", panel.Toggle, Key.F5);
        context.Frame += panel.RefreshIfVisible;
        context.Log("Multitasking slots are available in the Shop.");
    }

    private static IEnumerable<CodeInstruction> UsePurchasedSlots(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        MethodInfo stopAll = AccessTools.Method(typeof(ActiveActionManager), nameof(ActiveActionManager.TryStopActive));
        MethodInfo makeRoom = AccessTools.Method(typeof(ActionSlots), nameof(ActionSlots.MakeRoom));
        int callIndex = code.FindIndex(instruction => instruction.Calls(stopAll));
        if (callIndex < 0 || code.FindLastIndex(instruction => instruction.Calls(stopAll)) != callIndex)
        {
            throw new InvalidOperationException("ActionComponent.TryStart no longer has the expected stop call.");
        }

        // The stack already holds the manager and the force flag from the original call.
        CodeInstruction call = code[callIndex];
        var loadCurrent = new CodeInstruction(OpCodes.Ldarg_0);
        loadCurrent.labels.AddRange(call.labels);
        call.labels.Clear();
        code.Insert(callIndex, loadCurrent);
        call.opcode = OpCodes.Call;
        call.operand = makeRoom;
        return code;
    }
}
