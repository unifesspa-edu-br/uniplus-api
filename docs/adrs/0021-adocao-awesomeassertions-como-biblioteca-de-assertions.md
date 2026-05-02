---
status: "accepted"
date: "2026-05-02"
decision-makers:
  - "Tech Lead"
consulted:
  - "Backend (CTIC)"
  - "FullStack (CTIC)"
  - "Q.A (CTIC)"
informed:
  - "Time Uni+"
---

# ADR-0021: Adoção de AwesomeAssertions como biblioteca de assertions de testes

## Contexto e enunciado do problema

A suíte de testes do `uniplus-api` adotou inicialmente `FluentAssertions 8.9.0` (`Directory.Packages.props`) como biblioteca de assertions, com 11 projetos `.csproj` de teste e 32 arquivos `.cs` referenciando `using FluentAssertions;`.

A partir da versão 8, a Fluent Assertions passou a ser distribuída pela Xceed sob modelo de **licença comercial paga** para uso fora de cenários estritamente não comerciais. O `uniplus-api` é mantido por uma autarquia federal (Unifesspa) e o uso continuado da v8 sem aquisição formal de licença configura uma exposição regulatória e contratual evitável, especialmente em vista da política institucional de preferência por software open source com licenças permissivas.

Adicionalmente, o projeto prevê uma suíte de testes robusta para regras de negócio sensíveis (classificação de candidatos, aplicação de cotas, desempate, recursos), além de testes contratuais sobre DTOs, responses HTTP, eventos de domínio, comandos Wolverine e grafos de objetos transferidos entre módulos. A biblioteca de assertions adotada precisa suportar bem **equivalência estrutural configurável**, não apenas assertions diretas.

A Story `unifesspa-edu-br/uniplus-api#169` formaliza a remediação e decompõe a entrega em duas tasks: `#170` (esta ADR) e `#171` (refactor mecânico).

## Drivers da decisão

- **Compliance e licenciamento.** Eliminar dependência de pacote sob licença comercial paga não adquirida.
- **Capacidade de equivalência estrutural.** Suportar comparação configurável de DTOs, responses, eventos e grafos complexos por shape — `BeEquivalentTo`, customização por membro/tipo/caminho, `AssertionScope`. **Critério decisivo** entre os candidatos open source.
- **Política open source first.** Preferência institucional por bibliotecas com licenças permissivas (MIT, Apache-2.0, BSD).
- **Continuidade de manutenção.** Optar por biblioteca com mantenedores ativos e sinal de comunidade saudável.
- **Custo de migração.** A baixa fricção a partir do estado atual é um fator secundário desejável, não decisivo.

## Opções consideradas

- **AwesomeAssertions** (Apache-2.0) — fork comunitário do Fluent Assertions v7, mantido por `AwesomeAssertions/meenzen` no GitHub.
- **Shouldly** (BSD-3-Clause) — biblioteca de assertions independente, sintaxe `Should*` direta, foco em legibilidade.
- **`Assert` puro do xUnit** — sem biblioteca de assertions adicional.

## Resultado da decisão

**Escolhida:** "AwesomeAssertions", porque é o único candidato que combina licença permissiva (Apache-2.0) e capacidade avançada de equivalência estrutural configurável — os dois drivers de maior peso.

A versão alvo é **AwesomeAssertions 9.x** (estável atual: 9.4.0). A migração no PR-2 (`#171`) é mecânica: trocar a entry em `Directory.Packages.props`, as 11 referências em `.csproj` de teste e os 32 `using FluentAssertions;` por `using AwesomeAssertions;`, além de atualizar as docs (`CLAUDE.md`, `CONTRIBUTING.md`) que listam a biblioteca de assertions. Nenhuma alteração no corpo dos testes é prevista.

A escolha **não se reduz à facilidade de migração**. Mesmo num cenário hipotético em que a suíte fosse pequena, AwesomeAssertions ainda venceria pela capacidade de equivalência estrutural configurável — exigência projetada para a fase de implementação do motor de classificação ([ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md)) e dos contratos REST ([ADR-0015](0015-rest-contract-first-com-openapi.md)). A continuidade da DSL fluente, embora útil, é consequência da escolha — não sua justificativa.

**Guardrail de uso.** `BeEquivalentTo` e seus operadores estruturais devem ser usados com critério para validação de **contratos e shapes** (DTOs, responses HTTP, payloads de eventos). Para regras de negócio centrais — em especial decisões de aprovação, reprovação, classificação e desempate — a suíte deve preferir **assertions explícitas e nominais** sobre o resultado, evitando que uma comparação estrutural genérica mascare uma quebra semântica importante.

## Consequências

### Positivas

- Licença Apache-2.0 elimina o risco regulatório e o custo de licenciamento associados à v8 da Xceed.
- Migração custo-zero: a API pública de AwesomeAssertions v9 é drop-in compatível com a DSL fluente já em uso.
- Capacidade plena de equivalência estrutural preservada para os cenários previstos (motor de classificação, contratos REST, eventos Wolverine).
- Independência de fornecedor: o fork é mantido pela comunidade sob licença permissiva.

