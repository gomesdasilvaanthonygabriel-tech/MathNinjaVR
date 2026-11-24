# MathNinjaVR

Um jogo educativo em estilo Fruit Ninja onde você resolve contas matemáticas cortando as esferas com a resposta correta em uma sala de aula 3D.

## Como baixar e rodar

Siga os passos na ordem. Não precisa saber programar.

### 1. Instale a Unity certa: **6000.4.10f1** (exatamente essa versão)

1. Instale o **Unity Hub** (https://unity.com/download).
2. Instale o editor **6000.4.10f1** por um destes caminhos:
   - Clique no link direto (abre o Unity Hub): `unityhub://6000.4.10f1/feeafc12a938`
   - Ou, no Unity Hub: **Installs** → **Install Editor** → aba **Archive** → abra o arquivo de versões (https://unity.com/releases/editor/archive), procure **6000.4.10f1** e clique em instalar pelo Hub.
3. **Não use outra versão.** Com versão diferente os pacotes podem não carregar e a cena pode quebrar.

### 2. Baixe o projeto

Escolha **uma** das opções:

- **Opção A – ZIP (mais simples):** na página do repositório no GitHub, clique no botão verde **Code** → **Download ZIP**. Depois **extraia** o ZIP (não abra o projeto de dentro do ZIP).
- **Opção B – Git:** no terminal, rode:

  ```bash
  git clone https://github.com/gomesdasilvaanthonygabriel-tech/MathNinjaVR.git C:\Dev\MathNinjaVR
  ```

### 3. Coloque a pasta num caminho CURTO (muito importante)

Deixe o projeto em algo como **`C:\Dev\MathNinjaVR`**.

- **Não** deixe em `Downloads`, na `Área de Trabalho`, no `OneDrive` nem numa "pasta dentro de pasta" criada pelo ZIP (ex.: `MathNinjaVR-main\MathNinjaVR-main`).
- Motivo: o Windows tem limite de **260 caracteres** por caminho de arquivo. Se o caminho for longo, os pacotes da Unity (URP, Input System) falham ao importar e aparecem erros ou objetos rosa.
- Alternativa (avançado): ativar caminhos longos no Windows. Abra o **PowerShell como administrador**, rode o comando abaixo e **reinicie o computador**:

  ```powershell
  reg add HKLM\SYSTEM\CurrentControlSet\Control\FileSystem /v LongPathsEnabled /t REG_DWORD /d 1 /f
  ```

### 4. Abra no Unity Hub

1. Abra o **Unity Hub** → **Add** → **Add project from disk**.
2. Escolha a pasta do projeto — a que tem **`Assets`**, **`Packages`** e **`ProjectSettings`** logo dentro dela.
3. Confira que a versão do projeto é **6000.4.10f1** e abra.
4. **A primeira abertura demora** (vários minutos): a Unity baixa os pacotes e importa tudo. Precisa de internet nessa etapa. Espere a barra de progresso terminar.

> As pastas `Library`, `Temp`, `Logs` e `UserSettings` não vêm no repositório de propósito; a Unity cria todas elas sozinha.

### 5. Jogue

1. A cena **`Assets/Scenes/Game.unity`** abre sozinha. Se não abrir, no painel **Project** (embaixo) vá em `Assets > Scenes` e dê duplo clique em **`Game.unity`**.
2. Aperte o botão **Play** (triângulo no topo central da tela).

### Problemas comuns

- **Objetos rosa ou erros de importação de pacotes:** quase sempre é **caminho longo**. Feche a Unity, mova a pasta para `C:\Dev\MathNinjaVR` (ou ative caminhos longos, passo 3), apague a pasta `Library` do projeto e abra de novo pelo Unity Hub.
- **Versão diferente da Unity:** os pacotes não carregam. Se a Unity pedir para atualizar/converter o projeto, cancele, instale a **6000.4.10f1** e selecione-a no Unity Hub.
- **Aperto Play e "não acontece nada" / cena vazia:** a cena aberta não é a Game. Abra `Assets/Scenes/Game.unity` e aperte Play.
- **Erro de pacotes mesmo com caminho curto:** verifique a internet e confira em **Window > Package Manager** se algum pacote falhou. Se persistir, feche a Unity, apague a pasta `Library` e abra de novo.

## Como jogar

- **Controles:** segure o **botão esquerdo do mouse** e **arraste** o cursor pela tela para fazer o movimento de corte (não há controles no teclado).
- Na cena, um professor robô aparece em frente a um quadro-negro que mostra a conta matemática, avisos e a sua pontuação. Esferas coloridas com possíveis respostas são lançadas para cima — passe a lâmina do mouse sobre a esfera com o resultado certo para cortá-la ao meio. Se cortar a errada ou deixar cair, a pergunta continua até você acertar.

## Estado atual

Este projeto é um **protótipo** em desenvolvimento. Ele ainda **não roda em óculos VR** e não possui SDK de realidade virtual integrado — por enquanto, toda a jogabilidade funciona no computador usando apenas o mouse.

> **Nota:** a pasta `unity-mcp` presente no repositório é apenas uma ferramenta opcional de desenvolvimento e não é necessária para abrir ou jogar o projeto.
