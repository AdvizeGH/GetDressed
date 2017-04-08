using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GetDressed.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using xTile.Display;
using xTile.Layers;
using xTile.Tiles;
using Tile = GetDressed.Framework.Tile;

namespace GetDressed
{
    /// <summary>The main entry point.</summary>
    public class GetDressed : Mod
    {
        /*********
        ** Properties
        *********/
        /// <summary>Encapsulates the underlying mod texture management.</summary>
        private ContentHelper ContentHelper;

        /// <summary>The current per-save config settings.</summary>
        private LocalConfig PlayerConfig;

        /// <summary>The global config settings.</summary>
        private GlobalConfig GlobalConfig;

        /// <summary>Whether the mod is initialised.</summary>
        private bool IsInitialised => this.ContentHelper != null;

        /// <summary>Whether the game world is loaded and ready.</summary>
        private bool IsLoaded => this.IsInitialised && Game1.hasLoadedGame;

        /// <summary>Whether this is the first day since the player loaded their save.</summary>
        private bool IsFirstDay = true;

        /// <summary>The last patched load menu, if the game hasn't loaded yet.</summary>
        private IClickableMenu PreviousLoadMenu;

        /// <summary>The farmer data for all saves, if the game hasn't loaded yet.</summary>
        private Farmer[] Farmers = new Farmer[0];

        /// <summary>The per-save configs for all saves, if the game hasn't loaded yet.</summary>
        private LocalConfig[] FarmerConfigs = new LocalConfig[0];


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.GlobalConfig = helper.ReadConfig<GlobalConfig>();

            GameEvents.LoadContent += this.GameEvents_LoadContent;
            SaveEvents.AfterLoad += this.SaveEvents_AfterLoad;
            TimeEvents.AfterDayStarted += this.TimeEvents_AfterDayStarted;

            ControlEvents.MouseChanged += this.Event_MouseChanged;
            ControlEvents.ControllerButtonPressed += this.Event_ControllerButtonPressed;
            GameEvents.UpdateTick += this.Event_UpdateTick;
            ControlEvents.KeyPressed += this.Event_KeyPressed;
        }


        /*********
        ** Private methods
        *********/
        /// <summary>The event handler called when the mouse state changes.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void GameEvents_LoadContent(object sender, EventArgs e)
        {
            // load content manager
            this.ContentHelper = new ContentHelper(this.Helper, this.Monitor, Game1.content.ServiceProvider);

            // load per-save configs
            this.FarmerConfigs = this
                .ReadLocalConfigs()
                .OrderBy(config => config.SaveTime)
                .ToArray();
        }

        /// <summary>The event handler called when the game updates its state.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void Event_UpdateTick(object sender, EventArgs e)
        {
            // patch load menu
            if (this.IsInitialised && !Game1.hasLoadedGame)
            {
                this.PatchLoadMenu();
                return;
            }

            // remove load menu patcher
            this.Farmers = new Farmer[0];
            this.FarmerConfigs = new LocalConfig[0];
            if (!string.IsNullOrEmpty(ModConstants.PerSaveConfigPath))
                GameEvents.UpdateTick -= Event_UpdateTick;
        }

