using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Beatmaps.IO;
using SQLite;
using SQLiteNetExtensions.Extensions;

namespace osu.Game.Database
{
    public class BeatmapDatabase
    {
        private static SQLiteConnection db;
        private BasicStorage storage;
        public event Action<BeatmapSet> BeatmapSetAdded;

        public BeatmapDatabase(BasicStorage storage)
        {
            this.storage = storage;
            if (db == null)
            {
                db = storage.GetDatabase(@"beatmaps");
                db.CreateTable<BeatmapMetadata>();
                db.CreateTable<BaseDifficulty>();
                db.CreateTable<BeatmapSet>();
                db.CreateTable<Beatmap>();
            }
        }

        public void AddBeatmap(string path)
        {
            string hash = null;
            ArchiveReader reader;
            if (File.Exists(path)) // Not always the case, i.e. for LegacyFilesystemReader
            {
                using (var md5 = MD5.Create())
                using (var input = storage.GetStream(path))
                {
                    hash = BitConverter.ToString(md5.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                    input.Seek(0, SeekOrigin.Begin);
                    var outputPath = Path.Combine(@"beatmaps", hash.Remove(1), hash.Remove(2), hash);
                    using (var output = storage.GetStream(outputPath, FileAccess.Write))
                        input.CopyTo(output);
                    reader = ArchiveReader.GetReader(storage, path = outputPath);
                }
            }
            else
                reader = ArchiveReader.GetReader(storage, path);
            var metadata = reader.ReadMetadata();
            if (db.Table<BeatmapSet>().Where(b => b.BeatmapSetID == metadata.BeatmapSetID).Any())
                return; // TODO: Update this beatmap instead
            string[] mapNames = reader.ReadBeatmaps();

            var beatmapSet = new BeatmapSet
            {
                BeatmapSetID = metadata.BeatmapSetID,
                Metadata = metadata,
                Path = path,
                Hash = hash,
            };
            foreach (var name in mapNames)
            {
                using (var stream = new StreamReader(reader.ReadFile(name)))
                {
                    var decoder = BeatmapDecoder.GetDecoder(stream);
                    Beatmap beatmap = new Beatmap();
                    decoder.Decode(stream, beatmap);
                    beatmapSet.Beatmaps.Add(beatmap);

                    beatmap.BaseDifficultyID = beatmap.BaseDifficulty.ID;
                }
            }

            db.InsertWithChildren(beatmapSet);

            BeatmapSetAdded?.Invoke(beatmapSet);
        }

        public ArchiveReader GetReader(BeatmapSet beatmapSet)
        {
            return ArchiveReader.GetReader(storage, beatmapSet.Path);
        }

        /// <summary>
        /// Given a BeatmapSet pulled from the database, loads the rest of its data from disk.
        /// </summary>
        public void PopulateBeatmap(Beatmap beatmap)
        {
            using (var reader = GetReader(beatmap.BeatmapSet))
            {
                string[] mapNames = reader.ReadBeatmaps();
                foreach (var name in mapNames)
                {
                    using (var stream = new StreamReader(reader.ReadFile(name)))
                    {
                        var decoder = BeatmapDecoder.GetDecoder(stream);
                        decoder.Decode(stream, beatmap);
                    }
                }
            }
        }

        public BeatmapSet[] GetBeatmapSets()
        {
            var sets = db.Table<BeatmapSet>().ToArray();
            return sets;
        }

        public BeatmapSet GetBeatmapSet(int beatmapSetId) => db.Get<BeatmapSet>(beatmapSetId);
    }
}
