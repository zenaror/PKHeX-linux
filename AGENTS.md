# PKHeX Linux — orientação para agentes

Este repositório é um fork do PKHeX que cria uma versão **nativa de Linux** (sem Wine), com a interface
nova em Avalonia (`PKHeX.Avalonia/`). O `PKHeX.Core` continua sendo a camada de domínio, e o
`PKHeX.WinForms` é a referência de comportamento.

## Antes de começar

1. Consulte primeiro a sua memória interna sobre este projeto e, depois, a OMM pelo MCP, no escopo
   `pkhex-linux` (inclua o global quando ajudar). Essa é a ordem de consulta, não de autoridade: confirme
   qualquer fato no código, no `PORTING.md`, no histórico do git ou numa medição antes de usar.
2. Confira `git status --short --branch`. Trabalhe só na branch `feature/linuxport`.
3. Leia a implementação existente (WinForms e Avalonia) antes de mudar algo.

As regras completas deste port estão na OMM, nas anotações "Regras do port PKHeX Linux (1/2)" e
"(2/2)", e no texto original do antigo `CLAUDE.md`, que segue no histórico do git (commit `eabab02a3`).

## Regras que não podem ser esquecidas

- **Saves:** nunca use um save real do usuário como fixture. Trabalhe com cópias num diretório temporário
  e confira que o original não mudou. Correção dos dados vem antes de tudo.
- **Core:** nada de dependência de interface no `PKHeX.Core`. Mude o Core só quando for realmente
  necessário e documente o desvio no `PORTING.md`.
- **Upstream:** diferenças mínimas nos projetos do upstream. Código novo do Linux vai em arquivos ou
  projetos novos. Não reformate, renomeie nem reorganize sem necessidade.
- **WinForms:** não renomeie nem apague o `PKHeX.WinForms`; ele precisa continuar compilando.
- **Git:** nunca descarte mudanças do usuário nem use comandos destrutivos. Commit e push só quando
  pedirem. Mensagens de commit em inglês, como no resto do repositório.
- **Plugins internos** nunca entram neste repositório, que é público.
- Não acrescente automaticamente trailers `Co-authored-by` do Claude/Anthropic aos commits.
  Preserve os créditos humanos e as atribuições legítimas do upstream.

## Compilar, testar e relatar

- Compile sempre: `dotnet build PKHeX.Avalonia/PKHeX.Avalonia.csproj` (e `-c Release` de vez em quando).
  Avisos novos devem ser corrigidos, não escondidos.
- Rode os testes do Core: `dotnet test Tests/PKHeX.Core.Tests/PKHeX.Core.Tests.csproj`. Nunca mude o
  resultado esperado de um teste só para ele passar.
- Build não prova que funciona: abra o app e exercite o caminho da mudança quando der. Se não der para
  testar a interface, diga isso.
- No relato, separe **BUILD VERIFIED**, **RUNTIME VERIFIED**, **NOT YET TESTED** e **BLOCKED**.
- Atualize o `PORTING.md` quando o estado real mudar. "Completo" quer dizer que compila e que o
  comportamento importante foi exercitado.

## Ao terminar

Registre na OMM o conhecimento durável novo: fatos confirmados (com a origem), decisões e motivos,
pendências e o que ainda não foi provado. Ao passar um trabalho adiante, deixe um handoff. Mantenha a
memória interna e a OMM atualizadas; a memória interna pode ser mais curta, mas não pode ter algo
importante que falte na OMM. Não grave senhas, tokens ou dados pessoais em nenhuma das duas.
