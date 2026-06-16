using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Persistence;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id);
    Task<IEnumerable<Project>> GetAllAsync();
    Task AddAsync(Project project);
    Task UpdateAsync(Project project);
    Task DeleteAsync(Guid id);
}
