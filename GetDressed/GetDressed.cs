using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using xTile.Tiles;
using Tile = GetDressed.Framework.Tile;

namespace GetDressed
{
    public class GetDressed : Mod
    {
        private ContentHelper ContentHelper;
        private LocalConfig PlayerConfig;
        private GlobalConfig GlobalConfig;

        private bool titleSubMenuChanged = false;
        private IClickableMenu previousSubMenu = null;
        private List<Farmer> farmers = new List<Farmer>();
        private List<LocalConfig> farmerConfigs = new List<LocalConfig>();
        private int loadtime = 0;

        public override void Entry(IModHelper helper)
        {
            this.ContentHelper = new ContentHelper(helper, this.Monitor);

            ControlEvents.MouseChanged += Event_MouseChanged;
            ControlEvents.ControllerButtonPressed += Event_ControllerButtonPressed;
            GameEvents.UpdateTick += Event_UpdateTick;
            TimeEvents.DayOfMonthChanged += Event_DayOfMonthChanged;
            this.GlobalConfig = helper.ReadConfig<GlobalConfig>();
            ControlEvents.KeyPressed += Event_KeyPressed;
        }

        private void Event_MouseChanged(object sender, EventArgsMouseStateChanged e)
        {
            if (!Game1.hasLoadedGame) return;
            if (e.NewState.RightButton == ButtonState.Pressed && e.PriorState.RightButton != ButtonState.Pressed)
            {
                CheckForAction();
            }
        }

        private void Event_ControllerButtonPressed(object sender, EventArgsControllerButtonPressed e)
        {
            if (!Game1.hasLoadedGame) return;
            if (e.ButtonPressed == Buttons.A)
            {
                CheckForAction();
            }
        }

        private void Event_KeyPressed(object sender, EventArgsKeyPressed e)
        {
            if (e.KeyPressed.ToString().Equals(this.GlobalConfig.menuAccessKey))
            {
                Game1.player.completelyStopAnimatingOrDoingAction();

                if (Game1.hasLoadedGame && Game1.activeClickableMenu == null && this.PlayerConfig != null)
                {
                    Game1.playSound("bigDeSelect");
                    Game1.activeClickableMenu = new CharacterCustomizationMenu(this.ContentHelper, this.Helper, this.GlobalConfig, this.PlayerConfig, Game1.options.zoomLevel);
                }
            }
        }

