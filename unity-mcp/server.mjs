// Servidor MCP (stdio) que controla o Unity Editor via MCPBridge (HTTP em 127.0.0.1).
// Config: variavel UNITY_PROJECT_PATH (padrao: pasta acima deste arquivo).
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const project = process.env.UNITY_PROJECT_PATH || path.resolve(here, "..");
const infoFile = path.join(project, "Library", "mcp-bridge.json");

function bridgeInfo() {
  try {
    return JSON.parse(fs.readFileSync(infoFile, "utf8"));
  } catch {
    throw new Error("Bridge nao encontrado. Abra o projeto no Unity (arquivo " + infoFile + " ausente).");
  }
}

async function call(cmd, args = {}) {
  let lastErr;
  for (let i = 0; i < 6; i++) {
    try {
      const { port, token } = bridgeInfo();
      const r = await fetch(`http://127.0.0.1:${port}/`, {
        method: "POST",
        headers: { "X-MCP-Token": token, "Content-Type": "application/json" },
        body: JSON.stringify({ cmd, json: JSON.stringify(args) }),
        signal: AbortSignal.timeout(65000),
      });
      const d = await r.json();
      if (!d.ok) throw new Error(d.error);
      return d.result;
    } catch (e) {
      lastErr = e;
      // Unity pode estar recarregando scripts (domain reload): tenta de novo
      if (e.message && /fetch failed|ECONNREFUSED|Bridge nao|aborted/i.test(e.message)) {
        await new Promise((r) => setTimeout(r, 1500));
        continue;
      }
      throw e;
    }
  }
  throw lastErr;
}

const server = new McpServer({ name: "unity", version: "1.0.0" });
const text = (t) => ({ content: [{ type: "text", text: String(t) }] });
const tool = (name, desc, shape, cmd) =>
  server.tool(name, desc, shape, async (args) => {
    try {
      return text(await call(cmd ?? name, args));
    } catch (e) {
      return { isError: true, content: [{ type: "text", text: "Erro: " + e.message }] };
    }
  });

const P = z.string().describe("caminho do objeto na cena, ex.: 'Player/Arm'");
const V = z.string().describe("vetor 'x,y,z' (ponto como separador decimal)");

tool("ping", "Verifica a conexao com o Unity (versao, cena, play/compilando).", {});
tool("get_hierarchy", "Lista a hierarquia da cena ativa com componentes.", { maxDepth: z.number().int().min(0).max(10).optional() });
tool("create_gameobject", "Cria GameObject vazio, primitivo (Cube, Sphere, Capsule, Cylinder, Plane, Quad) ou instancia prefab (assetPath).", {
  name: z.string().optional(), parent: P.optional(), primitive: z.string().optional(), prefab: z.string().optional().describe("ex.: Assets/Prefabs/X.prefab"), position: V.optional(),
});
tool("delete_gameobject", "Remove um GameObject da cena.", { path: P });
tool("set_active", "Ativa ou desativa um GameObject.", { path: P, active: z.boolean() });
tool("set_transform", "Define posicao/rotacao(euler)/escala. local=true por padrao.", {
  path: P, position: V.optional(), rotation: V.optional(), scale: V.optional(), local: z.boolean().optional(),
});
tool("add_component", "Adiciona componente (nome do tipo, ex.: Rigidbody2D, ou script proprio).", { path: P, type: z.string() });
tool("get_properties", "Lista propriedades serializadas de um componente.", { path: P, component: z.string() });
tool("set_property", "Altera propriedade serializada. Valores: int/float/bool/string, enum por nome, 'x,y' (Vector2), 'x,y,z', cor 'r,g,b,a' (0-1), referencia por caminho de asset ou de objeto da cena, 'null'.", {
  path: P, component: z.string(), property: z.string().describe("propertyPath, ex.: m_Mass"), value: z.string(),
});
tool("write_file", "Grava arquivo (Assets/, Packages/ ou ProjectSettings/) e atualiza o AssetDatabase. Scripts .cs disparam compilacao.", {
  path: z.string().describe("relativo ao projeto, ex.: Assets/Scripts/Player.cs"), content: z.string(),
});
tool("read_file", "Le arquivo de Assets/, Packages/ ou ProjectSettings/.", { path: z.string() });
tool("list_files", "Lista arquivos (max 500) de uma pasta; padrao Assets.", { path: z.string().optional() });
tool("refresh", "AssetDatabase.Refresh (importa/compila alteracoes feitas fora do Unity).", {});
tool("play", "Controla o modo play: action = play | stop | pause.", { action: z.enum(["play", "stop", "pause"]) });
tool("scene", "Gerencia cenas: action = save | open | new | save_as (path para open/save_as).", {
  action: z.enum(["save", "open", "new", "save_as"]), path: z.string().optional(),
});
tool("menu", "Executa item de menu do Editor, ex.: 'File/Save Project'.", { item: z.string() });
tool("console", "Le as ultimas mensagens do Console (ou limpa com clear=true).", { count: z.number().int().optional(), clear: z.boolean().optional() });

await server.connect(new StdioServerTransport());
