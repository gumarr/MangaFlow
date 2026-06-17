using System.Threading.Tasks;
using MangaFlow.Application.DTOs;

namespace MangaFlow.Application.Interfaces;

public interface IOcrBenchmarkService
{
    Task<BenchmarkResult> RunBenchmarkAsync(OcrProvider provider, string datasetFolder);
}
