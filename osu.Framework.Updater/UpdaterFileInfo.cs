using System;

namespace osu_common.Updater
{
    [Serializable]
    public class UpdaterFileInfo
    {
        public int file_version;
        public string filename;
        public string file_hash;
        public int filesize;
        public DateTime timestamp;

        public int? patch_id;
        public int? patch_from;

        public string url_full;
        public string url_patch;

        public string response;

        public bool zip;
    }
}
