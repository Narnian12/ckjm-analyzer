using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CKJMAnalyzer
{
   class Program
   {
      static async Task Main(string[] args)
      {
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
         csv.AppendLine("Project,DI,MAI,DIW-MAI,LOC,CBO,DIW-CBO,DAM,MOA,DIT,MFA");

         foreach (var project in projects)
         {
            Console.WriteLine($"Analyzing project {currentProject++} of {totalProjects}");
            // Reset lists and dictionaries
            Initialize();
            // Path for specific path within `projects` folder
            var projectPath = projectBasePath + project.Split("\\").Last();

            var classFiles = Directory.EnumerateFiles(projectPath, "*.class", SearchOption.AllDirectories).ToList();
            File.WriteAllText("fileNames.txt", string.Join("\n", classFiles));
            
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

            // Compute maintainability
            var maintainability = ComputeMaintainability(MetricTotals);
            var diMaintainability = ComputeMaintainability(MetricTotals, true);
            var diProportion = MetricTotals["DI_PARAMS"].Accumulator / MetricTotals["TOTAL_PARAMS"].Accumulator;

            var newLine =
               $"{project.Split("\\").Last()},{diProportion.ToString(CultureInfo.InvariantCulture)},{maintainability.ToString(CultureInfo.InvariantCulture)},{diMaintainability.ToString(CultureInfo.InvariantCulture)},{MetricTotals["LOC"].Accumulator.ToString(CultureInfo.InvariantCulture)},{MetricTotals["CBO"].Mean.ToString(CultureInfo.InvariantCulture)},{MetricTotals["DIW_CBO"].Mean.ToString(CultureInfo.InvariantCulture)},{MetricTotals["DAM"].Mean.ToString(CultureInfo.InvariantCulture)},{MetricTotals["MOA"].Mean.ToString(CultureInfo.InvariantCulture)},{MetricTotals["DIT"].Mean.ToString(CultureInfo.InvariantCulture)},{MetricTotals["MFA"].Mean.ToString(CultureInfo.InvariantCulture)}";

            csv.AppendLine(newLine);
         }
         
         await File.WriteAllTextAsync("metric_output.csv", csv.ToString());
      }

      public static Dictionary<string, MetricData> ClassNameToMetricData { get; set; } = new();
      public static List<string> ClassNames { get; set; } = new();
      public static Dictionary<string, Metric> MetricTotals { get; set; } = new();

      public static void Initialize()
      {
         ClassNameToMetricData.Clear();
         ClassNames.Clear();
         MetricTotals.Clear();
         MetricTotals["CBO"] = new Metric("CBO");
         MetricTotals["LOC"] = new Metric("LOC");
         MetricTotals["DIW_CBO"] = new Metric("DIW_CBO");
         MetricTotals["DAM"] = new Metric("DAM");
         MetricTotals["MOA"] = new Metric("MOA");
         MetricTotals["DIT"] = new Metric("DIT");
         MetricTotals["MFA"] = new Metric("MFA");
         MetricTotals["DI_PARAMS"] = new Metric("DI_PARAMS");
         MetricTotals["TOTAL_PARAMS"] = new Metric("TOTAL_PARAMS");
      }

      public static void AccumulateMetrics(MetricData data)
      {
         MetricTotals["CBO"].Add(data.MetricNameValue["CBO"]);
         MetricTotals["LOC"].Add(data.MetricNameValue["LOC"]);
         MetricTotals["DIW_CBO"].Add(data.DiwCbo);
         MetricTotals["DAM"].Add(data.MetricNameValue["DAM"]);
         MetricTotals["MOA"].Add(data.MetricNameValue["MOA"]);
         MetricTotals["DIT"].Add(data.MetricNameValue["DIT"]);
         MetricTotals["MFA"].Add(data.MetricNameValue["MFA"]);
         MetricTotals["DI_PARAMS"].Add(data.DiParams);
         MetricTotals["TOTAL_PARAMS"].Add(data.MethodParams.Count);
      }

      public static double ComputeMaintainability(Dictionary<string, Metric> metricTotals, bool usingDI = false)
      {
         double meanCbo = usingDI ? metricTotals["DIW_CBO"].Mean : metricTotals["CBO"].Mean;
         return (0.5 * ((0.25 * metricTotals["DAM"].Mean) - (0.25 * meanCbo) + (0.5 * metricTotals["MOA"].Mean))) +
                (0.5 * ((0.5 * metricTotals["DIT"].Mean) - (0.5 * meanCbo) + (0.5 * metricTotals["MFA"].Mean)));
      }

      public class Metric
      {
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
      
         public string MetricName { get; set; }
         public double Accumulator { get; set; }
         public double Count { get; set; }
         public double Mean { get; set; }
      }

      public class MetricData
      {
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
            DiwCbo = MetricNameValue["CBO"] - (nonPrimitiveParams.Count / 2.0);
            DiParams = nonPrimitiveParams.Count;
         }

         public Dictionary<string, double> MetricNameValue { get; set; } = new();
         public double DiwCbo { get; set; }
         public double DiParams { get; set; }
         public List<string> MethodParams { get; set; } = new();
      }
   }
}