using Microsoft.Data.Sqlite;
using DictateForWindows.Core.Models;
using Microsoft.Extensions.Logging;

namespace DictateForWindows.Core.Data;

/// <summary>
/// Repository for managing prompts in SQLite database.
/// </summary>
public class PromptsRepository : IPromptsRepository, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<PromptsRepository>? _logger;
    private bool _disposed;

    private const int DatabaseVersion = 2;

    public PromptsRepository(string? databasePath = null, ILogger<PromptsRepository>? logger = null)
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
        return Path.Combine(dictateFolder, "prompts.db");
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
            CREATE TABLE IF NOT EXISTS PROMPTS (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                POS INTEGER NOT NULL,
                NAME TEXT NOT NULL,
                PROMPT TEXT NOT NULL,
                REQUIRES_SELECTION INTEGER NOT NULL DEFAULT 0,
                AUTO_APPLY INTEGER NOT NULL DEFAULT 0
            );
            """;
        createTableCmd.ExecuteNonQuery();

        // Check if we need to run migrations
        var versionCmd = connection.CreateCommand();
        versionCmd.CommandText = "PRAGMA user_version;";
        var currentVersion = Convert.ToInt32(versionCmd.ExecuteScalar());

        if (currentVersion < 2)
        {
            // Migration: Add AUTO_APPLY column
            try
            {
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE PROMPTS ADD COLUMN AUTO_APPLY INTEGER NOT NULL DEFAULT 0;";
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

        // Insert default prompts if table is empty
        if (Count() == 0)
        {
            InsertDefaultPrompts();
        }
    }

    private void InsertDefaultPrompts()
    {
        var defaultPrompts = new[]
        {
            new PromptModel
            {
                Position = 0,
                Name = "French Translator",
                Prompt = "Translate the following text to French:",
                RequiresSelection = true,
                AutoApply = false
            },
            new PromptModel
            {
                Position = 1,
                Name = "Formal Rewrite",
                Prompt = "Rewrite the following text in a formal tone. Keep the same language, add paragraphs when needed, keep intention:",
                RequiresSelection = true,
                AutoApply = false
            },
            new PromptModel
            {
                Position = 2,
                Name = "English Translator",
                Prompt = "Translate the Following to English:",
                RequiresSelection = true,
                AutoApply = false
            }
        };

        foreach (var prompt in defaultPrompts)
        {
            Add(prompt);
        }
    }

    /// <summary>
    /// Add a new prompt.
    /// </summary>
    public int Add(PromptModel prompt)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO PROMPTS (POS, NAME, PROMPT, REQUIRES_SELECTION, AUTO_APPLY)
            VALUES (@pos, @name, @prompt, @requires, @auto);
            SELECT last_insert_rowid();
            """;

        cmd.Parameters.AddWithValue("@pos", prompt.Position);
        cmd.Parameters.AddWithValue("@name", prompt.Name);
        cmd.Parameters.AddWithValue("@prompt", prompt.Prompt);
        cmd.Parameters.AddWithValue("@requires", prompt.RequiresSelection ? 1 : 0);
        cmd.Parameters.AddWithValue("@auto", prompt.AutoApply ? 1 : 0);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Update an existing prompt.
    /// </summary>
    public bool Update(PromptModel prompt)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE PROMPTS
            SET POS = @pos, NAME = @name, PROMPT = @prompt,
                REQUIRES_SELECTION = @requires, AUTO_APPLY = @auto
            WHERE ID = @id;
            """;

        cmd.Parameters.AddWithValue("@id", prompt.Id);
        cmd.Parameters.AddWithValue("@pos", prompt.Position);
        cmd.Parameters.AddWithValue("@name", prompt.Name);
        cmd.Parameters.AddWithValue("@prompt", prompt.Prompt);
        cmd.Parameters.AddWithValue("@requires", prompt.RequiresSelection ? 1 : 0);
        cmd.Parameters.AddWithValue("@auto", prompt.AutoApply ? 1 : 0);

        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Delete a prompt by ID.
    /// </summary>
    public bool Delete(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM PROMPTS WHERE ID = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Get a prompt by ID.
    /// </summary>
    public PromptModel? Get(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM PROMPTS WHERE ID = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return ReadPrompt(reader);
        }

        return null;
    }

    /// <summary>
    /// Get all prompts ordered by position.
    /// </summary>
    public IReadOnlyList<PromptModel> GetAll()
    {
        var prompts = new List<PromptModel>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM PROMPTS ORDER BY POS;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            prompts.Add(ReadPrompt(reader));
        }

        return prompts;
    }

    /// <summary>
    /// Get all prompts for keyboard display, including virtual buttons.
    /// </summary>
    public IReadOnlyList<PromptModel> GetAllForKeyboard()
    {
        var prompts = new List<PromptModel>();

        // Add instant output button
        prompts.Add(new PromptModel
        {
            Id = SpecialPromptIds.Instant,
            Name = "\u26a1", // Lightning emoji
            Prompt = "",
            RequiresSelection = false
        });

        // Add select all button
        prompts.Add(new PromptModel
        {
            Id = SpecialPromptIds.SelectAll,
            Name = "\u2611", // Checkbox emoji
            Prompt = "",
            RequiresSelection = false
        });

        // Add user prompts
        prompts.AddRange(GetAll());

        // Add "add new" button
        prompts.Add(new PromptModel
        {
            Id = SpecialPromptIds.Add,
            Name = "+",
            Prompt = "",
            RequiresSelection = false
        });

        return prompts;
    }

    /// <summary>
    /// Get IDs of prompts with auto-apply enabled.
    /// </summary>
    public IReadOnlyList<int> GetAutoApplyIds()
    {
        var ids = new List<int>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT ID FROM PROMPTS WHERE AUTO_APPLY = 1 ORDER BY POS;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    /// <summary>
    /// Get the number of prompts.
    /// </summary>
    public int Count()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PROMPTS;";

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Replace all prompts with the given list.
    /// </summary>
    public void ReplaceAll(IEnumerable<PromptModel> prompts)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Delete all existing prompts
            var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM PROMPTS;";
            deleteCmd.ExecuteNonQuery();

            // Insert new prompts
            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO PROMPTS (POS, NAME, PROMPT, REQUIRES_SELECTION, AUTO_APPLY)
                VALUES (@pos, @name, @prompt, @requires, @auto);
                """;

            var posParam = insertCmd.Parameters.Add("@pos", SqliteType.Integer);
            var nameParam = insertCmd.Parameters.Add("@name", SqliteType.Text);
            var promptParam = insertCmd.Parameters.Add("@prompt", SqliteType.Text);
            var requiresParam = insertCmd.Parameters.Add("@requires", SqliteType.Integer);
            var autoParam = insertCmd.Parameters.Add("@auto", SqliteType.Integer);

            int position = 0;
            foreach (var prompt in prompts)
            {
                posParam.Value = position++;
                nameParam.Value = prompt.Name;
                promptParam.Value = prompt.Prompt;
                requiresParam.Value = prompt.RequiresSelection ? 1 : 0;
                autoParam.Value = prompt.AutoApply ? 1 : 0;
                insertCmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Update positions after reordering.
    /// </summary>
    public void UpdatePositions(IEnumerable<(int Id, int Position)> positions)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE PROMPTS SET POS = @pos WHERE ID = @id;";
            var idParam = cmd.Parameters.Add("@id", SqliteType.Integer);
            var posParam = cmd.Parameters.Add("@pos", SqliteType.Integer);

            foreach (var (id, position) in positions)
            {
                idParam.Value = id;
                posParam.Value = position;
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static PromptModel ReadPrompt(SqliteDataReader reader)
    {
        return new PromptModel
        {
            Id = reader.GetInt32(reader.GetOrdinal("ID")),
            Position = reader.GetInt32(reader.GetOrdinal("POS")),
            Name = reader.GetString(reader.GetOrdinal("NAME")),
            Prompt = reader.GetString(reader.GetOrdinal("PROMPT")),
            RequiresSelection = reader.GetInt32(reader.GetOrdinal("REQUIRES_SELECTION")) == 1,
            AutoApply = reader.GetInt32(reader.GetOrdinal("AUTO_APPLY")) == 1
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // SQLite connections are disposed per-operation
    }
}

/// <summary>
/// Interface for prompts repository.
/// </summary>
public interface IPromptsRepository
{
    int Add(PromptModel prompt);
    bool Update(PromptModel prompt);
    bool Delete(int id);
    PromptModel? Get(int id);
    IReadOnlyList<PromptModel> GetAll();
    IReadOnlyList<PromptModel> GetAllForKeyboard();
    IReadOnlyList<int> GetAutoApplyIds();
    int Count();
    void ReplaceAll(IEnumerable<PromptModel> prompts);
    void UpdatePositions(IEnumerable<(int Id, int Position)> positions);
}
