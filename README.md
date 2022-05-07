# ckjm-analyzer

# How to Run:
* In the directory containing CKJM-Analyzer, add a "projects" directory containing subdirectories of Java projects.
  * The `ckjm-di-projects` repository contains directories starting with "projects..." that are properly structured for analysis. Rename the directory to "projects" and paste into the CKJM-Analyzer directory to run. In the paper, the "projects_exp" was run for experimental analysis, and "projects_class" and "projects_jar" were run for open-source analysis.
* Ensure your OS has Java installed.
* Add the `ckjm-di.jar` file from the CKJM-DI repository.
* Run CKJM-Analyzer.exe with a command line argument "-e" specifying a file extension to analyze. Currently supposed file extensions include "class" and "jar" extensions. A terminal window will display sohwing the analysis progression.
* Example command to run: `ckjm-analyzer.exe -e class`
* After the computation is complete, CKJM-Analyzer outputs a comma-separated values (CSV) file in the same relative folder to the application, containing aggregated averages of the metrics described.
