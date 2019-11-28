using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Atlassian.Jira;

namespace E2ETools
{
    internal class E2ETicketCreator
    {
        private readonly Jira _jira;
        private readonly Options _options;

        public E2ETicketCreator(Jira jira, Options options)
        {
            _jira = jira;
            _options = options;
        }

        public async Task Create()
        {
            var data = YamlHelper.ReadYamlData(_options);
            var description = YamlHelper.GenerateDescription(data);

            var issueUrl = data.Ticket;
            var issueKey = JiraHelper.GetIssueKey(issueUrl, _options.JiraProjectName);

            // Get the E2E ticket
            Console.Write("Getting ticket... ");
            var issue = await _jira.Issues.GetIssueAsync(issueKey);
            Console.WriteLine("done");

            if (issue.Type != "End-to-end Test")
            {
                throw new E2ECheckerException($"Ticket should be \"End-to-end Test\", but was \"{issue.Type}\"");
            }

            if (issue.Status != "Open" && issue.Status != "E2E Definition")
            {
                throw new E2ECheckerException($"Ticket is in wrong status: {issue.Status}");
            }

            var (faKey, attributeName) = ParseTicketSummary(issue.Summary);
            if (!string.IsNullOrWhiteSpace(data.Attribute) &&
                !attributeName.Equals(data.Attribute, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new E2ECheckerException($"Expected \"{data.Attribute}\" attribute, but ticket attribute is \"{attributeName}\"");
            }

            // Get links and close the Eng Problem if any
            Console.Write("Closing Eng.Problems (if any)... ");
            var links = await _jira.Links.GetLinksForIssueAsync(issue.Key.ToString(), CancellationToken.None);
            try
            {
                var closedCount = 0;
                foreach (var link in links)
                {
                    var linkIssue = link.InwardIssue.Key.ToString() == issue.Key.ToString()
                        ? link.OutwardIssue
                        : link.InwardIssue;

                    if (linkIssue.Type == "Eng Problem")
                    {
                        if (linkIssue.Status == "Identified")
                        {
                            await linkIssue.WorkflowTransitionAsync("Not Needed");
                        }
                    }
                }
                Console.WriteLine($"{closedCount} closed");
            }
            catch (Exception e)
            {
                ConsoleHelper.WriteLineColor("failed", ConsoleColor.Red);
            }

            Console.Write("Getting FA ticket... ");
            var faIssue = await _jira.Issues.GetIssueAsync(faKey);
            Console.WriteLine("done");

            // Update it
            // - Summary
            // - Description
            // - Product feature
            // Add link to FA
            Console.Write("Update E2E ticket properties... ");
            issue.Summary = data.Summary;
            issue.Description = description;

            if (issue.Assignee != null)
            {
                issue.Assignee = _options.JiraUserName;
            }

            issue.CustomFields.Add("TestSuite Category", "Smoke");
            issue.CustomFields.Add("Product Feature(s) covered",
                new[]
                {
                    $"{{\"label\":\"{faIssue.Key}: {faIssue.Summary}\",\"value\":\"{faIssue.Key}: {faIssue.Summary}\"}}"
                });

            await issue.SaveChangesAsync();
            Console.WriteLine("done");

            // Add link to Attribute
            await AddAttributeName(issue, attributeName);

            // Add link to dependent tickets
            var r = new Regex(_options.JiraProjectName + @"-\d+");
            var dependentTicketKeys = r.Matches(description)
                .Select(m => m.Value)
                .Distinct();

            foreach (var key in dependentTicketKeys)
            {
                Console.Write($"Adding link to {key}... ");
                await _jira.Links.CreateLinkAsync(key, issue.Key.ToString(), "Depends On", null);
                Console.WriteLine("done");
            }

            if (issue.Status == "Open")
            {
                Console.Write("Move to Definition state... ");
                await issue.WorkflowTransitionAsync("E2E Definition");
                Console.WriteLine("done");
            }

            //if (sendToReview)
            //{
            //    Console.Write("Move to Review state... ");
            //    await issue.WorkflowTransitionAsync("Send to E2E Review");
            //    Console.WriteLine("done");
            //}

            //var faSafeName = faIssue.Summary.Replace(":", string.Empty).Replace("&", "_");
            //var folderName = "c:\\projects\\E2Es\\" + issue.Key + " - " + faSafeName + " - " + attributeName;
            //Directory.CreateDirectory(folderName);
            //File.Copy(YamlHelper.LastFileName, Path.Combine(folderName, "data.yaml"));
            //File.WriteAllText(Path.Combine(folderName, "description.txt"), description);

            Console.WriteLine();
            Console.WriteLine("Auto-check:");

            var checker = new E2ETicketChecker(_jira, _options);
            await checker.Check(data.Ticket);

            Console.Write("Open ticket? [Y/n]: ");
            var answer = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(answer) || answer.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                OpenLink(data.Ticket);
            }
        }

        private void OpenLink(string qbLink)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = "/c start " + qbLink,
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", qbLink);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", qbLink);
            }
        }

        private async Task<Issue> CreateNewIssue(YamlData data, string description)
        {
            Console.Write("Creating new ticket... ");
            var newIssue = _jira.CreateIssue(_options.JiraProjectName);
            newIssue.Summary = data.Summary;
            newIssue.Type = "End-to-end Test";
            newIssue.Description = description;
            newIssue.CustomFields.Add("TestSuite Category", "Smoke");
            await newIssue.SaveChangesAsync();
            Console.WriteLine("done");
            return newIssue;
        }

        private async Task AddAttributeName(Issue newIssue, string attributeName)
        {
            Console.Write("Adding Attribute link... ");
            var attrKey = _options.Attributes[attributeName];
            await newIssue.LinkToIssueAsync(attrKey, "Functional Area Coverage");
            Console.WriteLine("done");
        }

        private (string, string) ParseTicketSummary(string engProblemSummary)
        {
            var r = new Regex($@"Create ACC E2E scenario for FA ({_options.JiraProjectName}-\d+) - (Accessible|Consistent|Fast|Secure|Traceable)");
            var m = r.Match(engProblemSummary);

            if (!m.Success)
            {
                throw new E2ECheckerException("Can't parse ticket summary");
            }

            var faKey = m.Groups[1].Value;
            var attributeName = m.Groups[2].Value;

            return (faKey, attributeName);
        }
    }
}