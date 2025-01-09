using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace RunServer
{
    class Program
    {
        static void Main(string[] args)
        {
            string ip = "";
            int port = 0;
            string dbServer = "";
            string dbDatabase = "";
            string dbUid = "";
            string dbPwd = "";
            string dbTable = "";

            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = config.AppSettings.Settings;

                if (settings["IP"] != null)
                {
                    ip = settings["IP"].Value;
                }
                if (settings["PORT"] != null && int.TryParse(settings["PORT"].Value, out int parsedPort))
                {
                    port = parsedPort;
                }
                if (settings["DB_SERVER"] != null)
                {
                    dbServer = settings["DB_SERVER"].Value;
                }
                if (settings["DB_DATABASE"] != null)
                {
                    dbDatabase = settings["DB_DATABASE"].Value;
                }
                if (settings["DB_UID"] != null)
                {
                    dbUid = settings["DB_UID"].Value;
                }
                if (settings["DB_PWD"] != null)
                {
                    dbPwd = settings["DB_PWD"].Value;
                }
                if (settings["DB_TABLE"] != null)
                {
                    dbTable = settings["DB_TABLE"].Value;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"App.Config 파일 읽기 오류: {e.Message}" + WriteTime());
                return;
            }

            Console.WriteLine("서버 관리자 시작.." + WriteTime());

            // MySQL 연결 설정
            string connectionString = $"Server={dbServer};Database={dbDatabase};Uid={dbUid};Pwd={dbPwd};";


            // DB Table에 데이터 삽입

            // MySqlService mySqlService2 = new MySqlService(connectionString);
            // // 샘플 데이터 삽입
            // Dictionary<string, object> sampleData = new Dictionary<string, object>
            //         {  // ID는 자동 증가일 수 있으므로 실제 사용 시 이 부분을 제거할 수 있습니다.
            //             { "ID", 3 },
            //             { "PW", "samplePassword123" }
            //         };

            //         bool insertResult = mySqlService2.InsertData(dbTable, sampleData);
            //         if (insertResult)
            //         {
            //             Console.WriteLine($"샘플 데이터가 {dbTable}에 성공적으로 삽입되었습니다." + WriteTime());
            //         }
            //         else
            //         {
            //             Console.WriteLine($"샘플 데이터 삽입 실패." + WriteTime());
            //         }

            try
            {
                // MySqlService 인스턴스 생성
                using (MySqlService mySqlService = new MySqlService(connectionString))
                {


                    Console.WriteLine($"데이터베이스 {dbDatabase}에 연결되었습니다." + WriteTime());
                    SocketHandler socketHandler = new SocketHandler(mySqlService);



                    // 데이터 조회
                    List<Dictionary<string, object>> allItems = mySqlService.GetAllItems(dbTable);
                    Console.WriteLine($"Table: {dbTable}에서 조회된 항목 수: {allItems.Count}" + WriteTime());

                    // 조회된 데이터 출력 (예시)
                    foreach (var item in allItems)
                    {
                        Console.WriteLine("항목:");
                        foreach (var kvp in item)
                        {
                            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                        }
                    }

                    IPAddress ipAddress = IPAddress.Parse(ip);
                    TcpListener server = new TcpListener(ipAddress, port);

                    server.Start();
                    Console.WriteLine($"서버가 {ip}:{port}에서 시작되었습니다." + WriteTime());

                    while (true)
                    {
                        Console.WriteLine();
                        Console.WriteLine("클라이언트 연결 대기 중..." + WriteTime());
                        TcpClient client = server.AcceptTcpClient();
                        Console.WriteLine("클라이언트가 연결되었습니다!" + WriteTime());

                        NetworkStream stream = client.GetStream();
                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        string receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();

                        Console.WriteLine($"클라이언트로부터 받은 데이터: {receivedData}" + WriteTime());

                        try
                        {
                            // JSON 파싱
                            var request = JsonSerializer.Deserialize<Dictionary<string, object>>(receivedData);

                            if (request != null && request.ContainsKey("type"))
                            {
                                string responseMessage;
                                switch (request["type"].ToString().ToLower())
                                {
                                    case "search":
                                        if (request.ContainsKey("id"))
                                        {
                                            string searchId = request["id"].ToString();
                                            string jsonResponse = mySqlService.GetItemByIdAsJson(dbTable, searchId);
                                            byte[] responseData = Encoding.UTF8.GetBytes(jsonResponse);
                                            stream.Write(responseData, 0, responseData.Length);
                                            Console.WriteLine($"클라이언트에게 검색 결과 전송 완료 (ID: {searchId})" + WriteTime());
                                        }
                                        break;

                                    case "save":
                                        if (request.ContainsKey("data"))
                                        {
                                            var userData = JsonSerializer.Deserialize<Dictionary<string, object>>(request["data"].ToString());
                                            if (userData != null)
                                            {
                                                bool saveResult = mySqlService.SaveUserData(dbTable, userData);
                                                responseMessage = saveResult ?
                                                    "데이터가 성공적으로 저장되었습니다." :
                                                    "데이터 저장에 실패했습니다.";
                                            }
                                            else
                                            {
                                                responseMessage = "잘못된 데이터 형식입니다.";
                                            }
                                            byte[] saveResponse = Encoding.UTF8.GetBytes(responseMessage);
                                            stream.Write(saveResponse, 0, saveResponse.Length);
                                            Console.WriteLine($"저장 결과: {responseMessage}" + WriteTime());
                                        }
                                        break;

                                    default:
                                        responseMessage = "Invalid request type";
                                        byte[] defaultResponse = Encoding.UTF8.GetBytes(responseMessage);
                                        stream.Write(defaultResponse, 0, defaultResponse.Length);
                                        break;
                                }
                            }
                            else
                            {
                                string message = "Invalid request format";
                                byte[] data = Encoding.ASCII.GetBytes(message);
                                stream.Write(data, 0, data.Length);
                                Console.WriteLine($"클라이언트에게 메시지 전송: {message}" + WriteTime());
                            }
                        }
                        catch (JsonException ex)
                        {
                            string message = $"Invalid JSON format: {ex.Message}";
                            byte[] data = Encoding.ASCII.GetBytes(message);
                            stream.Write(data, 0, data.Length);
                            Console.WriteLine($"잘못된 JSON 형식: {receivedData}" + WriteTime());
                        }
                        catch (Exception ex)
                        {
                            string message = $"Error: {ex.Message}";
                            byte[] data = Encoding.ASCII.GetBytes(message);
                            stream.Write(data, 0, data.Length);
                            Console.WriteLine($"오류 발생: {ex.Message}" + WriteTime());
                        }
                        finally
                        {
                            client.Close();
                        }

                        // 여기에서 필요할 때마다 mySqlService를 사용하여 쿼리 실행
                    }
                }
            }
            catch (MySqlException mysqlEx)
            {
                Console.WriteLine($"MySQL 오류 발생: {mysqlEx.Message}" + WriteTime());
            }
            catch (Exception e)
            {
                Console.WriteLine($"오류 발생: {e.Message}" + WriteTime());
            }
        }

        static public string WriteTime()
        {
            return " [" + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + "] ";
        }
    }
}
