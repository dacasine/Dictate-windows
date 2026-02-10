using Microsoft.Data.Sqlite;
using DictateForWindows.Core.Constants;
using DictateForWindows.Core.Models;
using Microsoft.Extensions.Logging;

namespace DictateForWindows.Core.Data;

/// <summary>
/// Repository for tracking API usage in SQLite database.
/// </summary>
public class UsageRepository : IUsageRepository, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<UsageRepository>? _logger;
    private bool _disposed;

    private const int DatabaseVersion = 2;

    public UsageRepository(string? databasePath = null, ILogger<UsageRepository>? logger = null)
    {
        var path = databasePath ?? GetDefaultDatabasePath();
        _connectionString = $"Data Source={path}";
        _logger = logger;

        EnsureDirectory(path);
        InitializeDatabase();
    }

    private static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dictateFolder = Path.Combine(appData, "DictateForWindows");
        return Path.Combine(dictateFolder, "usage.db");
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Create table
        var createTableCmd = connection.CreateCommand();
        createTableCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS USAGE (
                MODEL_NAME TEXT PRIMARY KEY,
                AUDIO_TIME INTEGER NOT NULL DEFAULT 0,
                INPUT_TOKENS INTEGER NOT NULL DEFAULT 0,
                OUTPUT_TOKENS INTEGER NOT NULL DEFAULT 0,
                MODEL_PROVIDER INTEGER NOT NULL DEFAULT 0
            );
            """;
        createTableCmd.ExecuteNonQuery();

        // Check if we need to run migrations
        var versionCmd = connection.CreateCommand();
        versionCmd.CommandText = "PRAGMA user_version;";
        var currentVersion = Convert.ToInt32(versionCmd.ExecuteScalar());

        if (currentVersion < 2)
        {
            // Migration: Add MODEL_PROVIDER column
            try
            {
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE USAGE ADD COLUMN MODEL_PROVIDER INTEGER NOT NULL DEFAULT 0;";
                alterCmd.ExecuteNonQuery();
            }
            catch
            {
                // Column might already exist
            }
        }

        // Update version
        var setVersionCmd = connection.CreateCommand();
        setVersionCmd.CommandText = $"PRAGMA user_version = {DatabaseVersion};";
        setVersionCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Record usage for a model (upsert).
    /// </summary>
    /// <param name="modelName">Name of the model.</param>
    /// <param name="audioTimeMs">Audio time in milliseconds (for transcription).</param>
    /// <param name="inputTokens">Number of input tokens (for chat).</param>
    /// <param name="outputTokens">Number of output tokens (for chat).</param>
    /// <param name="provider">API provider.</param>
    public void RecordUsage(string modelName, long audioTimeMs, long inputTokens, long outputTokens, ApiProvider provider)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Try to update existing record
        var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = """
            UPDATE USAGE
            SET AUDIO_TIME = AUDIO_TIME + @audio,
                INPUT_TOKENS = INPUT_TOKENS + @input,
                OUTPUT_TOKENS = OUTPUT_TOKENS + @output
            WHERE MODEL_NAME = @model;
            """;

        updateCmd.Parameters.AddWithValue("@model", modelName);
        updateCmd.Parameters.AddWithValue("@audio", audioTimeMs);
        updateCmd.Parameters.AddWithValue("@input", inputTokens);
        updateCmd.Parameters.AddWithValue("@output", outputTokens);

        var rowsAffected = updateCmd.ExecuteNonQuery();

        // If no rows updated, insert new record
        if (rowsAffected == 0)
        {
            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO USAGE (MODEL_NAME, AUDIO_TIME, INPUT_TOKENS, OUTPUT_TOKENS, MODEL_PROVIDER)
                VALUES (@model, @audio, @input, @output, @provider);
                """;

            insertCmd.Parameters.AddWithValue("@model", modelName);
            insertCmd.Parameters.AddWithValue("@audio", audioTimeMs);
            insertCmd.Parameters.AddWithValue("@input", inputTokens);
            insertCmd.Parameters.AddWithValue("@output", outputTokens);
            insertCmd.Parameters.AddWithValue("@provider", (int)provider);

            insertCmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Get all usage records.
    /// </summary>
    public IReadOnlyList<UsageModel> GetAll()
    {
        var usages = new List<UsageModel>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM USAGE ORDER BY MODEL_NAME;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var model = ReadUsage(reader);
            model.TotalCost = CalculateCost(model);
            usages.Add(model);
        }

        return usages;
    }

    /// <summary>
    /// Get usage for a specific model.
    /// </summary>
    public UsageModel? Get(string modelName)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM USAGE WHERE MODEL_NAME = @model;";
        cmd.Parameters.AddWithValue("@model", modelName);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var model = ReadUsage(reader);
            model.TotalCost = CalculateCost(model);
            return model;
        }

        return null;
    }

    /// <summary>
    /// Get the total cost across all models.
    /// </summary>
    public decimal GetTotalCost()
    {
        return GetAll().Sum(u => u.TotalCost);
    }

    /// <summary>
    /// Get the total audio time in milliseconds.
    /// </summary>
    public long GetTotalAudioTimeMs()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(AUDIO_TIME), 0) FROM USAGE;";

        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Reset all usage statistics.
    /// </summary>
    public void Reset()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM USAGE;";
        cmd.ExecuteNonQuery();

        _logger?.LogInformation("Usage statistics reset");
    }

    private static UsageModel ReadUsage(SqliteDataReader reader)
    {
        return new UsageModel
        {
            ModelName = reader.GetString(reader.GetOrdinal("MODEL_NAME")),
            AudioTimeMs = reader.GetInt64(reader.GetOrdinal("AUDIO_TIME")),
            InputTokens = reader.GetInt64(reader.GetOrdinal("INPUT_TOKENS")),
            OutputTokens = reader.GetInt64(reader.GetOrdinal("OUTPUT_TOKENS")),
            Provider = (ApiProvider)reader.GetInt32(reader.GetOrdinal("MODEL_PROVIDER"))
        };
    }

    private static decimal CalculateCost(UsageModel usage)
    {
        // Check if it's a transcription model
        if (ModelPricing.TranscriptionPricePerSecond.ContainsKey(usage.ModelName))
        {
            return ModelPricing.CalculateTranscriptionCost(usage.ModelName, usage.AudioTimeMs);
        }

        // Otherwise it's a rewording model
        return ModelPricing.CalculateRewordingCost(
            usage.ModelName,
            (int)usage.InputTokens,
            (int)usage.OutputTokens);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // SQLite connections are disposed per-operation
    }
}

/// <summary>
/// Interface for usage repository.
/// </summary>
public interface IUsageRepository
{
    void RecordUsage(string modelName, long audioTimeMs, long inputTokens, long outputTokens, ApiProvider provider);
    IReadOnlyList<UsageModel> GetAll();
    UsageModel? Get(string modelName);
    decimal GetTotalCost();
    long GetTotalAudioTimeMs();
    void Reset();
}
