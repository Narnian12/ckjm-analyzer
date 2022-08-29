namespace CKJMAnalyzer.Models
{
   public class MetricData
   {
      public Dictionary<string, double> MetricNameValue { get; set; } = new();
      public double DCe { get; set; }
      public double DCbo { get; set; }
      public double DIParamCount { get; set; }
      public List<string> ParameterTypes { get; set; } = new();
      public string Interface { get; set; } = "";
      public List<string> EfferentCouplings { get; set; } = new();

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

      public void AnalyzeDependencyInjection(List<string> classNames, List<string> xmlInterfaces)
      {
         // Intersect params with classNames and remove duplicates - this is the sum of parameters injected
         var nonPrimitiveParams = ParameterTypes.Intersect(classNames).Distinct().ToList();

         // If class uses xml injection, count classes injected via XML bean injection
         List<string> xmlParams = new();
         if (EfferentCouplings.Any(ce => ce.Contains("springframework")))
         {
            xmlParams = EfferentCouplings.Intersect(xmlInterfaces).Distinct().ToList();
         }

         // Intersection of parameter injection and xml injection is total DI params, or DCe
         List<string> diParams = new();
         diParams = nonPrimitiveParams.Union(xmlParams).Distinct().ToList();
         DCe = MetricNameValue["CE"] - diParams.Count;
         DIParamCount = diParams.Count;
         
         // DCBO = Ca + DCe
         DCbo = MetricNameValue["CA"] + DCe;
      }
   }
}
