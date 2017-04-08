using System;
using Newtonsoft.Json;

namespace GetDressed.Framework
{
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
}
