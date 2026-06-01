using Microsoft.EntityFrameworkCore;
using Planara.Files.Data;

namespace Planara.Files.Tests;

public static class DbTestUtils
{
    public static async Task ResetFilesDbAsync(
        DataContext db,
        CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            @"TRUNCATE TABLE ""FilesMetadata"" RESTART IDENTITY CASCADE;",
            cancellationToken);
    }
}