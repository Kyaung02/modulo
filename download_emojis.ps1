# Set encoding to handle emojis correctly in console
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$baseUrl = "https://raw.githubusercontent.com/googlefonts/noto-emoji/main/png/128/emoji_u"
$destDir = "Assets/Sprites/Emojis"

# Ensure destination directory exists
if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Force -Path $destDir
}

# Embedded CSV Data
$csvContent = @"
Name,Emoji
Earth,🌍
Water,💧
Wind,🌬️
Fire,🔥
Plant,🌱
Dandelion,🌼
Tree,🌳
Wish,✨
Money,💰
Gold,🥇
Lake,🌊
Wave,🌊
Sand,🏖️
Dust,💨
Glass,🍷
Telescope,🔭
Mirror,🪞
Radio,📻
Microwave,🍳
Teleport,🌀
Lava,🌋
Stone,🪨
Obsidian,💎
River,🏞️
Blade,🗡️
Axe,🪓
Head,👤
Source,⛲
Planet,🪐
Ocean,🌊
Sun,☀️
Solar,☀️
System,⚙️
Computer,💻
Submarine,🚢
Software,💾
Subsystem,🛠️
Module,📦
Mountain,🏔️
Steam,💨
Engine,🚂
Train,🚆
Tunnel,🚇
Paper,📄
Map,🗺️
Treasure,🏴☠️
X,❌
Xerox,📠
Copy,📋
Volcano,🌋
Avalanche,❄️
Tsunami,🌊
Smoke,💨
Swamp,🐊
Mud,💩
Dandelion Patch,🌼
Hourglass,⏳
Cloud,☁️
Tractor,🚜
Ash,🚬
Fjord,🏞️
Rocket,🚀
Island,🏝️
Snow,❄️
Wine,🍷
Incense,🕯️
Flower,🌸
Car,🚗
Rain,🌧️
Brick,🧱
Crash,💥
Yellow Car,🚕
Time,⏰
Vinegar,🏺
Jet,✈️
Tank,🚜
Pencil,✏️
Whale,🐋
Satellite,🛰️
Continent,🗺️
Surf,🏄
Asia,🌏
Moon,🌙
Sandpaper,📜
Prayer,🙏
Truck,🚚
Rich,🤑
House,🏠
America,🇺🇸
Yellow Submarine,🚢
Beach,🏖️
Surfer,🏄
Battery,🔋
War,⚔️
Book,📖
Internet,🌐
Australia,🇦🇺
Everest,🏔️
No,🚫
Eclipse,🌑
Rough,🌊
Temple,🏛️
Delivery,📦
Richer,💎
Town,🏘️
Google,🔍
The Beatles,🎸
Sauna,🧖
Steamroller,🚜
Remote,📺
Battle,🤺
Homework,📚
Fail,❌
Ever,♾️
Apocalypse,☄️
Church,⛪
Baptism,💧
Wiser,🧠
Port,⚓
Search,🔍
Yesterday,📅
Finnish,🇫🇮
Previous,⬅️
Older,👴
Fight,🥊
Clean,🧹
Never,🙅
Apoclipse,☄️
Holy Spirit,🕊️
Explore,🧭
Export,🚢
Try,🎯
Younger,👶
Done,✅
End,🔚
Pentecost,🔥
Import,📥
Ending,🔚
Rougher,🌊
Tongues,👅
Tougher,💪
Latin,🏛️
Enduring,⏳
Attempt,🎯
Endure,⛰️
Attempted,🎯
Failed,📉
Ended,🏁
Road,🛣️
Pancake,🥞
Bridge,🌉
Arch,⛩️
Stack,📚
Lily,🌸
Angel,👼
UPS,📦
Upgrade,⬆️
"@

# Helper to convert emoji string to Noto-compatible hex string
function Get-NotoHexCode($str) {
    if ([string]::IsNullOrEmpty($str)) { return $null }
    
    $codes = @()
    $chars = $str.ToCharArray()
    for ($i = 0; $i -lt $chars.Length; $i++) {
        $c = $chars[$i]
        $val = 0
        if ([char]::IsHighSurrogate($c)) {
            $val = [char]::ConvertToUtf32($str, $i)
            $i++
        } else {
            $val = [int]$c
        }

        # Noto Emoji convention:
        # - Lowercase hex
        # - Exclude FE0F (Variation Selector-16) generally
        # - Include ZWJ (200D)
        
        if ($val -ne 0xFE0F) {
            $codes += "{0:x}" -f $val
        }
    }
    return $codes -join "_"
}

# Manual Overrides for tricky sequences or flags
# Keys are the exact names from CSV
$manualOverrides = @{
    "Treasure" = "1f3f4_200d_2620"
    "America" = "1f1fa_1f1f8"
    "Australia" = "1f1e6_1f1fa"
    "Finnish" = "1f1eb_1f1ee"
    "Ended" = "1f3c1" 
    "Finish" = "1f3c1"
}

# Parse CSV
$items = $csvContent | ConvertFrom-Csv

foreach ($item in $items) {
    $name = $item.Name
    $emoji = $item.Emoji
    
    if ([string]::IsNullOrWhiteSpace($emoji)) { continue }

    if ($manualOverrides.ContainsKey($name)) {
        $code = $manualOverrides[$name]
        Write-Host "Using manual override for $name`t: $code"
    } else {
        $code = Get-NotoHexCode $emoji
    }
    
    $url = "$baseUrl$code.png"
    $outPath = Join-Path $destDir "$name.png"
    
    Write-Host "Downloading $name ($code)..."
    try {
        Invoke-WebRequest -Uri $url -OutFile $outPath
    } catch {
        Write-Warning "Failed to download $name ($code). Possible Noto mapping issue."
    }
}

Write-Host "Download Complete."
