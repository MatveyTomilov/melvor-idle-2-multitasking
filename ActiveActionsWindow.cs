using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Actions;
using Godot;
using MelvorModLoader;

namespace Multitasking;

internal sealed class ActiveActionsWindow(SceneTree tree)
{
    private readonly Dictionary<ActionComponent, Label> actionLabels = new();
    private Window? window;
    private VBoxContainer? actionList;
    private Label? status;
    private Button? stopAll;
    private Game? displayedGame;
    private ulong nextRefresh;

    private static bool InRussian => TranslationServer.GetLocale().StartsWith("ru", StringComparison.OrdinalIgnoreCase);

    public void Toggle()
    {
        window ??= CreateWindow();
        if (window.Visible)
        {
            Close();
            return;
        }

        Refresh();
        window.PopupCentered();
    }

    public void RefreshIfVisible()
    {
        if (window?.Visible == true && Time.GetTicksMsec() >= nextRefresh)
        {
            Refresh();
        }
    }

    private Window CreateWindow()
    {
        var result = new Window
        {
            Name = "MultitaskingActiveActions",
            Title = "Multitasking",
            Visible = false,
            MinSize = new Vector2I(340, 200),
            Size = new Vector2I(460, 340),
        };
        result.CloseRequested += Close;

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "top", "right", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", 14);
        }

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        status = new Label();
        body.AddChild(status);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        actionList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actionList.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(actionList);
        body.AddChild(scroll);

        stopAll = new Button { SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        stopAll.Pressed += () =>
        {
            Game? game = CurrentDisplayedGame();
            if (game != null)
            {
                game.ActionManager.TryStopActive();
                Refresh();
            }
        };
        body.AddChild(stopAll);
        margin.AddChild(body);
        result.AddChild(margin);
        tree.Root.AddChild(result);
        return result;
    }

    private void Refresh()
    {
        if (tree.Paused)
        {
            return;
        }

        nextRefresh = Time.GetTicksMsec() + 250;
        displayedGame = GameAccess.CurrentGame;
        IReadOnlyList<ActionComponent> actions = displayedGame?.ActionManager.ActiveActions ?? [];
        bool russian = InRussian;
        status!.Text = actions.Count > 0
            ? (russian ? $"Активных действий: {actions.Count}" : $"Active actions: {actions.Count}")
            : (russian ? "Нет активных действий" : "No active actions");
        stopAll!.Text = russian ? "Остановить все" : "Stop all";
        stopAll.Disabled = actions.Count == 0;

        foreach (ActionComponent action in actionLabels.Keys.Where(action => !actions.Contains(action)).ToArray())
        {
            Node row = actionLabels[action].GetParent();
            actionList!.RemoveChild(row);
            row.QueueFree();
            actionLabels.Remove(action);
        }

        for (int index = 0; index < actions.Count; index++)
        {
            ActionComponent action = actions[index];
            if (!actionLabels.TryGetValue(action, out Label? label))
            {
                label = AddActionRow(action, russian);
                actionLabels.Add(action, label);
            }

            string skill = action.Entity.DisplayName.Text;
            string? activity = action.CurrentDescription?.DisplayName.Text;
            label.Text = string.IsNullOrEmpty(activity) || activity == skill ? skill : $"{skill} · {activity}";
            Node row = label.GetParent();
            if (row.GetIndex() != index)
            {
                actionList!.MoveChild(row, index);
            }
        }
    }

    private Label AddActionRow(ActionComponent action, bool russian)
    {
        var row = new HBoxContainer();
        var label = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        var stop = new Button { Text = russian ? "Остановить" : "Stop" };
        stop.Pressed += () =>
        {
            if (CurrentDisplayedGame()?.ActionManager.ActiveActions.Contains(action) == true)
            {
                action.TryStop();
                Refresh();
            }
        };
        row.AddChild(label);
        row.AddChild(stop);
        actionList!.AddChild(row);
        return label;
    }

    private Game? CurrentDisplayedGame() =>
        !tree.Paused && displayedGame != null && ReferenceEquals(displayedGame, GameAccess.CurrentGame)
            ? displayedGame
            : null;

    private void Close()
    {
        window?.Hide();
        displayedGame = null;
        foreach (Node row in actionList!.GetChildren())
        {
            actionList.RemoveChild(row);
            row.QueueFree();
        }
        actionLabels.Clear();
    }
}
