using UnityEngine;

/// <summary>
/// 狗骑士商店货表：买入价与卖出价。
/// </summary>
public readonly struct ShopOffer
{
    public readonly int ItemId;
    public readonly int BuyPrice;
    public readonly int SellPrice;

    public ShopOffer(int itemId, int buyPrice, int sellPrice)
    {
        ItemId = itemId;
        BuyPrice = Mathf.Max(1, buyPrice);
        SellPrice = Mathf.Max(1, sellPrice);
    }
}

public static class KnightShopCatalog
{
    // 炎龙剑、蘑菇，以及主角背包里的大部分消耗品（略去剧毒/暗器）。
    private static readonly int[] BuyableItemIds =
    {
        1,
        3,
        6,
        7,
        101, 102, 103, 104, 105, 106, 107, 108, 109, 110,
        111, 112, 113, 114, 115, 117, 118, 120,
        121, 122, 123, 124, 125, 126, 127, 128, 129,
        131, 132, 133, 134, 135, 136, 137, 138, 139,
    };

    private static readonly ShopOffer[] PriceOverrides =
    {
        new ShopOffer(1, 80, 32),
        new ShopOffer(3, 10, 4),
        new ShopOffer(6, 50, 20),
        new ShopOffer(7, 90, 36),
        new ShopOffer(105, 12, 5),
        new ShopOffer(108, 18, 7),
        new ShopOffer(101, 25, 10),
        new ShopOffer(115, 22, 9),
        new ShopOffer(107, 32, 12),
        new ShopOffer(110, 28, 11),
        new ShopOffer(112, 40, 16),
        new ShopOffer(113, 40, 16),
    };

    private static ShopOffer[] offers;

    public static ShopOffer[] Offers
    {
        get
        {
            if (offers == null)
                offers = BuildOffers();

            return offers;
        }
    }

    public static bool TryGetOffer(int itemId, out ShopOffer offer)
    {
        ShopOffer[] shopOffers = Offers;
        for (int i = 0; i < shopOffers.Length; i++)
        {
            if (shopOffers[i].ItemId != itemId)
                continue;

            offer = shopOffers[i];
            return true;
        }

        offer = default;
        return false;
    }

    public static int GetSellPrice(ItemSO item)
    {
        if (item == null)
            return 1;

        if (TryGetOffer(item.id, out ShopOffer offer))
            return offer.SellPrice;

        return ComputeSellPrice(item);
    }

    private static ShopOffer[] BuildOffers()
    {
        ShopOffer[] result = new ShopOffer[BuyableItemIds.Length];
        for (int i = 0; i < BuyableItemIds.Length; i++)
            result[i] = ResolveOffer(BuyableItemIds[i]);

        return result;
    }

    private static ShopOffer ResolveOffer(int itemId)
    {
        for (int i = 0; i < PriceOverrides.Length; i++)
        {
            if (PriceOverrides[i].ItemId == itemId)
                return PriceOverrides[i];
        }

        ItemSO item = ItemCatalog.EnsureAvailable()?.GetItem(itemId);
        if (item == null)
            return new ShopOffer(itemId, 20, 8);

        int sell = ComputeSellPrice(item);
        int buy = Mathf.Max(sell * 5 / 2, sell + 6);
        return new ShopOffer(item.id, buy, sell);
    }

    private static int ComputeSellPrice(ItemSO item)
    {
        int hp = item.GetPropertyValue(ItemPropertyType.HPValue);
        int maxHp = item.GetPropertyValue(ItemPropertyType.MaxHPValue);
        int attack = item.GetPropertyValue(ItemPropertyType.AttackValue);
        int energy = item.GetPropertyValue(ItemPropertyType.EnergyValue);
        int speed = item.GetPropertyValue(ItemPropertyType.SpeedValue);
        int mental = item.GetPropertyValue(ItemPropertyType.MentalValue);
        int shield = item.GetPropertyValue(ItemPropertyType.ShieldDurability);
        int fallback = hp / 10 + maxHp * 3 + energy / 8 + attack + speed + mental / 4 + shield / 4;
        return Mathf.Max(3, fallback);
    }
}
