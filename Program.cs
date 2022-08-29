using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommandLine;
using System.Xml.Linq;
using CKJMAnalyzer.Models;

namespace CKJMAnalyzer
{
   class Program
   {
      static async Task Main(string[] args)
      {
         var options = default(CKJMAnalyzerOptions);
         Parser.Default.ParseArguments<CKJMAnalyzerOptions>(args)
            .WithParsed(parsedOptions =>
            {
               options = parsedOptions;
            });

         if (string.IsNullOrWhiteSpace(options?.Extension))
         {
            Console.WriteLine("Error: Must define an extension using -e and a valid extension, such as `class` or `jar`");
            return;
         }

         var fileExtension = "*." + options?.Extension;

         Console.WriteLine("CKJM Analyzer begin...");
         Process p = new Process();
         p.StartInfo.UseShellExecute = false;
         p.StartInfo.CreateNoWindow = true;
         p.StartInfo.RedirectStandardOutput = true;
         p.StartInfo.FileName = "ckjm_analysis.bat";

         var projectBasePath = ".\\projects\\";
         var projects = Directory.GetDirectories(Path.Combine(Environment.CurrentDirectory, "projects"));
         int totalProjects = projects.Length;
         int currentProject = 1;

         var csv = new StringBuilder();
         csv.AppendLine("Project,DI,LOC,CBO,NCBO,DCBO,NDCBO,CA,CE,DCE");

         foreach (var project in projects)
         {
            Console.WriteLine($"Analyzing project {currentProject++} of {totalProjects}");
            // Reset lists and dictionaries
            Initialize();
            // Path for specific path within `projects` folder
            var projectPath = projectBasePath + project.Split("\\").Last();

            // Accumulate Java beans XML DI
            var xmlFiles = Directory.EnumerateFiles(projectPath, "*.xml", SearchOption.AllDirectories).ToList();
            foreach ( var xmlFile in xmlFiles )
            {
               var xml = XElement.Load(xmlFile);
               var descendants = xml.Descendants();
               foreach ( var descendant in descendants )
               {
                  if ( descendant.Name.ToString().Contains( "bean" ) )
                  {
                     var beanAttributes = descendant.Attributes();
                     foreach (var attr in beanAttributes)
                     {
                        if (attr.Name.LocalName.ToLower().Equals("class"))
                        {
                           XmlConcreteClasses.Add(attr.Value);
                        }
                     }
                  }
               }
            }

            var sourceFiles = Directory.EnumerateFiles(projectPath, fileExtension, SearchOption.AllDirectories).ToList();
            File.WriteAllText("fileNames.txt", string.Join("\n", sourceFiles));
            
            p.Start();
            
            var ckjm_output = p.StandardOutput.ReadToEnd().Split("\r\n");
            // Loop through output, consume params and metrics
            foreach (var output_line in ckjm_output)
            {
               var line = output_line.Split(" ").ToList();
               if (line.First() == "ckjm-analyzer")
               {
                  // Remove "ckjm-analyzer" placeholder
                  line.RemoveAt(0);
                  // Get full class name
                  var className = line.First();
                  if (!ClassNameToMetricData.ContainsKey(className))
                  {
                     ClassNameToMetricData[className] = new MetricData();
                     ClassNames.Add(className);
                  }
                  line.RemoveAt(0);
                  var analysisType = line.First();
                  line.RemoveAt(0);
                  if (line.Count() == 0)
                  {
                     continue;
                  }
                  switch (analysisType)
                  {
                     case "parameter_types":
                        ClassNameToMetricData[className].ParameterTypes.AddRange(line);
                        break;
                     case "interfaces":
                        ClassNameToMetricData[className].Interface = line.First();
                        break;
                     case "efferent_couplings":
                        ClassNameToMetricData[className].EfferentCouplings.AddRange(line);
                        break;
                     case "metrics":
                        ClassNameToMetricData[className].ConsumeMetrics(line);
                        break;
                  }
               }
            }

            // Convert all concrete classes to interfaces in XML (efferent coupling will observe the interface, not the class)
            foreach (var xmlConcreteClass in XmlConcreteClasses)
            {
               if (ClassNameToMetricData.ContainsKey(xmlConcreteClass))
               {
                  XmlInterfaces.Add(ClassNameToMetricData[xmlConcreteClass].Interface);
               }
            }
            
            // Analyze DiwCbo using classNames list
            foreach (var metricData in ClassNameToMetricData.Values)
            {
               metricData.AnalyzeDependencyInjection(ClassNames, XmlInterfaces);
               AccumulateMetrics(metricData);
            }

            // DI proportion is the total couplings injected via DI divided by the total efferent couplings
            var diProportion = MetricTotals["CE"].Accumulator == 0
               ? 0
               : MetricTotals["DI_PARAMS"].Accumulator / MetricTotals["CE"].Accumulator;

            // Normalize CBO using module complexity (CM) equation CM = 1 - (1 / (1 + IS)), where IS is coupling complexity
            var normalizedCBO = 1 - (1 / (1 + MetricTotals["CBO"].ComputeMean()));
            var normalizedDCBO = 1 - (1 / (1 + MetricTotals["DCBO"].ComputeMean()));

            var newLine =
               $"{project.Split("\\").Last()},{diProportion.ToString(CultureInfo.InvariantCulture)},{MetricTotals["LOC"].Accumulator.ToString(CultureInfo.InvariantCulture)},{MetricTotals["CBO"].ComputeMean().ToString(CultureInfo.InvariantCulture)},{normalizedCBO.ToString(CultureInfo.InvariantCulture)},{MetricTotals["DCBO"].ComputeMean().ToString(CultureInfo.InvariantCulture)},{normalizedDCBO.ToString(CultureInfo.InvariantCulture)},{MetricTotals["CA"].ComputeMean().ToString(CultureInfo.InvariantCulture)},{MetricTotals["CE"].ComputeMean().ToString(CultureInfo.InvariantCulture)},{MetricTotals["DCE"].ComputeMean().ToString(CultureInfo.InvariantCulture)}";

            csv.AppendLine(newLine);
         }
         
         await File.WriteAllTextAsync("metric_output.csv", csv.ToString());
      }

