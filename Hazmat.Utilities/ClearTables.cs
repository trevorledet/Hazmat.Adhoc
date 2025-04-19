using System;
using System.Runtime.CompilerServices;
using Hazmat.Common.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Hazmat.Utilities;

public class ClearTables<T>
{
    private readonly IBaseRepository<T> _repository;
    private readonly int _pauseDelay;
    private readonly int _pauseInterval;

    public ClearTables(IBaseRepository<T> repository, IConfiguration configuration)
    {
        _repository = repository;
        _pauseDelay = configuration.GetValue<int>("HazmatImporter:PauseDelay");
        _pauseInterval = configuration.GetValue<int>("HazmatImporter:PauseInterval");
    }

    public async Task Clear()
    {
        int cnt = 0;

        IEnumerable<T> items = await _repository.GetAllAsync();
        foreach (var item in items)
        {
            await _repository.DeleteAsync(item);
            cnt++;
            if (cnt % _pauseInterval == 0)
            {
                Console.WriteLine($"Pausing at {cnt} for {_pauseDelay} milliseconds");
                await Task.Delay(_pauseDelay);
            }
        }
    }
}
