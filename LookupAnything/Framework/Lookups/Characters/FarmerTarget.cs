using System;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Pathoschild.Stardew.LookupAnything.Framework.Lookups.Characters
{
    /// <summary>Positional metadata about a farmer (i.e. player).</summary>
    internal class FarmerTarget : GenericTarget<Farmer>
    {
        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="gameHelper">Provides utility methods for interacting with the game code.</param>
        /// <param name="value">The underlying in-game entity.</param>
        /// <param name="getSubject">Get the subject info about the target.</param>
        public FarmerTarget(GameHelper gameHelper, Farmer value, Func<ISubject> getSubject)
            : base(gameHelper, SubjectType.Farmer, value, value.Tile, getSubject) { }

        /// <inheritdoc />
        public override Rectangle GetSpritesheetArea()
        {
            return this.Value.FarmerSprite.SourceRect;
        }

        /// <inheritdoc />
        public override Rectangle GetWorldArea()
        {
            return this.GetSpriteArea(this.Value.GetBoundingBox(), this.GetSpritesheetArea());
        }

        /// <inheritdoc />
        public override bool SpriteIntersectsPixel(Vector2 tile, Vector2 position, Rectangle spriteArea)
        {
            return spriteArea.Contains((int)position.X, (int)position.Y);
        }
    }
}
