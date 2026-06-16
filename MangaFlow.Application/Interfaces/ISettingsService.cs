using System.Threading.Tasks;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Interfaces;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync();
    Task UpdateSettingsAsync(AppSettings settings);
}
