# Orientação para revisão — testes de arquitetura

Complementa o [`CONTRIBUTING.md`](CONTRIBUTING.md). Vale para toda revisão de PR deste repositório.

Atualizado em 13/11/2026.

## Regra

Teste de arquitetura usa **regras declarativas do ArchUnitNET** (`TngTech.ArchUnitNET`): dependência entre tipos, chamada de membro (`CallAny`/`NotCallAny`), namespace, assembly, atributo e herança. A **reflexão sobre metadados de tipos, membros e atributos** entra só onde o ArchUnitNET não alcança. Exemplos: parâmetros do `Handle` e `AuthorizeAttribute.Roles`. O modelo efetivo do EF Core e o esquema do banco devem ser conferidos em testes de integração.

Não são aceitos:

- regex ou `File.ReadAll*` sobre o fonte C# ou TypeScript;
- leitor de IL escrito à mão (`Mono.Cecil`, `GetMethodBody`, leitor de opcodes próprio);
- árvore sintática ou semântica do Roslyn montada dentro do teste;
- `Regex.IsMatch` para filtrar tipos por nome: use os predicados do ArchUnitNET (`ResideInNamespace`, `HaveFullName`, `AreDeclaredIn`) ou tipos-âncora (`typeof(X).Assembly`).

Esses formatos deixam o teste frágil e caro de manter. **O que não puder ser garantido de forma profissional não vira teste**: fica na lista abaixo e passa a ser conferido na revisão.

## Quando a regra não cabe no ArchUnitNET

Antes de escolher outra técnica, nesta ordem:

1. **O desenho garante?** Tipo, visibilidade ou construtor precisam impedir a violação na compilação. `internal` não restringe chamadores dentro do próprio Domain, e um orquestrador único só basta quando as demais entradas não podem contorná-lo.
2. **Teste de comportamento garante?** Um teste unitário ou de integração prova o efeito: ordem por `Received.InOrder`, estado final no banco, recusa com o código esperado.
3. **Ferramenta pronta e configurada garante?** Só com licença compatível com a política do workspace: MIT, Apache-2.0, BSD, MPL-2.0 ou LGPL, sem depender de plano pago. Analisador pronto configurado por arquivo é aceito. Analisador escrito no repositório não é.
4. Se nada disso couber, a regra entra na lista "Conferido na revisão".

## Remoção dos testes legados

Os detectores que liam C# como texto, filtravam tipos com `Regex.IsMatch` ou liam IL manualmente foram retirados dos projetos `*ArchTests`. As garantias ainda sem substituto automático constam da [issue #2423](https://github.com/unifesspa-edu-br/uniplus-api/issues/2423). A remoção não significa que essas propriedades estejam comprovadas: até cada substituição ser implementada e executada, elas são critérios de revisão do PR.

Continuam válidos os testes que leem **dados** versionados (JSON e retratos), inspecionam o modelo efetivo do EF Core ou usam reflexão de metadados. O deslocamento dos testes de dados para projetos apropriados também está na issue #2423.

Substitutos já escritos nesta worktree:

- `DominioNaoUsaGuidNewGuidTests`: regra de chamada do ArchUnitNET, com canário que chama `Guid.NewGuid`.
- `DecisaoPeloPontoUnicoTests`: regra de chamada de `RequireRole` com a exceção da política técnica de instalação.
- `AcervoPublicoCaminhoUnicoDeEscritaTests`: regra de chamada de `CopiarComoObjetoPublicoAsync`, com a implementação permitida como controle positivo.
- `InstanteEmBelemTests`: comportamento do offset resolvido pela zona em 1985 e 2026.

A compilação dos projetos afetados passou. Na sessão TEST, os projetos `Unifesspa.UniPlus.ArchTests` e `Unifesspa.UniPlus.Selecao.ArchTests` executaram, respectivamente, 124 e 48 testes sem falhas; `InstanteEmBelemTests` passou nos dois casos. Os controles positivos de `Guid.NewGuid`, `RequireRole` e `CopiarComoObjetoPublicoAsync` também passaram. A suíte geral de arquitetura levou cerca de 8,2 minutos; o custo das regras de chamada de membro deve ser observado em mudanças futuras.

## Garantias a recompor

| Propriedade | Destino |
|---|---|
| Salvar antes de apagar rascunho; ausência de rascunho após publicação, retificação e fechamento | Testes unitários e de integração dos fluxos, incluindo falha no flush |
| `FOR UPDATE`, sessão carregada, transação ambiente, entrada de mutação e ordem do descarte | Teste de concorrência em PostgreSQL e testes de comportamento dos handlers |
| Escrita das partes do agregado apenas pela raiz ou pelos planos autorizados | Reexaminar visibilidade e desenho; testes de comportamento ou regra declarativa com canário quando possível |
| Ausência de gatilho próprio em `rol_de_regras` e de FK entre schemas | Testes de integração sobre `pg_trigger` e `pg_constraint` após migrations |
| Migration não sobrescreve proteção aprovada | Teste de integração com migração entre duas versões e valor aprovado previamente |
| Consultas com várias coleções usam `SplitQuery` | Padrão no `SelecaoDbContext`, teste da opção efetiva e manutenção do teste SQL da consulta crítica |
| Chamadas proibidas de relógio estático e carregamento de leitura em comandos | Avaliar regras ArchUnitNET de membro com canários; quando não couber, teste de comportamento ou revisão |
| Completude dos códigos de `DomainError` emitidos | Catálogo tipado antes de teste de completude; mapeamentos e status por reflexão permanecem testados |
| Restrições de autorização, publicação, layout INEP, fixture de container e portões de conformidade | Avaliar cada uma por desenho, comportamento e ArchUnitNET; registrar o que permanecer revisão |

