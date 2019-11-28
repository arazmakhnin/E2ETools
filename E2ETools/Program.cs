using System;
using System.IO;
using System.Threading.Tasks;
using Atlassian.Jira;
using C4Check;
using Newtonsoft.Json;

namespace E2ETools
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var options = ReadOptions();
            if (string.IsNullOrWhiteSpace(options.JiraProjectName))
            {
                Console.WriteLine("JiraProjectName is empty!");
                return;
            }

            var jira = Jira.CreateRestClient("https://jira.devfactory.com", options.JiraUserName, options.JiraPassword);

            while (true)
            {
                Console.WriteLine("========================");
                Console.Write("Enter command: ");
                var command = Console.ReadLine() ?? string.Empty;

                switch (command.Trim().ToLower())
                {
                    case "create":
                        var creator = new E2ETicketCreator(jira, options);
                        await creator.Create();
                        break;

                    case "check":
                        Console.Write("Enter jira ticket or key: ");
                        var ticketUrl = Console.ReadLine() ?? string.Empty;

                        var checker = new E2ETicketChecker(jira, options);
                        await checker.Check(ticketUrl);
                        break;

                    case "exit":
                        return;
                }
            }
        }

        private static Options ReadOptions()
        {
            var fileName = AppFolderHelper.GetFile("options.json");
            return JsonConvert.DeserializeObject<Options>(File.ReadAllText(fileName));
        }
    }
}
