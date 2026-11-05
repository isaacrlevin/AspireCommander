using System.Text;
using System.Diagnostics;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;
if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    Console.WriteLine("Open this resource's Terminal view in the Aspire dashboard.");
    return;
}

new CommanderApp().Run();

internal sealed class CommanderApp
{
    private readonly Pane[] _panes;
    private int _active;
    private string _message = "Tab switches panes • Enter opens • Backspace goes up • Q quits";

    public CommanderApp()
    {
        var container = Environment.GetEnvironmentVariable("ASPIRE_COMMANDER_CONTAINER");
        IFileSystem fileSystem = string.IsNullOrWhiteSpace(container)
            ? new LocalFileSystem()
            : new ContainerFileSystem(container);
        var initialPath = Environment.GetEnvironmentVariable("ASPIRE_COMMANDER_PATH") ?? "/";
        _panes = [new(fileSystem, initialPath), new(fileSystem, initialPath == "/" ? "/app" : initialPath)];
    }

    public void Run()
    {
        Console.Write("\e[?1049h\e[?25l");
        try
        {
            while (true)
            {
                Draw();
                var key = Console.ReadKey(true);
                if (key.Key is ConsoleKey.Q or ConsoleKey.Escape) break;
                Handle(key.Key);
            }
        }
        finally
        {
            Console.Write("\e[0m\e[?25h\e[?1049l");
        }
    }

    private void Handle(ConsoleKey key)
    {
        var pane = _panes[_active];
        switch (key)
        {
            case ConsoleKey.Tab:
                _active = 1 - _active;
                _message = "Switched pane";
                break;
            case ConsoleKey.UpArrow: pane.Move(-1); break;
            case ConsoleKey.DownArrow: pane.Move(1); break;
            case ConsoleKey.PageUp: pane.Move(-Math.Max(1, Console.WindowHeight - 8)); break;
            case ConsoleKey.PageDown: pane.Move(Math.Max(1, Console.WindowHeight - 8)); break;
            case ConsoleKey.Home: pane.SelectFirst(); break;
            case ConsoleKey.End: pane.SelectLast(); break;
            case ConsoleKey.Backspace:
                pane.GoUp();
                _message = pane.Error ?? pane.Path;
                break;
            case ConsoleKey.Enter: _message = pane.OpenSelected(); break;
            case ConsoleKey.R:
                pane.Refresh();
                _message = "Directory refreshed";
                break;
        }
    }

    private void Draw()
    {
        var width = Math.Max(40, Console.WindowWidth);
        var rows = Math.Max(6, Console.WindowHeight - 6);
        var leftWidth = (width - 1) / 2;
        Console.Write("\e[H\e[44;97m");
        WriteLine(Center(" Aspire Commander — container file browser ", width), width);
        Console.Write("\e[40;37m");
        var left = _panes[0].Render(leftWidth, rows, _active == 0);
        var right = _panes[1].Render(width - leftWidth - 1, rows, _active == 1);
        for (var i = 0; i < left.Count; i++)
            Console.Write(left[i] + "\e[36m│\e[0m" + right[i] + "\e[K\n");
        Console.Write("\e[44;97m");
        WriteLine(" " + _message, width);
        Console.Write("\e[30;47m");
        WriteLine(" ↑↓ Move   Enter Open   Backspace Up   Tab Other pane   R Refresh   Q Quit", width);
        Console.Write("\e[0m");
    }

    private static void WriteLine(string value, int width) => Console.Write(Fit(value, width) + "\e[K\n");

    private static string Center(string value, int width) =>
        new string(' ', Math.Max(0, (width - value.Length) / 2)) + value;

    internal static string Fit(string value, int width) =>
        value.Length > width ? value[..Math.Max(0, width - 1)] + "…" : value.PadRight(width);
}

internal sealed class Pane
{
    private readonly IFileSystem _fileSystem;
    private List<Entry> _entries = [];
    private int _selected, _offset;

    public Pane(IFileSystem fileSystem, string path)
    {
        _fileSystem = fileSystem;
        Path = fileSystem.DirectoryExists(path) ? path : "/";
        Refresh();
    }

    public string Path { get; private set; }
    public string? Error { get; private set; }

    public void Refresh()
    {
        Error = null;
        try
        {
            _entries = _fileSystem.Enumerate(Path)
                .OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
            _selected = Math.Clamp(_selected, 0, Math.Max(0, _entries.Count - 1));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _entries = [];
            Error = ex.Message;
        }
    }

    public void Move(int amount) => _selected = Math.Clamp(_selected + amount, 0, Math.Max(0, _entries.Count - 1));
    public void SelectFirst() => _selected = 0;
    public void SelectLast() => _selected = Math.Max(0, _entries.Count - 1);

    public void GoUp()
    {
        var parent = _fileSystem.GetParent(Path);
        if (parent is null) return;
        Path = parent;
        _selected = _offset = 0;
        Refresh();
    }

