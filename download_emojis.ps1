$baseUrl = "https://raw.githubusercontent.com/googlefonts/noto-emoji/main/png/128/emoji_u"
$destDir = "Assets/Sprites/Emojis"
New-Item -ItemType Directory -Force -Path $destDir

$emojis = @{
    "Earth" = "1f30d"
    "Wind" = "1f32c" 
    "Fire" = "1f525"
    "Water" = "1f4a7"
    "Dust" = "23f3"
    "Planet" = "1fa90"
    "Ocean" = "1f30a"
    "Sun" = "2600"
    "Solar" = "26a1"
    "System" = "2699"
    "Computer" = "1f4bb"
    "Submarine" = "2693"
    "Software" = "1f4be"
    "Subsystem" = "1f3d7"
    "Module" = "1f4e6"
}

foreach ($name in $emojis.Keys) {
    $code = $emojis[$name]
    $url = "$baseUrl$code.png"
    $outPath = Join-Path $destDir "$name.png"
    
    Write-Host "Downloading $name ($code)..."
    try {
        Invoke-WebRequest -Uri $url -OutFile $outPath
    } catch {
        Write-Error "Failed to download $name from $url"
    }
}
Write-Host "Done."
