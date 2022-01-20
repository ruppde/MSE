using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace SampleExchangeApi.Console.SampleDownload;

public interface ISampleStorageHandler
{
    long GetFileSizeForSha256(string sha256);
    Task<FileStreamResult> GetAsync(string sha256, string partner, CancellationToken token = default);
    Task WriteAsync(string sha256, Stream stream, CancellationToken token = default);
}
