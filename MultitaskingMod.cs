using System.Reflection;
using System.Reflection.Emit;
using GamesByMalcs.Melvor2.Components.Actions;
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
            transpiler: new HarmonyMethod(typeof(MultitaskingMod), nameof(KeepOtherActionsRunning)));

        var panel = new ActiveActionsWindow(context.Tree);
        context.AddWindow("Multitasking", panel.Toggle, Key.F5);
        context.Frame += panel.RefreshIfVisible;
        context.Log("Parallel actions enabled without a slot limit.");
    }

    private static IEnumerable<CodeInstruction> KeepOtherActionsRunning(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        MethodInfo stopAll = AccessTools.Method(typeof(ActiveActionManager), nameof(ActiveActionManager.TryStopActive));
        MethodInfo stopCurrent = AccessTools.Method(typeof(MultitaskingMod), nameof(StopPreviousActionForComponent));
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
        call.operand = stopCurrent;
        return code;
    }

    private static bool StopPreviousActionForComponent(ActiveActionManager manager, bool force, ActionComponent current)
    {
        // A component owns one timer and recipe. Restarting it keeps its normal stop/start lifecycle.
        return !manager.ActiveActions.Contains(current) || current.TryStop(force);
    }
}
