using Microsoft.Data.SqlClient;

namespace ModernExpenseApp.Services;

internal static class SqlDataReaderExtensions
{
    public static int GetInt32(this SqlDataReader reader, string columnName) => reader.GetInt32(reader.GetOrdinal(columnName));
    public static string GetString(this SqlDataReader reader, string columnName) => reader.GetString(reader.GetOrdinal(columnName));
    public static DateTime GetDateTime(this SqlDataReader reader, string columnName) => reader.GetDateTime(reader.GetOrdinal(columnName));
    public static bool IsDBNull(this SqlDataReader reader, string columnName) => reader.IsDBNull(reader.GetOrdinal(columnName));
}
