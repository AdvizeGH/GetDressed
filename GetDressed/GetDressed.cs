using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GetDressed.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using xTile.Display;
using xTile.Tiles;

namespace GetDressed
{
    public class GetDressed : Mod
    {
        public static IModHelper modHelper;
        public static IMonitor modMonitor;
        public static float playerZoomLevel;
        public static LocalConfig currentConfig { get; protected set; }
        public static GlobalConfig globalConfig { get; protected set; }
        private static ContentManager cm;
        private static Texture2D accessoriesTexture;
        public static Texture2D menuTextures;

        private bool titleSubMenuChanged = false;
        private IClickableMenu previousSubMenu = null;
        private List<Farmer> farmers = new List<Farmer>();
        private List<LocalConfig> farmerConfigs = new List<LocalConfig>();
        private int loadtime = 0;
        public const string versionNumber = "3.2";

        public override void Entry(IModHelper helper)
        {
            modHelper = helper;
            modMonitor = Monitor;
            ControlEvents.MouseChanged += Event_MouseChanged;
            ControlEvents.ControllerButtonPressed += Event_ControllerButtonPressed;
            GameEvents.UpdateTick += Event_UpdateTick;
            TimeEvents.DayOfMonthChanged += Event_DayOfMonthChanged;
            globalConfig = helper.ReadConfig<GlobalConfig>();
            ControlEvents.KeyPressed += Event_KeyPressed;

            /*modMonitor.Log("0", LogLevel.Trace);
            modMonitor.Log("1", LogLevel.Debug);
            modMonitor.Log("2", LogLevel.Info);
            modMonitor.Log("3", LogLevel.Warn);
            modMonitor.Log("4", LogLevel.Error);
            modMonitor.Log("5", LogLevel.Alert);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("lol");*/
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
            if (e.KeyPressed.ToString().Equals(globalConfig.menuAccessKey))
            {
                Game1.player.completelyStopAnimatingOrDoingAction();

                if (Game1.hasLoadedGame && Game1.activeClickableMenu == null && currentConfig != null)
                {
                    playerZoomLevel = Game1.options.zoomLevel;
                    Game1.playSound("bigDeSelect");
                    Game1.activeClickableMenu = new CharacterCustomizationMenu();
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
                        playerZoomLevel = Game1.options.zoomLevel;

                        Game1.playSound("bigDeSelect");
                        Game1.activeClickableMenu = new CharacterCustomizationMenu();
                    }
                }
            }
        }

