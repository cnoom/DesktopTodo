using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DesktopTodo.Interfaces;
using DesktopTodo.Models;
using Microsoft.Data.Sqlite;

namespace DesktopTodo.Services;

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopTodo");
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, "tasks.db");
        _connectionString = $"Data Source={dbPath}";
        MigrateFromLegacyPath(dbPath);
        InitializeDatabase();
    }

    /// <summary>
    /// 从旧路径（程序目录）迁移数据库到新路径（AppData）
    /// </summary>
    private void MigrateFromLegacyPath(string newPath)
    {
        if (File.Exists(newPath)) return;

        var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tasks.db");
        if (File.Exists(legacyPath))
        {
            try
            {
                File.Copy(legacyPath, newPath, overwrite: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] 数据库迁移失败: {ex.Message}");
            }
        }
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Tasks (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Title       TEXT NOT NULL,
                Description TEXT,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                CreatedAt   TEXT NOT NULL,
                ParentTaskId INTEGER,
                Color       TEXT,
                SortOrder   INTEGER NOT NULL DEFAULT 0
            );
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Categories (
                Id   INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE
            );
        ";
        cmd.ExecuteNonQuery();

        // 兼容旧数据库：添加 CategoryId 列
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Tasks') WHERE name = 'CategoryId'";
        var columnExists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        if (!columnExists)
        {
            cmd.CommandText = "ALTER TABLE Tasks ADD COLUMN CategoryId INTEGER REFERENCES Categories(Id) ON DELETE SET NULL";
            cmd.ExecuteNonQuery();
        }

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Tags (
                Id   INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE
            );
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS TaskTags (
                TaskId INTEGER NOT NULL,
                TagId  INTEGER NOT NULL,
                PRIMARY KEY (TaskId, TagId),
                FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
                FOREIGN KEY (TagId)  REFERENCES Tags(Id)  ON DELETE CASCADE
            );
        ";
        cmd.ExecuteNonQuery();
    }

    // ---- 任务基础操作 ----

    public List<TodoTask> GetAllTasks()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tasks ORDER BY SortOrder, Id";
        return ReadTasks(cmd);
    }

    /// <summary>
    /// 按分类 ID 查询任务（SQL 过滤，避免内存过滤）
    /// </summary>
    public List<TodoTask> GetTasksByCategory(int? categoryId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tasks WHERE CategoryId IS @categoryId ORDER BY SortOrder, Id";
        cmd.Parameters.AddWithValue("@categoryId", (object?)categoryId ?? DBNull.Value);
        return ReadTasks(cmd);
    }

    /// <summary>
    /// 按任务 ID 集合查询任务（用于标签过滤，避免先查全部再内存过滤）
    /// </summary>
    public List<TodoTask> GetTasksByTagIds(HashSet<int> taskIds)
    {
        if (taskIds == null || taskIds.Count == 0) return new List<TodoTask>();

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();

        // 动态构建 IN 子句参数
        var paramNames = new List<string>();
        var index = 0;
        foreach (var id in taskIds)
        {
            var paramName = $"@id{index++}";
            paramNames.Add(paramName);
            cmd.Parameters.AddWithValue(paramName, id);
        }

        cmd.CommandText = $"SELECT * FROM Tasks WHERE Id IN ({string.Join(",", paramNames)}) ORDER BY SortOrder, Id";
        return ReadTasks(cmd);
    }

    private static List<TodoTask> ReadTasks(SqliteCommand cmd)
    {
        var list = new List<TodoTask>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new TodoTask
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                IsCompleted = reader.GetInt32(3) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                ParentTaskId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                Color = reader.IsDBNull(6) ? null : reader.GetString(6),
                SortOrder = reader.GetInt32(7),
                CategoryId = reader.IsDBNull(8) ? null : reader.GetInt32(8)
            });
        }
        return list;
    }

    public int AddTask(TodoTask task)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Tasks (Title, Description, IsCompleted, CreatedAt, ParentTaskId, Color, SortOrder, CategoryId)
            VALUES (@title, @desc, @completed, @created, @parent, @color, @order, @categoryId);
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@title", task.Title);
        cmd.Parameters.AddWithValue("@desc", task.Description ?? "");
        cmd.Parameters.AddWithValue("@completed", task.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@created", task.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@parent", (object?)task.ParentTaskId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@color", (object?)task.Color ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@order", task.SortOrder);
        cmd.Parameters.AddWithValue("@categoryId", (object?)task.CategoryId ?? DBNull.Value);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void UpdateTask(TodoTask task)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Tasks SET
                Title = @title,
                Description = @desc,
                IsCompleted = @completed,
                ParentTaskId = @parent,
                Color = @color,
                SortOrder = @order,
                CategoryId = @categoryId
            WHERE Id = @id
        ";
        cmd.Parameters.AddWithValue("@id", task.Id);
        cmd.Parameters.AddWithValue("@title", task.Title);
        cmd.Parameters.AddWithValue("@desc", task.Description ?? "");
        cmd.Parameters.AddWithValue("@completed", task.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@parent", (object?)task.ParentTaskId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@color", (object?)task.Color ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@order", task.SortOrder);
        cmd.Parameters.AddWithValue("@categoryId", (object?)task.CategoryId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 使用事务删除任务及其所有子任务，保证原子性
    /// </summary>
    public void DeleteTask(int id)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;

            cmd.CommandText = "DELETE FROM Tasks WHERE ParentTaskId = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            cmd.Parameters.Clear();
            cmd.CommandText = "DELETE FROM Tasks WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void UpdateParentAndSortOrder(int taskId, int? newParentId, int newSortOrder)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Tasks SET ParentTaskId = @parent, SortOrder = @order WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", taskId);
        cmd.Parameters.AddWithValue("@parent", (object?)newParentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@order", newSortOrder);
        cmd.ExecuteNonQuery();
    }

    // ---- 标签相关 ----

    public List<Tag> GetAllTags()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tags ORDER BY Name";
        var tags = new List<Tag>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tags.Add(new Tag { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        }
        return tags;
    }

    public int AddTag(string tagName)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Tags WHERE Name = @name";
        cmd.Parameters.AddWithValue("@name", tagName);
        var existing = cmd.ExecuteScalar();
        if (existing != null) return Convert.ToInt32(existing);
        cmd.Parameters.Clear();
        cmd.CommandText = "INSERT INTO Tags (Name) VALUES (@name); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", tagName);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<Tag> GetTagsForTask(int taskId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT t.Id, t.Name FROM Tags t
            INNER JOIN TaskTags tt ON t.Id = tt.TagId
            WHERE tt.TaskId = @taskId
            ORDER BY t.Name";
        cmd.Parameters.AddWithValue("@taskId", taskId);
        var list = new List<Tag>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Tag { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        }
        return list;
    }

    public void AddTagToTask(int taskId, int tagId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO TaskTags (TaskId, TagId) VALUES (@taskId, @tagId)";
        cmd.Parameters.AddWithValue("@taskId", taskId);
        cmd.Parameters.AddWithValue("@tagId", tagId);
        cmd.ExecuteNonQuery();
    }

    public void RemoveTagFromTask(int taskId, int tagId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM TaskTags WHERE TaskId = @taskId AND TagId = @tagId";
        cmd.Parameters.AddWithValue("@taskId", taskId);
        cmd.Parameters.AddWithValue("@tagId", tagId);
        cmd.ExecuteNonQuery();
    }

    public List<int> GetTaskIdsWithTag(int tagId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TaskId FROM TaskTags WHERE TagId = @tagId";
        cmd.Parameters.AddWithValue("@tagId", tagId);
        var ids = new List<int>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) ids.Add(reader.GetInt32(0));
        return ids;
    }

    /// <summary>
    /// 使用事务级联删除标签及其关联，保证原子性
    /// </summary>
    public void DeleteTagCascade(int tagId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;

            cmd.CommandText = "DELETE FROM TaskTags WHERE TagId = @tagId";
            cmd.Parameters.AddWithValue("@tagId", tagId);
            cmd.ExecuteNonQuery();

            cmd.Parameters.Clear();
            cmd.CommandText = "DELETE FROM Tags WHERE Id = @tagId";
            cmd.Parameters.AddWithValue("@tagId", tagId);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // ---- 分类相关 ----

    public List<Category> GetAllCategories()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Categories ORDER BY Name";
        var list = new List<Category>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Category { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        }
        return list;
    }

    /// <summary>
    /// 先查询是否已存在同名分类，存在则返回已有 ID，不存在则插入后返回新 ID。
    /// 修复了原先 INSERT OR IGNORE + last_insert_rowid() 在忽略时返回错误 ID 的问题。
    /// </summary>
    public int AddCategory(string name)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();

        // 先检查是否已存在
        cmd.CommandText = "SELECT Id FROM Categories WHERE Name = @name";
        cmd.Parameters.AddWithValue("@name", name);
        var existing = cmd.ExecuteScalar();
        if (existing != null) return Convert.ToInt32(existing);

        // 不存在则插入
        cmd.Parameters.Clear();
        cmd.CommandText = "INSERT INTO Categories (Name) VALUES (@name); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// 使用事务删除分类并将关联任务置为未分类，保证原子性
    /// </summary>
    public void DeleteCategory(int categoryId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;

            cmd.CommandText = "UPDATE Tasks SET CategoryId = NULL WHERE CategoryId = @id";
            cmd.Parameters.AddWithValue("@id", categoryId);
            cmd.ExecuteNonQuery();

            cmd.Parameters.Clear();
            cmd.CommandText = "DELETE FROM Categories WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", categoryId);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
