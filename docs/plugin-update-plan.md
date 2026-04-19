# Plano de Atualizacao dos Plugins Retake

Data da analise: 2026-04-18

## Objetivo

Atualizar os plugins:

- `cs2-retakes`
- `cs2-instadefuse`
- `cs2-clutch-announce`
- `cs2-instaplant`

preservando customizacoes locais, corrigindo o pipeline de build/deploy e deixando um procedimento claro para copiar a atualizacao para o servidor.

## Resumo Executivo

O estado atual nao permite uma atualizacao segura apenas trocando arquivos upstream.

Motivos principais:

1. `cs2-retakes` local nao e um espelho do upstream; ele e uma arvore bastante divergente e com sinais claros de customizacao local.
2. `cs2-clutch-announce` nao existe hoje em `plugins_source/` nem entra no `scripts/build-plugins.sh`.
3. O modo retake carrega DLLs em `plugins/disabled/`, mas o build local publica em `components/csgo/addons/counterstrikesharp/plugins/`.
4. O `build-plugins.sh` nao publica todos os assets do `RetakesPlugin` (`lang/`, `map_config/`, `retakes_config.json`).
5. O `start.sh` e o `install.sh` sempre sobrescrevem `server/game/csgo/` com `components/csgo/`, entao qualquer ajuste manual feito direto no servidor precisa virar arquivo versionado em `components/`.

## Fontes Upstream Confirmadas

Refs consultadas em 2026-04-18:

- `cs2-retakes`
  - `master`: `19df0bb671a1e232ffb7a6dbe608534a2046a6c7`
  - ultima tag estavel visivel via refs: `3.0.3` em `f492091ae0cce34458018bbdbb68f2c4c8435ac9`
- `cs2-instadefuse`
  - `master` = `2.0.0`: `3a2e616be7d6151ecc2339dc21e585859da631a2`
- `cs2-instaplant`
  - `master`: `9cae8a2b7bd2079ac184e22287de5f6f9c3f1326`
  - ultima tag: `1.0.0`
- `cs2-clutch-announce`
  - `master`: `597141551fe0915a05f8881a3d72c7c2cb8f6aa4`
  - ultima tag: `1.1.0`

## Diagnostico por Plugin

### 1. `cs2-retakes`

Status: merge manual obrigatorio.

Sinais de divergencia local:

- `plugins_source/cs2-retakes/RetakesPlugin/RetakesPlugin.cs` local esta em `2.2.0` com `MinimumApiVersion(220)`.
- O upstream `master` ja esta na linha `3.0.x`, com estrutura de codigo diferente e exigencia maior de API.
- O `csproj` local tem referencias a `MongoDB.*`, que nao existem no upstream atual.
- Existem arquivos locais que indicam logica propria:
  - `plugins_source/cs2-retakes/RetakesPlugin/Modules/Database/MongoDB.cs`
  - `plugins_source/cs2-retakes/RetakesPlugin/Modules/Entities/Player.cs`
- A organizacao interna mudou muito entre local e upstream; nao e um update "fast-forward".

Conclusao:

- Nao sobrescrever `plugins_source/cs2-retakes` com upstream.
- Criar uma branch de migracao e portar manualmente as customizacoes locais para a base upstream escolhida.
- Para servidor de producao, prefira partir da tag `3.0.3`, nao do `master`, a menos que voce queira assumir risco de codigo ainda nao tagueado.

### 2. `cs2-instadefuse`

Status: atualizacao simples.

Resultado da comparacao:

- O codigo-fonte local esta funcionalmente igual ao upstream.
- A diferenca esta no `InstadefusePlugin.csproj`:
  - local usa `CounterStrikeSharp.API 1.0.365`
  - upstream ainda referencia `1.0.220`
  - local adiciona `OutDir` e `AppendTargetFrameworkToOutputPath=false`

Conclusao:

- Nao ha sinal de customizacao local de comportamento.
- Pode atualizar do upstream e manter o `csproj` local como padrao do seu pipeline.

### 3. `cs2-instaplant`

Status: atualizacao simples, praticamente vendor local.

Resultado da comparacao:

- O `InstaplantPlugin.cs` local esta igual ao upstream.
- A diferenca relevante esta no `csproj`:
  - upstream ainda esta em `net7.0` e referencia `CounterStrikeSharp.API` por `HintPath`
  - local ja foi adaptado para `net8.0` com `PackageReference` em `1.0.365`

Conclusao:

- Nao existe customizacao funcional local aparente.
- A "atualizacao" aqui e basicamente manter o codigo upstream e preservar sua modernizacao do `csproj`.

### 4. `cs2-clutch-announce`

Status: plugin ausente localmente.

Resultado da comparacao:

