using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Interfaces;

public interface IProjectService
{
    Task<Project?> GetProjectAsync(Guid id);
    Task<IEnumerable<Project>> GetAllProjectsAsync();
    Task<Project> CreateProjectAsync(string name, string description, string sourceLanguage, string targetLanguage, string folderPath);
    Task UpdateProjectAsync(Project project);
    Task DeleteProjectAsync(Guid id);
}
