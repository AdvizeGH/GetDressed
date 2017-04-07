using System.Collections.ObjectModel;
using xTile.Layers;
using xTile.Tiles;

namespace GetDressed
{
    public class Tile
    {
        public int layerIndex;
        public int x;
        public int y;
        public int tileIndex;
        public int tileSheetIndex;
        public string layer;
        public string tileSheet;

        public Tile(int layerIndex, int x, int y, int tileIndex, int tileSheetIndex = -1, string tileSheet = "")
        {
            this.layerIndex = layerIndex; this.x = x; this.y = y; this.tileIndex = tileIndex; this.tileSheetIndex = tileSheetIndex; this.tileSheet = tileSheet;
        }

        public Tile(string layer, int x, int y, int tileIndex, int tileSheetIndex = -1, string tileSheet = "")
        {
            this.layer = layer; this.x = x; this.y = y; this.tileIndex = tileIndex; this.tileSheetIndex = tileSheetIndex; this.tileSheet = tileSheet;
        }

        static public int getTileSheetIndex(string name, ReadOnlyCollection<TileSheet> tileSheets)
        {
            for (int i = 0; i < tileSheets.Count; i++)
            {
                if (tileSheets[i].Id.Equals(name))
                {
                    return i;
                }
            }
            //StardewModdingAPI.Log.Error("tileSheetName is incorrect, using first tile sheet");
            return 0;
        }

        static public string getTileSheetName(int index, ReadOnlyCollection<TileSheet> tileSheets)
        {
            if (index >= tileSheets.Count)
            {
                //StardewModdingAPI.Log.Error("tileSheetIndex out of range, using first layer");
                return tileSheets[0].Id;
            }
            return tileSheets[index].Id;
        }

        static public int getLayerIndex(string name, ReadOnlyCollection<Layer> layers)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].Id.Equals(name))
                {
                    return i;
                }
            }
            //StardewModdingAPI.Log.Error("layerName is incorrect, using first layer");
            return 0;
        }

        static public string getLayerName(int index, ReadOnlyCollection<Layer> layers)
        {
            if (index >= layers.Count)
            {
                //StardewModdingAPI.Log.Error("layerIndex out of range, using first layer");
                return layers[0].Id;
            }
            return layers[index].Id;
        }

    }
}
