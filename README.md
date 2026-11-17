# MathNinjaVR

Um jogo educativo em estilo Fruit Ninja onde você resolve contas matemáticas cortando as esferas com a resposta correta em uma sala de aula 3D.

## Requisitos

- **Unity Hub** instalado no computador.
- **Unity 6000.4.10f1** (exatamente essa versão).
  - Link direto para instalar pelo Unity Hub: `unityhub://6000.4.10f1/feeafc12a938`
  - Caso o link direto não abra automaticamente, baixe essa versão no arquivo oficial da Unity: https://unity.com/releases/editor/archive
- **Importante:** não abra o projeto em outra versão da Unity. Versões diferentes podem tentar converter os arquivos e quebrar a cena ou os pacotes do projeto.

## Como baixar

### Opção A: Baixar como arquivo ZIP (mais simples)
1. Na página do repositório no GitHub, clique no botão verde **Code** e depois em **Download ZIP**.
2. Extraia o arquivo ZIP inteiro para uma pasta em um caminho curto e **sem acentos nem espaços especiais** (por exemplo: `C:\MathNinjaVR`).
3. Não tente abrir o projeto de dentro do arquivo ZIP sem extrair antes.

### Opção B: Usando Git
Se você já tem o Git instalado, abra o terminal na pasta onde deseja salvar o projeto e execute:

```bash
git clone https://github.com/gomesdasilvaanthonygabriel-tech/MathNinjaVR.git
```

### Quais pastas importam?
No projeto baixado, as pastas principais são **`Assets`**, **`Packages`** e **`ProjectSettings`** — é delas que a Unity precisa. As pastas `Library`, `Temp`, `Logs` e `UserSettings` não vêm no repositório de propósito; a própria Unity recria todas elas automaticamente quando você abre o projeto.

## Como abrir na Unity

1. Abra o **Unity Hub**.
2. Clique em **Add** → **Add project from disk**.
3. Selecione a pasta do projeto que contém diretamente a pasta `Assets` dentro dela.
4. Confirme que a versão selecionada para o projeto é a **6000.4.10f1** e clique para abrir.
5. **Atenção:** a primeira abertura demora alguns minutos porque a Unity precisa baixar os pacotes do projeto e montar a pasta `Library`. É necessário estar conectado à internet nessa etapa.

## Como jogar

1. Com o projeto aberto na Unity, vá até o painel **Project** (embaixo), abra `Assets > Scenes` e dê um duplo clique em **`Game.unity`**.
2. Aperte o botão **Play** (triângulo no topo central da tela).
3. **Controles:** segure o **botão esquerdo do mouse** e **arraste** o cursor pela tela para fazer o movimento de corte (não há controles no teclado).
4. Na cena, um professor robô aparece em frente a um quadro-negro que mostra a conta matemática, avisos e a sua pontuação. Esferas coloridas com possíveis respostas são lançadas para cima — passe a lâmina do mouse sobre a esfera com o resultado certo para cortá-la ao meio. Se cortar a errada ou deixar cair, a pergunta continua até você acertar.

## Problemas comuns

- **Abertura em versão diferente da Unity:** cancele se a Unity pedir para atualizar ou converter o projeto. Instale a versão **6000.4.10f1** e selecione-a no Unity Hub antes de abrir.
- **Erro de pacotes ao abrir:** verifique sua conexão com a internet e confira em **Window > Package Manager** se algum pacote falhou ao baixar. Se persistir, feche a Unity, apague a pasta `Library` do projeto e abra novamente pela Unity Hub.
- **A cena aparece vazia ao abrir o projeto:** a Unity pode ter aberto uma cena padrão vazia. No painel **Project**, abra `Assets/Scenes/Game.unity`.
- **Pasta extraída dentro de outra pasta:** ao extrair o ZIP, às vezes é criada uma pasta dentro da outra (por exemplo `MathNinjaVR-main\MathNinjaVR-main`). No Unity Hub, escolha sempre a pasta interna que possui `Assets`, `Packages` e `ProjectSettings` logo dentro dela.

## Estado atual

Este projeto é um **protótipo** em desenvolvimento. Ele ainda **não roda em óculos VR** e não possui SDK de realidade virtual integrado — por enquanto, toda a jogabilidade funciona no computador usando apenas o mouse.

> **Nota:** a pasta `unity-mcp` presente no repositório é apenas uma ferramenta opcional de desenvolvimento e não é necessária para abrir ou jogar o projeto.
