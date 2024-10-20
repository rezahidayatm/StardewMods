using System;
using StardewValley;

namespace Pathoschild.Stardew.FastAnimations.Framework
{
    /// <summary>The base class for animation handlers.</summary>
    internal abstract class BaseAnimationHandler : IAnimationHandler
    {
        /*********
        ** Fields
        *********/
        /// <summary>The fractional skips left to apply, so fractional multipliers can be smudged over multiple update ticks. For example, a multiplier of 1.5 will skip a frame every other tick.</summary>
        private float Remainder;


        /*********
        ** Accessors
        *********/
        /// <summary>The animation speed multiplier to apply.</summary>
        protected readonly float Multiplier;

        /// <summary>The approximate number of milliseconds per update frame.</summary>
        protected const int MillisecondsPerFrame = 1000 / 60;


        /*********
        ** Public methods
        *********/
        /// <inheritdoc />
        public abstract bool TryApply(int playerAnimationId);

        /// <inheritdoc />
        public virtual void OnNewLocation(GameLocation location) { }


        /*********
        ** Protected methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="multiplier">The animation speed multiplier to apply.</param>
        protected BaseAnimationHandler(float multiplier)
        {
            this.Multiplier = multiplier;
        }

        /// <summary>Get the number of frames to skip for the current tick.</summary>
        protected int GetSkipsThisTick()
        {
            if (this.Multiplier <= 1)
                return 0;

            float skips = this.Multiplier + this.Remainder - 1; // 1 is the default speed (i.e. skip zero frames), so subtract it to get the number of skips
            this.Remainder = skips % 1;
            return (int)skips;
        }

        /// <summary>Apply an animation update for each frame that should be skipped in the current tick.</summary>
        /// <param name="run">Run one animation frame.</param>
        /// <returns>Returns whether any frames were skipped.</returns>
        protected bool ApplySkips(Action run)
        {
            int skips = this.GetSkipsThisTick();

            for (int i = 0; i < skips; i++)
                run();

            return skips > 0;
        }

        /// <summary>Apply an animation update for each frame that should be skipped in the current tick, or until the callback returns false to finish early.</summary>
        /// <param name="run">Run one animation frame. This can return false to stop skipping frames early.</param>
        /// <returns>Returns whether any frames were skipped.</returns>
        protected bool ApplySkipsWhile(Func<bool> run)
        {
            int skips = this.GetSkipsThisTick();

            for (int i = 0; i < skips; i++)
            {
                if (!run())
                    break;
            }

            return skips > 0;
        }

        /// <summary>Speed up the player by the given multiplier for the current update tick.</summary>
        /// <returns>Returns whether any frames were skipped.</returns>
        protected bool SpeedUpPlayer()
        {
            return this.ApplySkips(() =>
                Game1.player.Update(Game1.currentGameTime, Game1.player.currentLocation)
            );
        }

        /// <summary>Speed up the player by the given multiplier for the current update tick.</summary>
        /// <param name="until">Get whether the animation should stop being skipped.</param>
        /// <returns>Returns whether any frames were skipped.</returns>
        protected bool SpeedUpPlayer(Func<bool> until)
        {
            return this.ApplySkipsWhile(() =>
            {
                if (until())
                    return false;

                Game1.player.Update(Game1.currentGameTime, Game1.player.currentLocation);
                return true;
            });
        }

        /// <summary>Get whether the current player is riding a tractor from Tractor Mod.</summary>
        protected bool IsRidingTractor()
        {
            return Game1.player?.mount?.modData?.ContainsKey("Pathoschild.TractorMod") ?? false;
        }
    }
}
