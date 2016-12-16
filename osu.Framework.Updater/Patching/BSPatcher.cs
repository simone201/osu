using System;
using System.IO;
using System.IO.Compression;
using ICSharpCode.SharpZipLib.BZip2;
using Ionic.Zlib;

namespace osu_common.Libraries
{
    public delegate void ProgressUpdateHandler(object sender, long current, long total);

    public class BSPatcher
    {
        /// <summary>
        /// Occurs when more than one whole percentage has been processed.
        /// This is not totally accurate because we do not push updates during large stream reads
        /// for performance reasons.
        /// </summary>
        public event ProgressUpdateHandler OnProgress;

        /// <summary>
        /// Internal check for change in progress of over 1%.
        /// </summary>
        private int progress;

        private static int offtin(byte[] buf, int startOffset)
        {
            int y = buf[7 + startOffset] & 0x7F;
            y = y * 256;
            y += buf[6 + startOffset];
            y = y * 256;
            y += buf[5 + startOffset];
            y = y * 256;
            y += buf[4 + startOffset];
            y = y * 256;
            y += buf[3 + startOffset];
            y = y * 256;
            y += buf[2 + startOffset];
            y = y * 256;
            y += buf[1 + startOffset];
            y = y * 256;
            y += buf[0 + startOffset];

            if ((buf[7 + startOffset] & 0x80) > 0) y = -y;

            return y;
        }

        public void Patch(string oldFile, string newFile, string patchfile, Compression compression = Compression.BZip2)
        {
            long controlLength, dataLength, newSize;

            /*
            File format:
            0	8	"BSDIFF40"
            8	8	X
            16	8	Y
            24	8	sizeof(newfile)
            32	X	bzip2(control block)
            32+X	Y	bzip2(diff block)
            32+X+Y	???	bzip2(extra block)
            with control block a set of triples (x,y,z) meaning "add x bytes
            from oldfile to x bytes from the diff block; copy y bytes from the
            extra block; seek forwards in oldfile by z bytes".
            */

            using (FileStream header = File.OpenRead(patchfile))
            {
                byte[] headerBuf = new byte[32];

                if (header.Read(headerBuf, 0, 32) < 32)
                    throw new Exception("invalid patch file (too small)");

                if (!buffcmp(headerBuf, "BSDIFF40", 0, 8))
                    throw new Exception("invalid patch file (no magic)");

                controlLength = offtin(headerBuf, 8);
                dataLength = offtin(headerBuf, 16);
                newSize = offtin(headerBuf, 24);

                if (controlLength < 0 || dataLength < 0 || newSize < 0)
                    throw new Exception("invalid patch file (sizes are corrupt)");
            }

            long progressSize = newSize * 3;

            Stream controlStream, diffStream, extraStream;

            byte[] cfs, dfs, efs;

            using (FileStream stream = File.OpenRead(patchfile))
            {
                stream.Seek(32, SeekOrigin.Begin);

                cfs = new byte[controlLength];
                stream.Read(cfs, 0, cfs.Length);

                dfs = new byte[dataLength];
                stream.Read(dfs, 0, dfs.Length);

                efs = new byte[stream.Length - stream.Position];
                stream.Read(efs, 0, efs.Length);
            }

            switch (compression)
            {
                case Compression.BZip2:
                    controlStream = new BZip2InputStream(new MemoryStream(cfs));
                    diffStream = new BZip2InputStream(new MemoryStream(dfs));
                    extraStream = new BZip2InputStream(new MemoryStream(efs));
                    break;
                default:
                    controlStream = new GZipStream(new MemoryStream(cfs), CompressionMode.Decompress);
                    diffStream = new GZipStream(new MemoryStream(dfs), CompressionMode.Decompress);
                    extraStream = new GZipStream(new MemoryStream(efs), CompressionMode.Decompress);
                    break;
            }

            byte[] oldFileBytes;

            int segment_length = 1048573;

            using (FileStream stream = new FileStream(oldFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long length = stream.Length;
                oldFileBytes = new byte[(int)length];
                for (int i = 0; i < oldFileBytes.Length; i += segment_length)
                {
                    stream.Read(oldFileBytes, i, Math.Min(segment_length, oldFileBytes.Length - i));
                    updateProgress(i, progressSize);
                }
            }

            byte[] newFileBytes = new byte[newSize];

            long oldSize = oldFileBytes.Length;

            int newPos = 0;
            int oldPos = 0;

            int[] ctrl = new int[3];
            byte[] buf = new byte[8];

            /* Read control data */
            while (newPos < newSize)
            {
                int lenRead;

                for (int i = 0; i <= 2; i++)
                {
                    lenRead = controlStream.Read(buf, 0, 8);
                    if (lenRead < 8)
                        throw new Exception("invalid patch file (corrupt)");
                    ctrl[i] = offtin(buf, 0);
                }

                if (newPos + ctrl[0] > newSize)
                    throw new Exception("invalid patch file (corrupt)");

                /* Read diff string */
                lenRead = 0;

                for (int i = newPos; i < newPos + ctrl[0]; i += 65536)
                {
                    lenRead += diffStream.Read(newFileBytes, i, Math.Min(65536, (newPos + ctrl[0]) - i));
                    updateProgress(newSize + newPos + lenRead, progressSize);
                }

                if (lenRead < ctrl[0])
                    throw new Exception("invalid patch file (corrupt)");

                /* Add old data to diff string */
                for (int i = 0; i < ctrl[0]; i++)
                    if ((oldPos + i >= 0) && (oldPos + i < oldSize))
                        newFileBytes[newPos + i] += oldFileBytes[oldPos + i];

                /* Adjust pointers */
                newPos += ctrl[0];
                oldPos += ctrl[0];

                if (newPos > newSize)
                    throw new Exception("invalid patch file (corrupt)");

                /* Read extra string */
                lenRead = extraStream.Read(newFileBytes, newPos, ctrl[1]);
                if (lenRead < ctrl[1])
                    throw new Exception("invalid patch file (corrupt)");

                /* Sanity-check */
                newPos += ctrl[1];
                oldPos += ctrl[2];
            }

            controlStream.Close();
            diffStream.Close();
            extraStream.Close();

            using (FileStream str = File.Create(newFile))
            {
                for (int i = 0; i < newFileBytes.Length; i += segment_length)
                {
                    str.Write(newFileBytes, i, Math.Min(segment_length, newFileBytes.Length - i));
                    updateProgress(newSize * 2 + i, progressSize);
                }
            }

            if (OnProgress != null)
                OnProgress(this, 1, 1);
        }

        private void updateProgress(long pos, long size)
        {
            int lastPercent = progress;
            progress = (int)((float)pos / size * 100);
            if (lastPercent != progress && OnProgress != null)
                OnProgress(this, pos, size);
        }

        private static bool buffcmp(byte[] buf, string s, int start, int count)
        {
            for (int i = start; i < start + count; i++)
                if (buf[i] != s[i])
                    return false;
            return true;
        }
    }

    public enum Compression
    {
        GZip,
        BZip2
    }
}
