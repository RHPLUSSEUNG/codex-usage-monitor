using System.Diagnostics;
using System.IO.Compression;

namespace CodexUsageMonitor.Services;

internal sealed record PostUpdateSession(string TransactionId, string HealthFilePath);

internal static class UpdateInstaller
{
    private const string UpdateArgument = "--apply-update";
    private const string PostUpdateArgument = "--post-update";

    public static string DownloadsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexUsageMonitor",
            "Updates");

    public static bool IsUpdateMode(string[] args) =>
        args.Any(argument => string.Equals(
            argument,
            UpdateArgument,
            StringComparison.OrdinalIgnoreCase));

    public static int Run(string[] args)
    {
        try
        {
            Dictionary<string, string> arguments = ParseArguments(args);
            int parentPid = int.Parse(Required(arguments, "--parent-pid"));
            string packagePath = Path.GetFullPath(Required(arguments, "--package"));
            string targetExecutable = Path.GetFullPath(Required(arguments, "--target"));
            string transactionId = Required(arguments, "--transaction-id");
            if (!Guid.TryParseExact(transactionId, "N", out _))
                throw new ArgumentException("The update transaction id is invalid.");
            ValidateTarget(targetExecutable);
            WaitForParent(parentPid);
            Apply(packagePath, targetExecutable, transactionId);
            return 0;
        }
        catch (Exception exception)
        {
            WriteError(exception);
            return 1;
        }
    }

    public static void CleanupDownloads()
    {
        try
        {
            if (!Directory.Exists(DownloadsDirectory))
                return;
            foreach (string directory in Directory.EnumerateDirectories(DownloadsDirectory))
            {
                try
                {
                    if (Directory.GetCreationTimeUtc(directory) < DateTime.UtcNow.AddDays(-1))
                        Directory.Delete(directory, recursive: true);
                }
                catch
                {
                    // A recently running updater may still own files in this directory.
                }
            }
        }
        catch
        {
            // Update cleanup must never prevent the app from starting.
        }
    }

    public static bool TryGetPostUpdate(string[] args, out PostUpdateSession? session)
    {
        session = null;
        int marker = Array.FindIndex(
            args,
            argument => string.Equals(
                argument,
                PostUpdateArgument,
                StringComparison.OrdinalIgnoreCase));
        if (marker < 0 || marker + 2 >= args.Length)
            return false;

        string transactionId = args[marker + 1];
        string healthFilePath = Path.GetFullPath(args[marker + 2]);
        if (!Guid.TryParseExact(transactionId, "N", out _)
            || !IsPathInside(healthFilePath, DownloadsDirectory))
            return false;

        session = new PostUpdateSession(transactionId, healthFilePath);
        return true;
    }

    public static void SignalHealthy(PostUpdateSession session)
    {
        if (!IsPathInside(session.HealthFilePath, DownloadsDirectory))
            throw new InvalidOperationException("The update health path is invalid.");

        string temporaryPath = session.HealthFilePath + ".tmp";
        File.WriteAllText(temporaryPath, session.TransactionId);
        File.Move(temporaryPath, session.HealthFilePath, overwrite: true);
    }

    internal static void Apply(
        string packagePath,
        string targetExecutable,
        string transactionId,
        TimeSpan? startupTimeout = null,
        TimeSpan? survivalGrace = null)
    {
        if (!File.Exists(packagePath))
            throw new FileNotFoundException("The update package was not found.", packagePath);

        string updateDirectory = Path.GetDirectoryName(packagePath)!;
        string stagingDirectory = Path.Combine(updateDirectory, "staging");
        if (Directory.Exists(stagingDirectory))
            Directory.Delete(stagingDirectory, recursive: true);
        ZipFile.ExtractToDirectory(packagePath, stagingDirectory);

        string[] payloadExecutables = Directory
            .EnumerateFiles(stagingDirectory, "CodexUsageMonitor.exe", SearchOption.AllDirectories)
            .ToArray();
        if (payloadExecutables.Length != 1)
            throw new InvalidDataException(
                "The update package must contain exactly one CodexUsageMonitor.exe.");

        string payloadDirectory = Path.GetDirectoryName(payloadExecutables[0])!;
        string targetDirectory = Path.GetDirectoryName(targetExecutable)!;
        string backupDirectory = targetDirectory + ".backup-" + Guid.NewGuid().ToString("N");
        string healthFilePath = Path.Combine(
            updateDirectory,
            $"healthy-{transactionId}.signal");
        bool backupCreated = false;
        Process? updatedProcess = null;

        try
        {
            if (Directory.Exists(targetDirectory))
            {
                Directory.Move(targetDirectory, backupDirectory);
                backupCreated = true;
            }

            CopyDirectory(payloadDirectory, targetDirectory);
            if (!File.Exists(targetExecutable))
                throw new InvalidDataException("The installed executable is missing after the update.");

            TryDelete(healthFilePath);
            var startInfo = new ProcessStartInfo(targetExecutable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = targetDirectory
            };
            startInfo.ArgumentList.Add(PostUpdateArgument);
            startInfo.ArgumentList.Add(transactionId);
            startInfo.ArgumentList.Add(healthFilePath);
            updatedProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The updated application could not be started.");
            WaitForHealthyStartup(
                updatedProcess,
                healthFilePath,
                transactionId,
                startupTimeout,
                survivalGrace);
            updatedProcess.Dispose();
            updatedProcess = null;

            if (backupCreated)
            {
                try
                {
                    Directory.Delete(backupDirectory, recursive: true);
                }
                catch
                {
                    // A backup left behind is safer than failing a successful update.
                }
            }
        }
        catch
        {
            try
            {
                StopProcess(updatedProcess);
                if (Directory.Exists(targetDirectory))
                    Directory.Delete(targetDirectory, recursive: true);
                if (backupCreated && Directory.Exists(backupDirectory))
                    Directory.Move(backupDirectory, targetDirectory);
                if (File.Exists(targetExecutable))
                    Process.Start(new ProcessStartInfo(targetExecutable) { UseShellExecute = true });
            }
            catch
            {
                // Preserve the original failure in the update log.
            }
            throw;
        }
        finally
        {
            updatedProcess?.Dispose();
            TryDelete(healthFilePath);
        }
    }

    internal static void WaitForHealthyStartup(
        Process process,
        string healthFilePath,
        string transactionId,
        TimeSpan? startupTimeout = null,
        TimeSpan? survivalGrace = null)
    {
        TimeSpan timeoutLimit = startupTimeout ?? TimeSpan.FromSeconds(60);
        TimeSpan gracePeriod = survivalGrace ?? TimeSpan.FromSeconds(3);
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < timeoutLimit)
        {
            if (process.HasExited)
                throw new InvalidOperationException(
                    $"The updated application exited with code {process.ExitCode} before becoming healthy.");

            if (File.Exists(healthFilePath))
            {
                string signal = File.ReadAllText(healthFilePath).Trim();
                if (string.Equals(signal, transactionId, StringComparison.Ordinal))
                {
                    Thread.Sleep(gracePeriod);
                    if (process.HasExited)
                        throw new InvalidOperationException(
                            $"The updated application exited with code {process.ExitCode} after startup.");
                    return;
                }
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("The updated application did not report a healthy startup in time.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string destinationFile = Path.Combine(
                destination,
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }

    private static void WaitForParent(int processId)
    {
        try
        {
            using Process parent = Process.GetProcessById(processId);
            if (!parent.WaitForExit((int)TimeSpan.FromMinutes(1).TotalMilliseconds))
                throw new TimeoutException("The running application did not exit in time.");
        }
        catch (ArgumentException)
        {
            // The parent already exited.
        }
    }

    private static void StopProcess(Process? process)
    {
        if (process is null)
            return;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)TimeSpan.FromSeconds(10).TotalMilliseconds);
            }
        }
        catch
        {
            // Rollback continues even if the failed child is already gone.
        }
    }

    private static void ValidateTarget(string targetExecutable)
    {
        string expected = Path.GetFullPath(UpdateService.InstalledExecutablePath);
        if (!string.Equals(targetExecutable, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The update target is not the installed application.");
    }

    private static bool IsPathInside(string path, string directory)
    {
        string fullPath = Path.GetFullPath(path);
        string fullDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], UpdateArgument, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
                throw new ArgumentException("The update installer arguments are invalid.");
            result[args[index]] = args[++index];
        }
        return result;
    }

    private static string Required(Dictionary<string, string> arguments, string name) =>
        arguments.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"The required argument '{name}' is missing.");

    private static void WriteError(Exception exception)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexUsageMonitor");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "update-error.log"),
                $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}");
        }
        catch
        {
            // There is nowhere else to report errors from the background updater.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Health signals are temporary and can be cleaned on a later startup.
        }
    }
}
