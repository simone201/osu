using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using osu.Framework.IO.Network;
using osu.Framework.Logging;

namespace osu.Desktop.Updater
{
    public static class CommonUpdater
    {
        static Thread updateThread;

        public static string CENTRAL_UPDATE_URL = @"https://osu.ppy.sh/web/check-updates.php";
        public static string MIRROR_UPDATE_URL = @"https://m1.ppy.sh/release/";

        private static List<DownloadableFileInfo> ActiveFiles = new List<DownloadableFileInfo>();

        private static UpdateCompleteDelegate completeCallback;

        private static int totalUpdatableFiles = 0;

        private static Logger logger;

        public static void Log(string message = "", params object[] parms)
        {
            if (logger == null) logger = Logger.GetLogger();
            logger.Add(string.Format(message, parms), LogLevel.Important);
        }

        public static void LogSuccess()
        {
            logger.Clear(@"success");
        }

        public static float Percentage
        {
            get
            {
                lock (ActiveFiles)
                {
                    totalUpdatableFiles = Math.Max(ActiveFiles.Count, totalUpdatableFiles);

                    switch (totalUpdatableFiles)
                    {
                        case 0:
                            return 0;
                        case 1:
                            return ActiveFiles.Count > 0 ? ActiveFiles[0].progress : 100;
                        default:
                            float progress = (totalUpdatableFiles - ActiveFiles.Count) * 100;
                            foreach (DownloadableFileInfo dfi in ActiveFiles)
                                progress += dfi.progress;
                            return (progress / (totalUpdatableFiles * 100)) * 100;
                    }
                }
            }
        }

        public static Exception LastError;
        public static string LastErrorExtraInformation = string.Empty;

        public static ReleaseStream ReleaseStream;
        public static UpdateStates State;

        /// <summary>
        /// Folder used during download and patching.
        /// </summary>
        public const string STAGING_FOLDER = @"_staging";

        /// <summary>
        /// Folder used to stage final update files ready for application.
        /// </summary>
        public const string STAGING_COMPLETE_FOLDER = @"_pending";

        public static string GetStatusString(bool includeProgress = false)
        {
            switch (State)
            {
                case UpdateStates.Checking:
                    return "CommonUpdater_CheckingForUpdates";
                case UpdateStates.Error:
                    //if (LastError is MissingFrameworkVersionException)
                    //    return "GameBase_UpdateFailedFrameworkVersion";
                    //else
                        return "CommonUpdater_ErrorOccurred";
                case UpdateStates.NeedsRestart:
                    return "CommonUpdater_RestartRequired";
                case UpdateStates.Updating:
                    lock (ActiveFiles)
                    {
                        totalUpdatableFiles = Math.Max(ActiveFiles.Count, totalUpdatableFiles);

                        string progress = includeProgress && Percentage > 0 ? " (" + (int)Percentage + "%)" : string.Empty;

                        if (ActiveFiles.Count == 0 || totalUpdatableFiles == 0)
                            return "CommonUpdater_PerformingUpdates";

                        int runningCount = ActiveFiles.FindAll(f => f.isRunning).Count;

                        if (runningCount > 1)
                        {
                            if (totalUpdatableFiles == ActiveFiles.Count)
                                return string.Format("CommonUpdater_DownloadingRequiredFiles", totalUpdatableFiles) + progress;
                            else
                                //has at least one file finished.
                                return string.Format("CommonUpdater_DownloadingRequiredFiles2", totalUpdatableFiles - ActiveFiles.Count, totalUpdatableFiles) + progress;
                        }

                        DownloadableFileInfo lastRunning = ActiveFiles.FindLast(f => f.isRunning);
                        if (lastRunning != null)
                        {
                            if (lastRunning.isPatching)
                                return String.Format("CommonUpdater_PatchingFilePercentage", lastRunning.filename) + progress;
                            else
                                return String.Format("CommonUpdater_DownloadingFile", lastRunning.filename + (lastRunning.url_patch != null ? @"_patch" : string.Empty)) + progress;
                        }

                        return "CommonUpdater_PerformingUpdates";
                    }
                default:
                    return "CommonUpdater_Updated";
            }
        }

