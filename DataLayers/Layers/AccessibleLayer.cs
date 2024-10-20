using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Pathoschild.Stardew.DataLayers.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.Locations;
using xTile.Dimensions;
using xTile.Tiles;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Pathoschild.Stardew.DataLayers.Layers
{
    /// <summary>A data layer which shows whether tiles are traversable by the player.</summary>
    internal class AccessibleLayer : BaseLayer
    {
        /*********
        ** Fields
        *********/
        /// <summary>The legend entry for passable tiles.</summary>
        private readonly LegendEntry Clear;

        /// <summary>The legend entry for passable but occupied tiles.</summary>
        private readonly LegendEntry Occupied;

        /// <summary>The legend entry for impassable tiles.</summary>
        private readonly LegendEntry Impassable;

        /// <summary>The legend entry for warp tiles.</summary>
        private readonly LegendEntry Warp;

        /// <summary>The action tile property values which trigger a warp.</summary>
        /// <remarks>See remarks on <see cref="IsWarp"/>.</remarks>
        [SuppressMessage("ReSharper", "StringLiteralTypo", Justification = "These are game values.")]
        private readonly HashSet<string> WarpActions = ["EnterSewer", "LockedDoorWarp", "Mine", "Theater_Entrance", "Warp", "WarpCommunityCenter", "WarpGreenhouse", "WarpMensLocker", "WarpWomensLocker", "WizardHatch"];

        /// <summary>The touch action tile property values which trigger a warp.</summary>
        private readonly HashSet<string> TouchWarpActions = ["Door", "MagicWarp", "Warp"];


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="config">The data layer settings.</param>
        /// <param name="colors">The colors to render.</param>
        public AccessibleLayer(LayerConfig config, ColorScheme colors)
            : base(I18n.Accessible_Name(), config)
        {
            const string layerId = "Accessible";

            this.Legend = [
                this.Clear = new LegendEntry(I18n.Keys.Accessible_Clear, colors.Get(layerId, "Clear", Color.Green)),
                this.Occupied = new LegendEntry(I18n.Keys.Accessible_Occupied, I18n.Accessible_Occupied(), colors.Get(layerId, "Occupied", Color.Orange)),
                this.Impassable = new LegendEntry(I18n.Keys.Accessible_Impassable, I18n.Accessible_Impassable(), colors.Get(layerId, "Impassable", Color.Red)),
                this.Warp = new LegendEntry(I18n.Keys.Accessible_Warp, I18n.Accessible_Warp(), colors.Get(layerId, "Warp", Color.Blue))
            ];
        }

        /// <inheritdoc />
        public override TileGroup[] Update(ref readonly GameLocation location, ref readonly Rectangle visibleArea, ref readonly IReadOnlySet<Vector2> visibleTiles, ref readonly Vector2 cursorTile)
        {
            List<TileData> passableTiles = new();
            List<TileData> warpTiles = new();
            List<TileData> otherTiles = new();

            foreach (TileData tile in this.GetTiles(location, visibleTiles))
            {
                switch (tile.Type.Id)
                {
                    case I18n.Keys.Accessible_Clear:
                        passableTiles.Add(tile);
                        break;

                    case I18n.Keys.Accessible_Warp:
                        warpTiles.Add(this.AdjustWarpTileIfOffScreen(tile));
                        break;

                    default:
                        otherTiles.Add(tile);
                        break;
                }
            }

            return [
                new TileGroup(passableTiles, outerBorderColor: this.Clear.Color),
                new TileGroup(warpTiles, outerBorderColor: this.Clear.Color),
                new TileGroup(otherTiles)
            ];
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get a warp tile with a draw offset applied to keep it visible if it's at an off-screen position.</summary>
        /// <param name="tile">The tile to adjust.</param>
        private TileData AdjustWarpTileIfOffScreen(TileData tile)
        {
            int offsetAmount = Game1.tileSize / 3;

            Vector2 pixelPos = tile.TilePosition * Game1.tileSize;

            int offsetX = 0;
            if (pixelPos.X < Game1.viewport.X)
                offsetX = offsetAmount;
            else if (pixelPos.X >= Game1.viewport.X + Game1.viewport.Width)
                offsetX = -offsetAmount;

            int offsetY = 0;
            if (pixelPos.Y < Game1.viewport.Y)
                offsetY = offsetAmount;
            else if (pixelPos.Y >= Game1.viewport.Y + Game1.viewport.Height)
                offsetY = -offsetAmount;

            return offsetX != 0 || offsetY != 0
                ? new TileData(tile.TilePosition, tile.Type, tile.Color, new Point(offsetX, offsetY))
                : tile;
        }

        /// <summary>Get the updated data layer tiles.</summary>
        /// <param name="location">The current location.</param>
        /// <param name="visibleTiles">The tiles currently visible on the screen.</param>
        private IEnumerable<TileData> GetTiles(GameLocation location, IReadOnlySet<Vector2> visibleTiles)
        {
            // get building warps
            HashSet<Vector2> buildingDoors = [];
            foreach (Building building in location.buildings)
            {
                if (!building.HasIndoors() || (building.humanDoor.X < 0 && building.humanDoor.Y < 0))
                    continue;

                buildingDoors.Add(new Vector2(building.humanDoor.X + building.tileX.Value, building.humanDoor.Y + building.tileY.Value));
                buildingDoors.Add(new Vector2(building.humanDoor.X + building.tileX.Value, building.humanDoor.Y + building.tileY.Value - 1));
            }

            // get tile data
            foreach (Vector2 tile in visibleTiles)
            {
                // get pixel coordinates
                Rectangle tilePixels = new Rectangle((int)(tile.X * Game1.tileSize), (int)(tile.Y * Game1.tileSize), Game1.tileSize, Game1.tileSize);

                // get color
                LegendEntry type;
                if (this.IsWarp(location, tile, tilePixels, buildingDoors))
                    type = this.Warp;
                else if (location.isTilePassable(tile))
                    type = location.IsTileBlockedBy(tile, ignorePassables: CollisionMask.All) ? this.Occupied : this.Clear;
                else
                    type = this.Impassable;

                // yield
                yield return new TileData(tile, type);
            }
        }

        /// <summary>Get whether there's a warp on the given tile.</summary>
        /// <param name="location">The current location.</param>
        /// <param name="tile">The tile to check.</param>
        /// <param name="tilePixels">The tile area in pixels.</param>
        /// <param name="buildingDoors">The tile positions for farm building doors in the current location.</param>
        /// <remarks>Derived from <see cref="GameLocation.isCollidingWithWarp"/>, <see cref="GameLocation.performAction(string[],Farmer,Location)"/>, and <see cref="GameLocation.performTouchAction(string[],Vector2)"/>.</remarks>
        private bool IsWarp(GameLocation location, Vector2 tile, Rectangle tilePixels, HashSet<Vector2> buildingDoors)
        {
            // check farm building doors
            if (buildingDoors.Contains(tile))
                return true;

            // check tile actions
            Tile buildingTile = location.map.GetLayer("Buildings").PickTile(new Location(tilePixels.X, tilePixels.Y), Game1.viewport.Size);
            if (buildingTile != null && buildingTile.Properties.TryGetValue("Action", out string? action) && this.WarpActions.Contains(action.Split(' ')[0]))
                return true;

            // check tile touch actions
            Tile backTile = location.map.GetLayer("Back").PickTile(new Location(tilePixels.X, tilePixels.Y), Game1.viewport.Size);
            if (backTile != null && backTile.Properties.TryGetValue("TouchAction", out string? touchAction) && this.TouchWarpActions.Contains(touchAction.Split(' ')[0]))
                return true;

            // check map warps
            if (this.IsCollidingWithWarpOrDoor(location, tilePixels))
                return true;

            // check mine ladders/shafts
            const int ladderID = 173, shaftID = 174;
            if (location is MineShaft && buildingTile is { TileIndex: ladderID or shaftID } && buildingTile.TileSheet.Id == "mine")
                return true;

            return false;
        }

        /// <summary>Get whether there's a warp or door that overlaps the given tile.</summary>
        /// <param name="location">The current location.</param>
        /// <param name="tilePixels">The tile area in pixels.</param>
        private bool IsCollidingWithWarpOrDoor(GameLocation location, Rectangle tilePixels)
        {
            try
            {
                if (location.isCollidingWithWarpOrDoor(tilePixels) != null)
                {
                    // check again without the tile edges to avoid adjacent tiles being marked as warps
                    const int narrowBy = 2;
                    Rectangle narrowerArea = new Rectangle(tilePixels.X + narrowBy, tilePixels.Y + narrowBy, tilePixels.Width - 2 * narrowBy, tilePixels.Height - 2 * narrowBy);
                    return location.isCollidingWithWarpOrDoor(narrowerArea) != null;
                }
            }
            catch
            {
                // This fails in some cases like the movie theater entrance (which is checked via
                // this.WarpActions above) or TMX Loader's custom tile properties. It's safe to
                // ignore the error here, since that means it's not a valid warp.
            }

            return false;
        }
    }
}
