# Xades: Static Taint Analysis Framework for C# Applications
Xades is a static analysis framework designed for C# applications, specifically to detect sensitive data leakage in Xamarin apps.

---
# Getting started
Xades consists of three main modules: Code Extractor, C# Analyzer, and a modified version of FlowDroid.  
We evaluated Xades on a PC running Windows 10. To test Xades, you will need to build these modules.  
We also provide a PowerShell script for static analysis located at `/PowerShell/test.ps1`
Please note that you must modify the various paths defined as variables in the PowerShell script to match your environment.

## Code Extractor
We implemented the Code Extractor in Python using the well-known C# decompiler for the command line on Windows, [ilspycmd](https://www.nuget.org/packages/ilspycmd/).
Therefore, you need to download ilspycmd and modify the `$ilspycmdPath`

```
dotnet tool install --global ilspycmd --version 8.0.0.7345
```

## C# Analyzer
We implemented the C# analyzer on Visual Studio 2022.  
Before testing, you must build the `CSHARP-Analyzer.sln` and modify the `$exePath` for analyzer path on your PC.


## modified FlowDroid
We implemented the  modified FlowDroid in IntelliJ project with [FlowDroid](https://github.com/secure-software-engineering/FlowDroid/).  
Similar to FlowDroid's requirements, our requirements include the Android SDK files.
Before testing, you must build the `/JAVA-Analyzer` into an artifact with dependencies and modify the `$jarPath` to point to the analyzer path on your PC.  

# Script
After modifications to the PowerShell script, you can test Xades.
```
/PowerShell/test.ps1
```
---

