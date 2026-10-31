using TinyHex.Models;

namespace TinyHex.Services;

public interface ISettingsService
{
    AppSettings? Load();
    void Save(AppSettings settings);
}
