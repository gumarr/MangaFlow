using System.Threading.Tasks;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Persistence;

public interface IAppSettingsRepository
{
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}