- O repositorio upstream existe e contem:
  - `ClutchAnnouncePlugin.cs`
  - `ClutchAnnouncePlugin.csproj`
  - `Modules/Translator.cs`
  - `lang/*.json`
- O upstream ainda usa `net7.0` e `CounterStrikeSharp.API 1.0.198`.

Conclusao:

- Precisa ser incorporado ao repo local.
- Recomendado adaptar o `csproj` para o mesmo padrao dos outros plugins (`net8.0`, `PackageReference`, `OutDir` opcional).

## Problemas do Pipeline Atual

### 1. Destino de deploy incorreto para o modo retake

Hoje:

- `scripts/build-plugins.sh` publica em `components/csgo/addons/counterstrikesharp/plugins/`

Mas o modo retake carrega:

- `components/csgo/cfg/retake.cfg`
  - `plugins/disabled/RetakesPlugin/RetakesPlugin.dll`
  - `plugins/disabled/InstadefusePlugin/InstadefusePlugin.dll`

Impacto:

- O build local pode terminar com sucesso e ainda assim o servidor continuar usando binarios antigos em `plugins/disabled/`.

### 2. `cs2-clutch-announce` fora do build

Hoje `scripts/build-plugins.sh` conhece apenas:

- `cs2-autojoin`
- `cs2-instadefuse`
- `cs2-instaplant`
- `cs2-retakes`
- `cs2-inventory-simulator-plugin`

Impacto:

- O plugin novo nao entra no fluxo de restore/build/deploy.

### 3. Assets do `RetakesPlugin` nao entram no deploy do build

O script copia `lang/` apenas quando ela existe na raiz do plugin.

No `cs2-retakes`, os assets ficam em:

- `plugins_source/cs2-retakes/RetakesPlugin/lang/`
- `plugins_source/cs2-retakes/RetakesPlugin/map_config/`

Impacto:

- Mesmo quando a DLL e recompilada, traducoes e configs de mapa podem ficar desatualizadas.

### 4. `components/` e a fonte da verdade

`start.sh` e `install.sh` fazem overlay de `components/csgo/` em `server/game/csgo/`.

Impacto:

- Ajustes manuais no servidor, fora de `components/`, se perdem no proximo boot.

## Plano Recomendado

### Fase 0. Congelamento e backup

Antes de mudar qualquer coisa:

1. Criar branch de trabalho:

```bash
git checkout -b chore/update-retake-plugins
```

2. Backups dos artefatos versionados que entram no overlay:

```bash
mkdir -p tmp/plugin-backup
cp -R components/csgo/addons/counterstrikesharp/plugins/disabled/RetakesPlugin tmp/plugin-backup/
cp -R components/csgo/addons/counterstrikesharp/plugins/disabled/InstadefusePlugin tmp/plugin-backup/
cp -R components/csgo/addons/counterstrikesharp/plugins/InstaplantPlugin tmp/plugin-backup/
cp -R components/csgo/cfg/retake.cfg tmp/plugin-backup/
cp -R components/csgo/cfg/retake_settings.cfg tmp/plugin-backup/
```

3. Se existir servidor remoto ja em producao, salvar tambem:

- `addons/counterstrikesharp/plugins/disabled/RetakesPlugin/retakes_config.json`
- qualquer `map_config/*.json` customizado
- qualquer DLL customizada fora do repo

### Fase 1. Atualizar plugins de baixo risco

#### `cs2-instadefuse`

Acao:

- sincronizar codigo upstream
- manter o `csproj` local atual

Resultado esperado:

- plugin atualizado sem risco de regressao funcional local

#### `cs2-instaplant`

Acao:

- manter codigo atual, porque ja esta equivalente ao upstream
- opcionalmente registrar a origem upstream no README interno ou comentario de manutencao

Resultado esperado:

- plugin padronizado no pipeline .NET 8

#### `cs2-clutch-announce`

Acao:

1. importar o codigo do upstream para `plugins_source/cs2-clutch-announce`
2. adaptar o `ClutchAnnouncePlugin.csproj` para:
   - `net8.0`
   - `PackageReference Include="CounterStrikeSharp.API" Version="1.0.365"`
   - `Microsoft.Extensions.Logging` compativel com seu restante
3. decidir o destino de runtime:
   - se sera carregado junto do retake, publicar em `plugins/disabled/ClutchAnnouncePlugin`

Resultado esperado:

- plugin novo entra no repo e no fluxo de build

### Fase 2. Migrar `cs2-retakes` com preservacao de customizacoes

Acao recomendada:

1. Criar uma copia limpa do upstream `3.0.3` em um diretorio temporario de migracao.
2. Inventariar o que no seu fork e realmente customizacao local:
   - integracao MongoDB
   - entidade `Player`
   - qualquer alteracao no fluxo de filas, alocacao ou configs