      public static Dictionary<string, MetricData> ClassNameToMetricData { get; set; } = new();
      public static List<string> XmlConcreteClasses { get; set; } = new();
      public static List<string> XmlInterfaces { get; set; } = new();
      public static List<string> ClassNames { get; set; } = new();
      public static Dictionary<string, Metric> MetricTotals { get; set; } = new();

      public static void Initialize()
      {
         ClassNameToMetricData.Clear();
         ClassNames.Clear();
         MetricTotals.Clear();
         MetricTotals["CA"] = new Metric("CA");
         MetricTotals["CE"] = new Metric("CE");
         MetricTotals["DCE"] = new Metric("DCE");
         MetricTotals["CBO"] = new Metric("CBO");
         MetricTotals["LOC"] = new Metric("LOC");
         MetricTotals["DCBO"] = new Metric("DCBO");
         MetricTotals["LCOM"] = new Metric("LCOM");
         MetricTotals["RFC"] = new Metric("RFC");
         MetricTotals["DI_PARAMS"] = new Metric("DI_PARAMS");
      }

      public static void AccumulateMetrics(MetricData data)
      {
         MetricTotals["CA"].Add(data.MetricNameValue["CA"]);
         MetricTotals["CE"].Add(data.MetricNameValue["CE"]);
         MetricTotals["DCE"].Add(data.DCe);
         MetricTotals["CBO"].Add(data.MetricNameValue["CBO"]);
         MetricTotals["LOC"].Add(data.MetricNameValue["LOC"]);
         MetricTotals["DCBO"].Add(data.DCbo);
         MetricTotals["LCOM"].Add(data.MetricNameValue["LCOM"]);
         MetricTotals["RFC"].Add(data.MetricNameValue["RFC"]);
         MetricTotals["DI_PARAMS"].Add(data.DIParamCount);
      }
   }
}