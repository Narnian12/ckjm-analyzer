namespace CKJMAnalyzer.Models
{
   public class Metric
   {
      public string MetricName { get; set; }
      public double Accumulator { get; set; }
      public double Count { get; set; }

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

      public double ComputeMean()
      {
         return Accumulator / Count;
      }
   }
}
