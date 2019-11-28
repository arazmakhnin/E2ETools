using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Atlassian.Jira;

namespace E2ETools
{
    public class E2ETicketChecker
    {
        private readonly Jira _jira;
        private readonly Options _options;

        public E2ETicketChecker(Jira jira, Options options)
        {
            _jira = jira;
            _options = options;
        }

        public async Task Check(string ticketUrl)
        {
            var issueKey = JiraHelper.GetIssueKey(ticketUrl, _options.JiraProjectName);

            Console.Write("Getting ticket... ");
            var issue = await _jira.Issues.GetIssueAsync(issueKey);
            Console.WriteLine("done");

            if (issue.Type != "End-to-end Test")
            {
                throw new E2ECheckerException($"Ticket type should be \"End-to-end Test\", but \"{issue.Type}\" found");
            }

            Console.Write("Getting links... ");
            var links = await _jira.Links.GetLinksForIssueAsync(issueKey, CancellationToken.None);
            Console.WriteLine("done");

            var messages = new List<string>();
            CheckDescription(issue.Description, messages);

            // todo : check "Product Feature(s) covered"
            //var productFeatures = issue.CustomFields["Product Feature(s) covered"].Values;

            const string testSuiteCategory = "TestSuite Category";
            if (issue.CustomFields.All(f => f.Name != testSuiteCategory) ||
                string.Join("", issue.CustomFields[testSuiteCategory].Values) != "Smoke" &&
                string.Join("", issue.CustomFields[testSuiteCategory].Values) != "Regression")
            {
                messages.Add($"{testSuiteCategory} should be \"Smoke\" or \"Regression\"");
            }

            var r = new Regex(_options.JiraProjectName + @"-\d+");
            var dependentIssues = r.Matches(issue.Description)
                .Select(m => m.Value)
                .Distinct()
                .ToDictionary(m => m, m => false);

            var attributeCount = 0;
            var functionalAreaCount = 0;
            foreach (var link in links)
            {
                var linkIssue = link.InwardIssue.Key == issueKey ? link.OutwardIssue : link.InwardIssue;
                var linkRelation = GetLinkRelation(link, issueKey);

                if (linkIssue.Type == "Attribute")
                {
                    attributeCount++;
                    if (linkRelation != "covers")
                    {
                        messages.Add($"Attribute \"{linkIssue.Key}\" should be linked with relation \"covers\", but is linked with \"{linkRelation}\"");
                    }
                }

                if (linkIssue.Type == "Functional Area")
                {
                    functionalAreaCount++;
                    if (linkRelation != "covers")
                    {
                        messages.Add($"Functional area \"{linkIssue.Key}\" should be linked with relation \"covers\", but is linked with \"{linkRelation}\"");
                    }
                }

                if (linkIssue.Type == "End-to-end Test")
                {
                    if (linkRelation != "depends on")
                    {
                        messages.Add($"E2E {linkIssue.Key} should be linked with relation \"depends on\", but is linked with \"{linkRelation}\"");
                    }

                    if (!dependentIssues.ContainsKey(linkIssue.Key.ToString()))
                    {
                        messages.Add($"Ticket is linked with \"{linkIssue.Key}\", but it isn't mentioned in description");
                    }
                    else
                    {
                        dependentIssues[linkIssue.Key.ToString()] = true;
                    }
                }
            }

            foreach (var dependKey in dependentIssues.Where(p => !p.Value).Select(p => p.Key))
            {
                messages.Add($"Ticket \"{dependKey}\" is mentioned in description, but isn't linked to the current ticket");
            }
            
            Console.Write("Attributes linked: ");
            WriteCount(attributeCount);

            Console.Write("Functional Areas linked: ");
            WriteCount(functionalAreaCount);

            if (attributeCount == 0 || functionalAreaCount == 0 || messages.Count > 0)
            {
                ConsoleHelper.WriteLineColor("Failed", ConsoleColor.Red);
                foreach (var message in messages)
                {
                    ConsoleHelper.WriteLineColor($" - {message}", ConsoleColor.Red);
                }
            }
            else
            {
                ConsoleHelper.WriteLineColor("Passed", ConsoleColor.Green);
            }
        }

        private static string GetLinkRelation(IssueLink link, string issueKey)
        {
            if (link.InwardIssue.Key == issueKey)
            {
                return link.LinkType.Outward;
            }
            else if (link.OutwardIssue.Key == issueKey)
            {
                return link.LinkType.Inward;
            }
            else
            {
                throw new E2ECheckerException("Unknown link");
            }
        }

        private static void WriteCount(int count)
        {
            if (count == 1)
            {
                ConsoleHelper.WriteLineColor(count.ToString(), ConsoleColor.Green);
            }
            else
            {
                ConsoleHelper.WriteLineColor("0", ConsoleColor.Red);
            }
        }

        private void CheckDescription(string description, List<string> messages)
        {
            var lines = description.Trim().Split(new[]
            {
                '\r',
                '\n'
            }, StringSplitOptions.RemoveEmptyEntries);

            if (!lines[0].Equals("h2. Business Goal", StringComparison.CurrentCultureIgnoreCase))
            {
                messages.Add("Description should start with \"Business goal\"");
            }

            var businessGoal = lines.First(l => l.TrimStart().StartsWith("|")).TrimStart('|').TrimStart();
            var r = new Regex(_options.BusinessGoalRegex);
            if (!r.IsMatch(businessGoal))
            {
                messages.Add("Business goal should regard QB rules");
            }

            var steps = lines
                .Select(l => l.Trim())
                .SkipWhile(l => !l.Replace(" ", "").StartsWith("||1|") && !l.Replace(" ", "").StartsWith("|1|"))
                .ToArray();

            var i = 0;
            var shouldBeNewStep = true;
            foreach (var step in steps)
            {
                if (shouldBeNewStep)
                {
                    i++;

                    if (!step.StartsWith("||"))
                    {
                        messages.Add($"Step {i} expected, but regular line found");
                        break;
                    }

                    if (!step.StartsWith($"||{i}|"))
                    {
                        var stepNumber = step.Split('|', StringSplitOptions.RemoveEmptyEntries).First();
                        messages.Add($"Wrong numeration: expected step {i}, but step {stepNumber} found");
                        break;
                    }
                }
                else
                {
                    if (step.StartsWith("||"))
                    {
                        var stepNumber = step.Split('|', StringSplitOptions.RemoveEmptyEntries).First();
                        messages.Add($"Regular line expected for the step {i}, but step {stepNumber} found");
                        break;
                    }
                }

                shouldBeNewStep = step.EndsWith("|");
            }
        }
    }
}