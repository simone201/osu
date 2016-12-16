using System;
using System.IO;
using System.Threading;
using ICSharpCode.SharpZipLib.Zip;
using osu.Desktop.Updater.Patching;
using osu.Framework.IO.Network;

namespace osu.Desktop.Updater
{
    public class DownloadableFileInfo : UpdaterFileInfo
    {
        internal FileWebRequest netRequest;
        internal float progress;
        internal bool isPatching;
        internal bool isRunning;
        private int downloaded_bytes;
        public Exception Error;

        internal void Perform(bool doPatching = false)
        {
            isRunning = true;
            progress = 0;
            downloaded_bytes = 0;
            isPatching = false;

            if (doPatching && url_patch == null)
                throw new Exception(@"patch not available for this file!");

            string localPath = CommonUpdater.STAGING_FOLDER + "/" + filename + (doPatching ? "_patch" : @".zip");

            netRequest = new FileWebRequest(localPath, doPatching ? url_patch : (url_full + @".zip"));
            netRequest.DownloadProgress += delegate(WebRequest sender, long current, long total)
            {
                progress = (float)current / total * (doPatching ? 50 : 100);
                downloaded_bytes = (int)current;
                filesize = (int)total;
            };

            try
            {
                netRequest.BlockingPerform();
                if (netRequest.Aborted)
                    isRunning = false;
                else if (!File.Exists(localPath))
                    throw new Exception(@"couldn't find downloaded file (" + localPath + ")");
            }
            catch (ThreadAbortException)
            {
                isRunning = false;
                return;
            }

            if (netRequest.Filename.EndsWith(@".zip"))
            {
                FastZip fz = new FastZip();
                fz.ExtractZip(localPath, CommonUpdater.STAGING_FOLDER, @".*");
                File.Delete(localPath);
            }

            if (doPatching)
            {
                string beforePatch = CommonUpdater.STAGING_FOLDER + @"/" + filename;
                string afterPatch = CommonUpdater.STAGING_FOLDER + @"/" + filename + @"_patched";

                try
                {
                    isPatching = true;
                    BSPatcher patcher = new BSPatcher();
                    patcher.OnProgress += delegate(object sender, long current, long total) { progress = 50 + (float)current / total * 50; };
                    patcher.Patch(beforePatch, afterPatch, localPath);
                }
                finally
                {
                    File.Delete(localPath);
                    File.Delete(beforePatch);
                    if (File.Exists(afterPatch))
                        File.Move(afterPatch, beforePatch);
                    isPatching = false;
                }

                isRunning = false;
            }
        }

        private Thread thread;
        internal void PerformThreaded(Action onComplete = null, Action onError = null)
        {
            thread = new Thread(() =>
            {
                int attempts = 0;
                const int max_attempts = 4;

            retry:
                try
                {
                    Perform();
                    onComplete?.Invoke();
                }
                catch (ThreadAbortException)
                {
                    Abort();
                }
                catch (TimeoutException ex)
                {
                    while (++attempts < max_attempts)
                    {
                        if (attempts > 1)
                        {
                            string currentMirror = @"m" + (attempts - 1) + @".";
                            string nextMirror = @"m" + attempts + @".";

                            if (!string.IsNullOrEmpty(url_full)) url_full = url_full.Replace(currentMirror, nextMirror);
                            if (!string.IsNullOrEmpty(url_patch)) url_patch = url_patch.Replace(currentMirror, nextMirror);
                        }

                        Thread.Sleep(1500);
                        goto retry;
                    }

                    CommonUpdater.LastErrorExtraInformation = url_patch ?? url_full;

                    Error = ex;
                    onError?.Invoke();
                }
                catch (Exception ex)
                {
                    Error = ex;
                    onError?.Invoke();
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }

        internal void Abort()
        {
            if (thread != null)
            {
                thread.Abort();
                thread = null;
            }

            netRequest?.Abort();
        }

        public override string ToString()
        {
            return filename;
        }
    }
}
