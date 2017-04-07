using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json;
using xTile.Display;
using xTile.Tiles;

using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewModdingAPI;
using StardewModdingAPI.Events;

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
                    Game1.activeClickableMenu = new CharacterCustomization();
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
                        Game1.activeClickableMenu = new CharacterCustomization();
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

    public class CharacterCustomization : IClickableMenu
    {
        private List<Alert> alerts = new List<Alert>();

        private ColorPicker pantsColorPicker;

        private ColorPicker hairColorPicker;

        private ColorPicker eyeColorPicker;

        private ColorPicker lastHeldColorPicker;

        private List<ClickableComponent> menuTabs = new List<ClickableComponent>();

        private List<ClickableComponent> labels = new List<ClickableComponent>();

        private List<ClickableComponent> GDlabels = new List<ClickableComponent>();

        private List<ClickableComponent> arrowButtons = new List<ClickableComponent>();

        private List<ClickableComponent> genderButtons = new List<ClickableComponent>();

        private List<ClickableTextureComponent> primaryFavButtons = new List<ClickableTextureComponent>();

        private List<ClickableTextureComponent> secondaryFavButtons = new List<ClickableTextureComponent>();

        private List<ClickableTextureComponent> extraFavButtons = new List<ClickableTextureComponent>();

        private List<ClickableTextureComponent> saveFavButtons = new List<ClickableTextureComponent>();

        private ClickableTextureComponent charIcon;

        private ClickableTextureComponent favIcon;

        private ClickableTextureComponent aboutIcon;

        private ClickableTextureComponent quickFavsIcon;

        private ClickableTextureComponent extraFavsIcon;

        private ClickableTextureComponent cancelButton;

        private ClickableTextureComponent randomButton;

        private ClickableTextureComponent okButton;

        private ClickableTextureComponent maleOutlineButton;

        private ClickableTextureComponent femaleOutlineButton;

        private ClickableTextureComponent loadFavButton;

        private ClickableTextureComponent saveFavButton;

        private ClickableTextureComponent setNewMenuAccessKeyButton;

        private ClickableTextureComponent hideMaleSkirtsButton;

        private ClickableTextureComponent zoomOutButton;

        private ClickableTextureComponent zoomInButton;

        private ClickableTextureComponent resetConfigButton;

        private TemporaryAnimatedSprite floatingPurpleArrow;

        private TemporaryAnimatedSprite floatingNewButton;

        private int timesRandom;

        private int colorPickerTimer;

        private int favSelected = -1;

        private int currentTab = 1;

        public int faceType = GetDressed.currentConfig.chosenFace[0];

        public int noseType = GetDressed.currentConfig.chosenNose[0];

        public int bottoms = GetDressed.currentConfig.chosenBottoms[0];

        public int shoes = GetDressed.currentConfig.chosenShoes[0];

        private string hoverText = "";

        public bool letUserAssignOpenMenuKey = false;

        public bool showFloatingPurpleArrow = false;

        public CharacterCustomization() : base(Game1.viewport.Width / 2 - (680 + borderWidth * 2) / 2, Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize, 632 + borderWidth * 2, 600 + borderWidth * 4 + Game1.tileSize, false)
        {
            Game1.options.zoomLevel = GetDressed.globalConfig.menuZoomOut ? 0.75f : 1f;
            Game1.overrideGameMenuReset = true;
            //Program.gamePtr.refreshWindowSettings();
            Game1.game1.refreshWindowSettings();
            xPositionOnScreen = Game1.viewport.Width / 2 - (680 + borderWidth * 2) / 2;
            yPositionOnScreen = Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize;
            setUpPositions();
            Game1.player.faceDirection(2);
            Game1.player.FarmerSprite.StopAnimation();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            xPositionOnScreen = Game1.viewport.Width / 2 - (680 + borderWidth * 2) / 2;
            yPositionOnScreen = Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize;
            setUpPositions();
        }

        private void setUpPositions()
        {
            menuTabs.Clear();
            labels.Clear();
            GDlabels.Clear();
            arrowButtons.Clear();
            genderButtons.Clear();

            primaryFavButtons.Clear();
            secondaryFavButtons.Clear();
            extraFavButtons.Clear();
            saveFavButtons.Clear();

            //Tabs Buttons and Icons & Cancel Button
            charIcon = new ClickableTextureComponent("Customize Character", new Rectangle(xPositionOnScreen + 62, yPositionOnScreen + 40, 50, 50), "", "", GetDressed.menuTextures, new Rectangle(9, 48, 8, 11), Game1.pixelZoom);
            favIcon = new ClickableTextureComponent("Manage Favorites", new Rectangle(xPositionOnScreen + 125, yPositionOnScreen + 40, 50, 50), "", "", GetDressed.menuTextures, new Rectangle(24, 26, 8, 8), Game1.pixelZoom);
            aboutIcon = new ClickableTextureComponent("About", new Rectangle(xPositionOnScreen + 188, yPositionOnScreen + 33, 50, 50), "", "", GetDressed.menuTextures, new Rectangle(0, 48, 8, 11), Game1.pixelZoom);
            quickFavsIcon = new ClickableTextureComponent("Quick Outfits", new Rectangle(xPositionOnScreen - 23, yPositionOnScreen + 122, 50, 50), "", "", GetDressed.menuTextures, new Rectangle(8, 26, 8, 8), Game1.pixelZoom);
            extraFavsIcon = new ClickableTextureComponent("Extra Outfits", new Rectangle(xPositionOnScreen - 23, yPositionOnScreen + 186, 50, 50), "", "", GetDressed.menuTextures, new Rectangle(0, 26, 8, 8), Game1.pixelZoom);

            menuTabs.Add(charIcon);
            menuTabs.Add(favIcon);
            menuTabs.Add(aboutIcon);
            menuTabs.Add(quickFavsIcon);
            menuTabs.Add(extraFavsIcon);

            cancelButton = new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 675) + Game1.pixelZoom * 12, (yPositionOnScreen - 125) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), Game1.mouseCursors, new Rectangle(337, 494, 12, 12), Game1.pixelZoom);

            #region Set Up Character Page
            //Character Menu
            randomButton = new ClickableTextureComponent(new Rectangle(xPositionOnScreen + Game1.pixelZoom * 12, (yPositionOnScreen + 75) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), Game1.mouseCursors, new Rectangle(381, 361, 10, 10), Game1.pixelZoom);
            okButton = new ClickableTextureComponent("OK", new Rectangle(xPositionOnScreen + 30 + width - borderWidth - spaceToClearSideBorder - Game1.tileSize, yPositionOnScreen + height - borderWidth - spaceToClearTopBorder + Game1.tileSize / 4, Game1.tileSize, Game1.tileSize), null, null, Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46, -1, -1), 1f, false);

            int num = Game1.tileSize * 2;
            int num2 = 70;

            arrowButtons.Add(new ClickableTextureComponent("Direction", new Rectangle(xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize / 4, (yPositionOnScreen + 75) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "-1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44, -1, -1), 1f, false));
            arrowButtons.Add(new ClickableTextureComponent("Direction", new Rectangle(xPositionOnScreen + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth + Game1.tileSize * 2, (yPositionOnScreen + 75) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33, -1, -1), 1f, false));

            genderButtons.Add(new ClickableTextureComponent("Male", new Rectangle((xPositionOnScreen + 25) + spaceToClearSideBorder + borderWidth + Game1.tileSize / 2 + 8, (yPositionOnScreen + 70) + borderWidth + spaceToClearTopBorder + Game1.tileSize * 3, Game1.tileSize, Game1.tileSize), null, "Male", Game1.mouseCursors, new Rectangle(128, 192, 16, 16), (Game1.pixelZoom / 2), false));
            genderButtons.Add(new ClickableTextureComponent("Female", new Rectangle((xPositionOnScreen + 25) + spaceToClearSideBorder + borderWidth + Game1.tileSize / 2 + Game1.tileSize + 8, (yPositionOnScreen + 70) + borderWidth + spaceToClearTopBorder + Game1.tileSize * 3, Game1.tileSize, Game1.tileSize), null, "Female", Game1.mouseCursors, new Rectangle(144, 192, 16, 16), (Game1.pixelZoom / 2), false));

            maleOutlineButton = new ClickableTextureComponent("", new Rectangle((xPositionOnScreen + 24) + spaceToClearSideBorder + borderWidth + Game1.tileSize / 2 + 8, (yPositionOnScreen + 68) + borderWidth + spaceToClearTopBorder + Game1.tileSize * 3, Game1.tileSize, Game1.tileSize), "", "", GetDressed.menuTextures, new Rectangle(19, 38, 19, 19), (Game1.pixelZoom / 2), false);
            femaleOutlineButton = new ClickableTextureComponent("", new Rectangle((xPositionOnScreen + 22) + spaceToClearSideBorder + borderWidth + Game1.tileSize / 2 + Game1.tileSize + 8, (yPositionOnScreen + 67) + borderWidth + spaceToClearTopBorder + Game1.tileSize * 3, Game1.tileSize, Game1.tileSize), "", "", GetDressed.menuTextures, new Rectangle(19, 38, 19, 19), (Game1.pixelZoom / 2), false);

            num = Game1.tileSize * 4 + 8 + 50;
            arrowButtons.Add(new ClickableTextureComponent("Skin", new Rectangle((xPositionOnScreen + 16) + spaceToClearSideBorder + borderWidth + Game1.tileSize / 4, (yPositionOnScreen + 36) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "-1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44, -1, -1), 0.75f, false));
            labels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize / 4 + Game1.tileSize + 8, (yPositionOnScreen + 20) + borderWidth + spaceToClearTopBorder + num + 16, 1, 1), "Skin"));
            arrowButtons.Add(new ClickableTextureComponent("Skin", new Rectangle(xPositionOnScreen + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth + Game1.tileSize * 2, (yPositionOnScreen + 36) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33, -1, -1), 0.75f, false));

            arrowButtons.Add(new ClickableTextureComponent("Face Type", new Rectangle((xPositionOnScreen + 16) + Game1.tileSize * 4 + spaceToClearSideBorder + borderWidth, (yPositionOnScreen + 36) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "-1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44, -1, -1), 0.75f, false));
            GDlabels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + Game1.tileSize * 5 + 8 + spaceToClearSideBorder + borderWidth, (yPositionOnScreen + 20) + borderWidth + spaceToClearTopBorder + num + 16, 1, 1), "Face Type"));
            arrowButtons.Add(new ClickableTextureComponent("Face Type", new Rectangle(xPositionOnScreen + Game1.tileSize * 7 + spaceToClearSideBorder + borderWidth, (yPositionOnScreen + 36) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33, -1, -1), 0.75f, false));

            labels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth + Game1.tileSize * 4 + 8, (yPositionOnScreen + 0) + borderWidth + spaceToClearTopBorder + num2 + 16, 1, 1), "Eye Color:"));
            eyeColorPicker = new ColorPicker(xPositionOnScreen + spaceToClearSideBorder + Game1.tileSize * 6 + Game1.tileSize * 3 / 4 + borderWidth, (yPositionOnScreen + 0) + borderWidth + spaceToClearTopBorder + num2);
            eyeColorPicker.setColor(Game1.player.newEyeColor);
            num += Game1.tileSize + 8 + 30;
            num2 += Game1.tileSize + 8;
            arrowButtons.Add(new ClickableTextureComponent("Hair", new Rectangle((xPositionOnScreen + 16) + Game1.tileSize / 4 + borderWidth + spaceToClearSideBorder, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "-1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44, -1, -1), 0.75f, false));
            labels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth + Game1.tileSize + 8, yPositionOnScreen + borderWidth + spaceToClearTopBorder + num + 16, 1, 1), "Hair"));
            arrowButtons.Add(new ClickableTextureComponent("Hair", new Rectangle(xPositionOnScreen + Game1.tileSize / 4 + spaceToClearSideBorder + Game1.tileSize * 2 + borderWidth, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33, -1, -1), 0.75f, false));

            arrowButtons.Add(new ClickableTextureComponent("Nose Type", new Rectangle((xPositionOnScreen + 16) + Game1.tileSize * 4 + spaceToClearSideBorder + borderWidth, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "-1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44, -1, -1), 0.75f, false));
            GDlabels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + Game1.tileSize * 5 + 8 + spaceToClearSideBorder + borderWidth, yPositionOnScreen + borderWidth + spaceToClearTopBorder + num + 16, 1, 1), "Nose Type"));
            arrowButtons.Add(new ClickableTextureComponent("Nose Type", new Rectangle(xPositionOnScreen + Game1.tileSize * 7 + spaceToClearSideBorder + borderWidth, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33, -1, -1), 0.75f, false));

            labels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth + Game1.tileSize * 4 + 8, (yPositionOnScreen + 0) + borderWidth + spaceToClearTopBorder + num2 + 16, 1, 1), "Hair Color:"));
            hairColorPicker = new ColorPicker(xPositionOnScreen + spaceToClearSideBorder + Game1.tileSize * 6 + Game1.tileSize * 3 / 4 + borderWidth, (yPositionOnScreen + 0) + borderWidth + spaceToClearTopBorder + num2);
            hairColorPicker.setColor(Game1.player.hairstyleColor);
            num += Game1.tileSize + 8;
            num2 += Game1.tileSize + 8;
            arrowButtons.Add(new ClickableTextureComponent("Shirt", new Rectangle((xPositionOnScreen + 16) + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "-1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44, -1, -1), 0.75f, false));
            labels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth + Game1.tileSize + 4, yPositionOnScreen + borderWidth + spaceToClearTopBorder + num + 16, 1, 1), "Shirt"));
            arrowButtons.Add(new ClickableTextureComponent("Shirt", new Rectangle(xPositionOnScreen + Game1.tileSize / 4 + spaceToClearSideBorder + Game1.tileSize * 2 + borderWidth, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33, -1, -1), 0.75f, false));

            arrowButtons.Add(new ClickableTextureComponent("Bottoms", new Rectangle((xPositionOnScreen + 16) + Game1.tileSize * 4 + spaceToClearSideBorder + borderWidth, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "-1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44, -1, -1), 0.75f, false));
            GDlabels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + Game1.tileSize * 5 + 8 + spaceToClearSideBorder + borderWidth, yPositionOnScreen + borderWidth + spaceToClearTopBorder + num + 16, 1, 1), " Bottoms"));
            arrowButtons.Add(new ClickableTextureComponent("Bottoms", new Rectangle(xPositionOnScreen + Game1.tileSize * 7 + spaceToClearSideBorder + borderWidth, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33, -1, -1), 0.75f, false));

            labels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth + Game1.tileSize * 4 + 8, (yPositionOnScreen + 0) + borderWidth + spaceToClearTopBorder + num2 + 16, 1, 1), "Pants Color:"));
            pantsColorPicker = new ColorPicker(xPositionOnScreen + spaceToClearSideBorder + Game1.tileSize * 6 + Game1.tileSize * 3 / 4 + borderWidth, (yPositionOnScreen + 0) + borderWidth + spaceToClearTopBorder + num2);
            pantsColorPicker.setColor(Game1.player.pantsColor);

            num += Game1.tileSize + 8;
            arrowButtons.Add(new ClickableTextureComponent("Acc", new Rectangle((xPositionOnScreen + 16) + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "-1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44, -1, -1), 0.75f, false));
            labels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth + Game1.tileSize + 8, yPositionOnScreen + borderWidth + spaceToClearTopBorder + num + 16, 1, 1), "Acc."));
            arrowButtons.Add(new ClickableTextureComponent("Acc", new Rectangle(xPositionOnScreen + Game1.tileSize / 4 + spaceToClearSideBorder + Game1.tileSize * 2 + borderWidth, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33, -1, -1), 0.75f, false));

            arrowButtons.Add(new ClickableTextureComponent("Shoes", new Rectangle((xPositionOnScreen + 16) + Game1.tileSize * 4 + spaceToClearSideBorder + borderWidth, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "-1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44, -1, -1), 0.75f, false));
            GDlabels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + Game1.tileSize * 5 + 8 + spaceToClearSideBorder + borderWidth, yPositionOnScreen + borderWidth + spaceToClearTopBorder + num + 16, 1, 1), "  Shoes"));
            arrowButtons.Add(new ClickableTextureComponent("Shoes", new Rectangle(xPositionOnScreen + Game1.tileSize * 7 + spaceToClearSideBorder + borderWidth, (yPositionOnScreen + 16) + borderWidth + spaceToClearTopBorder + num, Game1.tileSize, Game1.tileSize), "", "1", Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33, -1, -1), 0.75f, false));

            //Text Above Primary Favorites Buttons
            labels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + 288 + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth + Game1.tileSize * 4 + 8, (yPositionOnScreen + 333) + borderWidth + spaceToClearTopBorder + 16, 1, 1), "Load"));
            labels.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + 271 + Game1.tileSize / 4 + spaceToClearSideBorder + borderWidth + Game1.tileSize * 4 + 8, (yPositionOnScreen + 358) + borderWidth + spaceToClearTopBorder + 16, 1, 1), "Favorite"));
            #endregion
            #region Set Up Favorites Pages
            //Manage Favorites and Extra Outfits Menu
            int y = favoriteExists(1) ? 26 : 67;
            primaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 565) + Game1.pixelZoom * 12, (yPositionOnScreen + 425) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(24, y, 8, 8), Game1.pixelZoom));
            secondaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 4) + Game1.pixelZoom * 12, (yPositionOnScreen + 348) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(24, y, 8, 8), 4.5f));

            y = favoriteExists(2) ? 26 : 67;
            primaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 565) + Game1.pixelZoom * 12, (yPositionOnScreen + 475) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(8, y, 8, 8), Game1.pixelZoom));
            secondaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 4) + Game1.pixelZoom * 12, (yPositionOnScreen + 423) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(8, y, 8, 8), 4.5f));

            y = favoriteExists(3) ? 26 : 67;
            primaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 565) + Game1.pixelZoom * 12, (yPositionOnScreen + 525) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(0, y, 8, 8), Game1.pixelZoom));
            secondaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 4) + Game1.pixelZoom * 12, (yPositionOnScreen + 498) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(0, y, 8, 8), 4.5f));

            y = favoriteExists(4) ? 26 : 67;
            primaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 610) + Game1.pixelZoom * 12, (yPositionOnScreen + 425) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(24, y, 8, 8), Game1.pixelZoom));
            secondaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 380) + Game1.pixelZoom * 12, (yPositionOnScreen + 348) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(24, y, 8, 8), 4.5f));

            y = favoriteExists(5) ? 26 : 67;
            primaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 610) + Game1.pixelZoom * 12, (yPositionOnScreen + 475) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(8, y, 8, 8), Game1.pixelZoom));
            secondaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 380) + Game1.pixelZoom * 12, (yPositionOnScreen + 423) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(8, y, 8, 8), 4.5f));

            y = favoriteExists(6) ? 26 : 67;
            primaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 610) + Game1.pixelZoom * 12, (yPositionOnScreen + 525) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(0, y, 8, 8), Game1.pixelZoom));
            secondaryFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 380) + Game1.pixelZoom * 12, (yPositionOnScreen + 498) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(0, y, 8, 8), 4.5f));      

            saveFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 225) + Game1.pixelZoom * 12, (yPositionOnScreen + 350) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 15, Game1.pixelZoom * 10), Game1.mouseCursors, new Rectangle(294, 428, 21, 11), 3f));
            saveFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 225) + Game1.pixelZoom * 12, (yPositionOnScreen + 425) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 15, Game1.pixelZoom * 10), Game1.mouseCursors, new Rectangle(294, 428, 21, 11), 3f));
            saveFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 225) + Game1.pixelZoom * 12, (yPositionOnScreen + 500) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 15, Game1.pixelZoom * 10), Game1.mouseCursors, new Rectangle(294, 428, 21, 11), 3f));

            saveFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 595) + Game1.pixelZoom * 12, (yPositionOnScreen + 350) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 15, Game1.pixelZoom * 10), Game1.mouseCursors, new Rectangle(294, 428, 21, 11), 3f));
            saveFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 595) + Game1.pixelZoom * 12, (yPositionOnScreen + 425) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 15, Game1.pixelZoom * 10), Game1.mouseCursors, new Rectangle(294, 428, 21, 11), 3f));
            saveFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 595) + Game1.pixelZoom * 12, (yPositionOnScreen + 500) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 15, Game1.pixelZoom * 10), Game1.mouseCursors, new Rectangle(294, 428, 21, 11), 3f));

            loadFavButton = new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 475) + Game1.pixelZoom * 12, (yPositionOnScreen + 405) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 20, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(0, 207, 26, 11), 3f);
            saveFavButton = new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 475) + Game1.pixelZoom * 12, (yPositionOnScreen + 455) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 20, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(0, 193, 26, 11), 3f);

            for (int i = 0; i < 10; i++)
            {
                y = favoriteExists(i + 7) ? 26 : 67;
                extraFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 80 + (i * 50)) + Game1.pixelZoom * 12, (yPositionOnScreen + 520) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(0, y, 8, 8), 4.5f));
            }

            for (int i = 0; i < 10; i++)
            {
                y = favoriteExists(i + 17) ? 26 : 67;
                extraFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 80 + (i * 50)) + Game1.pixelZoom * 12, (yPositionOnScreen + 570) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(0, y, 8, 8), 4.5f));
            }

            for (int i = 0; i < 10; i++)
            {
                y = favoriteExists(i + 27) ? 26 : 67;
                extraFavButtons.Add(new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 80 + (i * 50)) + Game1.pixelZoom * 12, (yPositionOnScreen + 620) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(0, y, 8, 8), 4.5f));
            }
            #endregion
            #region Set Up About Page
            //About Menu
            setNewMenuAccessKeyButton = new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 595) + Game1.pixelZoom * 12, (yPositionOnScreen + 400) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 15, Game1.pixelZoom * 10), Game1.mouseCursors, new Rectangle(294, 428, 21, 11), 3f);
            hideMaleSkirtsButton = new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 595) + Game1.pixelZoom * 12, (yPositionOnScreen + 475) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 15, Game1.pixelZoom * 10), Game1.mouseCursors, new Rectangle(294, 428, 21, 11), 3f); 
            zoomOutButton = new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 595) + Game1.pixelZoom * 12, (yPositionOnScreen + 550) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(0, GetDressed.globalConfig.menuZoomOut ? 177 : 167, 7, 9), 3f);
            zoomInButton = new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 636) + Game1.pixelZoom * 12, (yPositionOnScreen + 550) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 10, Game1.pixelZoom * 10), GetDressed.menuTextures, new Rectangle(10, GetDressed.globalConfig.menuZoomOut ? 167 : 177, 7, 9), 3f);
            resetConfigButton = new ClickableTextureComponent(new Rectangle((xPositionOnScreen + 595) + Game1.pixelZoom * 12, (yPositionOnScreen + 625) + Game1.tileSize + Game1.pixelZoom * 14, Game1.pixelZoom * 15, Game1.pixelZoom * 10), Game1.mouseCursors, new Rectangle(294, 428, 21, 11), 3f);
            #endregion

            setUpIcons();
            resetTabPositions();
        }

        private void setUpIcons()
        {
            Vector2 position1 = new Vector2(xPositionOnScreen - 90, yPositionOnScreen + 35);
            Vector2 position2 = new Vector2(xPositionOnScreen + 120, yPositionOnScreen - 38);

            floatingNewButton = new TemporaryAnimatedSprite(GetDressed.menuTextures, new Rectangle(0, 102, 23, 9), 115, 5, 1, position1, false, false, 0.89f, 0f, Color.White, Game1.pixelZoom, 0f, 0f, 0f, true)
            {
                totalNumberOfLoops = 1,
                yPeriodic = true,
                yPeriodicLoopTime = 1500f,
                yPeriodicRange = Game1.tileSize / 8
            };

            floatingPurpleArrow = new TemporaryAnimatedSprite(GetDressed.menuTextures, new Rectangle(0, 120, 12, 14), 100f, 3, 5, position2, false, false, 0.89f, 0f, Color.White, 3f, 0f, 0f, 0f, true)
            {
                yPeriodic = true,
                yPeriodicLoopTime = 1500f,
                yPeriodicRange = Game1.tileSize / 8
            };
        }

        private void resetTabPositions()
        {
            charIcon.bounds.Y = yPositionOnScreen + (isCurrentTab(1) ? 50 : 40);
            favIcon.bounds.Y = yPositionOnScreen + (isCurrentTab(2) || isCurrentTab(4) ? 50 : 40);
            aboutIcon.bounds.Y = yPositionOnScreen + (isCurrentTab(3) ? 43 : 33);
            quickFavsIcon.bounds.X = xPositionOnScreen - (isCurrentTab(2) ? 16 : 23);
            extraFavsIcon.bounds.X = xPositionOnScreen - (isCurrentTab(4) ? 16 : 23);
        }

        private void optionButtonClick(string name)
        {
            if (name != null)
            {
                if (name != "Male")
                {
                    if (name != "Female")
                    {
                        if (name == "OK")
                        {
                            Game1.exitActiveMenu();
                            Game1.flashAlpha = 1f;

                            Game1.playSound("yoba");
                            GetDressed.currentConfig.chosenAccessory[0] = Game1.player.accessory;
                            GetDressed.currentConfig.chosenFace[0] = faceType;
                            GetDressed.currentConfig.chosenNose[0] = noseType;
                            GetDressed.currentConfig.chosenBottoms[0] = bottoms;
                            GetDressed.currentConfig.chosenShoes[0] = shoes;
                            GetDressed.modHelper.WriteJsonFile(Path.Combine("psconfigs", $"{Constants.SaveFolderName}.json"), GetDressed.currentConfig);
                        }
                    }
                    else
                    {
                        changeGender(false);
                        Game1.player.changeHairStyle(16);
                    }
                }
                else
                {
                    changeGender(true);
                    Game1.player.changeHairStyle(0);
                }
            }
            Game1.playSound("coin");
        }

        private void selectionClick(string name, int change)
        {
            if (name != null)
            {
                if (name == "Skin")
                {
                    Game1.player.changeSkinColor(Game1.player.skin + change);
                    Game1.playSound("skeletonStep");
                    return;
                }
                if (name == "Hair")
                {
                    Game1.player.changeHairStyle(Game1.player.hair + change);
                    Game1.playSound("grassyStep");
                    return;
                }
                if (name == "Shirt")
                {
                    Game1.player.changeShirt(Game1.player.shirt + change);
                    Game1.playSound("coin");
                    return;
                }
                if (name == "Acc")
                {
                    changeAccessory(Game1.player.accessory + change);
                    Game1.playSound("purchase");
                    return;
                }
                if (name == "Face Type")
                {
                    changeFaceType(faceType + change);

                    if (faceType == 0 && Game1.player.accessory > 127)
                    {
                        Game1.player.accessory = Game1.player.accessory - 112;
                    }

                    if (faceType == 1 && Game1.player.accessory < 131 && Game1.player.accessory > 18)
                    {
                        Game1.player.accessory = Game1.player.accessory + 112;
                    }

                    Game1.playSound("skeletonStep");
                    return;
                }
                if (name == "Nose Type")
                {
                    changeNoseType(noseType + change);
                    Game1.playSound("grassyStep");
                    return;
                }
                if (name == "Bottoms")
                {
                    changeBottoms(bottoms + change);
                    Game1.playSound("coin");
                    return;
                }
                if (name == "Shoes")
                {
                    changeShoes(shoes + change);
                    Game1.playSound("purchase");
                    return;
                }
                if (name != "Direction")
                {
                    return;
                }
                Game1.player.faceDirection((Game1.player.facingDirection - change + 4) % 4);
                Game1.player.FarmerSprite.StopAnimation();
                Game1.player.completelyStopAnimatingOrDoingAction();
                Game1.playSound("pickUpItem");
                if (name != "Direction2")
                {
                    return;
                }
                Game1.player.faceDirection((Game1.player.facingDirection - change + 4) % 4);
                Game1.player.FarmerSprite.StopAnimation();
                Game1.player.completelyStopAnimatingOrDoingAction();
                Game1.playSound("pickUpItem");
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (charIcon.containsPoint(x, y))
            {
                if (isCurrentTab(1))
                    return;
                Game1.playSound("smallSelect");
                currentTab = 1;
                resetTabPositions();
            }

            if (favIcon.containsPoint(x, y))
            {
                if (isCurrentTab(2) || isCurrentTab(4))
                    return;
                Game1.playSound("smallSelect");
                currentTab = 2;
                resetTabPositions();
                showFloatingPurpleArrow = false;
            }

            if (isCurrentTab(2))
            {
                if (extraFavsIcon.containsPoint(x, y))
                {
                    Game1.playSound("smallSelect");
                    currentTab = 4;
                    resetTabPositions();
                    favSelected = -1;
                    foreach (ClickableTextureComponent extraFavButton in extraFavButtons)
                    {
                        extraFavButton.drawShadow = false;
                    }
                }
            }

            if (isCurrentTab(4))
            {
                if (quickFavsIcon.containsPoint(x, y))
                {
                    Game1.playSound("smallSelect");
                    currentTab = 2;
                    resetTabPositions();
                }
            }

            if (aboutIcon.containsPoint(x, y))
            {
                if (isCurrentTab(3))
                    return;
                Game1.playSound("smallSelect");
                currentTab = 3;
                resetTabPositions();
            }

            if (cancelButton.containsPoint(x, y))
            {
                Game1.exitActiveMenu();
                Game1.playSound("bigDeSelect");
                Game1.options.zoomLevel = GetDressed.playerZoomLevel;
                Game1.overrideGameMenuReset = true;
                //Program.gamePtr.refreshWindowSettings();
                Game1.game1.refreshWindowSettings();
                Game1.player.canMove = true;
            }

            #region Left Click Character Page
            if (isCurrentTab(1))
            {
                foreach (ClickableComponent current in genderButtons)
                {
                    if (current.containsPoint(x, y))
                    {
                        optionButtonClick(current.name);
                        current.scale -= 0.5f;
                        current.scale = Math.Max(3.5f, current.scale);
                    }
                }

                foreach (ClickableTextureComponent current in arrowButtons)
                {
                    if (current.containsPoint(x, y))
                    {
                        selectionClick(current.name, Convert.ToInt32(current.hoverText));
                        current.scale -= 0.25f;
                        current.scale = Math.Max(0.75f, current.scale);
                    }
                }

                if (okButton.containsPoint(x, y))
                {
                    optionButtonClick(okButton.name);
                    okButton.scale -= 0.25f;
                    okButton.scale = Math.Max(0.75f, okButton.scale);
                    Game1.options.zoomLevel = GetDressed.playerZoomLevel;
                    Game1.overrideGameMenuReset = true;
                    //Program.gamePtr.refreshWindowSettings();
                    Game1.game1.refreshWindowSettings();
                }
                if (hairColorPicker.containsPoint(x, y))
                {
                    Game1.player.changeHairColor(hairColorPicker.click(x, y));
                    lastHeldColorPicker = hairColorPicker;
                }
                else if (pantsColorPicker.containsPoint(x, y))
                {
                    Game1.player.changePants(pantsColorPicker.click(x, y));
                    lastHeldColorPicker = pantsColorPicker;
                }
                else if (eyeColorPicker.containsPoint(x, y))
                {
                    Game1.player.changeEyeColor(eyeColorPicker.click(x, y));
                    lastHeldColorPicker = eyeColorPicker;
                }

                for (int i = 0; i < primaryFavButtons.Count; i++)
                {
                    if (primaryFavButtons[i].containsPoint(x, y))
                    {
                        if (!favoriteExists(i + 1))
                        {
                            alerts.Add(new Alert(Game1.mouseCursors, new Rectangle(268, 470, 16, 16), Game1.viewport.Width / 2 - (700 + borderWidth * 2) / 2, Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize, "Uh oh! No Favorite is Set!", 1000, false));
                            setUpIcons();
                            showFloatingPurpleArrow = true;
                            return;
                        }

                        changeBottoms(GetDressed.currentConfig.chosenBottoms[i + 1]);
                        changeAccessory(GetDressed.currentConfig.chosenAccessory[i + 1]);
                        changeFaceType(GetDressed.currentConfig.chosenFace[i + 1]);
                        changeNoseType(GetDressed.currentConfig.chosenNose[i + 1]);
                        changeShoes(GetDressed.currentConfig.chosenShoes[i + 1]);
                        Game1.player.changeSkinColor(GetDressed.currentConfig.chosenSkin[i + 1]);
                        Game1.player.changeShirt(GetDressed.currentConfig.chosenShirt[i + 1]);
                        Game1.player.changeHairStyle(GetDressed.currentConfig.chosenHairstyle[i + 1]);

                        Color haircolorpackedvalue = new Color(0, 0, 0);
                        haircolorpackedvalue.PackedValue = GetDressed.currentConfig.chosenHairColor[i + 1];
                        Game1.player.changeHairColor(haircolorpackedvalue);
                        hairColorPicker.setColor(Game1.player.hairstyleColor);

                        Color eyecolorpackedvalue = new Color(0, 0, 0);
                        eyecolorpackedvalue.PackedValue = GetDressed.currentConfig.chosenEyeColor[i + 1];
                        Game1.player.changeEyeColor(eyecolorpackedvalue);
                        eyeColorPicker.setColor(Game1.player.newEyeColor);

                        Color bottomscolorpackedvalue = new Color(0, 0, 0);
                        bottomscolorpackedvalue.PackedValue = GetDressed.currentConfig.chosenBottomsColor[i + 1];
                        Game1.player.changePants(bottomscolorpackedvalue);
                        pantsColorPicker.setColor(Game1.player.pantsColor);

                        Game1.playSound("yoba");
                    }
                }

                if (randomButton.containsPoint(x, y))
                {
                    string cueName = "drumkit6";
                    if (timesRandom > 0)
                    {
                        switch (Game1.random.Next(15))
                        {
                            case 0:
                                cueName = "drumkit1";
                                break;
                            case 1:
                                cueName = "dirtyHit";
                                break;
                            case 2:
                                cueName = "axchop";
                                break;
                            case 3:
                                cueName = "hoeHit";
                                break;
                            case 4:
                                cueName = "fishSlap";
                                break;
                            case 5:
                                cueName = "drumkit6";
                                break;
                            case 6:
                                cueName = "drumkit5";
                                break;
                            case 7:
                                cueName = "drumkit6";
                                break;
                            case 8:
                                cueName = "junimoMeep1";
                                break;
                            case 9:
                                cueName = "coin";
                                break;
                            case 10:
                                cueName = "axe";
                                break;
                            case 11:
                                cueName = "hammer";
                                break;
                            case 12:
                                cueName = "drumkit2";
                                break;
                            case 13:
                                cueName = "drumkit4";
                                break;
                            case 14:
                                cueName = "drumkit3";
                                break;
                        }
                    }
                    Game1.playSound(cueName);
                    timesRandom++;
                    if (Game1.player.isMale)
                    {
                        Game1.player.changeHairStyle(Game1.random.Next(16));
                        changeBottoms(Game1.random.Next(GetDressed.globalConfig.maleBottomsTypes), true);
                        changeShoes(Game1.random.Next(GetDressed.globalConfig.maleShoeTypes), true);
                        changeFaceType(Game1.random.Next(GetDressed.globalConfig.maleFaceTypes), true);
                        changeNoseType(Game1.random.Next(GetDressed.globalConfig.maleNoseTypes), true);
                    }
                    else
                    {
                        Game1.player.changeHairStyle(Game1.random.Next(16, 32));
                        changeBottoms(Game1.random.Next(GetDressed.globalConfig.femaleBottomsTypes), true);
                        changeShoes(Game1.random.Next(GetDressed.globalConfig.femaleShoeTypes), true);
                        changeFaceType(Game1.random.Next(GetDressed.globalConfig.femaleFaceTypes), true);
                        changeNoseType(Game1.random.Next(GetDressed.globalConfig.femaleNoseTypes), true);
                    }
                    patchBase();

                    if (Game1.player.isMale)
                    {
                        Game1.player.changeHairStyle(Game1.random.Next(16));
                        if (GetDressed.globalConfig.hideMaleSkirts)
                        {
                            changeBottoms(Game1.random.Next(2));
                        }
                        else
                        {
                            changeBottoms(Game1.random.Next(GetDressed.globalConfig.maleBottomsTypes));
                        }

                        changeShoes(Game1.random.Next(GetDressed.globalConfig.maleShoeTypes));
                        changeFaceType(Game1.random.Next(GetDressed.globalConfig.maleFaceTypes));
                        changeNoseType(Game1.random.Next(GetDressed.globalConfig.maleNoseTypes));
                    }
                    else
                    {
                        Game1.player.changeHairStyle(Game1.random.Next(16, 32));
                        changeBottoms(Game1.random.Next(GetDressed.globalConfig.femaleBottomsTypes));
                        changeShoes(Game1.random.Next(GetDressed.globalConfig.femaleShoeTypes));
                        changeFaceType(Game1.random.Next(GetDressed.globalConfig.femaleFaceTypes));
                        changeNoseType(Game1.random.Next(GetDressed.globalConfig.femaleNoseTypes));
                    }
                    if (Game1.random.NextDouble() < 0.88)
                    {
                        int maxRange = (faceType == 0) ? 127 : 131;
                        int random = Game1.random.Next(maxRange);
                        if (random > 19 && maxRange > 127) random += 108;

                        if (Game1.player.isMale)
                        {
                            changeAccessory(random);
                        }
                        else
                        {
                            random = Game1.random.Next(6, maxRange);
                            if (random > 19 && maxRange > 127) random += 108;
                            changeAccessory(random);
                        }
                    }
                    else
                    {
                        Game1.player.changeAccessory(-1);
                    }
                    if (Game1.player.isMale)
                    {
                        Game1.player.changeHairStyle(Game1.random.Next(16));
                    }
                    else
                    {
                        Game1.player.changeHairStyle(Game1.random.Next(16, 32));
                    }
                    Color c = new Color(Game1.random.Next(25, 254), Game1.random.Next(25, 254), Game1.random.Next(25, 254));
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        c.R /= 2;
                        c.G /= 2;
                        c.B /= 2;
                    }
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        c.R = (byte)Game1.random.Next(15, 50);
                    }
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        c.G = (byte)Game1.random.Next(15, 50);
                    }
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        c.B = (byte)Game1.random.Next(15, 50);
                    }
                    Game1.player.changeHairColor(c);
                    Game1.player.changeShirt(Game1.random.Next(112));
                    Game1.player.changeSkinColor(Game1.random.Next(6));
                    if (Game1.random.NextDouble() < 0.25)
                    {
                        Game1.player.changeSkinColor(Game1.random.Next(24));
                    }
                    Color color = new Color(Game1.random.Next(25, 254), Game1.random.Next(25, 254), Game1.random.Next(25, 254));
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        color.R /= 2;
                        color.G /= 2;
                        color.B /= 2;
                    }
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        color.R = (byte)Game1.random.Next(15, 50);
                    }
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        color.G = (byte)Game1.random.Next(15, 50);
                    }
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        color.B = (byte)Game1.random.Next(15, 50);
                    }
                    Game1.player.changePants(color);
                    Color c2 = new Color(Game1.random.Next(25, 254), Game1.random.Next(25, 254), Game1.random.Next(25, 254));
                    c2.R /= 2;
                    c2.G /= 2;
                    c2.B /= 2;
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        c2.R = (byte)Game1.random.Next(15, 50);
                    }
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        c2.G = (byte)Game1.random.Next(15, 50);
                    }
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        c2.B = (byte)Game1.random.Next(15, 50);
                    }
                    Game1.player.changeEyeColor(c2);
                    randomButton.scale = Game1.pixelZoom - 0.5f;
                    pantsColorPicker.setColor(Game1.player.pantsColor);
                    eyeColorPicker.setColor(Game1.player.newEyeColor);
                    hairColorPicker.setColor(Game1.player.hairstyleColor);
                }
            }
            #endregion
            #region Left Click Mng Favorites Page
            if (isCurrentTab(2))
            {
                for (int i = 0; i < primaryFavButtons.Count; i++)
                {
                    if (saveFavButtons[i].containsPoint(x, y))
                    {
                        GetDressed.currentConfig.chosenBottoms[i + 1] = bottoms;
                        GetDressed.currentConfig.chosenAccessory[i + 1] = Game1.player.accessory;
                        GetDressed.currentConfig.chosenFace[i + 1] = faceType;
                        GetDressed.currentConfig.chosenNose[i + 1] = noseType;
                        GetDressed.currentConfig.chosenShoes[i + 1] = shoes;
                        GetDressed.currentConfig.chosenSkin[i + 1] = Game1.player.skin;
                        GetDressed.currentConfig.chosenShirt[i + 1] = Game1.player.shirt;
                        GetDressed.currentConfig.chosenHairstyle[i + 1] = Game1.player.hair;
                        GetDressed.currentConfig.chosenHairColor[i + 1] = Game1.player.hairstyleColor.PackedValue;
                        GetDressed.currentConfig.chosenEyeColor[i + 1] = Game1.player.newEyeColor.PackedValue;
                        GetDressed.currentConfig.chosenBottomsColor[i + 1] = Game1.player.pantsColor.PackedValue;

                        GetDressed.modHelper.WriteJsonFile(Path.Combine("psconfigs", $"{Constants.SaveFolderName}.json"), GetDressed.currentConfig);
                        //hide New Button
                        if (GetDressed.globalConfig.showIntroBanner)
                        {
                            GetDressed.globalConfig.showIntroBanner = false;
                            GetDressed.modHelper.WriteConfig(GetDressed.globalConfig);
                        }

                        //banana ;)
                        primaryFavButtons[i].sourceRect.Y = 26;
                        secondaryFavButtons[i].sourceRect.Y = 26;
                        alerts.Add(new Alert(Game1.mouseCursors, new Rectangle(310, 392, 16, 16), Game1.viewport.Width / 2 - (700 + borderWidth * 2) / 2, Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize, "New Favorite Saved.", 1200, false));

                        Game1.playSound("purchase");
                    }
                }
            }
            #endregion
            #region Left Click Extra Outfits Page
            if (isCurrentTab(4))
            {
                if (saveFavButton.containsPoint(x, y) && favSelected > -1)
                {
                    GetDressed.currentConfig.chosenBottoms[favSelected + 1] = bottoms;
                    GetDressed.currentConfig.chosenAccessory[favSelected + 1] = Game1.player.accessory;
                    GetDressed.currentConfig.chosenFace[favSelected + 1] = faceType;
                    GetDressed.currentConfig.chosenNose[favSelected + 1] = noseType;
                    GetDressed.currentConfig.chosenShoes[favSelected + 1] = shoes;
                    GetDressed.currentConfig.chosenSkin[favSelected + 1] = Game1.player.skin;
                    GetDressed.currentConfig.chosenShirt[favSelected + 1] = Game1.player.shirt;
                    GetDressed.currentConfig.chosenHairstyle[favSelected + 1] = Game1.player.hair;
                    GetDressed.currentConfig.chosenHairColor[favSelected + 1] = Game1.player.hairstyleColor.PackedValue;
                    GetDressed.currentConfig.chosenEyeColor[favSelected + 1] = Game1.player.newEyeColor.PackedValue;
                    GetDressed.currentConfig.chosenBottomsColor[favSelected + 1] = Game1.player.pantsColor.PackedValue;

                    GetDressed.modHelper.WriteJsonFile(Path.Combine("psconfigs", $"{Constants.SaveFolderName}.json"), GetDressed.currentConfig);
                    //hide New Button
                    if (GetDressed.globalConfig.showIntroBanner)
                    {
                        GetDressed.globalConfig.showIntroBanner = false;
                        GetDressed.modHelper.WriteConfig(GetDressed.globalConfig);
                    }

                    //banana ;)
                    extraFavButtons[favSelected - 6].sourceRect.Y = 26;

                    alerts.Add(new Alert(Game1.mouseCursors, new Rectangle(310, 392, 16, 16), Game1.viewport.Width / 2 - (700 + borderWidth * 2) / 2, Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize, "Favorite Saved To Slot " + (favSelected + 1) + " .", 1200, false));
                    Game1.playSound("purchase");
                }

                if (loadFavButton.containsPoint(x, y) && favSelected > -1)
                {
                    if (!favoriteExists(favSelected + 1))
                    {
                        alerts.Add(new Alert(Game1.mouseCursors, new Rectangle(268, 470, 16, 16), Game1.viewport.Width / 2 - (700 + borderWidth * 2) / 2, Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize, "Uh oh! No Favorite is Set!", 1000, false));
                        setUpIcons();
                        return;
                    }

                    changeBottoms(GetDressed.currentConfig.chosenBottoms[favSelected + 1]);
                    changeAccessory(GetDressed.currentConfig.chosenAccessory[favSelected + 1]);
                    changeFaceType(GetDressed.currentConfig.chosenFace[favSelected + 1]);
                    changeNoseType(GetDressed.currentConfig.chosenNose[favSelected + 1]);
                    changeShoes(GetDressed.currentConfig.chosenShoes[favSelected + 1]);
                    Game1.player.changeSkinColor(GetDressed.currentConfig.chosenSkin[favSelected + 1]);
                    Game1.player.changeShirt(GetDressed.currentConfig.chosenShirt[favSelected + 1]);
                    Game1.player.changeHairStyle(GetDressed.currentConfig.chosenHairstyle[favSelected + 1]);

                    Color haircolorpackedvalue = new Color(0, 0, 0);
                    haircolorpackedvalue.PackedValue = GetDressed.currentConfig.chosenHairColor[favSelected + 1];
                    Game1.player.changeHairColor(haircolorpackedvalue);
                    hairColorPicker.setColor(Game1.player.hairstyleColor);

                    Color eyecolorpackedvalue = new Color(0, 0, 0);
                    eyecolorpackedvalue.PackedValue = GetDressed.currentConfig.chosenEyeColor[favSelected + 1];
                    Game1.player.changeEyeColor(eyecolorpackedvalue);
                    eyeColorPicker.setColor(Game1.player.newEyeColor);

                    Color bottomscolorpackedvalue = new Color(0, 0, 0);
                    bottomscolorpackedvalue.PackedValue = GetDressed.currentConfig.chosenBottomsColor[favSelected + 1];
                    Game1.player.changePants(bottomscolorpackedvalue);
                    pantsColorPicker.setColor(Game1.player.pantsColor);
                    patchBase();
                    Game1.playSound("yoba");
                }
                for (int i = 0; i < extraFavButtons.Count; i++)
                {
                    if (extraFavButtons[i].containsPoint(x, y))
                    {
                        foreach (ClickableTextureComponent bigFavButton in extraFavButtons)
                        {
                            bigFavButton.drawShadow = false;
                        }
                        extraFavButtons[i].drawShadow = true;
                        favSelected = i + 6;
                    }
                }
            }
            #endregion
            #region Left Click About Page
            if (isCurrentTab(3))
            {
                if (setNewMenuAccessKeyButton.containsPoint(x, y))
                {
                    letUserAssignOpenMenuKey = true;
                    Game1.playSound("breathin");
                }
                if (hideMaleSkirtsButton.containsPoint(x, y))
                {
                    GetDressed.globalConfig.hideMaleSkirts = !GetDressed.globalConfig.hideMaleSkirts;
                    GetDressed.modHelper.WriteConfig(GetDressed.globalConfig);
                    alerts.Add(new Alert(GetDressed.menuTextures, new Rectangle(80, 144, 16, 16), Game1.viewport.Width / 2 - (700 + borderWidth * 2) / 2, Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize, "Skirts " + (GetDressed.globalConfig.hideMaleSkirts ? "Hidden" : "Unhidden") + " for Males.", 1200, false));
                    Game1.playSound("coin");
                }

                if (GetDressed.globalConfig.menuZoomOut)
                {
                    if (zoomInButton.containsPoint(x, y))
                    {
                        Game1.options.zoomLevel = 1f;
                        Game1.overrideGameMenuReset = true;
                        //Program.gamePtr.refreshWindowSettings();
                        Game1.game1.refreshWindowSettings();

                        xPositionOnScreen = Game1.viewport.Width / 2 - (680 + borderWidth * 2) / 2;
                        yPositionOnScreen = Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize;
                        setUpPositions();

                        GetDressed.globalConfig.menuZoomOut = false;
                        GetDressed.modHelper.WriteConfig(GetDressed.globalConfig);

                        zoomInButton.sourceRect.Y = 177;
                        zoomOutButton.sourceRect.Y = 167;
                        
                        Game1.playSound("drumkit6");
                        alerts.Add(new Alert(GetDressed.menuTextures, new Rectangle(80, 144, 16, 16), Game1.viewport.Width / 2 - (700 + borderWidth * 2) / 2, Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize, "Zoom Setting Changed.", 1200, false, 200));
                    }
                }
                else
                {
                    if (zoomOutButton.containsPoint(x, y))
                    {
                        Game1.options.zoomLevel = 0.75f;
                        Game1.overrideGameMenuReset = true;
                        //Program.gamePtr.refreshWindowSettings();
                        Game1.game1.refreshWindowSettings();

                        xPositionOnScreen = Game1.viewport.Width / 2 - (680 + borderWidth * 2) / 2;
                        yPositionOnScreen = Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize;
                        setUpPositions();

                        GetDressed.globalConfig.menuZoomOut = true;
                        GetDressed.modHelper.WriteConfig(GetDressed.globalConfig);

                        zoomInButton.sourceRect.Y = 167;
                        zoomOutButton.sourceRect.Y = 177;

                        Game1.playSound("coin");
                        alerts.Add(new Alert(GetDressed.menuTextures, new Rectangle(80, 144, 16, 16), Game1.viewport.Width / 2 - (700 + borderWidth * 2) / 2, Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize, "Zoom Setting Changed.", 1200, false, 200));
                    }
                }

                if (resetConfigButton.containsPoint(x, y))
                {
                    GetDressed.globalConfig.hideMaleSkirts = false;
                    GetDressed.globalConfig.menuAccessKey = "C";
                    Game1.options.zoomLevel = 0.75f;
                    Game1.overrideGameMenuReset = true;
                    //Program.gamePtr.refreshWindowSettings();
                    Game1.game1.refreshWindowSettings();
                    xPositionOnScreen = Game1.viewport.Width / 2 - (680 + borderWidth * 2) / 2;
                    yPositionOnScreen = Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize;
                    setUpPositions();
                    GetDressed.globalConfig.menuZoomOut = true;
                    GetDressed.modHelper.WriteConfig(GetDressed.globalConfig);
                    alerts.Add(new Alert(GetDressed.menuTextures, new Rectangle(160, 144, 16, 16), Game1.viewport.Width / 2 - (700 + borderWidth * 2) / 2, Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize, "Options Reset to Default", 1200, false, 200));
                    Game1.playSound("coin");
                }
            }
            #endregion
        }

        public override void leftClickHeld(int x, int y)
        {
            colorPickerTimer -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;
            if (colorPickerTimer <= 0)
            {
                if (lastHeldColorPicker != null)
                {
                    if (lastHeldColorPicker.Equals(hairColorPicker))
                    {
                        Game1.player.changeHairColor(hairColorPicker.clickHeld(x, y));
                    }
                    if (lastHeldColorPicker.Equals(pantsColorPicker))
                    {
                        Game1.player.changePants(pantsColorPicker.clickHeld(x, y));
                    }
                    if (lastHeldColorPicker.Equals(eyeColorPicker))
                    {
                        Game1.player.changeEyeColor(eyeColorPicker.clickHeld(x, y));
                    }
                }
                colorPickerTimer = 100;
            }
        }

        public override void releaseLeftClick(int x, int y)
        {
            hairColorPicker.releaseClick();
            pantsColorPicker.releaseClick();
            eyeColorPicker.releaseClick();
            lastHeldColorPicker = null;
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
        }

        public override void receiveKeyPress(Keys key)
        {
            if (Game1.options.menuButton.Contains(new InputButton(key)) && readyToClose())
            {
                Game1.exitActiveMenu();
                Game1.playSound("bigDeSelect");
                Game1.options.zoomLevel = GetDressed.playerZoomLevel;
                Game1.overrideGameMenuReset = true;
                //Program.gamePtr.refreshWindowSettings();
                Game1.game1.refreshWindowSettings();
                Game1.player.canMove = true;
            }

            if (isCurrentTab(3) && letUserAssignOpenMenuKey)
            {
                GetDressed.globalConfig.menuAccessKey = key.ToString();
                GetDressed.modHelper.WriteConfig(GetDressed.globalConfig);
                letUserAssignOpenMenuKey = false;
                alerts.Add(new Alert(GetDressed.menuTextures, new Rectangle(96, 144, 16, 16), Game1.viewport.Width / 2 - (700 + borderWidth * 2) / 2, Game1.viewport.Height / 2 - (600 + borderWidth * 2) / 2 - Game1.tileSize, "Menu Access Key Changed.", 1200, false));
                Game1.playSound("coin");
            }
        }

        public override void update(GameTime time)
        {
            base.update(time);

            for (int k = alerts.Count() - 1; k >= 0; k--)
            {
                if (alerts.ElementAt(k).update(time))
                {
                    alerts.RemoveAt(k);
                }
            }

            if (floatingPurpleArrow != null)
            {
                floatingPurpleArrow.update(time);
            }

            if (floatingNewButton != null)
            {
                floatingNewButton.update(time);
            }
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);

            hoverText = "";

            foreach (ClickableComponent current in menuTabs)
            {
                if (current.containsPoint(x, y))
                {
                    if (current.name.Equals("Quick Outfits") || current.name.Equals("Extra Outfits"))
                    {
                        if (!isCurrentTab(2) && !isCurrentTab(4))
                            return;
                    }
                    hoverText = current.name;
                    return;
                }
            }

            cancelButton.tryHover(x, y, 0.25f);
            cancelButton.tryHover(x, y, 0.25f);

            if (isCurrentTab(1))
            {
                foreach (ClickableTextureComponent textureComponent in arrowButtons)
                {
                    if (textureComponent.containsPoint(x, y))
                        textureComponent.scale = Math.Min(textureComponent.scale + 0.02f, textureComponent.baseScale + 0.1f);
                    else
                        textureComponent.scale = Math.Max(textureComponent.scale - 0.02f, textureComponent.baseScale);
                }
                foreach (ClickableTextureComponent textureComponent in genderButtons)
                {
                    if (textureComponent.containsPoint(x, y))
                        textureComponent.scale = Math.Min(textureComponent.scale + 0.02f, textureComponent.baseScale + 0.1f);
                    else
                        textureComponent.scale = Math.Max(textureComponent.scale - 0.02f, textureComponent.baseScale);
                }

                randomButton.tryHover(x, y, 0.25f);
                randomButton.tryHover(x, y, 0.25f);

                if (okButton.containsPoint(x, y))
                {
                    okButton.scale = Math.Min(okButton.scale + 0.02f, okButton.baseScale + 0.1f);
                }
                else
                {
                    okButton.scale = Math.Max(okButton.scale - 0.02f, okButton.baseScale);
                }

                for (int i = 0; i < primaryFavButtons.Count; i++)
                {
                    primaryFavButtons[i].tryHover(x, y, 0.25f);
                    primaryFavButtons[i].tryHover(x, y, 0.25f);
                    if (primaryFavButtons[i].containsPoint(x, y))
                    {
                        hoverText = favoriteExists(i + 1) ?  "" : "No Favorite Is Set";
                    }
                }
            }

            if (isCurrentTab(2))
            {
                for (int i = 0; i < saveFavButtons.Count; i++)
                {
                    saveFavButtons[i].tryHover(x, y, 0.25f);
                    saveFavButtons[i].tryHover(x, y, 0.25f);
                }
            }

            if (isCurrentTab(4))
            {
                loadFavButton.tryHover(x, y, 0.25f);
                loadFavButton.tryHover(x, y, 0.25f);

                saveFavButton.tryHover(x, y, 0.25f);
                saveFavButton.tryHover(x, y, 0.25f);
            }
            
            if (isCurrentTab(3))
            {
                setNewMenuAccessKeyButton.tryHover(x, y, 0.25f);
                setNewMenuAccessKeyButton.tryHover(x, y, 0.25f);

                hideMaleSkirtsButton.tryHover(x, y, 0.25f);
                hideMaleSkirtsButton.tryHover(x, y, 0.25f);

                resetConfigButton.tryHover(x, y, 0.25f);
                resetConfigButton.tryHover(x, y, 0.25f);

                if (GetDressed.globalConfig.menuZoomOut)
                {
                    zoomInButton.tryHover(x, y, 0.25f);
                    zoomInButton.tryHover(x, y, 0.25f);
                }
                else
                {
                    zoomOutButton.tryHover(x, y, 0.25f);
                    zoomOutButton.tryHover(x, y, 0.25f);
                }
            }
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);

            //Draw Menu Background
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width + 50, height, false, true, null, false);

            //Prepare Tabs
            Vector2 character = new Vector2(xPositionOnScreen + 45, yPositionOnScreen + (isCurrentTab(1) ? 25 : 16));
            Vector2 favorites = new Vector2(xPositionOnScreen + 110, yPositionOnScreen + (isCurrentTab(2) || isCurrentTab(4) ? 25 : 16));
            Vector2 about = new Vector2(xPositionOnScreen + 175, yPositionOnScreen + (isCurrentTab(3) ? 25 : 16));
            Vector2 quickFavorites = new Vector2(xPositionOnScreen - (isCurrentTab(2) ? 40 : 47), yPositionOnScreen + 107);
            Vector2 extraFavorites = new Vector2(xPositionOnScreen - (isCurrentTab(4) ? 40 : 47), yPositionOnScreen + 171);

            //FIRST TAB
            b.Draw(Game1.mouseCursors, character, new Rectangle?(new Rectangle(16, 368, 16, 16)), Color.White, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 0.0001f);
            Game1.player.FarmerRenderer.drawMiniPortrat(b, new Vector2(xPositionOnScreen + 53, yPositionOnScreen + (Game1.player.isMale ? (isCurrentTab(1) ? 35 : 26) : (isCurrentTab(1) ? 32 : 23))), 0.00011f, 3f, 2, Game1.player);
            //SECOND TAB
            b.Draw(Game1.mouseCursors, favorites, new Rectangle?(new Rectangle(16, 368, 16, 16)), Color.White, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 0.0001f);
            favIcon.draw(b);
            //THIRD TAB
            b.Draw(Game1.mouseCursors, about, new Rectangle?(new Rectangle(16, 368, 16, 16)), Color.White, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 0.0001f);
            aboutIcon.draw(b);

            //SIDE TABS
            if (isCurrentTab(2) || isCurrentTab(4))
            {
                b.Draw(GetDressed.menuTextures, quickFavorites, new Rectangle?(new Rectangle(52, 202, 16, 16)), Color.White, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 0.0001f);
                b.Draw(GetDressed.menuTextures, extraFavorites, new Rectangle?(new Rectangle(52, 202, 16, 16)), Color.White, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 0.0001f);
                quickFavsIcon.draw(b);
                extraFavsIcon.draw(b);
            }

            #region Draw Character Page
            if (isCurrentTab(1))
            {
                StardewValley.BellsAndWhistles.SpriteText.drawString(b, "Customize Character:", xPositionOnScreen + 55, yPositionOnScreen + 115, 999999, -1, 999999, 1, 0.88f, false, -1, "", -1);

                b.Draw(Game1.daybg, new Vector2(xPositionOnScreen + Game1.tileSize + Game1.tileSize * 2 / 3 - 2, (yPositionOnScreen + 75) + borderWidth + spaceToClearTopBorder - Game1.tileSize / 4), Color.White);
                Game1.player.FarmerRenderer.draw(b, Game1.player.FarmerSprite.CurrentAnimationFrame, Game1.player.FarmerSprite.CurrentFrame, Game1.player.FarmerSprite.SourceRect, new Vector2(xPositionOnScreen - 2 + Game1.tileSize * 2 / 3 + Game1.tileSize * 2 - Game1.tileSize / 2, (yPositionOnScreen + 75) + borderWidth - Game1.tileSize / 4 + spaceToClearTopBorder + Game1.tileSize / 2), Vector2.Zero, 0.8f, Color.White, 0f, 1f, Game1.player);

                foreach (ClickableTextureComponent textureComponent in genderButtons)
                {
                    textureComponent.draw(b);
                    if (textureComponent.name.Equals("Male") && Game1.player.isMale)
                    {
                        maleOutlineButton.draw(b);
                    }
                    if (textureComponent.name.Equals("Female") && !Game1.player.isMale)
                    {
                        femaleOutlineButton.draw(b);
                    }
                }

                foreach (ClickableTextureComponent favButton in primaryFavButtons)
                {
                    favButton.draw(b);
                }

                foreach (ClickableTextureComponent textureComponent in arrowButtons)
                    textureComponent.draw(b);

                foreach (ClickableComponent clickableComponent in labels)
                {
                    string text = "";
                    Color color = Game1.textColor;
                    int x = clickableComponent.bounds.X;
                    switch (clickableComponent.name.Substring(0, 4))
                    {
                        case "Shir":
                            text = string.Concat(Game1.player.shirt + 1);
                            x = x + 4;
                            break;
                        case "Skin":
                            text = string.Concat(Game1.player.skin + 1);
                            break;
                        case "Hair":
                            if (!clickableComponent.name.Contains("Color"))
                            {
                                text = string.Concat(Game1.player.hair + 1);
                                break;
                            }
                            break;
                        case "Acc.":
                            if (faceType == 0)
                            {
                                text = string.Concat(Game1.player.accessory + 2);
                            }
                            if (faceType == 1 && (Game1.player.accessory < 20))
                            {
                                text = string.Concat(Game1.player.accessory + 2);
                            }
                            if (faceType == 1 && (Game1.player.accessory > 20))
                            {
                                text = string.Concat(Game1.player.accessory - 110);
                            }

                            break;
                        default:
                            color = Game1.textColor;
                            break;
                    }
                    Utility.drawTextWithShadow(b, clickableComponent.name, Game1.smallFont, new Vector2(clickableComponent.bounds.X, clickableComponent.bounds.Y), color, 1f, -1f, -1, -1, 1f, 3);
                    if (Enumerable.Count(text) > 0)
                    {
                        if (text.Length == 1) text = " " + text + " ";
                        Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(x + Game1.smallFont.MeasureString("Shirt").X / 2f - Game1.smallFont.MeasureString("100").X / 2, clickableComponent.bounds.Y + Game1.tileSize / 2), color, 1f, -1f, -1, -1, 1f, 3);
                    }
                }

                foreach (ClickableComponent clickableComponent in GDlabels)
                {
                    string text = "";
                    Color color = Game1.textColor;
                    switch (clickableComponent.name.Substring(0, 4))
                    {
                        case "Face":
                            text = string.Concat(faceType + 1);
                            break;
                        case "Nose":
                            text = string.Concat(noseType + 1);
                            break;
                        case " Bot":
                            text = string.Concat(bottoms + 1);
                            break;
                        case "  Sh":
                            text = string.Concat(shoes + 1);
                            break;
                        default:
                            color = Game1.textColor;
                            break;
                    }
                    Utility.drawTextWithShadow(b, clickableComponent.name, Game1.smallFont, new Vector2(clickableComponent.bounds.X, clickableComponent.bounds.Y), color, 1f, -1f, -1, -1, 1f, 3);
                    if (Enumerable.Count(text) > 0)
                        Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(clickableComponent.bounds.X + Game1.smallFont.MeasureString("Face Type").X / 2f - Game1.smallFont.MeasureString("10").X / 2, clickableComponent.bounds.Y + Game1.tileSize / 2), color, 1f, -1f, -1, -1, 1f, 3);
                }

                okButton.draw(b);

                hairColorPicker.draw(b);
                pantsColorPicker.draw(b);
                eyeColorPicker.draw(b);

                randomButton.draw(b);
            }
            #endregion
            #region Draw Mng Favorites Page
            if (isCurrentTab(2))
            {
                //HEADER
                StardewValley.BellsAndWhistles.SpriteText.drawString(b, "Manage Favorites:", xPositionOnScreen + 55, yPositionOnScreen + 115, 999999, -1, 999999, 1, 0.88f, false, -1, "", -1);
                b.Draw(Game1.daybg, new Vector2(xPositionOnScreen + 430 + Game1.tileSize + Game1.tileSize * 2 / 3 - 2, (yPositionOnScreen + 105) + borderWidth + spaceToClearTopBorder - Game1.tileSize / 4), Color.White);
                Game1.player.FarmerRenderer.draw(b, Game1.player.FarmerSprite.CurrentAnimationFrame, Game1.player.FarmerSprite.CurrentFrame, Game1.player.FarmerSprite.SourceRect, new Vector2(xPositionOnScreen + 428 + Game1.tileSize * 2 / 3 + Game1.tileSize * 2 - Game1.tileSize / 2, (yPositionOnScreen + 105) + borderWidth - Game1.tileSize / 4 + spaceToClearTopBorder + Game1.tileSize / 2), Vector2.Zero, 0.8f, Color.White, 0f, 1f, Game1.player);

                Utility.drawTextWithShadow(b, "You can set up to 6 quick favorite appearance", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 175), Color.Black);

                Utility.drawTextWithShadow(b, "configurations for each character.", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 200), Color.Black);

                Utility.drawTextWithShadow(b, "Your current appearance is shown on", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 285), Color.Black);
                Utility.drawTextWithShadow(b, "the right, use one of the buttons below", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 310), Color.Black);
                Utility.drawTextWithShadow(b, "to set it as a favorite :", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 335), Color.Black);

                foreach (ClickableTextureComponent secondaryFavButton in secondaryFavButtons)
                {
                    secondaryFavButton.draw(b);
                }

                Utility.drawTextWithShadow(b, "1st Favorite", Game1.smallFont, new Vector2(xPositionOnScreen + 90, yPositionOnScreen + 475), Color.Black);

                Utility.drawTextWithShadow(b, "2nd Favorite", Game1.smallFont, new Vector2(xPositionOnScreen + 90, yPositionOnScreen + 550), Color.Black);

                Utility.drawTextWithShadow(b, "3rd Favorite", Game1.smallFont, new Vector2(xPositionOnScreen + 90, yPositionOnScreen + 625), Color.Black);

                Utility.drawTextWithShadow(b, "4th Favorite", Game1.smallFont, new Vector2(xPositionOnScreen + 467, yPositionOnScreen + 475), Color.Black);

                Utility.drawTextWithShadow(b, "5th Favorite", Game1.smallFont, new Vector2(xPositionOnScreen + 467, yPositionOnScreen + 550), Color.Black);

                Utility.drawTextWithShadow(b, "6th Favorite", Game1.smallFont, new Vector2(xPositionOnScreen + 467, yPositionOnScreen + 625), Color.Black);

                Utility.drawTextWithShadow(b, "Hint: Click the SET button lined up with each Favorite to", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 700), Color.Black);
                Utility.drawTextWithShadow(b, "set your current appearance as that Favorite.", Game1.smallFont, new Vector2(xPositionOnScreen + 110, yPositionOnScreen + 725), Color.Black);

                foreach (ClickableTextureComponent saveFavButton in saveFavButtons)
                {
                    saveFavButton.draw(b);
                }
            }
            #endregion
            #region Draw Extra Outfits Page
            if (isCurrentTab(4))
            {
                //HEADER
                StardewValley.BellsAndWhistles.SpriteText.drawString(b, "Manage Favorites:", xPositionOnScreen + 55, yPositionOnScreen + 115, 999999, -1, 999999, 1, 0.88f, false, -1, "", -1);

                b.Draw(Game1.daybg, new Vector2(xPositionOnScreen + 430 + Game1.tileSize + Game1.tileSize * 2 / 3 - 2, (yPositionOnScreen + 105) + borderWidth + spaceToClearTopBorder - Game1.tileSize / 4), Color.White);
                Game1.player.FarmerRenderer.draw(b, Game1.player.FarmerSprite.CurrentAnimationFrame, Game1.player.FarmerSprite.CurrentFrame, Game1.player.FarmerSprite.SourceRect, new Vector2(xPositionOnScreen + 428 + Game1.tileSize * 2 / 3 + Game1.tileSize * 2 - Game1.tileSize / 2, (yPositionOnScreen + 105) + borderWidth - Game1.tileSize / 4 + spaceToClearTopBorder + Game1.tileSize / 2), Vector2.Zero, 0.8f, Color.White, 0f, 1f, Game1.player);

                Utility.drawTextWithShadow(b, "You can set up to 30 additional favorite appearance", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 175), Color.Black);

                Utility.drawTextWithShadow(b, "configurations for each character.", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 200), Color.Black);

                Utility.drawTextWithShadow(b, "Your current appearance is shown on", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 285), Color.Black);
                Utility.drawTextWithShadow(b, "the right, select a favorite below to", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 310), Color.Black);
                Utility.drawTextWithShadow(b, "save your appearance in it or load the", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 335), Color.Black);
                Utility.drawTextWithShadow(b, "appearance saved in it :", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 360), Color.Black);

                foreach (ClickableTextureComponent bigFavButton in extraFavButtons)
                {
                    bigFavButton.draw(b);
                }

                string whatever;
                if (favSelected > -1)
                {
                    whatever = "Currently selected: " + (favSelected + 1) + "."; // <-- String for printing currently selected favorite
                    Utility.drawTextWithShadow(b, whatever, Game1.smallFont, new Vector2(xPositionOnScreen + 140, yPositionOnScreen + 533), Color.Black);
                    saveFavButton.draw(b);

                    Utility.drawTextWithShadow(b, "Overwrite Fav. Slot", Game1.smallFont, new Vector2(xPositionOnScreen + 140, yPositionOnScreen + 583), Color.Black);
                    loadFavButton.draw(b);
                }
                else
                {
                    whatever = "Please select a favorite...";
                    Utility.drawTextWithShadow(b, whatever, Game1.smallFont, new Vector2(xPositionOnScreen + 140, yPositionOnScreen + 533), Color.Black);
                }
            }
            #endregion
            #region Draw About Page
            if (isCurrentTab(3))
            {
                StardewValley.BellsAndWhistles.SpriteText.drawString(b, "About This Mod:", xPositionOnScreen + 55, yPositionOnScreen + 115, 999999, -1, 999999, 1, 0.88f, false, -1, "", -1);

                Utility.drawTextWithShadow(b, "Get Dressed created by JinxieWinxie and Advize", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 175), Color.Black);

                Utility.drawTextWithShadow(b, "You are using version:  " + GetDressed./*globalConfig.*/versionNumber, Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 225), Color.Black);

                StardewValley.BellsAndWhistles.SpriteText.drawString(b, "Settings:", xPositionOnScreen + 55, yPositionOnScreen + 275, 999999, -1, 999999, 1, 0.88f, false, -1, "", -1);

                Utility.drawTextWithShadow(b, "Face Types (M-F): " + GetDressed.globalConfig.maleFaceTypes + "-" + GetDressed.globalConfig.femaleFaceTypes, Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 345), Color.Black);
                Utility.drawTextWithShadow(b, "Nose Types (M-F): " + GetDressed.globalConfig.maleNoseTypes + "-" + GetDressed.globalConfig.femaleNoseTypes, Game1.smallFont, new Vector2(xPositionOnScreen + 400, yPositionOnScreen + 345), Color.Black);

                Utility.drawTextWithShadow(b, "Bottoms Types (M-F): " + (GetDressed.globalConfig.hideMaleSkirts ? 2 : GetDressed.globalConfig.maleBottomsTypes) + "-" + GetDressed.globalConfig.femaleBottomsTypes, Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 395), Color.Black);
                Utility.drawTextWithShadow(b, "Shoes Types (M-F): " + GetDressed.globalConfig.maleShoeTypes + "-" + GetDressed.globalConfig.femaleShoeTypes, Game1.smallFont, new Vector2(xPositionOnScreen + 400, yPositionOnScreen + 395), Color.Black);

                Utility.drawTextWithShadow(b, "Show Dresser: " + GetDressed.globalConfig.showDresser, Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 445), Color.Black);
                Utility.drawTextWithShadow(b, "Stove in Corner: " + GetDressed.globalConfig.stoveInCorner, Game1.smallFont, new Vector2(xPositionOnScreen + 400, yPositionOnScreen + 445), Color.Black);

                //Set Menu Access Key
                Utility.drawTextWithShadow(b, "Open Menu Key:  " + GetDressed.globalConfig.menuAccessKey, Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 525), Color.Black);
                setNewMenuAccessKeyButton.draw(b);

                //Set If Male Characters should have access to skirts
                Utility.drawTextWithShadow(b, "Toggle Skirts for Male Characters  ", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 600), Color.Black);
                hideMaleSkirtsButton.draw(b);

                //Set Zoom Level
                Utility.drawTextWithShadow(b, "Change Zoom Level  ", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 675), Color.Black);

                zoomOutButton.draw(b);
                zoomInButton.draw(b);

                //reset config options
                Utility.drawTextWithShadow(b, "Reset Options to Default  ", Game1.smallFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 750), Color.Black);
                resetConfigButton.draw(b);

                if (letUserAssignOpenMenuKey)
                {
                    b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.90f);
                    b.DrawString(Game1.dialogueFont, "Press new key...", new Vector2(xPositionOnScreen + 225, yPositionOnScreen + 350), Color.White);
                }
            }
            #endregion

            //Draw Temporary Animated Sprites
            if (GetDressed.globalConfig.showIntroBanner && floatingNewButton != null)
            {
                floatingNewButton.draw(b, true, 400, 950);
            }
            if (showFloatingPurpleArrow && floatingPurpleArrow != null)
            {
                floatingPurpleArrow.draw(b, true, 400, 950);
            }

            //Draw Alerts
            foreach (Alert alert in alerts)
            {
                alert.draw(b, Game1.smallFont);
            }

            //Draw Cancel Button
            cancelButton.draw(b);

            //Draw Cursor
            bool flag = !hoverText.Equals("No Favorite Is Set");

            if (flag)
            {
                b.Draw(Game1.mouseCursors, new Vector2(Game1.getOldMouseX(), Game1.getOldMouseY()), new Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 0, 16, 16)), Color.White, 0f, Vector2.Zero, Game1.pixelZoom + Game1.dialogueButtonScale / 150f, SpriteEffects.None, 1f);
            }
            else
            {
                b.Draw(GetDressed.menuTextures, new Vector2(Game1.getOldMouseX(), Game1.getOldMouseY()), new Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 6, 16, 16)), Color.White, 0f, Vector2.Zero, Game1.pixelZoom + Game1.dialogueButtonScale / 150f, SpriteEffects.None, 1f);
            }

            //Draw Tooltips
            drawHoverText(b, hoverText, Game1.smallFont, flag ? 0 : 20, flag ? 0 : 20, -1, null, -1, null, null, 0, -1, -1, -1, -1, 1f, null);
        }

        public void changeAccessory(int which)
        {
            if (which < -1)
            {
                which = (faceType == 0) ? 127 : 239;
            }
            if (which >= -1)
            {
                if (faceType != 0)
                {
                    if (which == 19)
                    {
                        which = 131;
                    }
                    if (which == 130)
                    {
                        which = 18;
                    }
                }
                if ((which >= 128 && faceType == 0) || which >= 240)
                {
                    which = -1;
                }
                Game1.player.accessory = which;
            }
        }

        public void changeFaceType(int which, bool noPatch = false)
        {
            if (which < 0)
            {
                which = (Game1.player.isMale ? GetDressed.globalConfig.maleFaceTypes : GetDressed.globalConfig.femaleFaceTypes) - 1;
            }
            if (which >= (Game1.player.isMale ? GetDressed.globalConfig.maleFaceTypes : GetDressed.globalConfig.femaleFaceTypes))
            {
                which = 0;
            }
            faceType = which;
            if (noPatch) return;
            patchBase();
        }

        public void changeNoseType(int which, bool noPatch = false)
        {
            if (which < 0)
            {
                which = (Game1.player.isMale ? GetDressed.globalConfig.maleNoseTypes : GetDressed.globalConfig.femaleNoseTypes) - 1;
            }
            if (which >= (Game1.player.isMale ? GetDressed.globalConfig.maleNoseTypes : GetDressed.globalConfig.femaleNoseTypes))
            {
                which = 0;
            }
            noseType = which;
            if (noPatch) return;
            patchBase();
        }

        public void changeBottoms(int which, bool noPatch = false)
        {
            if (which < 0)
            {
                which = (Game1.player.isMale ? (GetDressed.globalConfig.hideMaleSkirts ? 2 : GetDressed.globalConfig.maleBottomsTypes) : GetDressed.globalConfig.femaleBottomsTypes) - 1;
            }
            if (which >= (Game1.player.isMale ? (GetDressed.globalConfig.hideMaleSkirts ? 2 : GetDressed.globalConfig.maleBottomsTypes) : GetDressed.globalConfig.femaleBottomsTypes))
            {
                which = 0;
            }
            bottoms = which;
            if (noPatch) return;
            patchBase();
        }

        public void changeShoes(int which, bool noPatch = false)
        {
            if (which < 0)
            {
                which = (Game1.player.isMale ? GetDressed.globalConfig.maleShoeTypes : GetDressed.globalConfig.femaleShoeTypes) - 1;
            }
            if (which >= (Game1.player.isMale ? GetDressed.globalConfig.maleShoeTypes : GetDressed.globalConfig.femaleShoeTypes))
            {
                which = 0;
            }
            shoes = which;
            if (noPatch) return;
            patchBase();
        }

        public void changeGender(bool male)
        {
            if (male)
            {
                Game1.player.isMale = true;
                Game1.player.FarmerRenderer.baseTexture = GetDressed.InitTexture(true);
                Game1.player.FarmerRenderer.heightOffset = 0;
            }
            else
            {
                Game1.player.isMale = false;
                Game1.player.FarmerRenderer.heightOffset = 4;
                Game1.player.FarmerRenderer.baseTexture = GetDressed.InitTexture(false);
            }
            faceType = noseType = bottoms = shoes = 0;
            GetDressed.FixFarmerEffects(Game1.player);
        }

        public void patchBase()
        {
            string texturePath = (Game1.player.isMale ? "male_" : "female_");
            Game1.player.FarmerRenderer.baseTexture = GetDressed.InitTexture(Game1.player.isMale);
            GetDressed.PatchTexture(ref Game1.player.FarmerRenderer.baseTexture, texturePath + "faces.png", faceType * (Game1.player.isMale ? GetDressed.globalConfig.maleNoseTypes : GetDressed.globalConfig.femaleNoseTypes) + noseType + (shoes * ((Game1.player.isMale ? GetDressed.globalConfig.maleNoseTypes : GetDressed.globalConfig.femaleNoseTypes) * (Game1.player.isMale ? GetDressed.globalConfig.maleFaceTypes : GetDressed.globalConfig.femaleFaceTypes))), 0);
            GetDressed.PatchTexture(ref Game1.player.FarmerRenderer.baseTexture, texturePath + "bottoms.png", bottoms, 3);
            GetDressed.FixFarmerEffects(Game1.player);
        }

        private bool favoriteExists(int favoriteSlot)
        {
            if (favoriteSlot < 0 || favoriteSlot > GetDressed.currentConfig.chosenEyeColor.Count())
            {
                return false;
            }
            return GetDressed.currentConfig.chosenEyeColor[favoriteSlot] != 0;
        }

        private bool isCurrentTab(int t)
        {
            return (currentTab == t) ? true : false;
        }
    }

    public class Alert
    {
        int x;
        int y;
        string text;

        public float timeLeft;
        public float delayTimeLeft;
        public float transparency = 1f;
        public bool fadeIn;

        Texture2D texture = null;
        Rectangle sourceRect = new Rectangle(0, 0, 0, 0);

        public Alert(int x, int y, string text, float timeLeft, bool fadeIn, float delayTimeLeft = 0f)
        {
            this.x = x; this.y = y; this.text = text; this.timeLeft = timeLeft; this.fadeIn = fadeIn; this.delayTimeLeft = delayTimeLeft;
        }

        public Alert(Texture2D texture, Rectangle sourceRect, int x, int y, string text, float timeLeft, bool fadeIn, float delayTimeLeft = 0f)
        {
            this.texture = texture; this.sourceRect = sourceRect; this.x = x; this.y = y; this.text = text; this.timeLeft = timeLeft; this.fadeIn = fadeIn; this.delayTimeLeft = delayTimeLeft;
        }

        public bool update(GameTime time)
        {
            if (delayTimeLeft > 0f)
            {
                delayTimeLeft -= time.ElapsedGameTime.Milliseconds;
                return false;
            }
            timeLeft -= time.ElapsedGameTime.Milliseconds;
            if (timeLeft < 0f)
            {
                transparency -= 0.02f;
                if (transparency < 0f)
                {
                    return true;
                }
            }
            else if (fadeIn)
            {
                transparency = Math.Min(transparency + 0.02f, 1f);
            }
            return false;
        }

        public void draw(SpriteBatch b, SpriteFont font)
        {
            if (delayTimeLeft > 0f)
            {
                return;
            }
            int num = (int)font.MeasureString(text).X;

            b.Draw(Game1.mouseCursors, new Vector2(x + 370, y - 50), new Rectangle?(new Rectangle(319, 360, 1, 24)), Color.White * transparency, 0f, Vector2.Zero, new Vector2(num, Game1.pixelZoom), SpriteEffects.None, 0.0001f);

            b.Draw(Game1.mouseCursors, new Vector2(x + 275, y - 50), new Rectangle?(new Rectangle(293, 360, 24, 24)), Color.White * transparency, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 0.0001f);

            b.Draw(Game1.mouseCursors, new Vector2(x + 370 + num, y - 50), new Rectangle?(new Rectangle(322, 360, 7, 24)), Color.White * transparency, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 0.0001f);

            if (texture != null)
            {
                b.Draw(texture, new Vector2(x + 290, y - 35), new Rectangle?(sourceRect), Color.White * transparency, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 0.0001f);
            }

            Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(x + 375, y - 15), Color.Black * transparency, 1f, 1f, -1, -1, transparency, 3);
        }
    }

    public class LocalConfig : IComparable
    {
        public bool firstRun { get; set; }

        public int[] chosenAccessory = new int[numOfFavs];
        public int[] chosenFace = new int[numOfFavs];
        public int[] chosenNose = new int[numOfFavs];
        public int[] chosenBottoms = new int[numOfFavs];
        public int[] chosenShoes = new int[numOfFavs];

        public int[] chosenSkin = new int[numOfFavs];
        public int[] chosenShirt = new int[numOfFavs];
        public int[] chosenHairstyle = new int[numOfFavs];
        public uint[] chosenHairColor = new uint[numOfFavs];
        public uint[] chosenEyeColor = new uint[numOfFavs];
        public uint[] chosenBottomsColor = new uint[numOfFavs];

        [JsonIgnore]
        public int saveTime { get; set; }
        [JsonIgnore]
        public string saveName { get; set; }
        [JsonIgnore]
        private const int numOfFavs = 37;

        public LocalConfig()
        {
            firstRun = true;
        }

        public int CompareTo(object obj)
        {
            return ((LocalConfig)obj).saveTime - saveTime;
        }
    }

    public class GlobalConfig
    {
        public string versionNumber { get; set; }
        public string menuAccessKey { get; set; }
        public bool showIntroBanner { get; set; }
        public bool showDresser { get; set; }
        public int maleFaceTypes { get; set; }
        public int maleNoseTypes { get; set; }
        public int maleBottomsTypes { get; set; }
        public int maleShoeTypes { get; set; }
        public int femaleFaceTypes { get; set; }
        public int femaleNoseTypes { get; set; }
        public int femaleBottomsTypes { get; set; }
        public int femaleShoeTypes { get; set; }
        public bool stoveInCorner { get; set; }
        public bool hideMaleSkirts { get; set; }
        public bool menuZoomOut { get; set; }

        public GlobalConfig()
        {
            versionNumber = "3.2";
            menuAccessKey = "C";
            showIntroBanner = true;
            showDresser = true;
            maleFaceTypes = 2;
            maleNoseTypes = 3;
            maleBottomsTypes = 6;
            maleShoeTypes = 2;
            femaleFaceTypes = 2;
            femaleNoseTypes = 3;
            femaleBottomsTypes = 12;
            femaleShoeTypes = 4;
            stoveInCorner = false;
            hideMaleSkirts = false;
            menuZoomOut = true;
        }
    }
}