## Conferido na revisão

Até que a issue #2423 esteja concluída, o revisor confere no diff:

1. Nenhum handler chama portão de conformidade fora do orquestrador do congelamento.
2. Handler de comando carrega a raiz pela entrada de mutação e não contorna a transação ambiente.
3. Nenhuma regra usa código literal de tipo de ato, etapa, fase ou literal institucional no portão do cronograma.
4. Nomes e strings novos não recolocam o vocabulário `Edital` fora do contexto permitido.
5. Literais `realm_access` e `resource_access` não criam nova decisão por papel fora do ponto único. A chamada `RequireRole` tem regra declarativa; seu argumento literal permanece revisão.
6. Testes novos não escrevem offset de fuso à mão nem fazem asserção tautológica.
7. Mutadores de partes do agregado não ganham chamadores ou visibilidade sem justificativa e prova de comportamento.
8. Códigos de `DomainError` novos são estáveis, registrados no mapeamento HTTP quando necessário e coerentes com os consumidores; não são montados por interpolação.
9. `Program.cs` não registra rota de negócio fora de controller ou de endpoint técnico autorizado pela ADR-0036.
10. Chamadas novas a relógio estático, `AsSingleQuery()` e carregamento de leitura em comando são revisadas até haver os substitutos da issue #2423.
11. Mudanças em migrations, restauração, publicação, autorização, fixture de container e layout INEP mantêm as garantias descritas na issue #2423.

O limite de linhas dos métodos é medido pela MA0051 (ADR-0161).

## Decisões

- **Não adotar BannedApiAnalyzers neste momento.** A [versão estável 5.6.0 no NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers) foi publicada em julho de 2026, com licença MIT; o [desenvolvimento dos analisadores](https://github.com/dotnet/roslyn-analyzers) foi transferido para o repositório Roslyn da Microsoft. `IsInRole` já tem dono no ArchUnitNET. As regras de `Guid.NewGuid` e `RequireRole` foram escritas e executadas na mesma biblioteca, com escopo e exceção explícitos. Há um `RequireRole` deliberado na política `Instalacao`, portanto a proibição geral por projeto seria ampla demais. Reavaliar o analisador se alguma regra não puder ser expressa de modo legível. Não há emenda à ADR-0155 por esta decisão.
- **Consulta com várias coleções: adotar `SplitQuery` como padrão do `SelecaoDbContext`.** Testar a configuração efetiva e manter o teste de integração que verifica o SQL da consulta crítica. `AsSingleQuery()` pode sobrescrever o padrão; o revisor confere toda introdução dessa chamada no diff. A combinação com `MultipleCollectionIncludeWarning` não acrescenta cobertura, pois o [EF Core não emite esse aviso quando há configuração global](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries). A configuração altera número de comandos SQL e pode afetar consistência entre as leituras. Antes da troca, verificar também as consultas com uma coleção em `LoteDeNotasDoEnemRepository` e `AtoDoProcessoRepository`, hoje sem `AsSplitQuery()`, além dos fluxos sensíveis à concorrência. O detector textual foi removido; a substituição está pendente na issue #2423. Até lá, o revisor confere as consultas alteradas.
- **Códigos de `DomainError`: manter na revisão.** Vários módulos já têm catálogos `*ErrorCodes`; usá-los onde couber. Não impor um catálogo único nem migrar todos os códigos apenas para viabilizar um teste. Testar códigos que fazem parte de contrato por comportamento. Literais e interpolações restantes ficam no item 8 da revisão.

## Avaliadas e não adotadas

- **[Opengrep](https://github.com/opengrep/opengrep) (LGPL-2.1).** As regras consideradas para os literais seriam padrões sintáticos, sem demonstrar a garantia de tipo e fluxo necessária. Exigiria ferramenta e etapa de CI novas para regras de valor baixo.
- **[Semgrep](https://semgrep.dev/docs/writing-rules/glossary).** A edição comunitária é open source, mas não oferece análise entre arquivos; esse recurso usa o mecanismo proprietário. A [edição gratuita hospedada](https://semgrep.dev/pricing/) oferece recursos Pro com limites de uso, portanto não atende ao requisito de ferramenta aberta e autônoma.
- **[NetArchTest.Rules](https://www.nuget.org/packages/NetArchTest.Rules/) (MIT).** Sem nova versão publicada desde 2021 e sem capacidade adicional necessária frente ao ArchUnitNET.
- **Analisador Roslyn próprio, regra customizada do Sonar ou do Meziantou.** O primeiro é ferramenta mantida no repositório. O pacote Sonar usado na build não oferece configuração de regras C# próprias; no servidor, C# aceita [importação de problemas de ferramenta externa](https://docs.sonarsource.com/sonarqube-server/2026.1/extension-guide/adding-coding-rules), que ainda exigiria uma ferramenta nova. O Meziantou não oferece mecanismo de configuração de uma regra C# arbitrária.
