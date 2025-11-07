namespace Ratings
{
    public class Config
    {
        public bool Enabled { get; set; } = true;
        public int NotesCount { get; set; } = 96;
        public float Timescale { get; set; } = 1f;
        public float StarAccuracy { get; set; } = 0.96f;
    }
}
