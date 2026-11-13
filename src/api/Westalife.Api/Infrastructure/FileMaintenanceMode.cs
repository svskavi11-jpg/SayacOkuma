using System.Globalization;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Api.Infrastructure;

/// <summary>
/// Bakım bayrağı <c>maintenance.on</c> dosyasıdır. Üretimde bu dosya log volume'undadır;
/// konteyner silinse de volume durduğu için bayrak kalır.
/// </summary>
public sealed class FileMaintenanceMode : IMaintenanceMode
{
    private readonly string _path;
    private readonly object _gate = new();

    public FileMaintenanceMode(IHostEnvironment environment, IConfiguration configuration)
        : this(ResolvePath(environment, configuration))
    {
    }

    public FileMaintenanceMode(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Bakım bayrağı yolu boş olamaz.", nameof(path));
        }

        _path = path;
    }

    public bool IsEnabled
    {
        get
        {
            lock (_gate)
            {
                return File.Exists(_path);
            }
        }
    }

    public DateTimeOffset? EnabledOn
    {
        get
        {
            lock (_gate)
            {
                if (!File.Exists(_path))
                {
                    return null;
                }

                string text;
                try
                {
                    text = File.ReadAllText(_path);
                }
                catch (IOException)
                {
                    return null;
                }

                if (DateTimeOffset.TryParse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var when))
                {
                    return when;
                }

                return new DateTimeOffset(File.GetLastWriteTimeUtc(_path), TimeSpan.Zero);
            }
        }
    }

    public void Enable()
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = _path + ".partial";
            File.WriteAllText(temporary, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            File.Move(temporary, _path, overwrite: true);
        }
    }

    public void Disable()
    {
        lock (_gate)
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }

    private static string ResolvePath(IHostEnvironment environment, IConfiguration configuration)
    {
        var configured = configuration["Maintenance:FlagPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return Path.Combine(environment.ContentRootPath, "logs", "maintenance.on");
    }
}
