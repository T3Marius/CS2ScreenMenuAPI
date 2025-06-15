using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Dapper;
using static CS2ScreenMenuAPI.PlayerRes;

namespace CS2ScreenMenuAPI
{
    internal static class ResolutionDatabase
    {
        private static string GlobalDBConnectionString { get; set; } = string.Empty;
        public static bool _initialized = false;
        private static bool _initializing = false;
        private static readonly Dictionary<ulong, Resolution> CachedResolutions = new Dictionary<ulong, Resolution>();
        private static readonly string TableName = "player_resolutions";
        private static ILogger? _logger;
        private static readonly object _initLock = new object();
        private static DateTime _lastInitAttempt = DateTime.MinValue;

        public static async Task<bool> InitializeAsync(ILogger logger, Database_Config config)
        {
            lock (_initLock)
            {
                if (_initialized)
                    return true;

                if (_initializing || (DateTime.Now - _lastInitAttempt).TotalSeconds < 10)
                    return false;

                _initializing = true;
                _lastInitAttempt = DateTime.Now;
                _logger = logger;
            }

            try
            {
                await CreateDBAsync(config);

                await PreloadResolutions();

                _initialized = true;
                _initializing = false;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to initialize Resolution Database: {ex.Message}");
                _initializing = false;
                return false;
            }
        }

        private static async Task<MySqlConnection> ConnectAsync()
        {
            MySqlConnection connection = new MySqlConnection(GlobalDBConnectionString);
            await connection.OpenAsync();
            return connection;
        }

        private static void ExecuteAsync(string query, object? parameters)
        {
            if (!_initialized)
            {
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    using MySqlConnection connection = await ConnectAsync();
                    int rowsAffected = await connection.ExecuteAsync(query, parameters);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Database error: {ex.Message}");
                }
            });
        }

        private static async Task CreateDBAsync(Database_Config config)
        {
            MySqlConnectionStringBuilder builder = new()
            {
                Server = config.Host,
                Database = config.Name,
                UserID = config.User,
                Password = config.Password,
                Port = config.Port,
                Pooling = true,
                MinimumPoolSize = 0,
                MaximumPoolSize = 128,
                ConnectionIdleTimeout = 30
            };

            GlobalDBConnectionString = builder.ConnectionString;

            using MySqlConnection connection = await ConnectAsync();
            using MySqlTransaction transaction = await connection.BeginTransactionAsync();

            try
            {
                await connection.ExecuteAsync($@"
                    CREATE TABLE IF NOT EXISTS {TableName} (
                        SteamID BIGINT UNSIGNED NOT NULL,
                        PositionX FLOAT NOT NULL,
                        PositionY FLOAT NOT NULL,
                        LastSeen DATETIME DEFAULT CURRENT_TIMESTAMP,
                        PRIMARY KEY (SteamID)
                    );", transaction: transaction);

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger?.LogError($"Unable to create resolution database: {ex.Message}");
                throw;
            }
        }

        public static bool HasPlayerResolution(CCSPlayerController player)
        {
            if (player == null || player.SteamID == 0)
                return false;

            return CachedResolutions.ContainsKey(player.SteamID);
        }

        public static Resolution GetPlayerResolution(CCSPlayerController player)
        {
            if (player == null || player.SteamID == 0)
                return new Resolution();

            if (CachedResolutions.TryGetValue(player.SteamID, out Resolution? resolution))
                return resolution;

            if (_initialized)
            {
                LoadPlayerResolution(player);
            }

            return new Resolution();
        }

        public static void LoadPlayerResolution(CCSPlayerController player)
        {
            if (!_initialized || player == null || !player.IsValid || player.SteamID == 0)
                return;

            ulong steamId = player.SteamID;

            if (CachedResolutions.ContainsKey(steamId))
                return;

            Task.Run(async () =>
            {
                try
                {
                    using MySqlConnection connection = await ConnectAsync();

                    var result = await connection.QueryFirstOrDefaultAsync<ResolutionData>($@"
                        SELECT PositionX, PositionY FROM {TableName} WHERE SteamID = @SteamID;
                    ", new { SteamID = (long)steamId });

                    if (result != null)
                    {
                        Server.NextFrame(() =>
                        {
                            Resolution playerRes = new Resolution(result.PositionX, result.PositionY);
                            CachedResolutions[steamId] = playerRes;
                            _logger?.LogInformation($"Loaded resolution for player {steamId}: X:{result.PositionX}, Y:{result.PositionY}");
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error loading player resolution {steamId}: {ex.Message}");
                }
            });
        }

        public static void SetPlayerResolution(CCSPlayerController player, Resolution resolution)
        {
            if (!_initialized || player == null || !player.IsValid || player.SteamID == 0 || resolution == null)
                return;

            ulong steamId = player.SteamID;

            CachedResolutions[steamId] = resolution;

            ExecuteAsync($@"
                INSERT INTO {TableName} (SteamID, PositionX, PositionY, LastSeen)
                VALUES (@SteamID, @PositionX, @PositionY, @LastSeen)
                ON DUPLICATE KEY UPDATE
                    PositionX = @PositionX,
                    PositionY = @PositionY,
                    LastSeen = @LastSeen;",
                new
                {
                    SteamID = (long)steamId,
                    PositionX = resolution.PositionX,
                    PositionY = resolution.PositionY,
                    LastSeen = DateTime.Now
                });

            _logger?.LogDebug($"Saved resolution for player {steamId}: X:{resolution.PositionX}, Y:{resolution.PositionY}");
        }

        public static void HandlePlayerConnect(CCSPlayerController player)
        {
            if (!_initialized || player == null || !player.IsValid || player.SteamID == 0)
                return;

            LoadPlayerResolution(player);
        }

        public static void HandlePlayerDisconnect(CCSPlayerController player)
        {
            if (!_initialized || player == null || player.SteamID == 0)
                return;

            if (CachedResolutions.TryGetValue(player.SteamID, out Resolution? resolution))
            {
                ExecuteAsync($@"
                    UPDATE {TableName} 
                    SET LastSeen = @LastSeen
                    WHERE SteamID = @SteamID;",
                    new
                    {
                        SteamID = (long)player.SteamID,
                        LastSeen = DateTime.Now
                    });
            }
        }

        public static void ClearCache(ulong steamId)
        {
            if (!_initialized)
                return;

            if (CachedResolutions.ContainsKey(steamId))
                CachedResolutions.Remove(steamId);

            ExecuteAsync($@"
                DELETE FROM {TableName} WHERE SteamID = @SteamID;",
                new { SteamID = (long)steamId });
        }

        public static async Task PreloadResolutions()
        {
            if (!_initialized && !_initializing)
                return;

            try
            {
                using MySqlConnection connection = await ConnectAsync();

                var results = await connection.QueryAsync<ResolutionRecordData>($@"
                    SELECT SteamID, PositionX, PositionY FROM {TableName};
                ");

                CachedResolutions.Clear();

                foreach (var result in results)
                {
                    Resolution playerRes = new Resolution(result.PositionX, result.PositionY);
                    CachedResolutions[(ulong)result.SteamID] = playerRes;
                }

                _logger?.LogInformation($"Preloaded {results.Count()} player resolutions from database");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error preloading resolutions: {ex.Message}");
            }
        }

        private class ResolutionData
        {
            public float PositionX { get; set; }
            public float PositionY { get; set; }
        }

        private class ResolutionRecordData
        {
            public long SteamID { get; set; }
            public float PositionX { get; set; }
            public float PositionY { get; set; }
        }
    }

}