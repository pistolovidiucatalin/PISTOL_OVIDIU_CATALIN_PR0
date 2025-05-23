using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using SQLitePCL;
namespace TheAdventure
{
    public static class DatabaseManager
    {
        private static readonly string DbFile =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scores.db");
        private static readonly string ConnectionString = $"Data Source={DbFile}";

        static DatabaseManager()
        {
            Batteries_V2.Init();
            Create();
        }

        private static void Create()
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS Scores(
                    Id    INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name  TEXT    NOT NULL,
                    Score INTEGER NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        public static void InsertScore(string name, int score)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO Scores(Name,Score) VALUES ($n,$s);";
            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$s", score);
            cmd.ExecuteNonQuery();
        }

        public static int GetHighestScore()
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT IFNULL(MAX(Score),0) FROM Scores;";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static List<(string Name, int Score)> GetTopScores(int count)
        {
            var list = new List<(string, int)>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT Name,Score FROM Scores ORDER BY Score DESC LIMIT $c;";
            cmd.Parameters.AddWithValue("$c", count);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add((r.GetString(0), r.GetInt32(1)));
            return list;
        }

        public static void PurgeScores()
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM Scores;";
            cmd.ExecuteNonQuery();
        }
    }
}
