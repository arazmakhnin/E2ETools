using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using C4Check;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace E2ETools
{
    public static class YamlHelper
    {
        public static YamlData ReadYamlData(Options options)
        {
            string yamlFile;
            if (string.IsNullOrWhiteSpace(options.YamlFile))
            {
                Console.Write("Enter YAML file path: ");
                yamlFile = Console.ReadLine() ?? string.Empty;
            }
            else
            {
                yamlFile = options.YamlFile;
            }

            if (!File.Exists(yamlFile))
            {
                if (!File.Exists(AppFolderHelper.GetFile(yamlFile)))
                {
                    throw new E2ECheckerException($"File \"{yamlFile}\" doesn't exist");
                }
                else
                {
                    yamlFile = AppFolderHelper.GetFile(yamlFile);
                }
            }

            var data = ReadData(yamlFile);

            if (string.IsNullOrWhiteSpace(data.Summary))
            {
                throw new E2ECheckerException("Summary can't be empty");
            }

            if (string.IsNullOrWhiteSpace(data.BusinessGoal))
            {
                throw new E2ECheckerException("Business goal can't be empty");
            }

            if (string.IsNullOrWhiteSpace(data.Ticket))
            {
                throw new E2ECheckerException("Ticket can't be empty");
            }

            return data;
        }

        private static YamlData ReadData(string yamlFile)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            using (var reader = File.OpenText(yamlFile))
            {
                return deserializer.Deserialize<YamlData>(reader);
            }
        }

        public static string GenerateDescription(YamlData data)
        {
            var builder = new StringBuilder()

                .AppendLine("h2. Business Goal")
                .AppendLine()
                .AppendLine($"| {data.BusinessGoal} |")

                .AppendLine("h2. Pre-Conditions")
                .AppendLine()
                .AppendLine("||Pre-condition Item||Pre-condition information||Reference links||");

            AddSection(data.Preconditions.Environment, builder, "Environment");
            AddSection(data.Preconditions.UserCredentials, builder, "User credentials");
            AddSection(data.Preconditions.SystemSettings, builder, "System settings");
            AddSection(data.Preconditions.ApplicationConfiguration, builder, "Application configuration");
            AddSection(data.Preconditions.DataPrerequisites, builder, "Data prerequisites");

            builder.AppendLine("h2. Scenario")
                .AppendLine()
                .AppendLine("||Seq#||User Interaction sequence||Expected outcome||");

            var stepNumber = 0;
            foreach (var step in data.Steps)
            {
                stepNumber++;

                if (step.Count != 2)
                {
                    throw new E2ECheckerException($"There should be 2 items in \"step {stepNumber}\" section");
                }

                if (step[0].Contains("less then", StringComparison.InvariantCultureIgnoreCase) ||
                    step[1].Contains("less then", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new E2ECheckerException($"Typo \"less then\" found in the step {stepNumber}");
                }

                builder.AppendLine($"||{stepNumber}| {step[0].Trim()} | {step[1].Trim()} |");
            }

            return builder.ToString();
        }

        private static void AddSection(IList<object> dataSection, StringBuilder builder, string description)
        {
            if (dataSection == null || !dataSection.Any())
            {
                AddDataItem(new []{ "N/A", "N/A" }, builder, description);
                return;
            }

            var isMultipleData = dataSection[0] is IList<object>;

            IEnumerable<object> dataItem;
            if (isMultipleData)
            {
                dataItem = dataSection[0] as IEnumerable<object>;
            }
            else
            {
                dataItem = dataSection;
            }

            AddDataItem(dataItem, builder, description);

            if (isMultipleData)
            {
                foreach (var additionalDataItem in dataSection.Skip(1))
                {
                    AddDataItem(additionalDataItem as IList<object>, builder, " ");
                }
            }
        }

        private static void AddDataItem(IEnumerable<object> dataItem, StringBuilder builder, string description)
        {
            var array = dataItem
                .Cast<string>()
                .ToArray();

            if (array.Length != 2)
            {
                throw new E2ECheckerException("There should be 2 items in \"data\" section");
            }

            builder.AppendLine($"||{description}| {array[0].Trim()} | {array[1].Trim()} |");
        }
    }

    public class YamlData
    {
        public string Attribute { get; set; }
        public string Ticket { get; set; }
        public string Summary { get; set; }
        public string BusinessGoal { get; set; }
        public Preconditions Preconditions { get; set; }
        public ICollection<IList<string>> Steps { get; set; }
    }

    public class Preconditions
    {
        public IList<object> DataPrerequisites { get; set; }
        public IList<object> Environment { get; set; }
        public IList<object> UserCredentials { get; set; }
        public IList<object> SystemSettings { get; set; }
        public IList<object> ApplicationConfiguration { get; set; }
    }
}