        private void CheckForAction()
        {
            if (!Game1.player.UsingTool && !Game1.pickingTool && !Game1.menuUp && (!Game1.eventUp || Game1.currentLocation.currentEvent.playerControlSequence) && !Game1.nameSelectUp && Game1.numberOfSelectedItems == -1 && !Game1.fadeToBlack && Game1.activeClickableMenu == null)
            {
                Vector2 grabTile = new Vector2(Game1.getOldMouseX() + Game1.viewport.X, Game1.getOldMouseY() + Game1.viewport.Y) / Game1.tileSize;
                if (!Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
                {
                    grabTile = Game1.player.GetGrabTile();
                }
                xTile.Tiles.Tile tile = Game1.currentLocation.map.GetLayer("Buildings").PickTile(new xTile.Dimensions.Location((int)grabTile.X * Game1.tileSize, (int)grabTile.Y * Game1.tileSize), Game1.viewport.Size);
                xTile.ObjectModel.PropertyValue propertyValue = null;
                if (tile != null)
                {
                    tile.Properties.TryGetValue("Action", out propertyValue);
                }
                if (propertyValue != null)
                {
                    if (propertyValue == "GetDressed")
                    {
                        Game1.playSound("bigDeSelect");
                        Game1.activeClickableMenu = new CharacterCustomizationMenu(this.ContentHelper, this.Helper, this.GlobalConfig, this.PlayerConfig, Game1.options.zoomLevel);
                    }
                }
            }
        }

        private void Event_UpdateTick(object sender, EventArgs e)
        {
            if (!this.ContentHelper.IsInitialised)
                this.ContentHelper.InitializeContent(Game1.content.ServiceProvider);

            if (!Game1.hasLoadedGame)
            {
                FixLoadGameMenu();
                return;
            }
            farmers.Clear();
            farmerConfigs.Clear();

            if (string.IsNullOrEmpty(ModConstants.PerSaveConfigPath))
                return;

            GameEvents.EighthUpdateTick += Event_EightUpdateTick;
            GameEvents.UpdateTick -= Event_UpdateTick;
        }

        private void Event_EightUpdateTick(object sender, EventArgs e)
        {
            if (Game1.player.currentLocation != Game1.getLocationFromName("FarmHouse")) return;

            loadtime++;
            //Log.SyncColour("LOADTIME NUMBER " + loadtime, ConsoleColor.Cyan);
            if (loadtime > 5)
            {
                FarmHouse fh = Game1.getLocationFromName("FarmHouse") as FarmHouse;

                if (SaveGame.loaded != null)
                {
                    if (fh.upgradeLevel != SaveGame.loaded.player.houseUpgradeLevel)
                        return;
                }

                this.PlayerConfig = this.Helper.ReadJsonFile<LocalConfig>(ModConstants.PerSaveConfigPath) ?? new LocalConfig();

                Texture2D farmer_base = this.ContentHelper.InitTexture(Game1.player.isMale);
                if (Game1.player.isMale)
                {
                    this.ContentHelper.PatchTexture(ref farmer_base, "male_faces.png", this.PlayerConfig.chosenFace[0] * this.GlobalConfig.maleNoseTypes + this.PlayerConfig.chosenNose[0] + (this.PlayerConfig.chosenShoes[0] * (this.GlobalConfig.maleNoseTypes * this.GlobalConfig.maleFaceTypes)), 0);
                    this.ContentHelper.PatchTexture(ref farmer_base, "male_bottoms.png", (this.PlayerConfig.chosenBottoms[0] >= this.GlobalConfig.maleBottomsTypes) ? 0 : this.PlayerConfig.chosenBottoms[0], 3);
                }
                else
                {
                    this.ContentHelper.PatchTexture(ref farmer_base, "female_faces.png", this.PlayerConfig.chosenFace[0] * this.GlobalConfig.femaleNoseTypes + this.PlayerConfig.chosenNose[0] + (this.PlayerConfig.chosenShoes[0] * (this.GlobalConfig.femaleNoseTypes * this.GlobalConfig.femaleFaceTypes)), 0);
                    this.ContentHelper.PatchTexture(ref farmer_base, "female_bottoms.png", this.PlayerConfig.chosenBottoms[0], 3);
                }
                this.ContentHelper.PatchFarmerRenderer(Game1.player, farmer_base);

                if (this.PlayerConfig.firstRun)
                {
                    this.PlayerConfig.chosenAccessory[0] = Game1.player.accessory;
                    this.PlayerConfig.firstRun = false;
                    this.Helper.WriteJsonFile(ModConstants.PerSaveConfigPath, this.PlayerConfig);
                }
                else
                {
                    Game1.player.accessory = this.PlayerConfig.chosenAccessory[0];
                }

                try
                {
                    PatchTileSheet();
                }
                finally
                {
                    PatchMap(fh);
                }

                GameEvents.EighthUpdateTick -= Event_EightUpdateTick;
            }
        }

        private void Event_DayOfMonthChanged(object sender, EventArgs e)
        {
            int displayedUpgradeLevel = (int)typeof(FarmHouse).GetField("currentlyDisplayedUpgradeLevel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(((FarmHouse)Game1.getLocationFromName("FarmHouse")));

            if (((FarmHouse)Game1.getLocationFromName("FarmHouse")).upgradeLevel != displayedUpgradeLevel)
            {
                GameEvents.SecondUpdateTick += Event_SecondUpdateTick;
            }
        }

        private void Event_SecondUpdateTick(object sender, EventArgs e)
        {
            int displayedUpgradeLevel = (int)typeof(FarmHouse).GetField("currentlyDisplayedUpgradeLevel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(((FarmHouse)Game1.getLocationFromName("FarmHouse")));
            if (((FarmHouse)Game1.getLocationFromName("FarmHouse")).upgradeLevel != displayedUpgradeLevel) return;

            PatchMap(Game1.getLocationFromName("FarmHouse") as FarmHouse);
            GameEvents.SecondUpdateTick -= Event_SecondUpdateTick;
        }

        private void FixLoadGameMenu()
        {
            if (!(Game1.activeClickableMenu is TitleMenu)) return;

            IClickableMenu currentSubMenu = (IClickableMenu)typeof(TitleMenu).GetField("subMenu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Game1.activeClickableMenu);

            if (currentSubMenu != previousSubMenu)
                titleSubMenuChanged = true;

            if (currentSubMenu != null)
            {
                previousSubMenu = currentSubMenu;
                if (currentSubMenu is LoadGameMenu && titleSubMenuChanged)
                {
                    bool loading = (bool)typeof(LoadGameMenu).GetField("loading", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(currentSubMenu);
                    if (loading) return;

                    if (farmers.Count < 1)
                    {
                        GetFarmerConfigs();
                        farmers = (List<Farmer>)typeof(LoadGameMenu).GetField("saveGames", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(currentSubMenu);
                        if (farmers.Count != farmerConfigs.Count)
                        {
                            Monitor.Log("GetDressed could not load per-save configs", LogLevel.Error);
                            return;
                        }
                        for (int i = 0; i < farmers.Count; i++)
                        {
                            if (farmerConfigs[i].firstRun)
                            {
                                farmerConfigs[i].chosenAccessory[0] = farmers[i].accessory;
                                farmerConfigs[i].firstRun = false;
                                Helper.WriteJsonFile(Path.Combine("psconfigs", $"{farmerConfigs[i].saveName}.json"), farmerConfigs[i]);
                            }
                            Texture2D farmer_base = this.ContentHelper.InitTexture(farmers[i].isMale);
                            if (farmers[i].isMale)
                            {
                                this.ContentHelper.PatchTexture(ref farmer_base, "male_faces.png", farmerConfigs[i].chosenFace[0] * this.GlobalConfig.maleNoseTypes + farmerConfigs[i].chosenNose[0] + (farmerConfigs[i].chosenShoes[0] * (this.GlobalConfig.maleNoseTypes * this.GlobalConfig.maleFaceTypes)), 0);
                                this.ContentHelper.PatchTexture(ref farmer_base, "male_bottoms.png", (farmerConfigs[i].chosenBottoms[0] >= this.GlobalConfig.maleBottomsTypes) ? 0 : farmerConfigs[i].chosenBottoms[0], 3);
                            }
                            else
                            {
                                this.ContentHelper.PatchTexture(ref farmer_base, "female_faces.png", farmerConfigs[i].chosenFace[0] * this.GlobalConfig.femaleNoseTypes + farmerConfigs[i].chosenNose[0] + (farmerConfigs[i].chosenShoes[0] * (this.GlobalConfig.femaleNoseTypes * this.GlobalConfig.femaleFaceTypes)), 0);
                                this.ContentHelper.PatchTexture(ref farmer_base, "female_bottoms.png", farmerConfigs[i].chosenBottoms[0], 3);
                            }
                            this.ContentHelper.PatchFarmerRenderer(farmers[i], farmer_base);
                            farmers[i].accessory = farmerConfigs[i].chosenAccessory[0];
                        }
                    }

                    typeof(LoadGameMenu).GetField("saveGames", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(currentSubMenu, farmers);
                    titleSubMenuChanged = false;
                }
            }
        }

        private void GetFarmerConfigs()
        {
            string savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Saves");
            if (Directory.Exists(savePath))
            {
                string[] directories = Directory.GetDirectories(savePath);
                if (directories.Count() > 0)
                {
                    for (int j = 0; j < directories.Length; j++)
                    {
                        try
                        {
                            Stream stream = null;
                            try
                            {
                                stream = File.Open(Path.Combine(savePath, directories[j], "SaveGameInfo"), FileMode.Open);
                            }
                            catch (IOException)
                            {
                                if (stream != null)
                                {
                                    stream.Close();
                                }
                            }
                            if (stream != null)
                            {
                                Farmer farmer = (Farmer)SaveGame.farmerSerializer.Deserialize(stream);
                                SaveGame.loadDataToFarmer(farmer, farmer);
                                LocalConfig farmerConfig = Helper.ReadJsonFile<LocalConfig>(Path.Combine("psconfigs", $"{new DirectoryInfo(directories[j]).Name}.json")) ?? null;
                                if (farmerConfig == null)
                                {
                                    farmerConfig = new LocalConfig();
                                    Helper.WriteJsonFile(Path.Combine("psconfigs", $"{new DirectoryInfo(directories[j]).Name}.json"), farmerConfig);
                                }
                                farmerConfig.saveTime = farmer.saveTime;
                                farmerConfig.saveName = new DirectoryInfo(directories[j]).Name;
                                farmerConfigs.Add(farmerConfig);
                                stream.Close();
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
            farmerConfigs.Sort();
        }

        private void PatchTileSheet()
        {
            Dictionary<TileSheet, Texture2D> tileSheetTextures = (Dictionary<TileSheet, Texture2D>)typeof(XnaDisplayDevice).GetField("m_tileSheetTextures", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Game1.mapDisplayDevice as XnaDisplayDevice);
            Texture2D tex = tileSheetTextures[Game1.getLocationFromName("FarmHouse").map.TileSheets[Tile.getTileSheetIndex("untitled tile sheet", Game1.getLocationFromName("FarmHouse").map.TileSheets)]];
            if (tex != null)
            {
                this.ContentHelper.PatchTexture(ref tex, "dresser.png", 0, 231, 16, 16);
                this.ContentHelper.PatchTexture(ref tex, "dresser.png", 1, 232, 16, 16);
            }
        }

        private void PatchMap(FarmHouse fh)
        {
            if (!this.GlobalConfig.showDresser) return;

            List<Tile> tileArray = new List<Tile>();
            int x; int top = 231; int bottom = 232;

            switch (fh.upgradeLevel)
            {
                case 0:
                    x = this.GlobalConfig.stoveInCorner ? 7 : 10;
                    tileArray.Add(new Tile(2, x, 2, /*1224*/top, tileSheet: "untitled tile sheet"));
                    tileArray.Add(new Tile(1, x, 3, /*179*/bottom, tileSheet: "untitled tile sheet"));
                    break;
                case 1:
                    x = Game1.player.isMarried() ? 25 : 28;
                    tileArray.Add(new Tile(2, x, 2, /*1224*/top, tileSheet: "untitled tile sheet"));
                    tileArray.Add(new Tile(1, x, 3, /*179*/bottom, tileSheet: "untitled tile sheet"));
                    break;
                case 2:
                    tileArray.Add(new Tile(2, 33, 11, /*1224*/top, tileSheet: "untitled tile sheet"));
                    tileArray.Add(new Tile(1, 33, 12, /*179*/bottom, tileSheet: "untitled tile sheet"));
                    break;
                case 3:
                    tileArray.Add(new Tile(2, 33, 11, /*1224*/top, tileSheet: "untitled tile sheet"));
                    tileArray.Add(new Tile(1, 33, 12, /*179*/bottom, tileSheet: "untitled tile sheet"));
                    break;
                default:
                    Monitor.Log("Non-standard house upgrade level " + fh.upgradeLevel, LogLevel.Error);
                    break;
            }

            tileArray = InitializeTileArray(fh, tileArray);

            foreach (Tile tile in tileArray)
            {
                if (tile.tileIndex < 0)
                {
                    fh.removeTile(tile.x, tile.y, tile.layer);
                    continue;
                }

                if (fh.map.GetLayer(tile.layer).Tiles[tile.x, tile.y] == null || fh.map.GetLayer(tile.layer).Tiles[tile.x, tile.y].TileSheet.Id != tile.tileSheet)
                {
                    fh.map.GetLayer(tile.layer).Tiles[tile.x, tile.y] = new StaticTile(fh.map.GetLayer(tile.layer), fh.map.TileSheets[tile.tileSheetIndex], BlendMode.Alpha, tile.tileIndex);
                }
                else
                {
                    fh.setMapTileIndex(tile.x, tile.y, tile.tileIndex, fh.map.GetLayer(tile.layer).Id);
                }
            }
            switch (fh.upgradeLevel)
            {
                case 0:
                    x = this.GlobalConfig.stoveInCorner ? 7 : 10;
                    fh.setTileProperty(x, 3, "Buildings", "Action", "GetDressed");
                    break;
                case 1:
                    x = Game1.player.isMarried() ? 25 : 28;
                    fh.setTileProperty(x, 3, "Buildings", "Action", "GetDressed");
                    break;
                case 2:
                    fh.setTileProperty(33, 12, "Buildings", "Action", "GetDressed");
                    break;
                case 3:
                    fh.setTileProperty(33, 12, "Buildings", "Action", "GetDressed");
                    break;
                default:
                    Monitor.Log("Non-standard house upgrade level " + fh.upgradeLevel, LogLevel.Error);
                    break;
            }
        }

        private List<Tile> InitializeTileArray(GameLocation gl, List<Tile> tileArray)
        {
            foreach (Tile tile in tileArray)
            {
                if (tile.layerIndex < 0 || tile.layerIndex >= gl.map.Layers.Count)
                {
                    tile.layerIndex = Tile.getLayerIndex(tile.layer, gl.map.Layers);
                }
                if (tile.tileSheetIndex < 0 || tile.tileSheetIndex >= gl.map.TileSheets.Count)
                {
                    tile.tileSheetIndex = Tile.getTileSheetIndex(tile.tileSheet, gl.map.TileSheets);
                }
                if (string.IsNullOrEmpty(tile.layer))
                {
                    tile.layer = Tile.getLayerName(tile.layerIndex, gl.map.Layers);
                }
                if (string.IsNullOrEmpty(tile.tileSheet))
                {
                    tile.tileSheet = Tile.getTileSheetName(tile.tileSheetIndex, gl.map.TileSheets);
                }
            }
            return tileArray;
        }
    }
}