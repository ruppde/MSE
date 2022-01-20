using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SampleExchangeApi.Console.Models;

namespace SampleExchangeApi.Console.Database;

public class MongoMetadataHandler : ISampleMetadataHandler, IHostedService
{
    private readonly MongoMetadataOptions _options;
    private readonly MongoClient _mongoClient;

    public MongoMetadataHandler(IOptions<MongoMetadataOptions> options)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _mongoClient = new MongoClient(options.Value.ConnectionString);
    }

    public async Task<IEnumerable<ExportSample>> GetSamplesAsync(DateTime start, DateTime? end, string sampleSet, CancellationToken token = default)
    {
        var mongoDatabase = _mongoClient.GetDatabase(_options.DatabaseName);
        var sampleCollection = mongoDatabase.GetCollection<ExportSample>(_options.CollectionName);
        var list = end == null
            ? await sampleCollection
                .FindAsync(_ => _.SampleSet == sampleSet && _.Imported >= start, cancellationToken: token)
            : await sampleCollection
                .FindAsync(_ => _.SampleSet == sampleSet && _.Imported >= start && _.Imported <= end, cancellationToken: token);
        return list.ToList();
    }

    public async Task InsertSampleAsync(ExportSample sample, CancellationToken token = default)
    {
        var mongoDatabase = _mongoClient.GetDatabase(_options.DatabaseName);
        var sampleCollection = mongoDatabase.GetCollection<ExportSample>(_options.CollectionName);
        await sampleCollection.InsertOneAsync(sample, cancellationToken: token);
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        await PrepareIndexesAsync(token);
    }

    private async Task PrepareIndexesAsync(CancellationToken token = default)
    {
        var mongoDatabase = _mongoClient.GetDatabase(_options.DatabaseName);
        var mongoCollection = mongoDatabase.GetCollection<Sample>(_options.CollectionName);

        foreach (var index in _options.Indexes)
        {
            try
            {
                await mongoCollection.Indexes.CreateOneAsync(
                        new CreateIndexModel<Sample>(Builders<Sample>.IndexKeys.Ascending(index)), cancellationToken: token)
                    .ConfigureAwait(false);
            }
            catch (MongoCommandException)
            {
                await mongoCollection.Indexes.DropOneAsync($"{index}_1", token)
                    .ConfigureAwait(false);
                await mongoCollection.Indexes.CreateOneAsync(
                        new CreateIndexModel<Sample>(Builders<Sample>.IndexKeys.Ascending(index)), cancellationToken: token)
                    .ConfigureAwait(false);
            }
        }

        try
        {
            await mongoCollection.Indexes.CreateOneAsync(
                    new CreateIndexModel<Sample>(
                        Builders<Sample>
                            .IndexKeys.Ascending(_options.TimeSpanIndex),
                        new CreateIndexOptions { ExpireAfter = _options.Duration }), cancellationToken: token)
                .ConfigureAwait(false);
        }
        catch (MongoCommandException)
        {
            await mongoCollection.Indexes.DropOneAsync($"{_options.TimeSpanIndex}_1",
                token).ConfigureAwait(false);
            await mongoCollection.Indexes.CreateOneAsync(
                    new CreateIndexModel<Sample>(
                        Builders<Sample>.IndexKeys.Ascending(_options.TimeSpanIndex),
                        new CreateIndexOptions { ExpireAfter = _options.Duration }), cancellationToken: token)
                .ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
