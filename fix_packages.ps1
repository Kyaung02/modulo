# Unity Package Fix Script
# Unity Editor를 완전히 종료한 후 실행하세요

$projectPath = "C:\Users\yanghm\Unity Project\modulo"

Write-Host "Unity Package Fix Script" -ForegroundColor Green
Write-Host "=========================" -ForegroundColor Green
Write-Host ""

# Unity Editor가 실행 중인지 확인
$unityProcesses = Get-Process -Name "Unity" -ErrorAction SilentlyContinue
if ($unityProcesses) {
    Write-Host "WARNING: Unity Editor가 실행 중입니다!" -ForegroundColor Red
    Write-Host "Unity Editor를 완전히 종료한 후 다시 실행하세요." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Unity Editor를 종료한 후 아무 키나 누르세요"
}

# 폴더 삭제
$foldersToDelete = @("Library", "Temp")

foreach ($folder in $foldersToDelete) {
    $fullPath = Join-Path $projectPath $folder
    if (Test-Path $fullPath) {
        Write-Host "Deleting: $folder" -ForegroundColor Yellow
        Remove-Item -Path $fullPath -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path $fullPath) {
            Write-Host "  Failed to delete $folder - may be locked by Unity" -ForegroundColor Red
        } else {
            Write-Host "  Successfully deleted $folder" -ForegroundColor Green
        }
    } else {
        Write-Host "$folder does not exist, skipping..." -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "완료! Unity Editor를 다시 열면 패키지가 자동으로 재설치됩니다." -ForegroundColor Green
Write-Host ""
