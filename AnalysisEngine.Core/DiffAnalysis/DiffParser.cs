using System.Text.RegularExpressions;

namespace AnalysisEngine.Core.DiffAnalysis;

/// <summary>
/// Parses unified diff (git diff) output into structured file-level changes.
/// </summary>
public class DiffParser
{
    public record DiffFile(
        string FilePath,
        List<string> AddedLines,
        List<string> RemovedLines,
        List<DiffHunk> Hunks
    );

    public record DiffHunk(int OldStart, int NewStart, List<string> Lines);

    private static readonly Regex DiffHeaderRegex = new(@"^diff --git a/(.+?) b/(.+)$", RegexOptions.Compiled);
    private static readonly Regex HunkHeaderRegex = new(@"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.Compiled);

    /// <summary>
    /// Parse a full unified diff string into a list of DiffFile records.
    /// </summary>
    public List<DiffFile> Parse(string diffOutput)
    {
        var files = new List<DiffFile>();
        if (string.IsNullOrWhiteSpace(diffOutput))
            return files;

        var lines = diffOutput.Split('\n');
        DiffFile? currentFile = null;
        DiffHunk? currentHunk = null;

        foreach (var line in lines)
        {
            var headerMatch = DiffHeaderRegex.Match(line);
            if (headerMatch.Success)
            {
                if (currentFile is not null)
                    files.Add(currentFile);

                currentFile = new DiffFile(
                    headerMatch.Groups[2].Value,
                    new List<string>(),
                    new List<string>(),
                    new List<DiffHunk>()
                );
                currentHunk = null;
                continue;
            }

            if (currentFile is null)
                continue;

            // Skip --- and +++ lines
            if (line.StartsWith("---") || line.StartsWith("+++"))
                continue;

            // Skip index, mode lines
            if (line.StartsWith("index ") || line.StartsWith("new file") ||
                line.StartsWith("deleted file") || line.StartsWith("old mode") ||
                line.StartsWith("new mode") || line.StartsWith("similarity") ||
                line.StartsWith("rename") || line.StartsWith("Binary"))
            {
                // new file / deleted file cases are handled by ChangeClassifier
                continue;
            }

            var hunkMatch = HunkHeaderRegex.Match(line);
            if (hunkMatch.Success)
            {
                currentHunk = new DiffHunk(
                    int.Parse(hunkMatch.Groups[1].Value),
                    int.Parse(hunkMatch.Groups[2].Value),
                    new List<string>()
                );
                currentFile.Hunks.Add(currentHunk);
                continue;
            }

            if (currentHunk is null)
                continue;

            currentHunk.Lines.Add(line);

            if (line.StartsWith('+') && !line.StartsWith("+++"))
                currentFile.AddedLines.Add(line[1..].TrimStart());
            else if (line.StartsWith('-') && !line.StartsWith("---"))
                currentFile.RemovedLines.Add(line[1..].TrimStart());
        }

        if (currentFile is not null)
            files.Add(currentFile);

        return files;
    }
}
