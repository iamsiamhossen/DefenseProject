using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KYAUCPChapter.Infrastructure.Persistence;

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => ToDatabase(value),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
{
    private static DateTime ToDatabase(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new InvalidOperationException("Only UTC timestamps can be stored.");
}