        private void Event_UpdateTick(object sender, EventArgs e)
        {
            if (cm == null) InitializeContent();

            if (!Game1.hasLoadedGame)
            {
                FixLoadGameMenu();
                return;
            }
            farmers.Clear();
            farmerConfigs.Clear();

            if (string.IsNullOrEmpty(Constants.SaveFolderName))
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

                currentConfig = modHelper.ReadJsonFile<LocalConfig>(Path.Combine("psconfigs", $"{Constants.SaveFolderName}.json")) ?? new LocalConfig();

                Texture2D farmer_base = InitTexture(Game1.player.isMale);
                if (Game1.player.isMale)
                {
                    PatchTexture(ref farmer_base, "male_faces.png", currentConfig.chosenFace[0] * globalConfig.maleNoseTypes + currentConfig.chosenNose[0] + (currentConfig.chosenShoes[0] * (globalConfig.maleNoseTypes * globalConfig.maleFaceTypes)), 0);
                    PatchTexture(ref farmer_base, "male_bottoms.png", (currentConfig.chosenBottoms[0] >= globalConfig.maleBottomsTypes) ? 0 : currentConfig.chosenBottoms[0], 3);
                }
                else
                {
                    PatchTexture(ref farmer_base, "female_faces.png", currentConfig.chosenFace[0] * globalConfig.femaleNoseTypes + currentConfig.chosenNose[0] + (currentConfig.chosenShoes[0] * (globalConfig.femaleNoseTypes * globalConfig.femaleFaceTypes)), 0);
                    PatchTexture(ref farmer_base, "female_bottoms.png", currentConfig.chosenBottoms[0], 3);
                }
                PatchFarmerRenderer(Game1.player, farmer_base);

                if (currentConfig.firstRun)
                {
                    currentConfig.chosenAccessory[0] = Game1.player.accessory;
                    currentConfig.firstRun = false;
                    modHelper.WriteJsonFile(Path.Combine("psconfigs", $"{Constants.SaveFolderName}.json"), currentConfig);
                }
                else
                {
                    Game1.player.accessory = currentConfig.chosenAccessory[0];
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

        private static void InitializeContent()
        {
            if (cm != null) return;

            cm = new ContentManager(Game1.content.ServiceProvider, Path.Combine(modHelper.DirectoryPath, "overrides"));
            try
            {
                accessoriesTexture = cm.Load<Texture2D>("accessories");
                menuTextures = cm.Load<Texture2D>("menuTextures");
            }
            catch
            {
                modMonitor.Log("Could not find either the accessories file or the menuTextures file.", LogLevel.Error);
            }
        }

        public static Texture2D InitTexture(bool isMale, bool returnNew = true)
        {
            if (cm == null)
                InitializeContent();

            ContentManager content = !returnNew ? cm : new ContentManager(Game1.content.ServiceProvider, Path.Combine(modHelper.DirectoryPath, "overrides"));
            Texture2D farmer_base;
            try
            {
                farmer_base = isMale ? content.Load<Texture2D>("farmer_base") : content.Load<Texture2D>("farmer_girl_base");
                return farmer_base;
            }
            catch
            {
                modMonitor.Log("Could not find base file.", LogLevel.Error);
            }
            return null;
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
                            Texture2D farmer_base = InitTexture(farmers[i].isMale);
                            if (farmers[i].isMale)
                            {

                                PatchTexture(ref farmer_base, "male_faces.png", farmerConfigs[i].chosenFace[0] * globalConfig.maleNoseTypes + farmerConfigs[i].chosenNose[0] + (farmerConfigs[i].chosenShoes[0] * (globalConfig.maleNoseTypes * globalConfig.maleFaceTypes)), 0);
                                PatchTexture(ref farmer_base, "male_bottoms.png", (farmerConfigs[i].chosenBottoms[0] >= globalConfig.maleBottomsTypes) ? 0 : farmerConfigs[i].chosenBottoms[0], 3);
                            }
                            else
                            {
                                PatchTexture(ref farmer_base, "female_faces.png", farmerConfigs[i].chosenFace[0] * globalConfig.femaleNoseTypes + farmerConfigs[i].chosenNose[0] + (farmerConfigs[i].chosenShoes[0] * (globalConfig.femaleNoseTypes * globalConfig.femaleFaceTypes)), 0);
                                PatchTexture(ref farmer_base, "female_bottoms.png", farmerConfigs[i].chosenBottoms[0], 3);
                            }
                            PatchFarmerRenderer(farmers[i], farmer_base);
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

        public void PatchTileSheet()
        {
            Dictionary<TileSheet, Texture2D> tileSheetTextures = (Dictionary<TileSheet, Texture2D>)typeof(XnaDisplayDevice).GetField("m_tileSheetTextures", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Game1.mapDisplayDevice as XnaDisplayDevice);
            Texture2D tex = tileSheetTextures[Game1.getLocationFromName("FarmHouse").map.TileSheets[Tile.getTileSheetIndex("untitled tile sheet", Game1.getLocationFromName("FarmHouse").map.TileSheets)]];
            if (tex != null)
            {
                PatchTexture(ref tex, "dresser.png", 0, 231, 16, 16);
                PatchTexture(ref tex, "dresser.png", 1, 232, 16, 16);
            }
        }

        private void PatchMap(FarmHouse fh)
        {
            if (!globalConfig.showDresser) return;

            List<Tile> tileArray = new List<Tile>();
            int x; int top = 231; int bottom = 232;

            switch (fh.upgradeLevel)
            {
                case 0:
                    x = globalConfig.stoveInCorner ? 7 : 10;
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
                    x = globalConfig.stoveInCorner ? 7 : 10;
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

        public void PatchFarmerRenderer(Farmer farmer, Texture2D baseFile)
        {
            if (cm == null) InitializeContent();

            farmer.FarmerRenderer = new FarmerRenderer(baseFile);
            farmer.FarmerRenderer.heightOffset = farmer.isMale ? 0 : 4;
            FarmerRenderer.accessoriesTexture = accessoriesTexture;
            FixFarmerEffects(farmer);
        }

        public static void PatchTexture(ref Texture2D targetTexture, string overridingTexturePath, int sourceID, int targetID, int gridWidth = 96, int gridHeight = 672)
        {
            using (FileStream textureStream = new FileStream(Path.Combine(cm.RootDirectory, overridingTexturePath), FileMode.Open))
            {
                Texture2D sourceTexture = Texture2D.FromStream(Game1.graphics.GraphicsDevice, textureStream);
                Color[] data = new Color[gridWidth * gridHeight];
                sourceTexture.GetData(0, GetSourceRect(sourceID, sourceTexture, gridWidth, gridHeight), data, 0, data.Length);
                targetTexture.SetData(0, GetSourceRect(targetID, targetTexture, gridWidth, gridHeight), data, 0, data.Length);
            }
        }

        private static Rectangle GetSourceRect(int index, Texture2D texture, int gridWidth, int gridHeight) => new Rectangle(index % (texture.Width / gridWidth) * gridWidth, index / (texture.Width / gridWidth) * gridHeight, gridWidth, gridHeight);

        public static void FixFarmerEffects(Farmer farmer)
        {
            farmer.changeShirt(farmer.shirt);
            farmer.changeEyeColor(farmer.newEyeColor);
            farmer.changeSkinColor(farmer.skin);

            if (farmer.boots != null)
            {
                farmer.changeShoeColor(farmer.boots.indexInColorSheet);
            }
        }
    }
}