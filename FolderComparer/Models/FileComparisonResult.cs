namespace FolderComparer.Models;

public enum ComparisonStatus
{
    Match,
    SourceOnly,
    DestinationOnly,
    SizeMismatch,
    DateMismatch,
    SizeAndDateMismatch
}

public class FileComparisonResult
{
    public required string RelativePath { get; init; }
    public ComparisonStatus Status { get; init; }
    public long? SourceSize { get; init; }
    public long? DestinationSize { get; init; }
    public DateTime? SourceLastModified { get; init; }
    public DateTime? DestinationLastModified { get; init; }

    public string StatusDescription => Status switch
    {
        ComparisonStatus.Match => "Match",
        ComparisonStatus.SourceOnly => "Source only",
        ComparisonStatus.DestinationOnly => "Destination only",
        ComparisonStatus.SizeMismatch => "Size differs",
        ComparisonStatus.DateMismatch => "Date differs",
        ComparisonStatus.SizeAndDateMismatch => "Size & date differ",
        _ => "Unknown"
    };
}

public class ComparisonSummary
{
    public int TotalFiles { get; set; }
    public int MatchCount { get; set; }
    public int SourceOnlyCount { get; set; }
    public int DestinationOnlyCount { get; set; }
    public int DifferentCount { get; set; }
}
