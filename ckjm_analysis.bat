echo off
type fileNames.txt | java -jar ckjm-di.jar

Rem set arg1=%1
Rem dir %arg1% *.class /s /b | java -jar ckjm-di.jar
Rem java -jar ckjm-di.jar %arg1%