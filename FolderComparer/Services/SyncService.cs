using System.IO;
using FolderComparer.Models;

namespace FolderComparer.Services;

public class SyncService
{
    public async Task<SyncResult> SyncAsync(
        string sourcePath,
        string destinationPath,
        SyncDirection direction,
        bool overwriteMismatches,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var result = new SyncResult();
            var sourceDir = new DirectoryInfo(sourcePath);
            var destDir = new DirectoryInfo(destinationPath);

            if (!sourceDir.Exists || !destDir.Exists)
                throw new DirectoryNotFoundException("One or both folders do not exist.");

            progress?.Report(new SyncProgress { Status = "Scanning folders..." });

            // Handle bidirectional sync
            if (direction == SyncDirection.Bidirectional)
            {
                PerformSync(sourcePath, destinationPath, SyncDirection.SourceToDestination, overwriteMismatches, progress, cancellationToken, result);
                PerformSync(destinationPath, sourcePath, SyncDirection.DestinationToSource, overwriteMismatches, progress, cancellationToken, result);
            }
            else
            {
                PerformSync(sourcePath, destinationPath, direction, overwriteMismatches, progress, cancellationToken, result);
            }

            progress?.Report(new SyncProgress { Status = "Sync complete." });
            return result;
        }, cancellationToken);
    }

    private void PerformSync(
        string sourcePath,
        string destinationPath,
        SyncDirection direction,
        bool overwriteMismatches,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken,
        SyncResult result)
    {
        var sourceFiles = GetRelativeFiles(sourcePath);
        var destFiles = GetRelativeFiles(destinationPath);

        int processed = 0;
        int total = sourceFiles.Count;

        foreach (var (relativePath, sourceInfo) in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool existsInDest = destFiles.TryGetValue(relativePath, out var destInfo);

            try
            {
                if (!existsInDest)
                {
                    // File only in source, copy it
                    CopyFile(sourcePath, destinationPath, relativePath);
                    progress?.Report(new SyncProgress { Status = $"Copied: {relativePath}" });
                    result.FilesCopied++;
                }
                else if (overwriteMismatches && destInfo != null)
                {
                    // File exists in both, check if they differ
                    bool sizeDiffers = sourceInfo.Size != destInfo.Size;
                    bool dateDiffers = Math.Abs((sourceInfo.LastModified - destInfo.LastModified).TotalSeconds) > 2;

                    if (sizeDiffers || dateDiffers)
                    {
                        CopyFile(sourcePath, destinationPath, relativePath);
                        progress?.Report(new SyncProgress { Status = $"Updated: {relativePath}" });
                        result.FilesUpdated++;
                    }
                }

                processed++;
                if (processed % 10 == 0)
                {
                    progress?.Report(new SyncProgress { Status = $"Processing... {processed} of {total}" });
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(new SyncError { FilePath = relativePath, Message = ex.Message });
            }
        }
    }

    private static void CopyFile(string sourcePath, string destinationPath, string relativePath)
    {
        var sourceFile = Path.Combine(sourcePath, relativePath);
        var destFile = Path.Combine(destinationPath, relativePath);

        // Create destination directories if needed
        var destDirectory = Path.GetDirectoryName(destFile);
        if (!Directory.Exists(destDirectory))
        {
            Directory.CreateDirectory(destDirectory!);
        }

        // Copy file with overwrite
        File.Copy(sourceFile, destFile, overwrite: true);
    }

    private static Dictionary<string, FileMetadata> GetRelativeFiles(string rootPath)
    {
        var dir = new DirectoryInfo(rootPath);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Folder not found: {rootPath}");

        var files = new Dictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase);
        int rootLen = dir.FullName.Length;

        var pendingDirectories = new Stack<DirectoryInfo>();
        pendingDirectories.Push(dir);

        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();

            DirectoryInfo[] childDirectories;
            try
            {
                childDirectories = currentDirectory.GetDirectories();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                if (IsExcludedPath(childDirectory.FullName))
                    continue;

                FileAttributes attributes;
                try
                {
                    attributes = childDirectory.Attributes;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                if (IsExcluded(attributes))
                    continue;

                pendingDirectories.Push(childDirectory);
            }

            FileInfo[] currentFiles;
            try
            {
                currentFiles = currentDirectory.GetFiles();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var file in currentFiles)
            {
                if (IsExcludedPath(file.FullName))
                    continue;

                FileAttributes attributes;
                try
                {
                    attributes = file.Attributes;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                if (IsExcluded(attributes))
                    continue;

                int separatorIndex = file.FullName.Length > rootLen && file.FullName[rootLen] == Path.DirectorySeparatorChar
                    ? rootLen + 1
                    : rootLen;

                string relativePath = file.FullName[separatorIndex..];
                files[relativePath] = new FileMetadata
                {
                    Size = file.Length,
                    LastModified = file.LastWriteTimeUtc
                };
            }
        }

        return files;
    }

    private static bool IsExcluded(FileAttributes attributes)
    {
        return (attributes & FileAttributes.Hidden) != 0 ||
               (attributes & FileAttributes.System) != 0 ||
               (attributes & FileAttributes.ReadOnly) != 0;
    }

    private static bool IsExcludedPath(string fullPath)
    {
        return fullPath.Contains("\\System Volume Information", StringComparison.OrdinalIgnoreCase) ||
               fullPath.Contains("\\$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase);
    }

    private class FileMetadata
    {
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }
}

public enum SyncDirection
{
    SourceToDestination,
    DestinationToSource,
    Bidirectional
}

public class SyncProgress
{
    public string Status { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Processed { get; set; }
}

public class SyncResult
{
    public int FilesCopied { get; set; }
    public int FilesUpdated { get; set; }
    public List<SyncError> Errors { get; set; } = [];

    public string Summary => $"Copied: {FilesCopied}  |  Updated: {FilesUpdated}  |  Errors: {Errors.Count}";
}

public class SyncError
{
    public string FilePath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
