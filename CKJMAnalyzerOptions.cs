using CommandLine;

namespace CKJMAnalyzer
{
   public class CKJMAnalyzerOptions
   {
      [Option('e', "extension", HelpText = "File extension to analyze with CKJM")]
      public string? Extension { get; set; }
   }
}
