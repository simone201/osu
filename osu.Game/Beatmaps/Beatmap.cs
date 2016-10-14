//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTK.Graphics;
using osu.Game.Beatmaps.Objects;
using osu.Game.Beatmaps.Samples;
using osu.Game.Beatmaps.Timing;
using osu.Game.Database;
using osu.Game.GameModes.Play;
using osu.Game.Users;
using SQLite;
using SQLiteNetExtensions.Attributes;

namespace osu.Game.Beatmaps
{
    public class Beatmap
    {
        [PrimaryKey]
        public int BeatmapID { get; set; }

        [ForeignKey(typeof(BeatmapSet))]
        public int BeatmapSetID { get; set; }

        [ForeignKey(typeof(BeatmapMetadata))]
        public int? BeatmapMetadataID { get; set; }

        [ForeignKey(typeof(BaseDifficulty))]
        public int BaseDifficultyID { get; set; }

        [ManyToOne]
        public BeatmapSet BeatmapSet { get; set; }

        [Ignore]
        public List<HitObject> HitObjects { get; set; }

        [Ignore]
        public List<ControlPoint> ControlPoints { get; set; }

        [ManyToOne]
        public BeatmapMetadata Metadata { get; set; }

        [ManyToOne]
        public BaseDifficulty BaseDifficulty { get; set; }

        [Ignore]
        public List<Color4> ComboColors { get; set; }
        
        // General
        public int AudioLeadIn { get; set; }
        public bool Countdown { get; set; }
        public SampleSet SampleSet { get; set; }
        public float StackLeniency { get; set; }
        public bool SpecialStyle { get; set; }
        public PlayMode Mode { get; set; }
        public bool LetterboxInBreaks { get; set; }
        public bool WidescreenStoryboard { get; set; }
        
        // Editor
        // This bookmarks stuff is necessary because DB doesn't know how to store int[]
        public string StoredBookmarks { get; internal set; }
        [Ignore]
        public int[] Bookmarks
        {
            get
            {
                return StoredBookmarks.Split(',').Select(int.Parse).ToArray();
            }
            set
            {
                StoredBookmarks = string.Join(",", value);
            }
        }
        public double DistanceSpacing { get; set; }
        public int BeatDivisor { get; set; }
        public int GridSize { get; set; }
        public double TimelineZoom { get; set; }
        
        // Metadata
        public string Version { get; set; }

        private bool loaded;

        public BeatmapDatabase BackingDatabase;

        public void Load()
        {
            if (loaded) return;

            Debug.Assert(BackingDatabase != null);

            BackingDatabase.PopulateBeatmap(this);

            loaded = true;
        }
    }
}