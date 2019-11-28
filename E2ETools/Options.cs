using System.Collections.Generic;

namespace E2ETools
{
    public class Options
    {
        public string JiraUserName { get; set; }
        public string JiraPassword { get; set; }
        public string JiraProjectName { get; set; }
        public string BusinessGoalRegex { get; set; }
        public string YamlFile { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
    }
}