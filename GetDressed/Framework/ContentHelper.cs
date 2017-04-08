using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace GetDressed.Framework
{
    internal class ContentHelper
    {
        private readonly IModHelper modHelper;
        public readonly IMonitor modMonitor;

        private ContentManager cm;
        private Texture2D accessoriesTexture;
        public Texture2D menuTextures;

        public bool IsInitialised => this.cm != null;

        public ContentHelper(IModHelper helper, IMonitor monitor)
        {
            this.modHelper = helper;
            this.modMonitor = monitor;
        }

        public void InitializeContent(IServiceProvider serviceProvider)
        {
            if (cm != null) return;

            cm = new ContentManager(serviceProvider, Path.Combine(modHelper.DirectoryPath, "overrides"));
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

        public void PatchFarmerRenderer(Farmer farmer, Texture2D baseFile)
        {
            farmer.FarmerRenderer = new FarmerRenderer(baseFile);
            farmer.FarmerRenderer.heightOffset = farmer.isMale ? 0 : 4;
            FarmerRenderer.accessoriesTexture = accessoriesTexture;
            FixFarmerEffects(farmer);
        }

        public void PatchTexture(ref Texture2D targetTexture, string overridingTexturePath, int sourceID, int targetID, int gridWidth = 96, int gridHeight = 672)
        {
            using (FileStream textureStream = new FileStream(Path.Combine(cm.RootDirectory, overridingTexturePath), FileMode.Open))
            {
                Texture2D sourceTexture = Texture2D.FromStream(Game1.graphics.GraphicsDevice, textureStream);
                Color[] data = new Color[gridWidth * gridHeight];
                sourceTexture.GetData(0, GetSourceRect(sourceID, sourceTexture, gridWidth, gridHeight), data, 0, data.Length);
                targetTexture.SetData(0, GetSourceRect(targetID, targetTexture, gridWidth, gridHeight), data, 0, data.Length);
            }
        }

        public void FixFarmerEffects(Farmer farmer)
        {
            farmer.changeShirt(farmer.shirt);
            farmer.changeEyeColor(farmer.newEyeColor);
            farmer.changeSkinColor(farmer.skin);

            if (farmer.boots != null)
            {
                farmer.changeShoeColor(farmer.boots.indexInColorSheet);
            }
        }

        public Texture2D InitTexture(bool isMale, bool returnNew = true)
        {
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

        private Rectangle GetSourceRect(int index, Texture2D texture, int gridWidth, int gridHeight)
        {
            return new Rectangle(index % (texture.Width / gridWidth) * gridWidth, index / (texture.Width / gridWidth) * gridHeight, gridWidth, gridHeight);
        }
    }
}