        static object UpdateLock = new object();
        public static void Check(UpdateCompleteDelegate callback = null, ReleaseStream stream = ReleaseStream.Lazer, ThreadPriority priority = ThreadPriority.Normal)
        {
            lock (UpdateLock)
            {
                if (updateThread != null) return;

                ReleaseStream = stream;
                completeCallback = callback;

                setCallbackStatus(UpdateStates.Checking);

                updateThread = new Thread(() =>
                {
                    try
                    {
                        Log();
                        Log(@"Beginning update thread");
                        Log(@"Stream: " + ReleaseStream);
                        Log();
                        UpdateStates state = doUpdate();
                        Log();
                        Log(@"Ending update thread with result: " + state);
                        Log();
                        setCallbackStatus(state);
                    }
                    catch (ThreadAbortException)
                    {
                        setCallbackStatus(UpdateStates.NoUpdate);
                    }
                    catch (Exception e)
                    {
                        Log(@"Serious error occurred in update thread: " + e.ToString());
                        Log(@"Returning NoUpdate state to caller");
                        setCallbackStatus(UpdateStates.EmergencyFallback);
                    }

                    completeCallback = null;
                    updateThread = null;
                });

                updateThread.IsBackground = true;
                updateThread.Priority = priority;

                updateThread.Start();
            }
        }

        private static void setCallbackStatus(UpdateStates state)
        {
            if (State == state) return;

            Log(@"CallbackStatus updated to {0}", state.ToString());
            if (state == UpdateStates.Completed || state == UpdateStates.NoUpdate)
                LogSuccess();

            State = state;
            if (completeCallback != null) completeCallback(state);
        }

