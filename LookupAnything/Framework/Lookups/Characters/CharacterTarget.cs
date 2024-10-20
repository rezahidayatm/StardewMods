using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Monsters;

namespace Pathoschild.Stardew.LookupAnything.Framework.Lookups.Characters
{
    /// <summary>Positional metadata about an NPC.</summary>
    internal class CharacterTarget : GenericTarget<NPC>
    {
        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="gameHelper">Provides utility methods for interacting with the game code.</param>
        /// <param name="type">The target type.</param>
        /// <param name="value">The underlying in-game entity.</param>
        /// <param name="tilePosition">The object's tile position in the current location (if applicable).</param>
        /// <param name="getSubject">Get the subject info about the target.</param>
        public CharacterTarget(GameHelper gameHelper, SubjectType type, NPC value, Vector2 tilePosition, Func<ISubject> getSubject)
            : base(gameHelper, type, value, tilePosition, getSubject) { }

        /// <inheritdoc />
        public override Rectangle GetSpritesheetArea()
        {
            return this.Value.Sprite.SourceRect;
        }

        /// <inheritdoc />
        public override Rectangle GetWorldArea()
        {
            NPC npc = this.Value;
            AnimatedSprite sprite = npc.Sprite;
            var boundingBox = npc.GetBoundingBox(); // the 'occupied' area at the NPC's feet

            // calculate y origin
            float yOrigin;
            if (npc is DustSpirit)
                yOrigin = boundingBox.Bottom;
            else if (npc is Bat)
                yOrigin = boundingBox.Center.Y;
            else if (npc is Bug)
                yOrigin = boundingBox.Top - sprite.SpriteHeight * Game1.pixelZoom + (float)(Math.Sin(Game1.currentGameTime.TotalGameTime.Milliseconds / 1000.0 * (2.0 * Math.PI)) * 10.0);
            else if (npc is SquidKid squidKid)
                yOrigin = boundingBox.Bottom - sprite.SpriteHeight * Game1.pixelZoom + squidKid.yOffset;
            else
                yOrigin = boundingBox.Top;

            // get bounding box
            int height = sprite.SpriteHeight * Game1.pixelZoom;
            int width = sprite.SpriteWidth * Game1.pixelZoom;
            float x = boundingBox.Center.X - (width / 2);
            float y = yOrigin + boundingBox.Height - height + npc.yJumpOffset * 2;

            return new Rectangle((int)(x - Game1.uiViewport.X), (int)(y - Game1.uiViewport.Y), width, height);
        }

        /// <inheritdoc />
        public override bool SpriteIntersectsPixel(Vector2 tile, Vector2 position, Rectangle spriteArea)
        {
            NPC npc = this.Value;
            AnimatedSprite sprite = npc.Sprite;

            // allow any part of the sprite area for monsters
            // (Monsters have complicated and inconsistent sprite behaviour which isn't really
            // worth reverse-engineering, and sometimes move around so much that a pixel-perfect
            // check is inconvenient anyway.)
            if (npc is Monster)
                return spriteArea.Contains((int)position.X, (int)position.Y);

            // check sprite for non-monster NPCs
            SpriteEffects spriteEffects = npc.flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            return this.SpriteIntersectsPixel(tile, position, spriteArea, sprite.Texture, sprite.sourceRect, spriteEffects);
        }
    }
}