        /// <summary>The event handler called when the player loads a save and the world is ready.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void SaveEvents_AfterLoad(object sender, EventArgs e)
        {
            // load config
            this.PlayerConfig = this.Helper.ReadJsonFile<LocalConfig>(ModConstants.PerSaveConfigPath) ?? new LocalConfig();

            // patch player textures
            Texture2D playerTextures = this.ContentHelper.GetBaseFarmerTexture(Game1.player.isMale);
            if (Game1.player.isMale)
            {
                this.ContentHelper.PatchTexture(ref playerTextures, "male_faces.png", this.PlayerConfig.ChosenFace[0] * this.GlobalConfig.MaleNoseTypes + this.PlayerConfig.ChosenNose[0] + (this.PlayerConfig.ChosenShoes[0] * (this.GlobalConfig.MaleNoseTypes * this.GlobalConfig.MaleFaceTypes)), 0);
                this.ContentHelper.PatchTexture(ref playerTextures, "male_bottoms.png", (this.PlayerConfig.ChosenBottoms[0] >= this.GlobalConfig.MaleBottomsTypes) ? 0 : this.PlayerConfig.ChosenBottoms[0], 3);
            }
            else
            {
                this.ContentHelper.PatchTexture(ref playerTextures, "female_faces.png", this.PlayerConfig.ChosenFace[0] * this.GlobalConfig.FemaleNoseTypes + this.PlayerConfig.ChosenNose[0] + (this.PlayerConfig.ChosenShoes[0] * (this.GlobalConfig.FemaleNoseTypes * this.GlobalConfig.FemaleFaceTypes)), 0);
                this.ContentHelper.PatchTexture(ref playerTextures, "female_bottoms.png", this.PlayerConfig.ChosenBottoms[0], 3);
            }
            this.ContentHelper.PatchFarmerRenderer(Game1.player, playerTextures);

            // update config on first run
            if (this.PlayerConfig.FirstRun)
            {
                this.PlayerConfig.ChosenAccessory[0] = Game1.player.accessory;
                this.PlayerConfig.FirstRun = false;
                this.Helper.WriteJsonFile(ModConstants.PerSaveConfigPath, this.PlayerConfig);
            }
            else
                Game1.player.accessory = this.PlayerConfig.ChosenAccessory[0];

            // patch farmhouse tilesheet
            FarmHouse farmhouse = (FarmHouse)Game1.getLocationFromName("FarmHouse");
            this.PatchFarmhouseTilesheet(farmhouse);
        }

        /// <summary>The event handler called when the mouse state changes.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void TimeEvents_AfterDayStarted(object sender, EventArgs e)
        {
            FarmHouse farmhouse = (FarmHouse)Game1.getLocationFromName("FarmHouse");
            if (this.IsFirstDay || farmhouse.upgradeLevel != this.Helper.Reflection.GetPrivateValue<int>(farmhouse, "currentlyDisplayedUpgradeLevel"))
                this.PatchFarmhouseMap(farmhouse);
            this.IsFirstDay = false;
        }

        /// <summary>The event handler called when the mouse state changes.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void Event_MouseChanged(object sender, EventArgsMouseStateChanged e)
        {
            if (!this.IsLoaded)
                return;

            if (e.NewState.RightButton == ButtonState.Pressed && e.PriorState.RightButton != ButtonState.Pressed)
                this.CheckForAction();
        }

        /// <summary>The event handler called when the player presses a controller button.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void Event_ControllerButtonPressed(object sender, EventArgsControllerButtonPressed e)
        {
            if (!this.IsLoaded)
                return;

            if (e.ButtonPressed == Buttons.A)
                this.CheckForAction();
        }

        /// <summary>The event handler called when the player presses a keyboard button.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void Event_KeyPressed(object sender, EventArgsKeyPressed e)
        {
            if (!this.IsLoaded || Game1.activeClickableMenu != null)
                return;

            if (e.KeyPressed.ToString() == this.GlobalConfig.MenuAccessKey)
            {
                Game1.player.completelyStopAnimatingOrDoingAction();
                Game1.playSound("bigDeSelect");
                Game1.activeClickableMenu = new CharacterCustomizationMenu(this.ContentHelper, this.Helper, this.GlobalConfig, this.PlayerConfig, Game1.options.zoomLevel);
            }
        }

