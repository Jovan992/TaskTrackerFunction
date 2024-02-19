using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace TaskTrackerFunction;

public static class DataCopyFunction
{
    

    [FunctionName("CopyDataFunction")]
    public static void Run(
        [TimerTrigger("0 */1 * * * *")] TimerInfo myTimer,
        [CosmosDB(
            databaseName: "tasktrackercosmos",
            containerName: "Projects",
            Connection = "CosmosDBConnectionString")] out dynamic cosmosDocument,
        ILogger log)
    {
        log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

        cosmosDocument = null;

        var ccc = "AccountEndpoint=https://tasktrackercosmos.documents.azure.com:443/;AccountKey=LxNBkViWvGidaPuLGfkndB5AguCRhfyfGYpFhK560cxDI2qqqhpSQt6zJJaM64EJygNIOh1EgVENACDbSUD48A==;";

    string sqlConnectionString = "Server=tcp:tasktrackerdbserver.database.windows.net,1433;Initial Catalog=TaskTracker_db;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=\"Active Directory Default\";";
        string sqlCommand = "SELECT * FROM Projects";

        using (SqlConnection connection = new SqlConnection(sqlConnectionString))
        {
            connection.Open();
            using (SqlCommand command = new SqlCommand(sqlCommand, connection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        
                        cosmosDocument = new
                        {
                            ProjectId = reader["ProjectId"],
                            Name = reader["Name"],
                            StartDate = reader["StartDate"],
                            CompletionDate = reader["CompletionDate"],
                            Status = reader["Status"],
                            Priority = reader["Priority"],
                            Tasks = reader["Tasks"],
                     
                        };
                    }
                }
            }
        }
        if (cosmosDocument == null)
        {
            log.LogInformation("No data read from SQL Server. Exiting function.");
        }
    }
}

