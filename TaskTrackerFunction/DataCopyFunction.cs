using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Core;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TaskTrackerFunction;

public static class DataCopyFunction
{

    [FunctionName("CopyDataFunction")]
    public static void Run(
        [TimerTrigger("0 */1 * * * *")] TimerInfo myTimer,
        [CosmosDB(
            databaseName: "tasktrackercosmos",
            containerName: "ProjectsContainer",
            Connection = "CosmosDBConnectionString")] ICollector<Project> cosmosDocument,
        ILogger log)
    {
        log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

        string sqlConnectionString = GetSqlConnectionStringFromKeyVault();

        List<Project> projects = QueryProjectsFromSql(sqlConnectionString);
        List<TaskUnit> tasks = QueryTasksFromSql(sqlConnectionString);

        foreach (Project project in projects)
        {
            project.Tasks = tasks.Where(t => t.ProjectId == project.ProjectId).ToList();
            cosmosDocument.Add(project);
        }
    }

    private static string GetSqlConnectionStringFromKeyVault()
    {
        SecretClientOptions options = new()
        {
            Retry =
        {
            Delay= TimeSpan.FromSeconds(2),
            MaxDelay = TimeSpan.FromSeconds(16),
            MaxRetries = 5,
            Mode = RetryMode.Exponential
         }
        };

        var client = new SecretClient(new Uri("https://jovanranisavljevkeyvault.vault.azure.net/"), new DefaultAzureCredential(), options);

        KeyVaultSecret secret = client.GetSecret("SQLConnectionString");

        return secret.Value;
    }

    private static List<Project> QueryProjectsFromSql(string sqlConnectionString)
    {
        List<Project> projects = new();

        string sqlProjectsCommand = "SELECT * FROM Projects";

        using (SqlConnection connection = new SqlConnection(sqlConnectionString))
        {
            connection.Open();

            using (SqlCommand command = new SqlCommand(sqlProjectsCommand, connection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Project project = new Project
                        {
                            Id = reader["ProjectId"].ToString(),
                            ProjectId = (int)reader["ProjectId"],
                            Name = reader["Name"].ToString(),
                            StartDate = ConvertToDateTimeNullableFromDbVal(reader["StartDate"]),
                            CompletionDate = ConvertToDateTimeNullableFromDbVal(reader["CompletionDate"]),
                            Status = (ProjectStatusEnum)reader["Status"],
                            Priority = (int)reader["Priority"]
                        };

                        projects.Add(project);
                    }
                }
            }
        }

        return projects;
    }

    private static List<TaskUnit> QueryTasksFromSql(string sqlConnectionString)
    {
        List<TaskUnit> tasks = new();

        string sqlTasksCommand = "SELECT * FROM Tasks";

        using (SqlConnection connection = new SqlConnection(sqlConnectionString))
        {
            connection.Open();

            using (SqlCommand command = new SqlCommand(sqlTasksCommand, connection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        TaskUnit task = new TaskUnit
                        {
                            TaskId = (int)reader["TaskId"],
                            Name = reader["Name"].ToString(),
                            Description = reader["Description"].ToString(),
                            ProjectId = (int)reader["ProjectId"]
                        };

                        tasks.Add(task);
                    }
                }
            }
        }

        return tasks;
    }

    private static DateTime? ConvertToDateTimeNullableFromDbVal(object obj)
    {
        if (obj == null || obj == DBNull.Value)
        {
            return null;
        }
        else
        {
            return (DateTime?)obj;
        }
    }
}

public class Project
{
    [JsonProperty("id")]
    public string Id { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public ProjectStatusEnum? Status { get; set; }
    public int Priority { get; set; } = 1;
    public IEnumerable<TaskUnit>? Tasks { get; set; }
}

public enum ProjectStatusEnum
{
    NotStarted = 1,
    Active = 2,
    Completed = 3
}

public class TaskUnit
{
    public int TaskId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int ProjectId { get; set; }
}