        /// <summary>Open the customisation menu if the player activated the dresser.</summary>
        private void CheckForAction()
        {
            if (Game1.player.UsingTool || Game1.pickingTool || Game1.menuUp || (Game1.eventUp && !Game1.currentLocation.currentEvent.playerControlSequence) || Game1.nameSelectUp || Game1.numberOfSelectedItems != -1 || Game1.fadeToBlack || Game1.activeClickableMenu != null)
                return;

            // get the activated tile
            Vector2 grabTile = new Vector2(Game1.getOldMouseX() + Game1.viewport.X, Game1.getOldMouseY() + Game1.viewport.Y) / Game1.tileSize;
            if (!Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
                grabTile = Game1.player.GetGrabTile();

            // check tile action
            xTile.Tiles.Tile tile = Game1.currentLocation.map.GetLayer("Buildings").PickTile(new xTile.Dimensions.Location((int)grabTile.X * Game1.tileSize, (int)grabTile.Y * Game1.tileSize), Game1.viewport.Size);
            xTile.ObjectModel.PropertyValue propertyValue = null;
            tile?.Properties.TryGetValue("Action", out propertyValue);
            if (propertyValue?.ToString() != "GetDressed")
                return;

            // open menu
            Game1.playSound("bigDeSelect");
            Game1.activeClickableMenu = new CharacterCustomizationMenu(this.ContentHelper, this.Helper, this.GlobalConfig, this.PlayerConfig, Game1.options.zoomLevel);
        }

        /// <summary>Patch the textures in the load menu if it's active.</summary>
        private void PatchLoadMenu()
        {
            if (!(Game1.activeClickableMenu is TitleMenu titleMenu))
                return;

            // get load menu
            LoadGameMenu loadMenu = this.Helper.Reflection.GetPrivateValue<IClickableMenu>(titleMenu, "subMenu") as LoadGameMenu;
            if (loadMenu == null || loadMenu == this.PreviousLoadMenu)
                return;
            this.PreviousLoadMenu = loadMenu;

            // skip if loading
            if (this.Helper.Reflection.GetPrivateValue<bool>(loadMenu, "loading"))
                return;

            // override load-game textures
            if (this.Farmers.Length < 1)
            {
                this.Farmers = this.Helper.Reflection.GetPrivateValue<List<Farmer>>(loadMenu, "saveGames").ToArray();
                if (this.Farmers.Length != this.FarmerConfigs.Length)
                {
                    this.Monitor.Log("GetDressed could not load per-save configs.", LogLevel.Error);
                    return;
                }

                // override textures
                for (int i = 0; i < this.Farmers.Length; i++)
                {
                    if (this.FarmerConfigs[i].FirstRun)
                    {
                        this.FarmerConfigs[i].ChosenAccessory[0] = this.Farmers[i].accessory;
                        this.FarmerConfigs[i].FirstRun = false;
                        this.Helper.WriteJsonFile(Path.Combine("psconfigs", $"{this.FarmerConfigs[i].SaveName}.json"), this.FarmerConfigs[i]);
                    }
                    Texture2D farmerBase = this.ContentHelper.GetBaseFarmerTexture(this.Farmers[i].isMale);
                    if (this.Farmers[i].isMale)
                    {
                        this.ContentHelper.PatchTexture(ref farmerBase, "male_faces.png", this.FarmerConfigs[i].ChosenFace[0] * this.GlobalConfig.MaleNoseTypes + this.FarmerConfigs[i].ChosenNose[0] + (this.FarmerConfigs[i].ChosenShoes[0] * (this.GlobalConfig.MaleNoseTypes * this.GlobalConfig.MaleFaceTypes)), 0);
                        this.ContentHelper.PatchTexture(ref farmerBase, "male_bottoms.png", (this.FarmerConfigs[i].ChosenBottoms[0] >= this.GlobalConfig.MaleBottomsTypes) ? 0 : this.FarmerConfigs[i].ChosenBottoms[0], 3);
                    }
                    else
                    {
                        this.ContentHelper.PatchTexture(ref farmerBase, "female_faces.png", this.FarmerConfigs[i].ChosenFace[0] * this.GlobalConfig.FemaleNoseTypes + this.FarmerConfigs[i].ChosenNose[0] + (this.FarmerConfigs[i].ChosenShoes[0] * (this.GlobalConfig.FemaleNoseTypes * this.GlobalConfig.FemaleFaceTypes)), 0);
                        this.ContentHelper.PatchTexture(ref farmerBase, "female_bottoms.png", this.FarmerConfigs[i].ChosenBottoms[0], 3);
                    }
                    this.ContentHelper.PatchFarmerRenderer(this.Farmers[i], farmerBase);
                    this.Farmers[i].accessory = this.FarmerConfigs[i].ChosenAccessory[0];
                }
            }

            // inject new farmers
            this.Helper.Reflection
                .GetPrivateField<List<Farmer>>(loadMenu, "saveGames")
                .SetValue(this.Farmers.ToList());
        }

        /// <summary>Read all per-save configs from disk.</summary>
        private IEnumerable<LocalConfig> ReadLocalConfigs()
        {
            // get saves path
            string savePath = Constants.SavesPath;
            if (!Directory.Exists(savePath))
                yield break;

            // get save names
            string[] directories = Directory.GetDirectories(savePath);
            if (!directories.Any())
                yield break;

            // get per-save configs
            foreach (string saveDir in directories)
            {
                // get farmer info
                Farmer farmer;
                try
                {
                    using (Stream stream = File.Open(Path.Combine(savePath, saveDir, "SaveGameInfo"), FileMode.Open))
                    {
                        farmer = (Farmer)SaveGame.farmerSerializer.Deserialize(stream);
                        SaveGame.loadDataToFarmer(farmer, farmer);
                    }
                }
                catch (IOException)
                {
                    continue;
                }

                // get config
                string localConfigPath = Path.Combine("psconfigs", $"{new DirectoryInfo(saveDir).Name}.json");
                LocalConfig farmerConfig = this.Helper.ReadJsonFile<LocalConfig>(localConfigPath);
                if (farmerConfig == null)
                {
                    farmerConfig = new LocalConfig();
                    this.Helper.WriteJsonFile(localConfigPath, farmerConfig);
                }
                farmerConfig.SaveTime = farmer.saveTime;
                farmerConfig.SaveName = new DirectoryInfo(saveDir).Name;
                yield return farmerConfig;
            }
        }

        /// <summary>Patch the dresser into the farmhouse tilesheet.</summary>
        /// <param name="farmhouse">The farmhouse to patch.</param>
        private void PatchFarmhouseTilesheet(FarmHouse farmhouse)
        {
            IDictionary<TileSheet, Texture2D> tilesheetTextures = this.Helper.Reflection.GetPrivateValue<Dictionary<TileSheet, Texture2D>>(Game1.mapDisplayDevice as XnaDisplayDevice, "m_tileSheetTextures");
            Texture2D texture = tilesheetTextures[farmhouse.map.GetTileSheet("untitled tile sheet")];
            if (texture != null)
            {
                this.ContentHelper.PatchTexture(ref texture, "dresser.png", 0, 231, 16, 16);
                this.ContentHelper.PatchTexture(ref texture, "dresser.png", 1, 232, 16, 16);
            }
        }

        /// <summary>Patch the dresser into the farmhouse map.</summary>
        /// <param name="farmhouse">The farmhouse to patch.</param>
        private void PatchFarmhouseMap(FarmHouse farmhouse)
        {
            if (!this.GlobalConfig.ShowDresser)
                return;

            // get dresser coordinates
            Point position;
            switch (farmhouse.upgradeLevel)
            {
                case 0:
                    position = new Point(this.GlobalConfig.StoveInCorner ? 7 : 10, 2);
                    break;
                case 1:
                    position = new Point(Game1.player.isMarried() ? 25 : 28, 2);
                    break;
                case 2:
                    position = new Point(33, 11);
                    break;
                case 3:
                    position = new Point(33, 11);
                    break;
                default:
                    this.Monitor.Log($"Couldn't patch dresser into farmhouse, unknown upgrade level {farmhouse.upgradeLevel}", LogLevel.Warn);
                    return;
            }

            // inject dresser
            Tile[] tiles = {
                new Tile(TileLayer.Front, position.X, position.Y, 231, "untitled tile sheet"), // dresser top
                new Tile(TileLayer.Buildings, position.X, position.Y + 1, 232, "untitled tile sheet") // dresser bottom
            };
            foreach (Tile tile in tiles)
            {
                Layer layer = farmhouse.map.GetLayer(tile.LayerName);
                TileSheet tilesheet = farmhouse.map.GetTileSheet(tile.Tilesheet);

                if (layer.Tiles[tile.X, tile.Y] == null || layer.Tiles[tile.X, tile.Y].TileSheet.Id != tile.Tilesheet)
                    layer.Tiles[tile.X, tile.Y] = new StaticTile(layer, tilesheet, BlendMode.Alpha, tile.TileID);
                else
                    farmhouse.setMapTileIndex(tile.X, tile.Y, tile.TileID, layer.Id);
            }

            // add action attribute
            farmhouse.setTileProperty(position.X, position.Y + 1, "Buildings", "Action", "GetDressed");
        }
    }
}
