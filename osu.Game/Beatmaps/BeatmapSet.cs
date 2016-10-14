//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Collections.Generic;
using osu.Game.Database;
using osu.Game.Users;
using SQLite;
using SQLiteNetExtensions.Attributes;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// A beatmap set contains multiple beatmap (difficulties).
    /// </summary>
    public class BeatmapSet
    {
        [PrimaryKey]
        public int BeatmapSetID { get; set; }

        [ForeignKey(typeof(BeatmapMetadata))]
        public int BeatmapMetadataID { get; set; }

        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public virtual List<Beatmap> Beatmaps { get; set; }

        [OneToOne(CascadeOperations = CascadeOperation.All)]
        public BeatmapMetadata Metadata { get; set; }

        [Ignore]
        public User Creator { get; set; }

        public string Hash { get; set; }

        public string Path { get; set; }
    }
}
