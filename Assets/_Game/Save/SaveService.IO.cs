using System;
using System.IO;
using System.Text;
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed partial class SaveService
    {
        private void AtomicWriteRunSave(string json, string path, string tempPath, string backupPath)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs, utf8NoBom))
            {
                writer.Write(json);
                writer.Flush();
                fs.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
                File.Copy(path, backupPath, overwrite: true);
            }
        }

        private SaveResult TryReadRunFile(string primaryPath, string backupPath, bool allowBackup, out RunSaveFile file, out string sourcePath)
        {
            file = null;
            sourcePath = primaryPath;

            if (!File.Exists(primaryPath))
            {
                if (allowBackup && File.Exists(backupPath))
                {
                    sourcePath = backupPath;
                }
                else
                {
                    return new SaveResult(SaveResultCode.NotFound, "No run save");
                }
            }

            try
            {
                var json = File.ReadAllText(sourcePath);
                file = JsonUtility.FromJson<RunSaveFile>(json);
                if (file == null)
                {
                    if (allowBackup && sourcePath != backupPath && File.Exists(backupPath))
                    {
                        sourcePath = backupPath;
                        json = File.ReadAllText(sourcePath);
                        file = JsonUtility.FromJson<RunSaveFile>(json);
                    }
                }

                if (file == null)
                    return new SaveResult(SaveResultCode.Failed, "Invalid json. Retry or load backup.");

                return new SaveResult(SaveResultCode.Ok, "Loaded run file");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SaveLoad] TryReadRunFile failed: " + ex);
                if (allowBackup && sourcePath != backupPath && File.Exists(backupPath))
                {
                    try
                    {
                        sourcePath = backupPath;
                        var json = File.ReadAllText(sourcePath);
                        file = JsonUtility.FromJson<RunSaveFile>(json);
                        if (file != null)
                            return new SaveResult(SaveResultCode.Ok, "Loaded backup run file");
                    }
                    catch (Exception backupEx)
                    {
                        Debug.LogError("[SaveLoad] Backup read also failed: " + backupEx);
                    }
                }

                return new SaveResult(SaveResultCode.Failed, ex.Message);
            }
        }
    }
}
