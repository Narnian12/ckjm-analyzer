using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommandLine;

// using System.Xml.Linq;

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
         csv.AppendLine("Project,DI,MAI,DMAI,LOC,CBO,DCBO,LCOM,RFC,NCBO,NDCBO,NLCOM,NRFC");

         foreach (var project in projects)
         {
            Console.WriteLine($"Analyzing project {currentProject++} of {totalProjects}");
            // Reset lists and dictionaries
            Initialize();
            // Path for specific path within `projects` folder
            var projectPath = projectBasePath + project.Split("\\").Last();

            // var xmlFiles = Directory.EnumerateFiles(projectPath, "*.xml", SearchOption.AllDirectories).ToList();
            // foreach (var xmlFile in xmlFiles)
            // {
            //    var xml = XElement.Load(xmlFile);
            //    var descendants = xml.Descendants();
            //    foreach (var descendant in descendants)
            //    {
            //       if (descendant.Name.ToString().Contains("bean"))
            //       {
            //          var beanAttributes = descendant.Attributes();
            //       }
            //    }
            // }
            
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
                  if (analysisType == "params")
                  {
                     ClassNameToMetricData[className].AddParams(line);
                  }
                  else if (analysisType == "metrics")
                  {
                     ClassNameToMetricData[className].ConsumeMetrics(line);
                  }
                  else if (analysisType == "methods")
                  {
                     NumberOfMethods += int.Parse(line[0]);
                  }
               }
            }
            
            // Analyze DiwCbo using classNames list
            foreach (var metricData in ClassNameToMetricData.Values)
            {
               metricData.AnalyzeDependencyInjection(ClassNames);
               AccumulateMetrics(metricData);
            }

            foreach (var metric in MetricTotals.Values)
            {
               metric.ComputeMean();
            }
            
            var diProportion = MetricTotals["CBO"].Accumulator == 0
               ? 0
               : MetricTotals["DI_PARAMS"].Accumulator * 2 / MetricTotals["CBO"].Accumulator;

            // Normalize CBO using module complexity (CM) equation CM = 1 - (1 / (1 + IS)), where IS is coupling complexity
            var normalizedCBO = 1 - (1 / (1 + MetricTotals["CBO"].Mean));
            var normalizedDCBO = 1 - (1 / (1 + MetricTotals["DCBO"].Mean));
            // Normalize LCOM with bestfit
            var normalizedLCOM = MetricTotals["LCOM"].Mean == 0 ? 0 : 1.0 / MetricTotals["LCOM"].Mean;
            // Normalize RFC using module complexity (CM) equation above
            var normalizedRFC = 1 - (1 / (1 + MetricTotals["RFC"].Mean));

            // Compute maintainability
            var maintainability = ComputeMaintainability(normalizedCBO, normalizedLCOM, normalizedRFC);
            var diMaintainability = ComputeMaintainability(normalizedDCBO, normalizedLCOM, normalizedRFC);

            var newLine =
               $"{project.Split("\\").Last()},{diProportion.ToString(CultureInfo.InvariantCulture)},{maintainability.ToString(CultureInfo.InvariantCulture)},{diMaintainability.ToString(CultureInfo.InvariantCulture)},{MetricTotals["LOC"].Accumulator.ToString(CultureInfo.InvariantCulture)},{MetricTotals["CBO"].Mean.ToString(CultureInfo.InvariantCulture)},{MetricTotals["DCBO"].Mean.ToString(CultureInfo.InvariantCulture)},{MetricTotals["LCOM"].Mean.ToString(CultureInfo.InvariantCulture)},{MetricTotals["RFC"].Mean.ToString(CultureInfo.InvariantCulture)},{normalizedCBO.ToString(CultureInfo.InvariantCulture)},{normalizedDCBO.ToString(CultureInfo.InvariantCulture)},{normalizedLCOM.ToString(CultureInfo.InvariantCulture)},{normalizedRFC.ToString(CultureInfo.InvariantCulture)}";

            csv.AppendLine(newLine);
         }
         
         await File.WriteAllTextAsync("metric_output.csv", csv.ToString());
      }

      public static Dictionary<string, MetricData> ClassNameToMetricData { get; set; } = new();
      public static List<string> ClassNames { get; set; } = new();
      public static Dictionary<string, Metric> MetricTotals { get; set; } = new();
      public static double NumberOfMethods { get; set; } = 0;

      public static void Initialize()
      {
         ClassNameToMetricData.Clear();
         ClassNames.Clear();
         MetricTotals.Clear();
         NumberOfMethods = 0;
         MetricTotals["CBO"] = new Metric("CBO");
         MetricTotals["LOC"] = new Metric("LOC");
         MetricTotals["DCBO"] = new Metric("DCBO");
         MetricTotals["LCOM"] = new Metric("LCOM");
         MetricTotals["RFC"] = new Metric("RFC");
         MetricTotals["DI_PARAMS"] = new Metric("DI_PARAMS");
         MetricTotals["TOTAL_PARAMS"] = new Metric("TOTAL_PARAMS");
      }

      public static void AccumulateMetrics(MetricData data)
      {
         MetricTotals["CBO"].Add(data.MetricNameValue["CBO"]);
         MetricTotals["LOC"].Add(data.MetricNameValue["LOC"]);
         MetricTotals["DCBO"].Add(data.DiwCbo);
         MetricTotals["LCOM"].Add(data.MetricNameValue["LCOM"]);
         MetricTotals["RFC"].Add(data.MetricNameValue["RFC"]);
         MetricTotals["DI_PARAMS"].Add(data.DiParams);
         MetricTotals["TOTAL_PARAMS"].Add(data.MethodParams.Count);
      }

      public static double ComputeMaintainability(double cbo, double lcom, double rfc)
      {
         return 1 - (cbo / 3) - (lcom / 3) - (rfc / 3);
      }

      public class Metric
      {
         public string MetricName { get; set; }
         public double Accumulator { get; set; }
         public double Count { get; set; }
         public double Mean { get; set; }

         public Metric(string name)
         {
            MetricName = name;
            Accumulator = 0;
            Count = 0;
         }
      
         public void Add(double value)
         {
            Accumulator += value;
            ++Count;
         }

         public void ComputeMean()
         {
            Mean = Accumulator / Count;
         }
      }

      public class MetricData
      {
         public Dictionary<string, double> MetricNameValue { get; set; } = new();
         public double DiwCbo { get; set; }
         public double DiParams { get; set; }
         public List<string> MethodParams { get; set; } = new();

         public void AddParams(List<string> para)
         {
            MethodParams = para;
         }

         public void ConsumeMetrics(List<string> metrics)
         {
            // Weighted methods per class
            MetricNameValue["WMC_NOM"] = double.Parse(metrics[0]);
            // Depth of inheritance tree
            MetricNameValue["DIT"] = double.Parse(metrics[1]);
            // Number of children
            MetricNameValue["NOC"] = double.Parse(metrics[2]);
            // Coupling between objects
            MetricNameValue["CBO"] = double.Parse(metrics[3]);
            // Response for class
            MetricNameValue["RFC"] = double.Parse(metrics[4]);
            // Lack of cohesion in methods
            MetricNameValue["LCOM"] = double.Parse(metrics[5]);
            // Afferent couplings
            MetricNameValue["CA"] = double.Parse(metrics[6]);
            // Efferent couplings
            MetricNameValue["CE"] = double.Parse(metrics[7]);
            // Number of public methods
            MetricNameValue["NPM"] = double.Parse(metrics[8]);
            // Lack of cohesion in methods varying between 0 and 2
            MetricNameValue["LCOM3"] = double.Parse(metrics[9]);
            // Lines of code
            MetricNameValue["LOC"] = double.Parse(metrics[10]);
            // Data access metric
            MetricNameValue["DAM"] = double.Parse(metrics[11]);
            // Measure of aggregation
            MetricNameValue["MOA"] = double.Parse(metrics[12]);
            // Measure of functional abstraction
            MetricNameValue["MFA"] = double.Parse(metrics[13]);
            // Cohesion among methods of classes
            MetricNameValue["CAM"] = double.Parse(metrics[14]);
            // Inheritance coupling
            MetricNameValue["IC"] = double.Parse(metrics[15]);
            // Coupling between methods
            MetricNameValue["CBM"] = double.Parse(metrics[16]);
            // Average method complexity
            MetricNameValue["AMC"] = double.Parse(metrics[17]);
         }

         public void AnalyzeDependencyInjection(List<string> classNames)
         {
            // Intersect params with classNames
            var nonPrimitiveParams = MethodParams.Intersect(classNames).ToList();
            // Remove duplicates
            nonPrimitiveParams = nonPrimitiveParams.Distinct().ToList();
            DiwCbo = MetricNameValue["CBO"] - nonPrimitiveParams.Count;
            DiParams = nonPrimitiveParams.Count;
         }
      }
   }
}