        private static UpdateStates doUpdate()
        {
            //ensure we have the correct version of .net available.
            if (ReleaseStream.ToString().Contains(@"40") && !DotNetFramework.CheckInstalled(FrameworkVersion.dotnet4))
            {
                Log(@"Not updating due to missing .NET 4 installation");
                LastError = new MissingFrameworkVersionException(FrameworkVersion.dotnet4);
                return UpdateStates.Error;
            }

            try
            {
                ConfigManagerCompact.LoadConfig();

                //ensure we are starting clean.
                //note that we don't clear a pending update just yet because there are scenarios we can use this (see variable stagedAndWaitingToUpdate below).
                Cleanup(10000, false);

                List<DownloadableFileInfo> streamFiles = null;

                string rawString = string.Empty;

                try
                {
                    Log(@"Requesting update information...");

                    var sn = new JsonWebRequest<List<DownloadableFileInfo>>(string.Format(CENTRAL_UPDATE_URL + @"?action=check&stream={0}&time={1}", ReleaseStream.ToString().ToLower(), DateTime.Now.Ticks));
                    sn.BlockingPerform();
                    rawString = sn.ResponseString;

                    if (rawString == @"fallback")
                    {
                        Log(@"Server has requested an emergency fallback!");
                        return UpdateStates.EmergencyFallback;
                    }

                    streamFiles = sn.ResponseObject;
                }
                catch (Exception e)
                {
                    LastError = e;
                    LastErrorExtraInformation = rawString;

                    Log(LastError.ToString());
                    Log(rawString);
                    return UpdateStates.Error;
                }

                if (streamFiles == null || streamFiles.Count == 0)
                {
                    LastError = new Exception($@"update file returned no results ({rawString})");
                    Log(LastError.ToString());
                    return UpdateStates.Error;
                }

                tryAgain:
                bool stagedAndWaitingToUpdate = Directory.Exists(STAGING_COMPLETE_FOLDER);
                List<DownloadableFileInfo> updateFiles = new List<DownloadableFileInfo>();

                if (stagedAndWaitingToUpdate)
                    Log(@"A pending update is already waiting. Checking what we're working with...");

                foreach (DownloadableFileInfo file in streamFiles)
                {
                    if (stagedAndWaitingToUpdate)
                    {
                        //special case where we already have a pending update.
                        //if we find *any* file which doesn't match, we should remove the folder and restart the process.

                        string pendingFilename = STAGING_COMPLETE_FOLDER + @"/" + file.filename;

                        if (File.Exists(pendingFilename))
                        {
                            Log(file.filename + @": PENDING");
                            //pending file exists and matches this update precisely.
                            if (getMd5(pendingFilename) == file.file_hash)
                                continue;
                        }
                        else if (File.Exists(file.filename) && getMd5(file.filename, true) == file.file_hash)
                        {
                            Log(file.filename + @": LATEST");
                            //current version is already newest.
                            continue;
                        }

                        Log(file.filename + @": MISMATCH");

                        //something isn't up-to-date. let's run the update process again without our pending version.
                        Reset(false);

                        goto tryAgain;
                    }

                    if (!File.Exists(file.filename))
                    {
                        updateFiles.Add(file);
                        Log(file.filename + @": NEW");
                    }
                    else if (getMd5(file.filename, true) != file.file_hash)
                    {
                        Log(file.filename + @": CHANGED (cached)");
                        updateFiles.Add(file); //cached md5 check failed.
                    }
                    else if (file.filename == @"osu!.exe" && getMd5(file.filename, true) != getMd5(file.filename)) //special intensive check for main osu!.exe
                    {
                        Log(file.filename + @": CHANGED");
                        ConfigManagerCompact.ResetHashes();
                        goto tryAgain;
                    }
                }

                if (stagedAndWaitingToUpdate)
                {
                    Log(@"Pending updating is waiting and requires no further changes.");
                    //return early, as we've already done the rest of the process below in a previous run.
                    return MoveInPlace() ? UpdateStates.Completed : UpdateStates.NeedsRestart;
                }

                if (updateFiles.Count == 0)
                {
                    Log(@"No changes to apply!");

                    if (!DotNetFramework.CheckInstalled(FrameworkVersion.dotnet4))
                    {
                        Log(@"Becuase we had no changes, let's let the user know they need a newer framework in the future");

                        //if there are no pending updates but the user doesn't have dotnet4 available, we should tell them to get it.
                        LastError = new MissingFrameworkVersionException(FrameworkVersion.dotnet4);
                        return UpdateStates.Error;
                    }

                    return UpdateStates.NoUpdate;
                }

                totalUpdatableFiles = updateFiles.Count;

                if (!Directory.Exists(STAGING_FOLDER))
                {
                    Log(@"Creating staging folder");
                    DirectoryInfo di = Directory.CreateDirectory(STAGING_FOLDER);
                    di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
                }

                setCallbackStatus(UpdateStates.Updating);

                lock (ActiveFiles) ActiveFiles.AddRange(updateFiles);

                foreach (DownloadableFileInfo f in updateFiles)
                {
                    DownloadableFileInfo file = f;

                    Log(@"Processing {0}...", file.ToString());

                    if (File.Exists(file.filename))
                    {
                        Log(@"Exists locally; checking for patchability...");
                        string localHash = getMd5(file.filename);

                        //check for patchability...
                        try
                        {
                            var sn = new JsonWebRequest<List<DownloadableFileInfo>>(@"{0}?action=path&stream={1}&target={2}&existing={3}&time={4}",
                                CENTRAL_UPDATE_URL,
                                ReleaseStream.ToString().ToLower(),
                                file.file_version,
                                localHash,
                                DateTime.Now.Ticks);

                            sn.BlockingPerform();

                            List<DownloadableFileInfo> patchFiles = sn.ResponseObject;

                            if (patchFiles.Count > 1)
                            {
                                Log(@"Server returned {0} patch files", patchFiles.Count.ToString());
                                //copy the local version to the staging path.

                                File.Copy(file.filename, STAGING_FOLDER + @"/" + file.filename);

                                bool success = false;

                                lock (ActiveFiles)
                                    ActiveFiles.AddRange(patchFiles.GetRange(0, patchFiles.Count - 1));
                                totalUpdatableFiles += patchFiles.Count - 1;

                                try
                                {
                                    //we now know we have a patchable path, so let's try patching!
                                    foreach (DownloadableFileInfo patch in patchFiles)
                                    {
                                        try
                                        {
                                            if (patch.file_version == file.file_version)
                                            {
                                                Log(@"Reached end of patch chain ({0} / {1}), checking file checksum...",
                                                    patch.file_version.ToString(), patch.file_hash);

                                                //reached the end of patching.
                                                if (getMd5(STAGING_FOLDER + @"/" + file.filename) != file.file_hash)
                                                {
                                                    Log(@"Patching FAILED!");
                                                    throw new Exception(@"patching failed to end with correct checksum.");
                                                }

                                                Log(@"Patching success!");
                                                success = true;
                                                break; //patching successful!
                                            }

                                            string localPatchFilename = STAGING_FOLDER + @"/" + patch.filename + @"_patch";

                                            Log(@"Applying patch {0} (to {1})...", patch.file_version.ToString(), patch.file_hash);
                                            patch.Perform(true);
                                        }
                                        finally
                                        {
                                            lock (ActiveFiles) ActiveFiles.Remove(patch);
                                        }
                                    }

                                    if (success)
                                    {
                                        lock (ActiveFiles) ActiveFiles.Remove(file);
                                        continue;
                                    }
                                }
                                finally
                                {
                                    lock (ActiveFiles)
                                        patchFiles.ForEach(pf => ActiveFiles.Remove(pf));
                                }
                            }

                            Log(@"No patches available; falling back to full download");
                        }
                        catch (Exception e)
                        {
                            Log(@"Error occured during patching: " + e);
                            //an error occurred when trying to patch; fallback to a full update.
                        }
                    }

                    Log(@"Beginning download of {0} ({1})...", file.filename, file.url_full);
                    file.PerformThreaded(delegate
                    {
                        lock (ActiveFiles) ActiveFiles.Remove(file);
                        Log(@"Completed download of {0} ({1} files remain)!", file.filename, ActiveFiles.Count);
                    },
                    delegate
                    {
                        Log(@"Failed download of {0}! Error: {1}", file.filename, file.Error);
                        //error occurred
                    });
                }

                while (ActiveFiles.Count > 0)
                {
                    foreach (DownloadableFileInfo dfi in ActiveFiles)
                        if (dfi.Error != null)
                        {
                            LastError = dfi.Error;
                            return UpdateStates.Error;
                        }
                    Thread.Sleep(100);
                }

                if (State == UpdateStates.Updating)
                {
                    if (Directory.Exists(STAGING_COMPLETE_FOLDER))
                        Directory.Delete(STAGING_COMPLETE_FOLDER, true);

                    GeneralHelper.RecursiveMove(STAGING_FOLDER, STAGING_COMPLETE_FOLDER);

                    ConfigManagerCompact.Configuration[@"_ReleaseStream"] = ReleaseStream.ToString();

                    return MoveInPlace() ? UpdateStates.Completed : UpdateStates.NeedsRestart;
                }

                return UpdateStates.NoUpdate;
            }
            catch (ThreadAbortException)
            {
                Log(@"Thread was aborted!");
                foreach (DownloadableFileInfo dfi in ActiveFiles)
                    dfi.Abort();
                return UpdateStates.NoUpdate;
            }
            catch (Exception e)
            {
                Log(@"Error: " + e.ToString());
                LastError = e;
                return UpdateStates.Error;
            }
            finally
            {
                Log(@"Saving out global config");
                ConfigManagerCompact.SaveConfig();
                updateThread = null;
            }
        }

