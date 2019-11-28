using System;
using System.Text.RegularExpressions;

namespace E2ETools
{
    public class JiraHelper
    {
        public static string GetIssueKey(string url, string projectName)
        {
            var jiraUrlRegex = new Regex(projectName + @"-\d+$");
            var match = jiraUrlRegex.Match(url);
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || !match.Success)
            {
                throw new E2ECheckerException("Unknown jira ticket");
            }

            return match.Value;
        }

        public static void CheckUrl(string url, string projectName)
        {
            var jiraUrlRegex = new Regex(projectName + @"-\d+$");
            var match = jiraUrlRegex.Match(url);
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || !match.Success)
            {
                throw new E2ECheckerException("Unknown jira ticket");
            }
        }
    }
}