    public string OpenSelected()
    {
        if (_entries.Count == 0) return Error ?? "Directory is empty";
        var entry = _entries[_selected];
        if (entry.IsDirectory)
        {
            Path = entry.FullPath;
            _selected = _offset = 0;
            Refresh();
            return Error ?? Path;
        }

        try
        {
            var line = _fileSystem.ReadFirstLine(entry.FullPath);
            return line is null ? $"{entry.Name} is empty" : $"{entry.Name}: {Sanitize(line)}";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return ex.Message;
        }
    }

    public List<string> Render(int width, int rows, bool active)
    {
        var result = new List<string>(rows + 2);
        var border = active ? "\e[93m" : "\e[36m";
        result.Add(border + CommanderApp.Fit("┌ " + Path + " ", width).Replace(' ', '─') + "\e[0m");
        if (_selected < _offset) _offset = _selected;
        if (_selected >= _offset + rows) _offset = _selected - rows + 1;
        for (var row = 0; row < rows; row++)
        {
            var index = _offset + row;
            if (index >= _entries.Count)
            {
                var text = row == 0 && _entries.Count == 0 ? (Error is null ? "  <empty>" : "  <access denied>") : "";
                result.Add(CommanderApp.Fit(text, width));
                continue;
            }

            var entry = _entries[index];
            var line = " " + entry.Name + (entry.IsDirectory ? "/" : $"  {FormatSize(entry.Size)}");
            var fitted = CommanderApp.Fit(line, width);
            result.Add(index == _selected && active ? "\e[44;97m" + fitted + "\e[0m" :
                entry.IsDirectory ? "\e[96m" + fitted + "\e[0m" : fitted);
        }

        result.Add(border + CommanderApp.Fit($"└ {_entries.Count} items ", width).Replace(' ', '─') + "\e[0m");
        return result;
    }

    private static string FormatSize(long size) => size switch
    {
        >= 1_073_741_824 => $"{size / 1_073_741_824d:0.#}G", >= 1_048_576 => $"{size / 1_048_576d:0.#}M",
        >= 1024 => $"{size / 1024d:0.#}K", _ => $"{size}B"
    };

    private static string Sanitize(string text) =>
        new(text.Take(120).Select(c => char.IsControl(c) ? ' ' : c).ToArray());
}

internal sealed record Entry(string Name, string FullPath, bool IsDirectory, long Size);

internal interface IFileSystem
{
    bool DirectoryExists(string path);
    IEnumerable<Entry> Enumerate(string path);
    string? GetParent(string path);
    string? ReadFirstLine(string path);
}

internal sealed class LocalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<Entry> Enumerate(string path) => new DirectoryInfo(path).EnumerateFileSystemInfos()
        .Select(x => new Entry(x.Name, x.FullName, x is DirectoryInfo, x is FileInfo file ? SafeSize(file) : 0));

    public string? GetParent(string path) => Directory.GetParent(path)?.FullName;

    public string? ReadFirstLine(string path)
    {
        using var reader = new StreamReader(path);
        return reader.ReadLine();
    }

    private static long SafeSize(FileInfo file)
    {
        try
        {
            return file.Length;
        }
        catch
        {
            return 0;
        }
    }
}

internal sealed class ContainerFileSystem(string container) : IFileSystem
{
    public bool DirectoryExists(string path) => Run("test", "-d", path).ExitCode == 0;

    public IEnumerable<Entry> Enumerate(string path)
    {
        var listing = Run("find", path, "-mindepth", "1", "-maxdepth", "1", "-print0");
        EnsureSuccess(listing);
        foreach (var fullPath in listing.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var stat = Run("stat", "-c", "%F\t%s", fullPath);
            EnsureSuccess(stat);
            var fields = stat.Output.TrimEnd().Split('\t', 2);
            var isDirectory = fields[0].Contains("directory", StringComparison.OrdinalIgnoreCase);
            var size = fields.Length > 1 && long.TryParse(fields[1], out var value) ? value : 0;
            yield return new Entry(PosixName(fullPath), fullPath, isDirectory, size);
        }
    }

    public string? GetParent(string path)
    {
        if (path == "/") return null;
        var trimmed = path.TrimEnd('/');
        var separator = trimmed.LastIndexOf('/');
        return separator <= 0 ? "/" : trimmed[..separator];
    }

    public string? ReadFirstLine(string path)
    {
        var result = Run("head", "-n", "1", path);
        EnsureSuccess(result);
        return result.Output.Split('\n', 2)[0].TrimEnd('\r');
    }

    private CommandResult Run(params string[] args)
    {
        var start = new ProcessStartInfo("docker")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add(container);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Could not start the Docker CLI.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, outputTask.GetAwaiter().GetResult(),
            errorTask.GetAwaiter().GetResult());
    }

    private static void EnsureSuccess(CommandResult result)
    {
        if (result.ExitCode != 0) throw new IOException(result.Error.Trim());
    }

    private static string PosixName(string path) => path.TrimEnd('/').Split('/')[^1];

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}