        private static string getMd5(string filename, bool useCache = false)
        {
            if (!File.Exists(filename)) return null;

            if (useCache)
            {
                try
                {
                    if (!ConfigManagerCompact.Configuration.ContainsKey(@"h_" + filename))
                        ConfigManagerCompact.Configuration[@"h_" + filename] = getMd5(filename);
                    return ConfigManagerCompact.Configuration[@"h_" + filename];
                }
                catch (Exception)
                {

                }
            }

            try
            {
                using (Stream s = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    MD5 md5 = MD5.Create();
                    byte[] data = md5.ComputeHash(s);
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < data.Length; i++)
                        sb.Append(data[i].ToString(@"x2"));
                    return sb.ToString();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool SafelyMove(string src, string dest, int timeoutMilliseconds = 2000, int retryCount = 5, bool allowDefiniteMove = true)
        {
            int waitLength = timeoutMilliseconds / retryCount;

            try
            {
                File.Delete(dest + @"_old");
            }
            catch { }

            while (retryCount-- > 0)
            {
                try
                {
                    if (allowDefiniteMove)
                        File.Delete(dest);
                    else
                        File.Delete(dest);
                }
                catch
                { }

                try
                {
                    File.Move(src, dest);
                    return true; //success pattern 1
                }
                catch
                {
                    try
                    {
                        File.Copy(src, dest, true);
                        File.Delete(src);
                        return true; //success pattern 2
                    }
                    catch
                    {
                    }
                }

                Thread.Sleep(waitLength);
            }

            return false;
        }

        public static bool MoveInPlace(bool allowDefiniteMove = false)
        {
            if (!Directory.Exists(STAGING_COMPLETE_FOLDER)) return true;

            Log(@"Attempting to MoveInPlace");

            ConfigManagerCompact.LoadConfig();

            if (!moveInPlaceRecursive(STAGING_COMPLETE_FOLDER, allowDefiniteMove))
                return false;

            Cleanup();

            ConfigManagerCompact.SaveConfig();

            Log(@"MoveInPlace successful!");

            return true;
        }

        private static bool moveInPlaceRecursive(string folder, bool allowDefiniteMove = false)
        {
            try
            {
                foreach (string directory in Directory.GetDirectories(folder))
                    if (!moveInPlaceRecursive(directory))
                        return false;

                foreach (string file in Directory.GetFiles(folder))
                {
                    if (!AuthenticodeTools.IsTrusted(file))
                    {
                        if (Path.GetFileName(file) == @"bass.dll")
                        {
                            //bass.dll has internal checksum verification that causes it to think the file was tampered if we sign it.
                        }
                        else
                        {
                            Log(@"Authenticode signature check failed on {0}!", file);
                            File.Delete(file);
                            continue;
                        }
                    }

                    if (new FileInfo(file).Length == 0)
                    {
                        File.Delete(file);
                        continue;
                    }

                    //get a relative path.
                    string[] path = file.Split(Path.DirectorySeparatorChar);
                    int i = 0;
                    while (path[i] != STAGING_COMPLETE_FOLDER) i++;

                    string destination = string.Join(Path.DirectorySeparatorChar.ToString(), path, i + 1, path.Length - (i + 1));

                    string dirPart = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(dirPart))
                        Directory.CreateDirectory(dirPart);

                    if (SafelyMove(file, destination, 200, 5, allowDefiniteMove))
                    {
                        Log(@"{0} => {1}: OK", file, destination);
                        ConfigManagerCompact.Configuration[@"h_" + destination] = getMd5(destination);
                    }
                    else
                    {
                        Log(@"{0} => {1}: FAIL", file, destination);
                        return false;
                    }
                }
            }
            catch
            {
                Log($@"Failed on {folder}");
                return false;
            }

            return true;
        }

        public static bool Cleanup(int waitTime = 5000, bool cleanPending = true)
        {
            Log(@"Running cleanup..");
            const int sleep_period = 100;

            //in case the user has had an issue with the inline updater, this will ensure they can still run osu! after running osume
            //using the old update method.
            int attempts = Math.Max(2, waitTime / sleep_period);

            while (attempts-- > 0)
            {
                bool couldClean = true;

                if (cleanPending)
                {
                    try
                    {
                        if (File.Exists(STAGING_COMPLETE_FOLDER))
                            File.Delete(STAGING_COMPLETE_FOLDER);
                    }
                    catch
                    {
                        Log(@"Failed to cleanup STAGING_COMPLETE");
                        couldClean = false;
                    }

                    try
                    {
                        if (Directory.Exists(STAGING_COMPLETE_FOLDER))
                            Directory.Delete(STAGING_COMPLETE_FOLDER, true);
                    }
                    catch
                    {
                        Log(@"Failed to cleanup STAGING_COMPLETE");
                        couldClean = false;
                    }
                }

                try
                {
                    if (File.Exists(STAGING_FOLDER))
                        File.Delete(STAGING_FOLDER);
                }
                catch
                {
                    Log(@"Failed to cleanup STAGING");
                    couldClean = false;
                }

                try
                {
                    if (Directory.Exists(STAGING_FOLDER))
                        Directory.Delete(STAGING_FOLDER, true);
                }
                catch
                {
                    Log(@"Failed to cleanup STAGING");
                    couldClean = false;
                }

                if (couldClean)
                {
                    Log(@"Cleanup successful!");
                    return true;
                }

                Thread.Sleep(sleep_period);
            }

            return false;
        }

        public static void Reset(bool abortThread = true)
        {
            if (abortThread)
            {
                Thread t = updateThread;
                if (t != null && t.IsAlive)
                {
                    t.Abort();
                    while (t.IsAlive) Thread.Sleep(20);
                }

                updateThread = null;
            }

            lock (ActiveFiles) ActiveFiles.Clear();
            totalUpdatableFiles = 0;

            Cleanup();
            ResetError();

            Log(@"Resetting update process");
        }

        public static void ResetError()
        {
            LastError = null;
            LastErrorExtraInformation = string.Empty;
        }
    }

    public delegate void UpdateCompleteDelegate(UpdateStates state);

    public enum UpdateStates
    {
        NoUpdate,
        Checking,
        Updating,
        Error,
        NeedsRestart,
        Completed,
        EmergencyFallback
    }

    public enum ReleaseStream
    {
        Lazer,
        //Stable40,
        //Beta40,
        //Stable
    }
}
