param (
    [string]$path
)

$userHome = "C:\Users\sehwan"

$testingPath = "C:\WorkDir\AndroZoo\data\$($Path)"
$Global:testingOutPath = "C:\WorkDir\AndroZoo\$($Path)"

if (!(Test-Path $testingOutPath -PathType Container)) {
    New-Item -ItemType Directory -Path $testingOutPath -Force | Out-Null
} 

$pythonPath = "$userHome\AppData\Local\Programs\Python\Python311\python.exe"
$Global:dllCnt = 0
$Global:status = 0
$Global:total = 0
function ExtractSolution(){
    $apkPath = $Global:apkDirPath
    $ilspycmdPath = "$userHome\.dotnet\tools\ilspycmd.exe"
    $outputPath = "$apkPath\output"  
    $pyLogPath = "$apkPath\output\pythonLog.txt"
    $ilspyLogPath = "$apkPath\output\ilspyLog.txt"
    
    if (-not (Test-Path $outputPath)) {
        New-Item -ItemType Directory -Path $outputPath | Out-Null
    }

    $pyScriptPath = "C:\WorkDir\Xamarin-TaintAnalyzer\ExtractNormalDlls\main.py"
    Write-Output "`n[INFO][$(Get-Date)][$Global:status/$Global:total] Starting excute ""$pyScriptPath"""

    $process = Start-Process $pythonPath "$pyScriptPath $apkPath" -PassThru -WindowStyle Hidden -Wait   -RedirectStandardOutput $pyLogPath

    if (!$process.HasExited) {
        Stop-Process $process
    }
    Write-Output "[INFO][$(Get-Date)][$Global:status/$Global:total] Finished excute ""$pyScriptPath"""

    ## Decompile dlls\
    
    $dllPath = "$apkPath\dlls"
    $decompilePath = $apkPath + "\decompile"

    $dLLFiles = Get-ChildItem -Path $dllPath -Filter *.dll  
    $dllFilePathArray = $dLLFiles | ForEach-Object {$_.FullName}
    $Global:dllCnt = $dllFiles.Length

    Write-Output "`n[INFO][$(Get-Date)][$Global:status/$Global:total] Starting decompile $Global:dllCnt dlls..."
    if (-not (Test-Path "$decompilePath\decompile.sln")) {    
        $process = Start-Process $ilspycmdPath "$dllFilePathArray -o $decompilePath -p" -PassThru -WindowStyle Hidden -Wait -RedirectStandardOutput $ilspyLogPath 
        
        if (!$process.HasExited) {
            Stop-Process $process
        }
        Write-Output "[INFO][$(Get-Date)][$Global:status/$Global:total] Finished make solution file.. ""$decompilePath\decompile.sln""."
    }
    else{
        Write-Output "[WARNING][$(Get-Date)][$Global:status/$Global:total] Already exist solution file.. ""$decompilePath\decompile.sln""."
    }
}

function AnalyzeSolution(){
    $apkPath = $Global:apkDirPath
    $exePath = "C:\WorkDir\Xamarin-TaintAnalyzer\CSHARP-Analyzer\CSHARP-Analyzer\bin\Release\net472\Rosyln-Analysis.exe"

    $slnPath = "$apkPath\decompile\decompile.sln"
    $outputPath = "$apkPath\output"  
    $configurePath = "C:\WorkDir\Xamarin-TaintAnalyzer\Configure"

    $dotnetLogPath = "$apkPath\output\dotnetLog.txt"
    $dotnetErrorPath = "$apkPath\output\dotnetError.txt"
    $graphLogPath = "$apkPath\output\graphLog.txt"

    Write-Output "`n[INFO][$(Get-Date)][$Global:status/$Global:total] Starting excution ""$exePath"".."
    if (-not (Test-Path $outputPath)) {
        New-Item -ItemType Directory -Path $outputPath | Out-Null
    }
   # Write-Output "-sln $slnPath -out $outputPath -conf $configurePath"
    $process = Start-Process $exePath "-sln $slnPath -out $outputPath -conf $configurePath  -t 900" -PassThru -WindowStyle Hidden -RedirectStandardOutput $dotnetLogPath  -RedirectStandardError $dotnetErrorPath -Wait

    $pyScriptPath = "C:\WorkDir\Xamarin-TaintAnalyzer\Merge-Callgraph\main.py"
    Write-Output "`n[INFO][$(Get-Date)][$Global:status/$Global:total] Starting execute ""$pyScriptPath"".."
    $jsonPath = "$outputPath\roslyn-tainted-callgraph.json"
    $pngPath = "$outputPath\roslyn-tainted-callgraph.png"
    $process = Start-Process $pythonPath "$pyScriptPath $jsonPath $pngPath " -PassThru -WindowStyle Hidden -Wait -RedirectStandardOutput $graphLogPath
  
    Write-Output "[INFO][$(Get-Date)][$Global:status/$Global:total] Finished excution ""$pyScriptPath""."
}

