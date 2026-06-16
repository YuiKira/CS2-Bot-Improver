Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

[System.Windows.Forms.Application]::EnableVisualStyles()

$script:KnifeTypes = @(
    @{ Id = 500; Name = "刺刀" },
    @{ Id = 503; Name = "经典匕首" },
    @{ Id = 505; Name = "折叠刀" },
    @{ Id = 506; Name = "穿肠刀" },
    @{ Id = 507; Name = "爪子刀" },
    @{ Id = 508; Name = "M9 刺刀" },
    @{ Id = 509; Name = "猎杀者匕首" },
    @{ Id = 512; Name = "弯刀" },
    @{ Id = 514; Name = "鲍伊猎刀" },
    @{ Id = 515; Name = "蝴蝶刀" },
    @{ Id = 516; Name = "暗影双匕" },
    @{ Id = 517; Name = "系绳匕首" },
    @{ Id = 518; Name = "求生匕首" },
    @{ Id = 519; Name = "熊刀" },
    @{ Id = 520; Name = "折叠刀" },
    @{ Id = 521; Name = "流浪者匕首" },
    @{ Id = 522; Name = "短剑" },
    @{ Id = 523; Name = "锯齿爪刀" },
    @{ Id = 525; Name = "骷髅匕首" },
    @{ Id = 526; Name = "廓尔喀刀" }
)

$script:KnifePaints = @(
    @{ Id = 5; Name = "森林 DDPAT" },
    @{ Id = 12; Name = "深红之网" },
    @{ Id = 38; Name = "渐变之色" },
    @{ Id = 40; Name = "暗夜" },
    @{ Id = 42; Name = "蓝钢" },
    @{ Id = 43; Name = "人工染色" },
    @{ Id = 44; Name = "表面淬火" },
    @{ Id = 59; Name = "屠夫" },
    @{ Id = 72; Name = "狩猎网格" },
    @{ Id = 77; Name = "北方森林" },
    @{ Id = 98; Name = "紫外线" },
    @{ Id = 143; Name = "都市伪装" },
    @{ Id = 175; Name = "枯焦之色" },
    @{ Id = 409; Name = "虎牙" },
    @{ Id = 413; Name = "渐变大理石" },
    @{ Id = 414; Name = "外表生锈" },
    @{ Id = 415; Name = "多普勒 红宝石" },
    @{ Id = 416; Name = "多普勒 蓝宝石" },
    @{ Id = 417; Name = "多普勒 黑珍珠" },
    @{ Id = 418; Name = "多普勒 P1" },
    @{ Id = 419; Name = "多普勒 P2" },
    @{ Id = 420; Name = "多普勒 P3" },
    @{ Id = 421; Name = "多普勒 P4" },
    @{ Id = 568; Name = "伽玛多普勒 绿宝石" },
    @{ Id = 569; Name = "伽玛多普勒 P1" },
    @{ Id = 570; Name = "伽玛多普勒 P2" },
    @{ Id = 571; Name = "伽玛多普勒 P3" },
    @{ Id = 572; Name = "伽玛多普勒 P4" }
)

function New-ChoiceText($item) {
    return "{0} ({1})" -f $item.Name, $item.Id
}

function Get-IdFromChoice($text) {
    if ($text -match "\((\d+)\)$") {
        return [int]$matches[1]
    }
    return $null
}

function Get-ConfigPath($root) {
    return Join-Path $root "addons\counterstrikesharp\plugins\BotRandomizer\BotRandomizer.custom.json"
}

function Get-DefaultConfig {
    return [pscustomobject]@{
        knife_def_indexes = @(507, 515)
        knife_paint_kits = @(415, 416)
        auto_drop_bot_knife_copy = $true
        drop_delay_seconds = 1.0
    }
}

