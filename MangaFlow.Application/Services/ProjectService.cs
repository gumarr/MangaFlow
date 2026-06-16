using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IProjectRepository projectRepository, ILogger<ProjectService> _logger)
    {
        _projectRepository = projectRepository;
        this._logger = _logger;
    }

    public async Task<Project?> GetProjectAsync(Guid id)
    {
        _logger.LogInformation("Retrieving project with ID: {ProjectId}", id);
        try
        {
            return await _projectRepository.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve project with ID: {ProjectId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Project>> GetAllProjectsAsync()
    {
        _logger.LogInformation("Retrieving all projects");
        try
        {
            return await _projectRepository.GetAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve all projects");
            throw;
        }
    }

    public async Task<Project> CreateProjectAsync(string name, string description, string sourceLanguage, string targetLanguage, string folderPath)
    {
        _logger.LogInformation("Creating new project with name: {ProjectName}", name);
        try
        {
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                FolderPath = folderPath,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _projectRepository.AddAsync(project);
            _logger.LogInformation("Successfully created project: {ProjectId}", project.Id);
            return project;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create project: {ProjectName}", name);
            throw;
        }
    }

    public async Task UpdateProjectAsync(Project project)
    {
        _logger.LogInformation("Updating project with ID: {ProjectId}", project.Id);
        try
        {
            project.UpdatedAt = DateTime.UtcNow;
            await _projectRepository.UpdateAsync(project);
            _logger.LogInformation("Successfully updated project: {ProjectId}", project.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update project: {ProjectId}", project.Id);
            throw;
        }
    }

    public async Task DeleteProjectAsync(Guid id)
    {
        _logger.LogInformation("Deleting project with ID: {ProjectId}", id);
        try
        {
            await _projectRepository.DeleteAsync(id);
            _logger.LogInformation("Successfully deleted project: {ProjectId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project with ID: {ProjectId}", id);
            throw;
        }
    }
}
