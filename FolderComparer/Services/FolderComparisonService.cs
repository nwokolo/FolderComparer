using System.IO;
using FolderComparer.Models;

namespace FolderComparer.Services;

public class FolderComparisonService
{
    public async Task<(List<FileComparisonResult> Results, ComparisonSummary Summary)> CompareAsync(
        string sourcePath,
        string destinationPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<FileComparisonResult>();

            progress?.Report("Scanning source folder...");
            var sourceFiles = GetRelativeFiles(sourcePath);
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report("Scanning destination folder...");
            var destFiles = GetRelativeFiles(destinationPath);
            cancellationToken.ThrowIfCancellationRequested();

            var allRelativePaths = new HashSet<string>(sourceFiles.Keys, StringComparer.OrdinalIgnoreCase);
            allRelativePaths.UnionWith(destFiles.Keys);

            var summary = new ComparisonSummary { TotalFiles = allRelativePaths.Count };
            int processed = 0;

            foreach (var relativePath in allRelativePaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool inSource = sourceFiles.TryGetValue(relativePath, out var sourceInfo);
                bool inDest = destFiles.TryGetValue(relativePath, out var destInfo);

                ComparisonStatus status;

                if (inSource && !inDest)
                {
                    status = ComparisonStatus.SourceOnly;
                    summary.SourceOnlyCount++;
                }
                else if (!inSource && inDest)
                {
                    status = ComparisonStatus.DestinationOnly;
                    summary.DestinationOnlyCount++;
                }
                else
                {
                    bool sizeDiffers = sourceInfo!.Size != destInfo!.Size;
                    bool dateDiffers = Math.Abs((sourceInfo.LastModified - destInfo.LastModified).TotalSeconds) > 2;

                    if (sizeDiffers && dateDiffers)
                    {
                        status = ComparisonStatus.SizeAndDateMismatch;
                        summary.DifferentCount++;
                    }
                    else if (sizeDiffers)
                    {
                        status = ComparisonStatus.SizeMismatch;
                        summary.DifferentCount++;
                    }
                    else if (dateDiffers)
                    {
                        status = ComparisonStatus.DateMismatch;
                        summary.DifferentCount++;
                    }
                    else
                    {
                        status = ComparisonStatus.Match;
                        summary.MatchCount++;
                    }
                }

                results.Add(new FileComparisonResult
                {
                    RelativePath = relativePath,
                    Status = status,
                    SourceSize = inSource ? sourceInfo!.Size : null,
                    DestinationSize = inDest ? destInfo!.Size : null,
                    SourceLastModified = inSource ? sourceInfo!.LastModified : null,
                    DestinationLastModified = inDest ? destInfo!.LastModified : null
                });

                processed++;
                if (processed % 100 == 0)
                {
                    progress?.Report($"Compared {processed} of {allRelativePaths.Count} files...");
                }
            }

            progress?.Report("Comparison complete.");
            return (results, summary);
        }, cancellationToken);
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

            IEnumerable<DirectoryInfo> childDirectories;
            try
            {
                childDirectories = currentDirectory.EnumerateDirectories();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                if (IsExcluded(childDirectory.Attributes))
                    continue;

                pendingDirectories.Push(childDirectory);
            }

            IEnumerable<FileInfo> currentFiles;
            try
            {
                currentFiles = currentDirectory.EnumerateFiles();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var file in currentFiles)
            {
                if (IsExcluded(file.Attributes))
                    continue;

                string relativePath = file.FullName[(rootLen + 1)..];
                files[relativePath] = new FileMetadata(file.Length, file.LastWriteTimeUtc);
            }
        }

        return files;
    }

    private static bool IsExcluded(FileAttributes attributes)
    {
        return attributes.HasFlag(FileAttributes.Hidden) ||
               attributes.HasFlag(FileAttributes.ReadOnly);
    }

    private record FileMetadata(long Size, DateTime LastModified);
}
