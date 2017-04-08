namespace GetDressed.Framework
{
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
