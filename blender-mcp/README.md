# Blender MCP (MathNinja)

Permite que o Claude Code modele em 3D no Blender. São duas partes:

- `addon/mathninja_mcp`: add-on do Blender que abre uma ponte HTTP só em `127.0.0.1:8766`. Os comandos rodam na thread principal do Blender. A cada início, ele gera um token novo e o grava em `%USERPROFILE%\.mathninja\blender-bridge.json`. Requisições sem esse token são recusadas, e requisições vindas de navegador também.
- `server.mjs`: servidor MCP (stdio) que o Claude Code executa. Ele lê o `blender-bridge.json` e repassa os comandos ao add-on.

## Instalação

```powershell
powershell -ExecutionPolicy Bypass -File blender-mcp\install.ps1
claude mcp add blender --scope user -- node C:\Users\cliente\MathNinja\blender-mcp\server.mjs
```

O `install.ps1` copia o add-on para `%APPDATA%\Blender Foundation\Blender\5.2\scripts\addons`, ativa-o nas preferências do usuário e roda `npm install`.

## Uso

1. Abra o Blender normalmente. O servidor inicia sozinho, e o status aparece na aba **MathNinja** da barra lateral N da 3D View, onde também há um botão para iniciar ou parar.
2. No Claude Code, peça por exemplo: "crie um shuriken low-poly no Blender e exporte para o Unity".

Ferramentas: `ping`, `scene_info`, `object_info`, `create_primitive`, `delete_object`, `set_transform`, `add_modifier`, `apply_modifier`, `set_material`, `set_mode`, `execute_python` (Python livre com bpy/bmesh/mathutils), `viewport_screenshot`, `export` (fbx/glb/obj; o padrão é `Assets/Models/<nome>.fbx` com eixos do Unity), `save_file` e `undo`.

Se o Blender estiver fechado ou o add-on estiver desligado, as ferramentas avisam: "Blender não conectado".
