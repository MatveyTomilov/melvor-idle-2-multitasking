using System.Runtime.CompilerServices;
using System.Text.Json;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Components.Shop;
using GamesByMalcs.Melvor2.Conditions;
using GamesByMalcs.Melvor2.Core.InventoryWalletTransactions;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Data.Currencies;
using GamesByMalcs.Melvor2.Data.Recipes;
using GamesByMalcs.Melvor2.Entities.Shop;
using GamesByMalcs.Melvor2.Localization;
using Godot;

namespace Multitasking;

internal static class ActionSlots
{
    private const string NamespaceId = "Multitasking";
    private static readonly int[] SlotPrices = [30_000, 60_000, 360_000, 960_000, 1_800_000, 3_000_000];
    private static readonly ConditionalWeakTable<ActiveActionManager, SlotPurchases> PurchasesByManager = new();

    private sealed record SlotPurchases(ShopComponent Shop, IReadOnlyList<ShopPurchase> Purchases);

    internal static bool InRussian => TranslationServer.GetLocale().StartsWith("ru", StringComparison.OrdinalIgnoreCase);

    // Register purchases before the shop indexes its categories and the save resolves purchase IDs.
    internal static void AddShopPurchases(Game __instance)
    {
        Game game = __instance;
        int purchaseCount = game.Entities.GetAllObjects()
            .SelectMany(entity => entity.Components.Values).OfType<ActionComponent>().Count() - 1;
        if (purchaseCount < 1 || PurchasesByManager.TryGetValue(game.ActionManager, out _))
        {
            return;
        }

        var shop = (ShopEntity)game.Entities.GetObjectById("MelvorBase:Shop");
        var modNamespace = new DataNamespace(NamespaceId, "Multitasking", isModded: true);
        game.Data.RegisterDataNamespace(modNamespace);

        var icon = GD.Load<Texture2D>(shop.IconPath) ?? new PlaceholderTexture2D();
        var title = new LocalizedString("Multitasking");
        bool russian = InRussian;
        var categoryDescription = new LocalizedString(russian
            ? "Постоянные слоты для одновременных действий."
            : "Permanent slots for running actions at the same time.");
        var category = new ShopCategory(title, icon, categoryDescription, new ConditionGroup([]), null, modNamespace, "ActionSlots");
        var subcategory = new ShopSubcategory(title, icon, categoryDescription, category, modNamespace, "ActionSlots");
        var database = game.Data.Database;
        database.GetRegistry<ShopCategory>().RegisterObject(category);
        database.GetRegistry<ShopSubcategory>().RegisterObject(subcategory);

        Currency gold = game.Data.GetObject<Currency>("MelvorBase:GoldPieces");
        var purchases = new List<ShopPurchase>(purchaseCount);
        for (int slot = 2; slot <= purchaseCount + 1; slot++)
        {
            int price = SlotPrices[Math.Min(slot - 2, SlotPrices.Length - 1)];
            var requirements = new ConditionGroup(purchases.Count == 0
                ? []
                : [new ShopPurchaseCondition(purchases[^1], 1, shop.Shop, isOr: false)]);
            var purchase = new ShopPurchase(
                new LocalizedString(russian ? $"Слот действия {slot}" : $"Action Slot {slot}"),
                icon,
                new LocalizedString(russian
                    ? $"Увеличивает лимит одновременных действий до {slot}."
                    : $"Increases your simultaneous action limit to {slot}."),
                new ShopPurchaseProvides([], [], null),
                subcategory,
                allowBulkPurchase: false,
                defaultBuyLimit: 1,
                showBuyLimit: false,
                showPurchasedIndicator: true,
                requirements,
                new ConditionGroup([]),
                new RecipeIngredients([], [new CurrencyQuantity(gold, price)]),
                modNamespace,
                $"Slot{slot}");
            database.GetRegistry<ShopPurchase>().RegisterObject(purchase);
            purchases.Add(purchase);
        }

        shop.Config.RegisterData(game.Data, JsonSerializer.SerializeToElement(new
        {
            Categories = new[] { new { Id = category.Id, Priority = 100_000 } },
            Subcategories = new[] { new { Id = subcategory.Id, Priority = 100_000 } },
            Purchases = purchases.Select((purchase, index) => new { Id = purchase.Id, Priority = index }),
        }));
        PurchasesByManager.Add(game.ActionManager, new SlotPurchases(shop.Shop, purchases));
    }

    internal static bool SkipEmptyPurchase(
        ShopComponent __instance, ShopPurchase purchase, int quantity, ref InventoryWalletTransactionResult __result)
    {
        if (purchase.Namespace.Id != NamespaceId || (quantity > 0 && !__instance.IsPurchased(purchase)))
        {
            return true;
        }

        // The native shop removes a tier's cost tracker after purchase, so a retry must not raise PurchaseBought again.
        __result = InventoryWalletTransactionResult.Success;
        return false;
    }

    internal static int LimitFor(ActiveActionManager manager) => PurchasesByManager.TryGetValue(manager, out var slots)
        ? 1 + slots.Purchases.Count(slots.Shop.IsPurchased)
        : 1;

    internal static bool MakeRoom(ActiveActionManager manager, bool force, ActionComponent current)
    {
        if (!PurchasesByManager.TryGetValue(manager, out _))
        {
            return manager.TryStopActive(force);
        }

        IReadOnlyList<ActionComponent> active = manager.ActiveActions;
        if (active.Contains(current))
        {
            return current.TryStop(force);
        }

        int limit = LimitFor(manager);
        if (active.Count < limit)
        {
            return true;
        }

        foreach (ActionComponent action in active.Take(active.Count - limit + 1).ToArray())
        {
            if (!action.TryStop(force))
            {
                return false;
            }
        }
        return active.Count < limit;
    }

    internal static void EnforceLoadedLimit(Game __instance)
    {
        ActiveActionManager manager = __instance.ActionManager;
        if (!PurchasesByManager.TryGetValue(manager, out _))
        {
            return;
        }

        IReadOnlyList<ActionComponent> active = manager.ActiveActions;
        int excess = active.Count - LimitFor(manager);
        foreach (ActionComponent action in active.Take(excess).ToArray())
        {
            action.TryStop(force: true);
        }
    }
}
