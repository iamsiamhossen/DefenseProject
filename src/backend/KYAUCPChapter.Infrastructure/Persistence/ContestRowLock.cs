using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KYAUCPChapter.Infrastructure.Persistence;

internal static class ContestRowLock
{
    public static async Task<IDbContextTransaction> BeginAsync(AppDbContext db, Guid contestId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT `Id` FROM `Contests` WHERE `Id` = {contestId} FOR UPDATE", cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