function CollectLog(){
    $apkPath = $Global:apkDirPath
    $pyScriptPath = "C:\WorkDir\Xamarin-TaintAnalyzer\LogCollector\main.py"
    
    $pyLogPath = "$apkPath\output\pythonLog.txt"
    $pyErrPath = "$apkPath\output\pythonError.txt"
    Write-Output "`n[INFO][$Global:status/$Global:total] Starting excution ""$pyScriptPath"""
    $process = Start-Process $pythonPath "$pyScriptPath $apkPath $Global:dllCnt result_$path.csv" -WindowStyle Hidden -PassThru -RedirectStandardOutput $pyLogPath  -RedirectStandardError $pyErrPath -Wait
    Write-Output "[INFO][$Global:status/$Global:total] Finished excution ""$pyScriptPath"""

}

function UnzipApkFile($apkPath) {
    $fileName = [System.IO.Path]::GetFileName($apkPath)
    $fileNameWithoutExtension = [System.IO.Path]::GetFileNameWithoutExtension($fileName)


    $unzipPath = "$Global:testingOutPath\$fileNameWithoutExtension"

    Write-Output "`n[INFO][$(Get-Date)][$Global:status/$Global:total] Starting unzip apk file ""$apkPath""."
    Start-Process bz "x -y $apkPath $unzipPath"  -WindowStyle Hidden -Wait
    Write-Output "[INFO][$(Get-Date)][$Global:status/$Global:total] Finished unzip apk file."
}

function RemoveExtraFiles() {
    $apkPath = $Global:apkDirPath
    $excludeFolder = "output"
    
    Get-ChildItem -Path $apkPath -Exclude $excludeFolder | ForEach-Object {
        if ($_.PSIsContainer) {
            Remove-Item -Path $_.FullName -Recurse -Force
        } else {
            Remove-Item -Path $_.FullName -Force
        }
    }
}
function RunFlowDroid($apkPath){
    $jarPath = "C:/WorkDir/Xamarin-TaintAnalyzer/JAVA-Analyzer/out/artifacts/java_analyzer_jar/java_analyzer.jar"
    $outputDir ="$Global:apkDirPath\output"
    $logPath1 = "$outputDir\FlowDroidLog1.txt"
    $logPath2 = "$outputDir\FlowDroidLog2.txt"
    Write-Output  "-cp $jarPath java_analyzer.Main --output $outputDir --ss C:\WorkDir\Xamarin-TaintAnalyzer\Configure\SourcesAndSinks.txt --apk $apkPath --timeout 1800"

    Write-Output "`n[INFO][$(Get-Date)][$Global:status/$Global:total] Starting excution FlowDroid $apkPath."
    $process = Start-Process java "-cp $jarPath java_analyzer.Main --output $outputDir --ss C:\WorkDir\Xamarin-TaintAnalyzer\Configure\Java_SourcesAndSinks.txt --apk $apkPath --timeout 1800" -WindowStyle Hidden -PassThru  -RedirectStandardOutput $logPath1  -RedirectStandardError $logPath2 -Wait
    
    Write-Output "[INFO][$(Get-Date)][$Global:status/$Global:total] Finished excution FlowDroid."
}
function MoveDoneAPK($apkPath){
    $dstPath = Join-Path -Path $donePath -ChildPath $apkPath
    Move-Item -Path $apkPath.FullName -Destination $dstPath
    Write-Output "`n[INFO][$(Get-Date)][$Global:status/$Global:total] Move finished apk file '$($apkPath.FullName)'to '$dstPath'."
}

#### MAIN ####
$apkPaths = Get-ChildItem -Path $testingPath -Filter *.apk
$Global:total = $apkPaths.Length
Write-Output "[MAIN-INFO] Target is $Global:total Apks"

$donePath = "C:\WorkDir\AndroZoo\data\$($Path)_done"
if (-not (Test-Path $donePath)) {
    New-Item -ItemType Directory -Path $donePath
}

foreach($apkPath in $apkPaths){
    $fileName = [System.IO.Path]::GetFileName($apkPath)
    $fileNameWithoutExtension = [System.IO.Path]::GetFileNameWithoutExtension($fileName)

    $Global:apkDirPath = "$Global:testingOutPath\$fileNameWithoutExtension"
    UnzipApkFile($apkPath.FullName)

    Write-Output "`n[INFO][$(Get-Date)][$Global:status/$Global:total] Starting Csharp Taint Analysis about ""$apkPath""."
    ExtractSolution;
    AnalyzeSolution;
    RunFlowDroid($apkPath.FullName)
    CollectLog;
    RemoveExtraFiles; 
    MoveDoneAPK($apkPath)
    $Global:status += 1
    Write-Output "`n=================================================================================================================================================`n."
}