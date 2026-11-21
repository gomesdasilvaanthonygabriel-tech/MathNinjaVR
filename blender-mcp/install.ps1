# Instala o add-on MathNinja MCP no Blender 5.2, ativa-o nas preferencias do usuario e instala as dependencias Node.
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
$addons = Join-Path $env:APPDATA "Blender Foundation\Blender\5.2\scripts\addons"
$dest = Join-Path $addons "mathninja_mcp"

if (-not (Test-Path $blender)) { throw "Blender nao encontrado em $blender" }

Write-Host "Copiando add-on para $dest"
New-Item -ItemType Directory -Force $addons | Out-Null
if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
Copy-Item -Recurse (Join-Path $here "addon\mathninja_mcp") $dest

Write-Host "Ativando o add-on (preferencias do usuario)"
$expr = "import addon_utils, bpy; addon_utils.enable('mathninja_mcp', default_set=True, persistent=True); bpy.ops.wm.save_userpref(); print('MATHNINJA_ADDON_OK' if 'mathninja_mcp' in bpy.context.preferences.addons else 'MATHNINJA_ADDON_FALHOU')"
$out = & $blender --background --python-expr $expr 2>&1 | Out-String
if ($out -notmatch "MATHNINJA_ADDON_OK") { Write-Host $out; throw "Falha ao ativar o add-on no Blender" }
Write-Host "Add-on ativo."

Write-Host "Instalando dependencias Node"
Push-Location $here
try { npm install --no-audit --no-fund; if ($LASTEXITCODE -ne 0) { throw "npm install falhou" } } finally { Pop-Location }

Write-Host "Pronto. Registre no Claude Code: claude mcp add blender --scope user -- node `"$here\server.mjs`""
