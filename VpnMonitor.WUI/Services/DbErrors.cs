using Npgsql;
using System;

namespace VpnMonitor.WUI.Services
{
    public static class DbErrors
    {
        /// <summary>
        /// Text for the user. A database without the latest migrations gets a hint instead of a technical error:
        /// the app expects the schema that the latest service applies on start.
        /// </summary>
        public static string Describe(Exception ex)
        {
            if (IsOutdatedSchema(ex))
                return "База данных ещё не обновлена: запустите новую версию сервиса, он применит миграции при старте.";

            return ex.Message;
        }

        public static bool IsOutdatedSchema(Exception ex)
        {
            // A missing table, or byte counters still stored as text
            return ex is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn }
                || ex is InvalidCastException;
        }
    }
}
