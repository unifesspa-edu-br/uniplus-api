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

## Transição da dívida existente

Ainda há testes de arquitetura legados que leem o fonte como texto. Eles continuam valendo até serem substituídos. A substituição segue estas regras:

- Um teste legado só é removido no mesmo PR em que cada garantia automática necessária tem substituto implementado e verde. Quando a garantia não couber em teste profissional, o PR registra expressamente o critério de revisão que a assume antes de retirar o detector textual.
- PR que altera um teste legado migra esse teste para a forma aceita. A regra é desta orientação; não deriva da seção "Dívida existente e CI" do `CONTRIBUTING.md`, que trata das métricas de tamanho e complexidade.
- Classe legada com várias garantias só sai quando todas têm destino definido e aplicado. As classes já inventariadas estão abaixo.
- Teste novo nasce na forma aceita.
- ADR que prescreve teste textual é emendada no PR que faz a migração. É o caso da ADR-0032 (`Guid.NewGuid`) e da ADR-0068 (relógio).

## Destino dos testes legados

O destino abaixo é um plano de migração, não uma garantia já implantada. Há provas parciais, como o teste de integração da consulta crítica com `AsSplitQuery()` e a verificação de FK do schema de Autorização. Cada teste legado permanece até todas as garantias que carrega terem destino aplicado.

| Regra | Teste legado que a protege hoje | Destino previsto |
|---|---|---|
| Ordem "salvar antes de apagar o rascunho" | `RascunhoMorreComOAtoTests` (texto) | Teste unitário da gravação da versão com `Received.InOrder`, com caso de falha no flush |
| Rascunho não sobrevive à publicação, retificação ou fechamento | `RascunhoMorreComOAtoTests` (texto) | Teste de comportamento do fluxo de congelamento |
| `FOR UPDATE` trava e carrega a sessão | `CarregamentoDeMutacaoTests.CarregamentoDeMutacao_TrazSessaoETrava` (texto) | Teste de integração (Testcontainers) com duas transações concorrentes |
| Caller do carregamento de mutação não desliga a transação ambiente | `CarregamentoDeMutacaoTests.CarregamentoDeMutacao_NaoConviveComTransacaoDesligada` (texto) | Sem substituto definido: o teste de concorrência do repositório não prova a propriedade dos handlers. O teste legado permanece |
| Handler de comando carrega a raiz pelo carregamento de mutação | `CarregamentoDeMutacaoTests.HandlersDeComando_NaoUsamCarregamentoDeLeitura` e `HandlersDeComando_UsamCarregamentoDeMutacao` (texto) | A proibição do carregamento de leitura vira regra do ArchUnitNET. Ela é parcial: não impede outro método de carregamento. A verificação positiva permanece no teste legado até haver forma declarativa |
| Só os callers autorizados mutam etapa e encerram a sessão; descarte repõe antes de encerrar | `CarregamentoDeMutacaoTests.AtualizarDadosDeEtapa_SoPelosCallersAutorizados`, `EncerrarSessao_SoPelosCallersAutorizados` e `Descarte_RestauraAntesDeEncerrarASessao` (texto) | Restrição de chamador: candidata a regra do ArchUnitNET sobre quem chama o método, a confirmar na migração. Ordem do descarte: teste unitário com `Received.InOrder`. Sem substituto confirmado, o teste legado permanece |
| Fora dos planos, ninguém vincula, repõe nem altera partes do processo | `FronteirasDoProcessoSeletivoTests.A4_SoOsPlanosVinculamEMutam` | `internal` não basta: impede outros assemblies, mas não outra política do próprio Domain. O teste permanece até haver substituto que preserve a fronteira dentro do Domain |
| **Ausência** de gatilho próprio em `rol_de_regras` | `RolDeRegrasSemGatilhoTests` (texto da migration) | Teste de integração após aplicar as migrations: consultar `pg_trigger` para a tabela no schema da Seleção, excluindo gatilhos internos (`tgisinternal`) de restrições do PostgreSQL |
| Ausência de FK entre schemas de módulos | `FatoCandidatoCatalogoTests.Migrations_SemFkCrossSchemaParaFatoCandidato` e outros detectores textuais | Completar a cobertura em testes de integração com `pg_constraint` e os schemas de origem e destino; `PersistenciaDoPerfilTests.Migration_NaoCriaFkEntreSchemas` já cobre Autorização |
| `UpdateData` de migration não reescreve proteção aprovada | `SementeDoCatalogoNaoSobrescreveProtecaoAprovadaTests` (texto) | Teste de integração com `IMigrator.MigrateAsync(alvo)` antes e depois da aprovação |
| Consulta com várias coleções usa divisão por padrão | `SelecaoRepositoriosUsamSplitQueryTests` (texto) | Configurar `SplitQuery` como padrão do `SelecaoDbContext` e testar essa opção; revisar cada `AsSingleQuery()` explícito |
| `Program` não mapeia rota | `PontosUnicosDeAcessoTests.ProgramNaoMapeiaRota` (texto) | Revisão do diff de `Program.cs`: `EndpointDataSource` não revela quem registrou a rota. Retirar só esse detector quando o critério de revisão estiver incorporado ao fluxo do PR |
| Seeds e contratos JSON | Testes de dados nos `*ArchTests` | Os mesmos testes, movidos para fora dos projetos `*ArchTests` |

## Conferido na revisão (não vira teste)

O revisor confere estes pontos no diff. Nenhum deles tem forma declarativa ou ferramenta proporcional.

