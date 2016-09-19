using System;
using System.Collections.Generic;
using StardewValley;

namespace Pathoschild.LookupAnything.Framework.Models
{
    /// <summary>Represents metadata about a recipe.</summary>
    internal class RecipeData
    {
        /*********
        ** Properties
        *********/
        /// <summary>The item that be created by this recipe.</summary>
        private readonly Func<Item> Item;


        /*********
        ** Accessors
        *********/
        /// <summary>The name of the recipe.</summary>
        public string Name { get; }

        /// <summary>How the recipe is used to create an object.</summary>
        public RecipeType Type { get; }

        /// <summary>The items needed to craft the recipe (item ID => number needed).</summary>
        public IDictionary<int, int> Ingredients { get; }

        /// <summary>Whether the recipe must be learned before it can be used.</summary>
        public bool MustBeLearned { get; }


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="recipe">The recipe to parse.</param>
        public RecipeData(CraftingRecipe recipe)
            : this(
                name: recipe.name,
                type: recipe.isCookingRecipe ? RecipeType.Cooking : RecipeType.Crafting,
                ingredients: GameHelper.GetPrivateField<Dictionary<int, int>>(recipe, "recipeList"),
                item: recipe.createItem,
                mustBeLearned: true
            )
        { }

        /// <summary>Construct an instance.</summary>
        /// <param name="name">The name of the recipe.</param>
        /// <param name="type">How the recipe is used to create an object.</param>
        /// <param name="ingredients">The items needed to craft the recipe (item ID => number needed).</param>
        /// <param name="item">The item that be created by this recipe.</param>
        /// <param name="mustBeLearned">Whether the recipe must be learned before it can be used.</param>
        public RecipeData(string name, RecipeType type, IDictionary<int, int> ingredients, Func<Item> item, bool mustBeLearned)
        {
            this.Name = name;
            this.Type = type;
            this.Ingredients = ingredients;
            this.Item = item;
            this.MustBeLearned = mustBeLearned;
        }

        /// <summary>Create the item crafted by this recipe.</summary>
        public Item CreateItem()
        {
            return this.Item();
        }

        /// <summary>Get whether a player knows this recipe.</summary>
        /// <param name="farmer">The farmer to check.</param>
        public bool KnowsRecipe(Farmer farmer)
        {
            return farmer.knowsRecipe(this.Name);
        }
    }
}