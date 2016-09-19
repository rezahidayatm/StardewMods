﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathoschild.LookupAnything.Components;
using Pathoschild.LookupAnything.Framework;
using Pathoschild.LookupAnything.Framework.Constants;
using Pathoschild.LookupAnything.Framework.Models;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace Pathoschild.LookupAnything
{
    /// <summary>Provides utility methods for interacting with the game code.</summary>
    internal static class GameHelper
    {
        /*********
        ** Properties
        *********/
        /// <summary>The cached villagers' gift tastes, indexed by taste and then villager name. Each item reference is a category (negative value) or parent sprite index (positive value).</summary>
        private static Lazy<IDictionary<GiftTaste, IDictionary<string, int[]>>> GiftTastes;

        /// <summary>The cached list of characters who can receive gifts.</summary>
        private static Lazy<NPC[]> GiftableVillagers;

        /// <summary>The cached recipes.</summary>
        private static Lazy<RecipeData[]> Recipes;


        /*********
        ** Public methods
        *********/
        /****
        ** State
        ****/
        /// <summary>Reset the low-level cache used to store expensive query results, so the data is recalculated on demand.</summary>
        public static void ResetCache()
        {
            GameHelper.GiftTastes = new Lazy<IDictionary<GiftTaste, IDictionary<string, int[]>>>(GameHelper.FetchGiftTastes);
            GameHelper.GiftableVillagers = new Lazy<NPC[]>(GameHelper.FetchGiftableVillagers);
            GameHelper.Recipes = new Lazy<RecipeData[]>(GameHelper.FetchRecipes);
        }

        /****
        ** Data helpers
        ****/
        /// <summary>Add a day offset to the current date.</summary>
        /// <param name="offset">The offset to add in days.</param>
        /// <param name="daysInSeason">The number of days in a season.</param>
        /// <returns>Returns the resulting season and day.</returns>
        public static Tuple<string, int> GetDayOffset(int offset, int daysInSeason)
        {
            // simple case
            string season = Game1.currentSeason;
            int day = Game1.dayOfMonth + offset;

            // handle season transition
            if (day > daysInSeason)
            {
                string[] seasons = { Constant.SeasonNames.Spring, Constant.SeasonNames.Summer, Constant.SeasonNames.Fall, Constant.SeasonNames.Winter };
                int curSeasonIndex = Array.IndexOf(seasons, Game1.currentSeason);
                if (curSeasonIndex == -1)
                    throw new InvalidOperationException($"The current season '{Game1.currentSeason}' wasn't recognised.");
                season = seasons[curSeasonIndex + (day / daysInSeason) % seasons.Length];
                day = day % daysInSeason;
            }

            return Tuple.Create(season, day);
        }

        /// <summary>Get how much each NPC likes receiving an item as a gift.</summary>
        /// <param name="item">The item to check.</param>
        public static IDictionary<NPC, GiftTaste> GetGiftTastes(Item item)
        {
            // can't be gifted
            if (!item.canBeGivenAsGift())
                return new Dictionary<NPC, GiftTaste>();

            // fetch game data
            var giftTastes = GameHelper.GiftTastes.Value;
            var giftableVillagers = GameHelper.GiftableVillagers.Value;

            // get tastes
            IDictionary<NPC, GiftTaste> tastes = new Dictionary<NPC, GiftTaste>();
            foreach (NPC npc in giftableVillagers)
            {
                // get taste
                foreach (GiftTaste taste in Enum.GetValues(typeof(GiftTaste)))
                {
                    if (giftTastes[taste][npc.getName()].Contains(item.category) || giftTastes[taste][npc.getName()].Contains(item.parentSheetIndex))
                    {
                        tastes[npc] = taste;
                        break;
                    }
                }

                // default to neutral
                if (!tastes.ContainsKey(npc))
                    tastes[npc] = GiftTaste.Neutral;
            }
            return tastes;
        }

        /// <summary>Get the recipes for which an item is needed.</summary>
        public static IEnumerable<RecipeData> GetRecipes()
        {
            return GameHelper.Recipes.Value;
        }

        /// <summary>Get the recipes for which an item is needed.</summary>
        /// <param name="itemID">The item ID.</param>
        public static IEnumerable<RecipeData> GetRecipesForIngredient(int itemID)
        {
            return GameHelper.GetRecipes().Where(p => p.Ingredients.ContainsKey(itemID));
        }

        /// <summary>Get the items a specified NPC can receive.</summary>
        /// <param name="npc">The NPC to check.</param>
        public static IDictionary<Item, GiftTaste> GetGiftTastes(NPC npc)
        {
            // get game data
            var giftTastes = GameHelper.GiftTastes.Value;
            var giftableVillagers = GameHelper.GiftableVillagers.Value;
            if (!giftableVillagers.Contains(npc))
                return new Dictionary<Item, GiftTaste>();

            // get tastes
            IDictionary<Item, GiftTaste> tastes = new Dictionary<Item, GiftTaste>();
            foreach (GiftTaste taste in Enum.GetValues(typeof(GiftTaste)))
            {
                foreach (Object item in giftTastes[taste][npc.getName()].SelectMany(GameHelper.GetObjectsByReferenceID))
                    tastes[item] = taste;
            }
            return tastes;
        }

        /// <summary>Get an object by its parent sprite index.</summary>
        /// <param name="index">The parent sprite index.</param>
        /// <param name="stack">The number of items in the stack.</param>
        public static Object GetObjectBySpriteIndex(int index, int stack = 1)
        {
            return new Object(index, stack);
        }

        /// <summary>Get all objects matching the reference ID.</summary>
        /// <param name="refID">The reference ID. This can be a category (negative value) or parent sprite index (positive value).</param>
        public static IEnumerable<Object> GetObjectsByReferenceID(int refID)
        {
            // category
            if (refID < 0)
            {
                return (
                    from pair in Game1.objectInformation
                    where Regex.IsMatch(pair.Value, $"\b{refID}\b")
                    select GameHelper.GetObjectBySpriteIndex(pair.Key)
                );
            }

            // parent sprite index
            return new[] { GameHelper.GetObjectBySpriteIndex(refID) };
        }

        /// <summary>Get whether an item can have a quality (which increases its sale price).</summary>
        /// <param name="item">The item.</param>
        public static bool CanHaveQuality(Item item)
        {
            // check category
            if (new[] { "Artifact", "Trash", "Crafting", "Seed", "Decor", "Resource", "Fertilizer", "Bait", "Fishing Tackle" }.Contains(item.getCategoryName()))
                return false;

            // check type
            if (new[] { "Crafting", "asdf" /*dig spots*/, "Quest" }.Contains((item as Object)?.Type))
                return false;

            return true;
        }

        /****
        ** Reflection
        ****/
        /// <summary>Get a private field value.</summary>
        /// <typeparam name="T">The field type.</typeparam>
        /// <param name="parent">The parent object.</param>
        /// <param name="name">The field name.</param>
        /// <param name="required">Whether to throw an exception if the private field is not found.</param>
        public static T GetPrivateField<T>(object parent, string name, bool required = true)
        {
            if (parent == null)
                return default(T);

            // get field from hierarchy
            FieldInfo field = null;
            for (Type type = parent.GetType(); type != null && field == null; type = type.BaseType)
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

            // validate
            if (field == null)
            {
                if (required)
                    throw new InvalidOperationException($"The {parent.GetType().Name} object doesn't have a private '{name}' field.");
                return default(T);
            }

            // get value
            return (T)field.GetValue(parent);
        }

        /// <summary>Get a private method.</summary>
        /// <param name="parent">The parent object.</param>
        /// <param name="name">The field name.</param>
        /// <param name="required">Whether to throw an exception if the private field is not found.</param>
        public static MethodInfo GetPrivateMethod(object parent, string name, bool required = true)
        {
            if (parent == null)
                throw new InvalidOperationException($"Can't get private method '{name}' on null instance.");

            // get method from hierarchy
            MethodInfo method = null;
            for (Type type = parent.GetType(); type != null && method == null; type = type.BaseType)
                method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);

            // validate
            if (method == null)
            {
                if (required)
                    throw new InvalidOperationException($"The {parent.GetType().Name} object doesn't have a private '{name}' method.");
                return null;
            }

            // get value
            return method;
        }

        /// <summary>Validate that the game versions match the minimum requirements, and return an appropriate error message if not.</summary>
        public static string ValidateGameVersion()
        {
            string gameVersion = Regex.Replace(Game1.version, "^([0-9.]+).*", "$1");
            string apiVersion = Constants.Version.VersionString;

            if (string.Compare(gameVersion, Constant.MinimumGameVersion, StringComparison.InvariantCultureIgnoreCase) == -1)
                return $"The LookupAnything mod requires the latest version of the game. Please update Stardew Valley from {gameVersion} to {Constant.MinimumGameVersion}.";
            if (string.Compare(apiVersion, Constant.MinimumApiVersion, StringComparison.InvariantCultureIgnoreCase) == -1)
                return $"The LookupAnything mod requires the latest version of SMAPI. Please update SMAPI from {apiVersion} to {Constant.MinimumApiVersion}.";

            return null;
        }

        /****
        ** Formatting
        ****/
        /// <summary>Select the correct plural form for a word.</summary>
        /// <param name="count">The number.</param>
        /// <param name="single">The singular form.</param>
        /// <param name="plural">The plural form.</param>
        public static string Pluralise(int count, string single, string plural = null)
        {
            return count == 1 ? single : (plural ?? single + "s");
        }

        /****
        ** Coordinates
        ****/
        /// <summary>Get the viewport coordinates from the current cursor position.</summary>
        public static Vector2 GetScreenCoordinatesFromCursor()
        {
            return new Vector2(Game1.getOldMouseX(), Game1.getOldMouseY());
        }

        /// <summary>Get the viewport coordinates represented by a tile position.</summary>
        /// <param name="coordinates">The absolute coordinates.</param>
        public static Vector2 GetScreenCoordinatesFromAbsolute(Vector2 coordinates)
        {
            return coordinates - new Vector2(Game1.viewport.X, Game1.viewport.Y);
        }

        /// <summary>Get the viewport coordinates represented by a tile position.</summary>
        /// <param name="tile">The tile position.</param>
        public static Rectangle GetScreenCoordinatesFromTile(Vector2 tile)
        {
            Vector2 position = GameHelper.GetScreenCoordinatesFromAbsolute(tile * new Vector2(Game1.tileSize));
            return new Rectangle((int)position.X, (int)position.Y, Game1.tileSize, Game1.tileSize);
        }

        /// <summary>Get whether a sprite on a given tile could occlude a specified tile position.</summary>
        /// <param name="spriteTile">The tile of the possible sprite.</param>
        /// <param name="occludeTile">The tile to check for possible occlusion.</param>
        public static bool CouldSpriteOccludeTile(Vector2 spriteTile, Vector2 occludeTile)
        {
            Vector2 spriteSize = Constant.MaxTargetSpriteSize;
            return
                spriteTile.Y >= occludeTile.Y // sprites never extend downard from their tile
                && Math.Abs(spriteTile.X - occludeTile.X) <= spriteSize.X
                && Math.Abs(spriteTile.Y - occludeTile.Y) <= spriteSize.Y;
        }

        /// <summary>Get the pixel coordinates within a sprite sheet corresponding to a sprite displayed in the world.</summary>
        /// <param name="worldPosition">The pixel position in the world.</param>
        /// <param name="worldRectangle">The sprite rectangle in the world.</param>
        /// <param name="spriteRectangle">The sprite rectangle in the sprite sheet.</param>
        /// <param name="spriteEffects">The transformation to apply on the sprite.</param>
        public static Vector2 GetSpriteSheetCoordinates(Vector2 worldPosition, Rectangle worldRectangle, Rectangle spriteRectangle, SpriteEffects spriteEffects = SpriteEffects.None)
        {
            // get position within sprite rectangle
            float x = (worldPosition.X - worldRectangle.X) / Game1.pixelZoom;
            float y = (worldPosition.Y - worldRectangle.Y) / Game1.pixelZoom;

            // flip values
            if (spriteEffects.HasFlag(SpriteEffects.FlipHorizontally))
                x = spriteRectangle.Width - x;
            if (spriteEffects.HasFlag(SpriteEffects.FlipVertically))
                y = spriteRectangle.Height - y;

            // get position within sprite sheet
            x += spriteRectangle.X;
            y += spriteRectangle.Y;

            // return coordinates
            return new Vector2(x, y);
        }

        /// <summary>Get a pixel from a sprite sheet.</summary>
        /// <typeparam name="TPixel">The pixel value type.</typeparam>
        /// <param name="spriteSheet">The sprite sheet.</param>
        /// <param name="position">The position of the pixel within the sprite sheet.</param>
        public static TPixel GetSpriteSheetPixel<TPixel>(Texture2D spriteSheet, Vector2 position) where TPixel : struct
        {
            // get pixel index
            int x = (int)position.X;
            int y = (int)position.Y;
            int spriteIndex = y * spriteSheet.Width + x; // (pixels in preceding rows) + (preceding pixels in current row)

            // get pixel
            TPixel[] pixels = new TPixel[spriteSheet.Width * spriteSheet.Height];
            spriteSheet.GetData<TPixel>(pixels);
            return pixels[spriteIndex];
        }

        /// <summary>Get the sprite for an item.</summary>
        /// <param name="item">The item.</param>
        /// <returns>Returns a tuple containing the sprite sheet and the sprite's position and dimensions within the sheet.</returns>
        public static Tuple<Texture2D, Rectangle> GetSprite(Item item)
        {
            // stardard object
            if (item is Object)
            {
                Object obj = (Object)item;
                return obj.bigCraftable
                    ? Tuple.Create(Game1.bigCraftableSpriteSheet, Object.getSourceRectForBigCraftable(obj.ParentSheetIndex))
                    : Tuple.Create(Game1.objectSpriteSheet, Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, obj.ParentSheetIndex, Object.spriteSheetTileSize, Object.spriteSheetTileSize));
            }

            // boots or ring
            if (item is Boots || item is Ring)
            {
                int indexInTileSheet = (item as Boots)?.indexInTileSheet ?? ((Ring)item).indexInTileSheet;
                return Tuple.Create(Game1.objectSpriteSheet, Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, indexInTileSheet, Object.spriteSheetTileSize, Object.spriteSheetTileSize));
            }

            // unknown item
            return null;
        }


        /****
        ** UI
        ****/
        /// <summary>Draw a pretty hover box for the given text.</summary>
        /// <param name="spriteBatch">The sprite batch being drawn.</param>
        /// <param name="label">The text to display.</param>
        /// <param name="position">The position at which to draw the text.</param>
        /// <param name="wrapWidth">The maximum width to display.</param>
        public static Vector2 DrawHoverBox(SpriteBatch spriteBatch, string label, Vector2 position, float wrapWidth)
        {
            const int paddingSize = 27;
            const int gutterSize = 20;

            Vector2 labelSize = spriteBatch.DrawStringBlock(Game1.smallFont, label, position + new Vector2(gutterSize), wrapWidth); // draw text to get wrapped text dimensions
            IClickableMenu.drawTextureBox(spriteBatch, Game1.menuTexture, new Rectangle(0, 256, 60, 60), (int)position.X, (int)position.Y, (int)labelSize.X + paddingSize + gutterSize, (int)labelSize.Y + paddingSize, Color.White);
            spriteBatch.DrawStringBlock(Game1.smallFont, label, position + new Vector2(gutterSize), wrapWidth); // draw again over texture box

            return labelSize + new Vector2(paddingSize);
        }

        /// <summary>Show an informational message to the player.</summary>
        /// <param name="message">The message to show.</param>
        public static void ShowInfoMessage(string message)
        {
            Game1.addHUDMessage(new HUDMessage(message, 3) { noIcon = true });
        }

        /// <summary>Show an error message to the player.</summary>
        /// <param name="message">The message to show.</param>
        public static void ShowErrorMessage(string message)
        {
            Game1.addHUDMessage(new HUDMessage(message, 3));
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get a list of characters who can receive gifts.</summary>
        private static NPC[] FetchGiftableVillagers()
        {
            // NPCs are giftable if they have at least one preference
            var uniqueKeys = new HashSet<string>(
                GameHelper.GiftTastes.Value
                    .SelectMany(p => p.Value)
                    .Select(p => p.Key)
            );

            // get characters matching keys
            return Utility.getAllCharacters()
                .Where(npc => npc.isVillager() && uniqueKeys.Contains(npc.getName()))
                .ToArray();
        }

        /// <summary>Get the villagers' gift tastes.</summary>
        /// <remarks>Reverse engineered from <c>Data\NPCGiftTastes</c> and <see cref="NPC.getGiftTasteForThisItem"/>.</remarks>
        private static IDictionary<GiftTaste, IDictionary<string, int[]>> FetchGiftTastes()
        {
            // parse game data
            var universalTastes = new Dictionary<GiftTaste, int[]>();
            var personalTastes = new Dictionary<string, Dictionary<GiftTaste, int[]>>();
            {
                // define keys
                var universalKeys = new Dictionary<string, GiftTaste>
                {
                    ["Universal_Love"] = GiftTaste.Love,
                    ["Universal_Like"] = GiftTaste.Like,
                    ["Universal_Neutral"] = GiftTaste.Neutral,
                    ["Universal_Dislike"] = GiftTaste.Dislike,
                    ["Universal_Hate"] = GiftTaste.Hate
                };
                var personalMetadataKeys = new Dictionary<int, GiftTaste>
                {
                    // metadata is paired: odd values contain a list of item references, even values contain the reaction dialogue
                    [1] = GiftTaste.Love,
                    [3] = GiftTaste.Like,
                    [5] = GiftTaste.Dislike,
                    [7] = GiftTaste.Hate,
                    [9] = GiftTaste.Neutral
                };

                // read data
                foreach (string key in Game1.NPCGiftTastes.Keys)
                {
                    // universal tastes
                    if (universalKeys.ContainsKey(key))
                    {
                        GiftTaste taste = universalKeys[key];
                        universalTastes[taste] = Game1.NPCGiftTastes[key]
                            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(int.Parse)
                            .ToArray();
                    }

                    // personal tastes
                    else
                    {
                        personalTastes[key] = new Dictionary<GiftTaste, int[]>();
                        string[] metadata = Game1.NPCGiftTastes[key].Split('/');
                        foreach (int i in personalMetadataKeys.Keys)
                        {
                            GiftTaste taste = personalMetadataKeys[i];
                            personalTastes[key][taste] = metadata[i]
                                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(int.Parse)
                                .ToArray();
                        }
                    }
                }
            }

            // merge data structures
            var giftTastes = new Dictionary<GiftTaste, IDictionary<string, int[]>>
            {
                [GiftTaste.Love] = new Dictionary<string, int[]>(),
                [GiftTaste.Like] = new Dictionary<string, int[]>(),
                [GiftTaste.Neutral] = new Dictionary<string, int[]>(),
                [GiftTaste.Dislike] = new Dictionary<string, int[]>(),
                [GiftTaste.Hate] = new Dictionary<string, int[]>()
            };
            foreach (string villagerName in personalTastes.Keys)
            {
                foreach (GiftTaste taste in personalTastes[villagerName].Keys)
                    giftTastes[taste][villagerName] = personalTastes[villagerName][taste];
            }
            foreach (GiftTaste taste in universalTastes.Keys)
            {
                foreach (string villagerName in giftTastes[taste].Keys.ToArray())
                    giftTastes[taste][villagerName] = giftTastes[taste][villagerName].Concat(universalTastes[taste]).ToArray();
            }

            return giftTastes;
        }

        /// <summary>Get the recipe ingredients.</summary>
        private static RecipeData[] FetchRecipes()
        {
            List<RecipeData> recipes = new List<RecipeData>();

            // cooking recipes
            recipes.AddRange(
                from entry in CraftingRecipe.cookingRecipes
                let recipe = new CraftingRecipe(entry.Key, isCookingRecipe: true)
                select new RecipeData(recipe)
            );

            // crafting recipes
            recipes.AddRange(
                from entry in CraftingRecipe.craftingRecipes
                let recipe = new CraftingRecipe(entry.Key, isCookingRecipe: false)
                select new RecipeData(recipe)
            );

            // furnace recipes
            {
                var ores = new Dictionary<int, Tuple<int, int>>
                {
                    [Object.quartzIndex] = Tuple.Create(338, 1),
                    [Object.copper] = Tuple.Create(Object.copperBar, 5),
                    [Object.iron] = Tuple.Create(Object.ironBar, 5),
                    [Object.gold] = Tuple.Create(Object.goldBar, 5),
                    [Object.iridium] = Tuple.Create(Object.iridiumBar, 5)
                };
                recipes.AddRange(
                    from data in ores
                    let oreIndex = data.Key
                    let barIndex = data.Value.Item1
                    let orePerBar = data.Value.Item2
                    let name = new Object(barIndex, 1).Name
                    let ingredients = new Dictionary<int, int> { [Object.coal] = 1, [oreIndex] = orePerBar }
                    select new RecipeData(name, RecipeType.Furnace, ingredients, () => new Object(barIndex, 1), mustBeLearned: false)
                );
            }

            return recipes.OrderBy(p => p.Name).ToArray();
        }
    }
}