1. **Quantas vezes e em que posição o fluxo chama cada portão de conformidade.** O orquestrador único do congelamento e os testes de recusa garantem o resultado. Na revisão, confira que nenhum handler volta a chamar portão fora dele.
2. **"Quem carrega a raiz usa o carregamento de mutação."** A proibição do carregamento de leitura nos comandos é proteção parcial: não impede um método de carregamento novo. Enquanto a verificação positiva for só do teste legado, confira na revisão o carregamento usado em handler novo e recuse método de carregamento novo da raiz.
3. **Literal de código de tipo de ato, de etapa ou de fase em C# e SQL, e literal institucional no portão do cronograma.** Literal não é dependência entre tipos. Na revisão, recuse regra presa a código fixo (o sistema é baseado em configuração).
4. **Vocabulário "Edital" dentro de strings.** Só nomes de tipo e membro são conferidos automaticamente.
5. **Literais `realm_access` e `resource_access`.** Hoje, `IsInRole` é proibido por regra do ArchUnitNET, com escopo e exceções próprios, e `RequireRole` só é detectado pelo teste legado textual `PapelFixoPorUnidadeTests`, que continua até ter substituto declarativo. O literal é conferido na revisão.
6. **Offset de fuso escrito à mão e asserção tautológica em teste.** É estilo de teste.
7. **Escrita de estado transitiva fora dos planos do agregado.** Há mutadores `internal` e também mutadores públicos necessários, como `EtapaProcesso.AtualizarDados`; a visibilidade não fecha sozinha essa fronteira. O teste legado `A4_SoOsPlanosVinculamEMutam` continua até a migração. Na revisão, confira o chamador de cada mutador novo e toda ampliação de visibilidade.
8. **Código de `DomainError` escrito como literal ou interpolação.** Prefira os catálogos `*ErrorCodes` já existentes. Quando surgir um código novo, confira no diff se é estável, registrado no mapeamento HTTP quando necessário e coerente com os consumidores.
9. **Mapeamento de rota no `Program.cs`.** Confira no diff que uma rota de negócio nova permanece em controller ou no ponto técnico autorizado pela ADR-0036. A lista de endpoints em runtime não informa qual arquivo registrou a rota.

O limite de linhas dos métodos é medido pela MA0051 (ADR-0161). Detectores-canário de regex saem junto com os detectores textuais; nenhum dos dois é item de revisão.

## Decisões

- **Não adotar BannedApiAnalyzers neste momento.** A [versão estável 5.6.0 no NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers) foi publicada em julho de 2026, com licença MIT; o [desenvolvimento dos analisadores](https://github.com/dotnet/roslyn-analyzers) foi transferido para o repositório Roslyn da Microsoft. A ferramenta é viável, mas `IsInRole` já tem dono no ArchUnitNET; `Guid.NewGuid` e `RequireRole` são candidatas a regras de chamada de membro nessa biblioteca e precisam ser validadas na migração. Há um `RequireRole` deliberado na política `Instalacao`, portanto a proibição geral por projeto seria ampla demais. Migrar primeiro os testes textuais para regras declarativas com escopo e exceções explícitos. Reavaliar a ferramenta se a regra não puder ser expressa de modo legível. Não há emenda à ADR-0155 por esta decisão.
- **Consulta com várias coleções: adotar `SplitQuery` como padrão do `SelecaoDbContext`.** Testar a configuração efetiva e manter o teste de integração que verifica o SQL da consulta crítica. `AsSingleQuery()` pode sobrescrever o padrão; o revisor confere toda introdução dessa chamada no diff. A combinação com `MultipleCollectionIncludeWarning` não acrescenta cobertura, pois o [EF Core não emite esse aviso quando há configuração global](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries). A configuração altera número de comandos SQL e pode afetar consistência entre as leituras. Antes da troca, verificar também as consultas com uma coleção em `LoteDeNotasDoEnemRepository` e `AtoDoProcessoRepository`, hoje sem `AsSplitQuery()`, além dos fluxos sensíveis à concorrência. A remoção de `SelecaoRepositoriosUsamSplitQueryTests` ocorre no mesmo PR que implementa e valida o substituto.
- **Códigos de `DomainError`: manter na revisão.** Vários módulos já têm catálogos `*ErrorCodes`; usá-los onde couber. Não impor um catálogo único nem migrar todos os códigos apenas para viabilizar um teste. Testar códigos que fazem parte de contrato por comportamento. Literais e interpolações restantes ficam no item 8 da revisão.

## Avaliadas e não adotadas

- **[Opengrep](https://github.com/opengrep/opengrep) (LGPL-2.1).** As regras consideradas para os literais seriam padrões sintáticos, sem demonstrar a garantia de tipo e fluxo necessária. Exigiria ferramenta e etapa de CI novas para regras de valor baixo.
- **[Semgrep](https://semgrep.dev/docs/writing-rules/glossary).** A edição comunitária é open source, mas não oferece análise entre arquivos; esse recurso usa o mecanismo proprietário. A [edição gratuita hospedada](https://semgrep.dev/pricing/) oferece recursos Pro com limites de uso, portanto não atende ao requisito de ferramenta aberta e autônoma.
- **[NetArchTest.Rules](https://www.nuget.org/packages/NetArchTest.Rules/) (MIT).** Sem nova versão publicada desde 2021 e sem capacidade adicional necessária frente ao ArchUnitNET.
- **Analisador Roslyn próprio, regra customizada do Sonar ou do Meziantou.** O primeiro é ferramenta mantida no repositório. O pacote Sonar usado na build não oferece configuração de regras C# próprias; no servidor, C# aceita [importação de problemas de ferramenta externa](https://docs.sonarsource.com/sonarqube-server/2026.1/extension-guide/adding-coding-rules), que ainda exigiria uma ferramenta nova. O Meziantou não oferece mecanismo de configuração de uma regra C# arbitrária.
