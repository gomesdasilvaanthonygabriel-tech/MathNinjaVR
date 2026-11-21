// Servidor MCP (stdio) que controla o Blender via add-on MathNinja MCP (HTTP em 127.0.0.1).
// O add-on grava porta e token em %USERPROFILE%\.mathninja\blender-bridge.json a cada inicio.
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const infoFile = process.env.BLENDER_BRIDGE_FILE || path.join(os.homedir(), ".mathninja", "blender-bridge.json");
const NOT_CONNECTED =
  "Blender não conectado: abra o Blender e confira se o add-on MathNinja MCP está ativo (aba MathNinja na barra N)";

function bridgeInfo() {
  try {
    return JSON.parse(fs.readFileSync(infoFile, "utf8"));
  } catch {
    throw new Error(NOT_CONNECTED);
  }
}

async function call(cmd, args = {}) {
  const { port, token } = bridgeInfo(); // relido a cada chamada (token muda a cada inicio)
  let r;
  try {
    r = await fetch(`http://127.0.0.1:${port}/cmd`, {
      method: "POST",
      headers: { "X-MCP-Token": token, "Content-Type": "application/json" },
      body: JSON.stringify({ cmd, args }),
      signal: AbortSignal.timeout(65000),
    });
  } catch (e) {
    if (/fetch failed|ECONNREFUSED|ECONNRESET/i.test(e.message + (e.cause?.code ?? ""))) throw new Error(NOT_CONNECTED);
    throw e;
  }
  if (r.status === 401) throw new Error(NOT_CONNECTED + " (token desatualizado: reinicie o servidor no painel MathNinja)");
  let d;
  try {
    d = await r.json();
  } catch {
    throw new Error(`resposta inválida do Blender (HTTP ${r.status})`);
  }
  if (!d.ok) throw new Error(d.error + (d.traceback ? "\n" + d.traceback : ""));
  return d.result;
}

const server = new McpServer({ name: "blender", version: "1.0.0" });
const text = (t) => ({ content: [{ type: "text", text: typeof t === "string" ? t : JSON.stringify(t, null, 2) }] });
const fail = (e) => ({ isError: true, content: [{ type: "text", text: "Erro: " + e.message }] });
const tool = (name, desc, shape, cmd) =>
  server.tool(name, desc, shape, async (args) => {
    try {
      return text(await call(cmd ?? name, args));
    } catch (e) {
      return fail(e);
    }
  });

const N = z.string().describe("nome do objeto no Blender, ex.: 'Cube'");
const V3 = z.array(z.number()).length(3);
const RGBA = z.array(z.number().min(0)).min(3).max(4).describe("cor [r,g,b] ou [r,g,b,a], valores 0-1 (linear)");

tool("ping", "Verifica a conexão com o Blender (versão, arquivo aberto, cena).", {});
tool("scene_info", "Lista os objetos da cena: nome, tipo, localização, rotação (graus), escala, dimensões, materiais, vértices e faces.", {});
tool("object_info", "Detalhes de um objeto: transform, modificadores (com propriedades), bounding box em coordenadas do mundo, materiais.", { name: N });
tool("create_primitive", "Cria uma malha primitiva. Unidades em metros; rotação em graus. size vale para cube/plane/monkey (e diâmetro de esferas/cilindro se radius não for dado); radius para esferas, cilindro, cone, torus (raio maior), circle; depth para cilindro/cone; segments = vértices/segmentos (ico_sphere: subdivisões).", {
  type: z.enum(["cube", "uv_sphere", "ico_sphere", "cylinder", "cone", "torus", "plane", "circle", "monkey"]),
  name: z.string().optional(),
  location: V3.optional().describe("[x,y,z]"),
  rotation: V3.optional().describe("[x,y,z] em graus"),
  scale: V3.optional().describe("[x,y,z]"),
  size: z.number().positive().optional(),
  radius: z.number().positive().optional(),
  depth: z.number().positive().optional(),
  segments: z.number().int().min(1).optional(),
});
tool("delete_object", "Remove um objeto da cena.", { name: N });
tool("set_transform", "Define localização, rotação (graus, Euler XYZ) e/ou escala de um objeto.", {
  name: N, location: V3.optional(), rotation: V3.optional().describe("graus"), scale: V3.optional(),
});
tool("add_modifier", "Adiciona um modificador. type = BEVEL, SUBSURF, MIRROR, ARRAY, SOLIDIFY, BOOLEAN, DECIMATE, WEIGHTED_NORMAL etc. props = propriedades do modificador, ex.: {\"width\":0.05,\"segments\":3} (ponteiros como 'object' aceitam o nome do objeto).", {
  name: N, type: z.string(), props: z.record(z.any()).optional(), modifier_name: z.string().optional(),
});
tool("apply_modifier", "Aplica (torna permanente) um modificador do objeto.", { name: N, modifier: z.string().describe("nome do modificador, ex.: 'Bevel'") });
tool("set_material", "Cria/atualiza um material Principled BSDF e atribui ao objeto (slot 0). Padrão do nome: <objeto>_Mat.", {
  name: N,
  material_name: z.string().optional(),
  base_color: RGBA.optional(),
  metallic: z.number().min(0).max(1).optional(),
  roughness: z.number().min(0).max(1).optional(),
  emission_color: RGBA.optional(),
  emission_strength: z.number().min(0).optional(),
});
tool("set_mode", "Torna o objeto ativo e troca o modo: OBJECT, EDIT, SCULPT, VERTEX_PAINT, WEIGHT_PAINT, TEXTURE_PAINT.", {
  name: N, mode: z.string().describe("ex.: OBJECT ou EDIT"),
});
tool("execute_python", "Executa código Python no Blender (thread principal) com bpy, bmesh, mathutils e math disponíveis. Ferramenta geral para modelagem livre. Devolve o stdout (print) e a variável 'result', se definida.", {
  code: z.string(),
});
server.tool(
  "viewport_screenshot",
  "Captura a 3D View do Blender (ou, sem interface, renderiza pela câmera) e devolve a imagem PNG.",
  { max_size: z.number().int().min(64).max(4096).optional().describe("maior lado em pixels (padrão 1024)") },
  async (args) => {
    try {
      const r = await call("viewport_screenshot", args);
      const data = fs.readFileSync(r.path).toString("base64");
      try { fs.unlinkSync(r.path); } catch {}
      return { content: [{ type: "image", data, mimeType: "image/png" }, { type: "text", text: `método: ${r.method}` }] };
    } catch (e) {
      return fail(e);
    }
  },
);
tool("export", "Exporta a cena (ou só a seleção) para fbx, glb ou obj. Padrão: C:\\Users\\cliente\\MathNinja\\Assets\\Models\\<nome>.fbx, com eixos compatíveis com o Unity.", {
  path: z.string().optional().describe("caminho completo do arquivo"),
  format: z.enum(["fbx", "glb", "obj"]).optional(),
  selected_only: z.boolean().optional(),
});
tool("save_file", "Salva o .blend (path opcional para 'salvar como').", { path: z.string().optional() });
tool("undo", "Desfaz a(s) última(s) ação(ões) no Blender.", { steps: z.number().int().min(1).max(50).optional() });

await server.connect(new StdioServerTransport());
