$app = Join-Path $PSScriptRoot "CS2 Bot Tools.exe"
if (-not (Test-Path -LiteralPath $app)) { throw "CS2 Bot Tools.exe was not found. Extract the complete release package." }
Start-Process -FilePath $app
