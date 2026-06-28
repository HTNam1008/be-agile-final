using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;

namespace Moe.StudentFinance.Persistence;

public sealed class SqlDistributedLock(MoeDbContext dbContext) : IDistributedLock
{
    public async Task<bool> TryAcquireAsync(string lockKey, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        // For InMemory database testing fallback
        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory") return InMemoryLock.TryAcquire(lockKey);

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var command = connection.CreateCommand();
        command.CommandText = "sp_getapplock";
        command.CommandType = CommandType.StoredProcedure;

        var resourceParam = command.CreateParameter();
        resourceParam.ParameterName = "@Resource";
        resourceParam.Value = lockKey;
        command.Parameters.Add(resourceParam);

        var modeParam = command.CreateParameter();
        modeParam.ParameterName = "@LockMode";
        modeParam.Value = "Exclusive";
        command.Parameters.Add(modeParam);

        var ownerParam = command.CreateParameter();
        ownerParam.ParameterName = "@LockOwner";
        ownerParam.Value = "Session";
        command.Parameters.Add(ownerParam);

        var timeoutParam = command.CreateParameter();
        timeoutParam.ParameterName = "@LockTimeout";
        timeoutParam.Value = (int)timeout.TotalMilliseconds;
        command.Parameters.Add(timeoutParam);

        var returnParam = command.CreateParameter();
        returnParam.Direction = ParameterDirection.ReturnValue;
        command.Parameters.Add(returnParam);

        await command.ExecuteNonQueryAsync(cancellationToken);

        int result = (int)returnParam.Value!;
        return result >= 0; // 0 or 1 indicates success
    }

    public async Task ReleaseAsync(string lockKey)
    {
        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            InMemoryLock.Release(lockKey);
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            return; // Session died, lock auto-released
        }

        using var command = connection.CreateCommand();
        command.CommandText = "sp_releaseapplock";
        command.CommandType = CommandType.StoredProcedure;

        var resourceParam = command.CreateParameter();
        resourceParam.ParameterName = "@Resource";
        resourceParam.Value = lockKey;
        command.Parameters.Add(resourceParam);

        var ownerParam = command.CreateParameter();
        ownerParam.ParameterName = "@LockOwner";
        ownerParam.Value = "Session";
        command.Parameters.Add(ownerParam);

        await command.ExecuteNonQueryAsync();
    }
}

internal static class InMemoryLock
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> Locks = new();
    public static bool TryAcquire(string key) => Locks.TryAdd(key, true);
    public static void Release(string key) => Locks.TryRemove(key, out _);
}
