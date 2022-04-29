using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace CKJMAnalyzer
{
   public class CKJMAnalyzerOptions
   {
      [Option('e', "extension", HelpText = "File extension to analyze with CKJM")]
      public string Extension { get; set; }
   }
}
