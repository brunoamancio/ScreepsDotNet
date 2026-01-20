namespace ScreepsDotNet.Common.Constants;

/// <summary>
/// Represents a factory commodity production recipe.
/// </summary>
/// <param name="Amount">Amount of commodity produced.</param>
/// <param name="Cooldown">Factory cooldown ticks after production.</param>
/// <param name="Components">Resource components required for production (resourceType => amount).</param>
/// <param name="Level">Factory level requirement (null = level 0, any factory can produce).</param>
public sealed record CommodityRecipe(int Amount, int Cooldown, IReadOnlyDictionary<string, int> Components, int? Level = null);

/// <summary>
/// Factory commodity production recipes from Screeps COMMODITIES constant.
/// Maps commodity type to production recipe (amount, cooldown, components, level).
/// </summary>
public static class CommodityRecipes
{
    private static readonly Dictionary<string, CommodityRecipe> Recipes = new()
    {
        // Base mineral compression/decompression (level 0)
        [ResourceTypes.UtriumBar] = new(
            Amount: 100,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Utrium] = 500,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Utrium] = new(
            Amount: 500,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.UtriumBar] = 100,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.LemergiumBar] = new(
            Amount: 100,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Lemergium] = 500,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Lemergium] = new(
            Amount: 500,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.LemergiumBar] = 100,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.ZynthiumBar] = new(
            Amount: 100,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Zynthium] = 500,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Zynthium] = new(
            Amount: 500,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.ZynthiumBar] = 100,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.KeaniumBar] = new(
            Amount: 100,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Keanium] = 500,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Keanium] = new(
            Amount: 500,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.KeaniumBar] = 100,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.GhodiumMelt] = new(
            Amount: 100,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Ghodium] = 500,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Ghodium] = new(
            Amount: 500,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.GhodiumMelt] = 100,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Oxidant] = new(
            Amount: 100,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Oxygen] = 500,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Oxygen] = new(
            Amount: 500,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Oxidant] = 100,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Reductant] = new(
            Amount: 100,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Hydrogen] = 500,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Hydrogen] = new(
            Amount: 500,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Reductant] = 100,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Purifier] = new(
            Amount: 100,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Catalyst] = 500,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Catalyst] = new(
            Amount: 500,
            Cooldown: 20,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Purifier] = 100,
                [ResourceTypes.Energy] = 200
            }),
        [ResourceTypes.Battery] = new(
            Amount: 50,
            Cooldown: 10,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Energy] = 600
            }),
        [ResourceTypes.Energy] = new(
            Amount: 500,
            Cooldown: 10,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Battery] = 50
            }),

        // Base commodities (level 1-3)
        [ResourceTypes.Composite] = new(
            Amount: 20,
            Cooldown: 50,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.UtriumBar] = 20,
                [ResourceTypes.ZynthiumBar] = 20,
                [ResourceTypes.Energy] = 20
            },
            Level: 1),
        [ResourceTypes.Crystal] = new(
            Amount: 6,
            Cooldown: 21,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.LemergiumBar] = 6,
                [ResourceTypes.KeaniumBar] = 6,
                [ResourceTypes.Purifier] = 6,
                [ResourceTypes.Energy] = 45
            },
            Level: 2),
        [ResourceTypes.Liquid] = new(
            Amount: 12,
            Cooldown: 60,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Oxidant] = 12,
                [ResourceTypes.Reductant] = 12,
                [ResourceTypes.GhodiumMelt] = 12,
                [ResourceTypes.Energy] = 90
            },
            Level: 3),

        // Electronics chain (level 0-5)
        [ResourceTypes.Wire] = new(
            Amount: 20,
            Cooldown: 8,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.UtriumBar] = 20,
                [ResourceTypes.Silicon] = 100,
                [ResourceTypes.Energy] = 40
            }),
        [ResourceTypes.Switch] = new(
            Amount: 5,
            Cooldown: 70,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Wire] = 40,
                [ResourceTypes.Oxidant] = 95,
                [ResourceTypes.UtriumBar] = 35,
                [ResourceTypes.Energy] = 20
            },
            Level: 1),
        [ResourceTypes.Transistor] = new(
            Amount: 1,
            Cooldown: 59,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Switch] = 4,
                [ResourceTypes.Wire] = 15,
                [ResourceTypes.Reductant] = 85,
                [ResourceTypes.Energy] = 8
            },
            Level: 2),
        [ResourceTypes.Microchip] = new(
            Amount: 1,
            Cooldown: 250,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Transistor] = 2,
                [ResourceTypes.Composite] = 50,
                [ResourceTypes.Wire] = 117,
                [ResourceTypes.Purifier] = 25,
                [ResourceTypes.Energy] = 16
            },
            Level: 3),
        [ResourceTypes.Circuit] = new(
            Amount: 1,
            Cooldown: 800,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Microchip] = 1,
                [ResourceTypes.Transistor] = 5,
                [ResourceTypes.Switch] = 4,
                [ResourceTypes.Oxidant] = 115,
                [ResourceTypes.Energy] = 32
            },
            Level: 4),
        [ResourceTypes.Device] = new(
            Amount: 1,
            Cooldown: 600,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Circuit] = 1,
                [ResourceTypes.Microchip] = 3,
                [ResourceTypes.Crystal] = 110,
                [ResourceTypes.GhodiumMelt] = 150,
                [ResourceTypes.Energy] = 64
            },
            Level: 5),

        // Biologicals chain (level 0-5)
        [ResourceTypes.Cell] = new(
            Amount: 20,
            Cooldown: 8,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.LemergiumBar] = 20,
                [ResourceTypes.Biomass] = 100,
                [ResourceTypes.Energy] = 40
            }),
        [ResourceTypes.Phlegm] = new(
            Amount: 2,
            Cooldown: 35,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Cell] = 20,
                [ResourceTypes.Oxidant] = 36,
                [ResourceTypes.LemergiumBar] = 16,
                [ResourceTypes.Energy] = 8
            },
            Level: 1),
        [ResourceTypes.Tissue] = new(
            Amount: 2,
            Cooldown: 164,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Phlegm] = 10,
                [ResourceTypes.Cell] = 10,
                [ResourceTypes.Reductant] = 110,
                [ResourceTypes.Energy] = 16
            },
            Level: 2),
        [ResourceTypes.Muscle] = new(
            Amount: 1,
            Cooldown: 250,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Tissue] = 3,
                [ResourceTypes.Phlegm] = 3,
                [ResourceTypes.ZynthiumBar] = 50,
                [ResourceTypes.Reductant] = 50,
                [ResourceTypes.Energy] = 16
            },
            Level: 3),
        [ResourceTypes.Organoid] = new(
            Amount: 1,
            Cooldown: 800,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Muscle] = 1,
                [ResourceTypes.Tissue] = 5,
                [ResourceTypes.Purifier] = 208,
                [ResourceTypes.Oxidant] = 256,
                [ResourceTypes.Energy] = 32
            },
            Level: 4),
        [ResourceTypes.Organism] = new(
            Amount: 1,
            Cooldown: 600,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Organoid] = 1,
                [ResourceTypes.Liquid] = 150,
                [ResourceTypes.Tissue] = 6,
                [ResourceTypes.Cell] = 310,
                [ResourceTypes.Energy] = 64
            },
            Level: 5),

        // Mechanicals chain (level 0-5)
        [ResourceTypes.Alloy] = new(
            Amount: 20,
            Cooldown: 8,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.ZynthiumBar] = 20,
                [ResourceTypes.Metal] = 100,
                [ResourceTypes.Energy] = 40
            }),
        [ResourceTypes.Tube] = new(
            Amount: 2,
            Cooldown: 45,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Alloy] = 40,
                [ResourceTypes.ZynthiumBar] = 16,
                [ResourceTypes.Energy] = 8
            },
            Level: 1),
        [ResourceTypes.Fixtures] = new(
            Amount: 1,
            Cooldown: 115,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Composite] = 20,
                [ResourceTypes.Alloy] = 41,
                [ResourceTypes.Oxidant] = 161,
                [ResourceTypes.Energy] = 8
            },
            Level: 2),
        [ResourceTypes.Frame] = new(
            Amount: 1,
            Cooldown: 125,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Fixtures] = 2,
                [ResourceTypes.Tube] = 4,
                [ResourceTypes.Reductant] = 330,
                [ResourceTypes.ZynthiumBar] = 31,
                [ResourceTypes.Energy] = 16
            },
            Level: 3),
        [ResourceTypes.Hydraulics] = new(
            Amount: 1,
            Cooldown: 800,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Liquid] = 150,
                [ResourceTypes.Fixtures] = 3,
                [ResourceTypes.Tube] = 15,
                [ResourceTypes.Purifier] = 208,
                [ResourceTypes.Energy] = 32
            },
            Level: 4),
        [ResourceTypes.Machine] = new(
            Amount: 1,
            Cooldown: 600,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Hydraulics] = 1,
                [ResourceTypes.Frame] = 2,
                [ResourceTypes.Fixtures] = 3,
                [ResourceTypes.Tube] = 12,
                [ResourceTypes.Energy] = 64
            },
            Level: 5),

        // Mysticals chain (level 0-5)
        [ResourceTypes.Condensate] = new(
            Amount: 20,
            Cooldown: 8,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.KeaniumBar] = 20,
                [ResourceTypes.Mist] = 100,
                [ResourceTypes.Energy] = 40
            }),
        [ResourceTypes.Concentrate] = new(
            Amount: 3,
            Cooldown: 41,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Condensate] = 30,
                [ResourceTypes.KeaniumBar] = 15,
                [ResourceTypes.Reductant] = 54,
                [ResourceTypes.Energy] = 12
            },
            Level: 1),
        [ResourceTypes.Extract] = new(
            Amount: 2,
            Cooldown: 128,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Concentrate] = 10,
                [ResourceTypes.Condensate] = 30,
                [ResourceTypes.Oxidant] = 60,
                [ResourceTypes.Energy] = 16
            },
            Level: 2),
        [ResourceTypes.Spirit] = new(
            Amount: 1,
            Cooldown: 200,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Extract] = 2,
                [ResourceTypes.Concentrate] = 6,
                [ResourceTypes.Reductant] = 90,
                [ResourceTypes.Purifier] = 20,
                [ResourceTypes.Energy] = 16
            },
            Level: 3),
        [ResourceTypes.Emanation] = new(
            Amount: 1,
            Cooldown: 800,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Spirit] = 2,
                [ResourceTypes.Extract] = 2,
                [ResourceTypes.Concentrate] = 3,
                [ResourceTypes.KeaniumBar] = 112,
                [ResourceTypes.Energy] = 32
            },
            Level: 4),
        [ResourceTypes.Essence] = new(
            Amount: 1,
            Cooldown: 600,
            Components: new Dictionary<string, int>
            {
                [ResourceTypes.Emanation] = 1,
                [ResourceTypes.Spirit] = 3,
                [ResourceTypes.Crystal] = 110,
                [ResourceTypes.GhodiumMelt] = 150,
                [ResourceTypes.Energy] = 64
            },
            Level: 5)
    };

    /// <summary>
    /// Attempts to retrieve a commodity production recipe by commodity type.
    /// </summary>
    /// <param name="commodityType">The commodity resource type (e.g., "battery", "utrium_bar").</param>
    /// <param name="recipe">The recipe if found, null otherwise.</param>
    /// <returns>True if recipe exists, false otherwise.</returns>
    public static bool TryGetRecipe(string commodityType, out CommodityRecipe? recipe)
        => Recipes.TryGetValue(commodityType, out recipe);

    /// <summary>
    /// Gets all commodity types that can be produced in factories.
    /// </summary>
    public static IEnumerable<string> GetAllCommodityTypes()
        => Recipes.Keys;
}