function Read-Config($root) {
    $path = Get-ConfigPath $root
    if (-not (Test-Path $path)) {
        return Get-DefaultConfig
    }
    try {
        return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    } catch {
        [System.Windows.Forms.MessageBox]::Show(
            "配置文件格式无效，将加载默认配置。",
            "Bot 刀具面板",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        ) | Out-Null
        return Get-DefaultConfig
    }
}

function Set-CheckedChoices($list, $choices, $ids) {
    for ($i = 0; $i -lt $choices.Count; $i++) {
        $id = [int]$choices[$i].Id
        $list.SetItemChecked($i, @($ids) -contains $id)
    }
}

function Get-CheckedIds($list) {
    $ids = New-Object System.Collections.Generic.List[int]
    foreach ($item in $list.CheckedItems) {
        $id = Get-IdFromChoice ([string]$item)
        if ($null -ne $id) {
            $ids.Add($id)
        }
    }
    return $ids.ToArray()
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$defaultRoot = $scriptDir
if (-not (Test-Path (Join-Path $defaultRoot "addons\counterstrikesharp\plugins\BotRandomizer"))) {
    $defaultRoot = ""
}

$form = New-Object System.Windows.Forms.Form
$form.Text = "CS2 Bot 刀具面板"
$form.StartPosition = "CenterScreen"
$form.ClientSize = New-Object System.Drawing.Size(760, 560)
$form.MinimumSize = New-Object System.Drawing.Size(760, 560)

$pathLabel = New-Object System.Windows.Forms.Label
$pathLabel.Text = "CS2 game/csgo 文件夹"
$pathLabel.Location = New-Object System.Drawing.Point(16, 16)
$pathLabel.AutoSize = $true
$form.Controls.Add($pathLabel)

$pathBox = New-Object System.Windows.Forms.TextBox
$pathBox.Location = New-Object System.Drawing.Point(16, 40)
$pathBox.Size = New-Object System.Drawing.Size(610, 24)
$pathBox.Text = $defaultRoot
$form.Controls.Add($pathBox)

$browseButton = New-Object System.Windows.Forms.Button
$browseButton.Text = "浏览"
$browseButton.Location = New-Object System.Drawing.Point(640, 38)
$browseButton.Size = New-Object System.Drawing.Size(90, 28)
$form.Controls.Add($browseButton)

$knifeLabel = New-Object System.Windows.Forms.Label
$knifeLabel.Text = "刀型"
$knifeLabel.Location = New-Object System.Drawing.Point(16, 84)
$knifeLabel.AutoSize = $true
$form.Controls.Add($knifeLabel)

$knifeList = New-Object System.Windows.Forms.CheckedListBox
$knifeList.CheckOnClick = $true
$knifeList.Location = New-Object System.Drawing.Point(16, 108)
$knifeList.Size = New-Object System.Drawing.Size(340, 330)
foreach ($item in $script:KnifeTypes) {
    [void]$knifeList.Items.Add((New-ChoiceText $item))
}
$form.Controls.Add($knifeList)

$paintLabel = New-Object System.Windows.Forms.Label
$paintLabel.Text = "刀皮肤"
$paintLabel.Location = New-Object System.Drawing.Point(390, 84)
$paintLabel.AutoSize = $true
$form.Controls.Add($paintLabel)

$paintList = New-Object System.Windows.Forms.CheckedListBox
$paintList.CheckOnClick = $true
$paintList.Location = New-Object System.Drawing.Point(390, 108)
$paintList.Size = New-Object System.Drawing.Size(340, 330)
foreach ($item in $script:KnifePaints) {
    [void]$paintList.Items.Add((New-ChoiceText $item))
}
$form.Controls.Add($paintList)

$autoDrop = New-Object System.Windows.Forms.CheckBox
$autoDrop.Text = "冻结时间丢出每个 Bot 的当前刀复制品"
$autoDrop.Location = New-Object System.Drawing.Point(16, 456)
$autoDrop.Size = New-Object System.Drawing.Size(360, 24)
$form.Controls.Add($autoDrop)

$delayLabel = New-Object System.Windows.Forms.Label
$delayLabel.Text = "丢刀延迟"
$delayLabel.Location = New-Object System.Drawing.Point(390, 458)
$delayLabel.AutoSize = $true
$form.Controls.Add($delayLabel)

$delayBox = New-Object System.Windows.Forms.NumericUpDown
$delayBox.DecimalPlaces = 1
$delayBox.Minimum = 0.1
$delayBox.Maximum = 10
$delayBox.Increment = 0.1
$delayBox.Value = 1.0
$delayBox.Location = New-Object System.Drawing.Point(470, 456)
$delayBox.Size = New-Object System.Drawing.Size(80, 24)
$form.Controls.Add($delayBox)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Text = ""
$statusLabel.Location = New-Object System.Drawing.Point(16, 492)
$statusLabel.Size = New-Object System.Drawing.Size(520, 24)
$form.Controls.Add($statusLabel)

$reloadButton = New-Object System.Windows.Forms.Button
$reloadButton.Text = "重新加载"
$reloadButton.Location = New-Object System.Drawing.Point(542, 490)
$reloadButton.Size = New-Object System.Drawing.Size(88, 30)
$form.Controls.Add($reloadButton)

$saveButton = New-Object System.Windows.Forms.Button
$saveButton.Text = "保存"
$saveButton.Location = New-Object System.Drawing.Point(642, 490)
$saveButton.Size = New-Object System.Drawing.Size(88, 30)
$form.Controls.Add($saveButton)

function Load-UiConfig {
    $root = $pathBox.Text.Trim()
    if ([string]::IsNullOrWhiteSpace($root)) {
        return
    }
    $config = Read-Config $root
    Set-CheckedChoices $knifeList $script:KnifeTypes $config.knife_def_indexes
    Set-CheckedChoices $paintList $script:KnifePaints $config.knife_paint_kits
    $autoDrop.Checked = [bool]$config.auto_drop_bot_knife_copy
    if ($config.drop_delay_seconds -ne $null) {
        $value = [decimal]$config.drop_delay_seconds
        if ($value -lt $delayBox.Minimum) { $value = $delayBox.Minimum }
        if ($value -gt $delayBox.Maximum) { $value = $delayBox.Maximum }
        $delayBox.Value = $value
    }
    $statusLabel.Text = "已加载: $(Get-ConfigPath $root)"
}

$browseButton.Add_Click({
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = "选择 CS2 game/csgo 文件夹"
    if (-not [string]::IsNullOrWhiteSpace($pathBox.Text)) {
        $dialog.SelectedPath = $pathBox.Text
    }
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $pathBox.Text = $dialog.SelectedPath
        Load-UiConfig
    }
})

$reloadButton.Add_Click({
    Load-UiConfig
})

$saveButton.Add_Click({
    $root = $pathBox.Text.Trim()
    if ([string]::IsNullOrWhiteSpace($root)) {
        [System.Windows.Forms.MessageBox]::Show("请先选择 CS2 game/csgo 文件夹。") | Out-Null
        return
    }

    $knifeIds = @(Get-CheckedIds $knifeList)
    $paintIds = @(Get-CheckedIds $paintList)
    if ($knifeIds.Count -eq 0 -or $paintIds.Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show("至少选择一个刀型和一个刀皮肤。") | Out-Null
        return
    }

    $path = Get-ConfigPath $root
    $dir = Split-Path -Parent $path
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $config = [pscustomobject]@{
        knife_def_indexes = $knifeIds
        knife_paint_kits = $paintIds
        auto_drop_bot_knife_copy = $autoDrop.Checked
        drop_delay_seconds = [double]$delayBox.Value
    }
    $config | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $path -Encoding UTF8
    $statusLabel.Text = "已保存: $path"
    [System.Windows.Forms.MessageBox]::Show("已保存。重启 CS2，或在服务器控制台执行 bot_randomizer_reload。") | Out-Null
})

Load-UiConfig
[void]$form.ShowDialog()