### Negativas

- AwesomeAssertions é um fork relativamente recente (publicado a partir do divisor de águas de licenciamento da v8). A maturidade do projeto upstream é alta (vem de Fluent Assertions v7), mas o time precisa monitorar a saúde do fork (releases, issues, mantenedores).
- Adoção bem-sucedida da DSL fluente facilita escrita de assertions longas e verbosas; isso exige disciplina de revisão para não diluir a clareza dos testes.

### Neutras

- Versionamento divergente do upstream: AwesomeAssertions usa numeração v9.x, enquanto Fluent Assertions ficou em v8 — não há ambiguidade de versão entre as duas, mas a referência cruzada exige atenção.

## Confirmação

Mecanismos para confirmar a decisão ao longo do tempo:

1. **Grep no monorepo** (executável em CI ou localmente):

   ```bash
   grep -rn "FluentAssertions" --include='*.cs' --include='*.csproj' --include='*.props' . \
     | grep -v 'docs/adrs/0021'
   ```

   Resultado esperado pós-PR-2: zero ocorrências. O PR-2 (`#171`) deve incluir a atualização das docs (`CLAUDE.md` seção *Stack e versões*, `CONTRIBUTING.md` tabela *Padrões de Código*) para evitar que o grep retorne ocorrências espúrias em arquivos de documentação que ainda listem `FluentAssertions`.

2. **Lint rule futura** (issue de follow-up): adicionar um analisador Roslyn ou check de CI dedicado que falhe quando `using FluentAssertions;` for introduzido. Tracking em issue separada.

3. **Revisão de PRs**: o checklist de `/review-pr` deve sinalizar uso novo de `FluentAssertions` como bloqueador.

## Prós e contras das opções

### AwesomeAssertions

- Bom, porque é Apache-2.0 (sem custo de licença, sem amarra contratual).
- Bom, porque é drop-in compatível com a DSL fluente já em uso (32 arquivos migrados sem alteração de corpo).
- Bom, porque oferece suporte completo a equivalência estrutural, customização por membro/tipo/caminho e `AssertionScope` — necessários para testes de contratos e grafos complexos.
- Bom, porque preserva o tempo de aprendizado já investido pelo time na DSL.
- Ruim, porque é um fork comunitário relativamente novo — exige acompanhamento periódico de saúde do projeto upstream.

### Shouldly

- Bom, porque é maduro, BSD-3-Clause, com sintaxe direta e muito legível para assertions simples.
- Bom, porque tem comunidade ativa e ecossistema estabelecido.
- Ruim, porque a equivalência estrutural (`ShouldBeEquivalentTo`) é menos configurável — limitada para customização por membro, tipo ou caminho. Há registro upstream da limitação na [issue shouldly/shouldly#1116](https://github.com/shouldly/shouldly/issues/1116) (aberta solicitando suporte a configuração granular de `ShouldBeEquivalentTo`, ainda sem entrega no momento desta ADR).
- Ruim, porque a migração a partir de Fluent Assertions exigiria reescrita manual de assertions estruturais nos 32 arquivos atuais.
- Ruim, porque a divergência de DSL impõe custo de relearning ao time.

### `Assert` puro do xUnit

- Bom, porque elimina qualquer dependência adicional.
- Ruim, porque exige reescrita de toda a suíte de assertions existente.
- Ruim, porque não oferece equivalência estrutural configurável — esse cenário precisaria ser implementado manualmente com helpers customizados.
- Ruim, porque mensagens de erro são significativamente menos informativas que a DSL fluente para diagnóstico em CI.

## Mais informações

- [Repositório AwesomeAssertions](https://github.com/AwesomeAssertions/AwesomeAssertions) (fork comunitário Apache-2.0)
- [Pacote NuGet AwesomeAssertions](https://www.nuget.org/packages/AwesomeAssertions)
- [Documentação de equivalência estrutural (Fluent/AwesomeAssertions)](https://fluentassertions.com/objectgraphs/)
- [Política de licenciamento da Fluent Assertions / Xceed](https://xceed.com/documentation/xceed-fluent-assertions-for-net/)
- [Documentação Shouldly](https://docs.shouldly.org/)
- [Issue Shouldly sobre customização de `ShouldBeEquivalentTo`](https://github.com/shouldly/shouldly/issues/1116)
- Story: [`unifesspa-edu-br/uniplus-api#169`](https://github.com/unifesspa-edu-br/uniplus-api/issues/169)
- Tasks: [`#170`](https://github.com/unifesspa-edu-br/uniplus-api/issues/170) (esta ADR), [`#171`](https://github.com/unifesspa-edu-br/uniplus-api/issues/171) (refactor)
- ADRs relacionadas: [ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md) (motor de classificação), [ADR-0015](0015-rest-contract-first-com-openapi.md) (contratos REST)
