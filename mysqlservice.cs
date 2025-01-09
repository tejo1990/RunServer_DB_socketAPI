using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using System.Text.Json;

namespace RunServer
{
    public class MySqlService : IDisposable
    {
        private readonly string _connectionString;
        private MySqlConnection _connection;

        public MySqlService(string connectionString)
        {
            _connectionString = connectionString;
            _connection = new MySqlConnection(_connectionString);
            _connection.Open();
        }

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }

        // 테이블의 모든 항목을 보여주는 함수
        public List<Dictionary<string, object>> GetAllItems(string tableName)
        {
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            using (MySqlConnection connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = $"SELECT * FROM {tableName}";
                using (MySqlCommand command = new MySqlCommand(query, connection))
                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Dictionary<string, object> row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader[i];
                        }
                        result.Add(row);
                    }
                }
            }
            return result;
        }

        // 테이블에 데이터를 저장하는 함수
        public bool InsertData(string tableName, Dictionary<string, object> data)
        {
            try
            {
                if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                }
                
                string columns = string.Join(", ", data.Keys);
                string values = string.Join(", ", data.Keys.Select(k => "@" + k));
                
                string query = $"INSERT INTO {tableName} ({columns}) VALUES ({values})";
                
                using (MySqlCommand command = new MySqlCommand(query, _connection))
                {
                    foreach (var item in data)
                    {
                        command.Parameters.AddWithValue("@" + item.Key, item.Value);
                    }
                    
                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

        // 테이블의 모든 항목을 JSON 형태로 반환하는 함수
        public string GetAllItemsAsJson(string tableName)
        {
            var items = GetAllItems(tableName);
            return JsonSerializer.Serialize(items);
        }

        public Dictionary<string, object> GetItemById(string tableName, string id)
    {
        using (MySqlCommand cmd = new MySqlCommand($"SELECT * FROM {tableName} WHERE ID = @id", _connection))
        {
            cmd.Parameters.AddWithValue("@id", id);
            using (MySqlDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    Dictionary<string, object> item = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        item.Add(reader.GetName(i), reader[i]);
                    }
                    return item;
                }
                return null;
            }
        }
    }

    public string GetItemByIdAsJson(string tableName, string id)
    {
        var item = GetItemById(tableName, id);
        return item != null ? JsonSerializer.Serialize(item) : "null";
    }

    public bool SaveUserData(string tableName, Dictionary<string, object> userData)
    {
        try
        {
            string columns = string.Join(", ", userData.Keys);
            string values = string.Join(", ", userData.Keys.Select(k => "@" + k));
            string query = $"INSERT INTO {tableName} ({columns}) VALUES ({values})";

            using (MySqlCommand cmd = new MySqlCommand(query, _connection))
            {
                foreach (var item in userData)
                {
                    cmd.Parameters.AddWithValue("@" + item.Key, item.Value);
                }
                int result = cmd.ExecuteNonQuery();
                return result > 0;
            }
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"데이터 저장 중 오류 발생: {ex.Message}");
            return false;
        }
    }




    }

}