3. Portar essas customizacoes para a base `3.0.3`.
4. So depois substituir `plugins_source/cs2-retakes`.

Ordem sugerida de merge:

1. configs e modelos
2. servicos externos (MongoDB)
3. comandos e eventos
4. traducoes
5. `map_config`

O que nao fazer:

- nao copiar o seu `RetakesPlugin.cs` inteiro por cima do upstream `3.x`
- nao atualizar direto para `master` sem validar compatibilidade da API usada no servidor

### Fase 3. Corrigir o pipeline local

O build/deploy precisa refletir como o servidor realmente carrega plugins.

Mudancas necessarias:

1. `scripts/build-plugins.sh`
   - adicionar `cs2-clutch-announce`
   - publicar `RetakesPlugin` e `InstadefusePlugin` no destino usado pelo modo retake:
     - `components/csgo/addons/counterstrikesharp/plugins/disabled/RetakesPlugin`
     - `components/csgo/addons/counterstrikesharp/plugins/disabled/InstadefusePlugin`
   - se `ClutchAnnouncePlugin` for usado no retake, publicar tambem em `plugins/disabled/ClutchAnnouncePlugin`
   - copiar assets do `RetakesPlugin`:
     - `lang/`
     - `map_config/`
     - preservar `retakes_config.json` se ele for configuracao local do servidor

2. Opcao alternativa
   - manter deploy em `plugins/`
   - alterar `components/csgo/cfg/retake.cfg` para carregar dessa pasta

Recomendacao:

- manter o padrao atual do projeto e publicar em `plugins/disabled/`, porque a documentacao e os configs ja assumem esse modelo.

### Fase 4. Validacao local

Executar:

```bash
dotnet restore plugins_source/cs2-instadefuse/InstadefusePlugin.csproj
dotnet build plugins_source/cs2-instadefuse/InstadefusePlugin.csproj -c Release

dotnet restore plugins_source/cs2-instaplant/InstaplantPlugin.csproj
dotnet build plugins_source/cs2-instaplant/InstaplantPlugin.csproj -c Release

dotnet restore plugins_source/cs2-clutch-announce/ClutchAnnouncePlugin.csproj
dotnet build plugins_source/cs2-clutch-announce/ClutchAnnouncePlugin.csproj -c Release

dotnet restore plugins_source/cs2-retakes/RetakesPlugin/RetakesPlugin.csproj
dotnet build plugins_source/cs2-retakes/RetakesPlugin/RetakesPlugin.csproj -c Release
```

Validar tambem:

- `css_plugins list`
- carregamento do modo `retake`
- mensagens de traducao
- leitura de `map_config`
- leitura de `retakes_config.json`
- ausencia de erro de dependencia em runtime

### Fase 5. Rollout para o servidor

O servidor deve receber o overlay de `components/csgo/`, nao apenas DLLs soltas.

Procedimento recomendado:

1. parar o servidor
2. copiar o overlay atualizado
3. subir o servidor
4. validar plugins carregados

Exemplo de comandos no host:

```bash
cd /opt/cs2-linux-server
git pull

# se o build for feito no proprio host
./scripts/build-plugins.sh cs2-instadefuse
./scripts/build-plugins.sh cs2-instaplant
./scripts/build-plugins.sh cs2-clutch-announce
./scripts/build-plugins.sh cs2-retakes

# reiniciar
./start.sh
```

Se o build for feito em outra maquina, copie a pasta versionada:

```bash
rsync -av components/csgo/ user@host:/opt/cs2-linux-server/components/csgo/
```

Depois no servidor:

```bash
cd /opt/cs2-linux-server
./start.sh
```

## Ordem de Execucao Recomendada

1. corrigir `build-plugins.sh`
2. adicionar `cs2-clutch-announce`
3. atualizar `cs2-instadefuse`
4. validar `cs2-instaplant`
5. migrar `cs2-retakes`
6. validar localmente
7. publicar `components/csgo/` no servidor
8. reiniciar e validar via `css_plugins list`

## Riscos

Risco alto:

- migracao do `cs2-retakes` por causa da divergencia grande entre seu fork local e o upstream `3.x`

Risco medio:

- `retakes_config.json` e `map_config/*.json` serem sobrescritos durante o deploy

Risco medio:

- build atualizar `plugins/` enquanto o servidor continua carregando `plugins/disabled/`

Risco baixo:

- `cs2-instadefuse` e `cs2-instaplant`, porque o codigo local esta alinhado com o upstream

## Decisao Recomendada

Para minimizar risco:

1. tratar `cs2-instadefuse`, `cs2-instaplant` e `cs2-clutch-announce` agora
2. corrigir o pipeline para `plugins/disabled/`
3. fazer a migracao do `cs2-retakes` em uma branch separada
4. publicar no servidor apenas depois de validar o modo retake completo

