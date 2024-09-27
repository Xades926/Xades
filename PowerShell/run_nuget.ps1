$Global:fileFullName
$Global:folderPath

function AnalyzeSolution(){
    $exePath = "C:\WorkDir\Xamarin-TaintAnalyzer\CSHARP-Analyzer\CSHARP-Analyzer\bin\Release\net472\Rosyln-Analysis.exe"

    $slnPath = $Global:fileFullName
    $outputPath = $Global:folderPath  
    $configurePath = "C:\WorkDir\Xamarin-TaintAnalyzer\Configure"

    $dotnetLogPath = "$Global:folderPath\dotnetLog.txt"
    $dotnetErrorPath = "$Global:folderPath\dotnetError.txt"
    Write-Output "`n[INFO][$(Get-Date) Starting analyze ""$slnPath""."
    Write-Output "`n[INFO][$(Get-Date) Starting excution ""$exePath""."
    if (-not (Test-Path -Path $outputPath)) {
        New-Item -ItemType Directory -Path $outputPath | Out-Null
    }
   # Write-Output "-sln $slnPath -out $outputPath -conf $configurePath"
    $process = Start-Process $exePath "-sln $slnPath -out $outputPath -conf $configurePath  -t 900" -PassThru -WindowStyle Hidden -RedirectStandardOutput $dotnetLogPath  -RedirectStandardError $dotnetErrorPath -Wait

    Write-Output "[INFO][$(Get-Date)] Finished excution ""$exePath""."
}


$searchDir = "C:\WorkDir\nuger_repository"
$resultPath = "C:\WorkDir\Xamarin-TaintAnalyzer\PowerShell\nuget_repository"

$slnFiles = Get-ChildItem -Path $searchDir -Filter *.sln -Recurse
foreach ($file in $slnFiles) {
    $Global:fileFullName = $file.FullName
    $fileName = $file.Name

    # 파일명에서 확장자 제거
    $folderName = [System.IO.Path]::GetFileNameWithoutExtension($fileName)
    $Global:folderPath = "$resultPath\$folderName.output"

    AnalyzeSolution;
}