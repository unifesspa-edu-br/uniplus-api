---
status: "proposed"
date: "2026-11-13"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Equipe de desenvolvimento do Uni+"
---

# ADR-0167: Serviços de aplicação com dependência no construtor e orquestrador único do congelamento da versão

## Contexto e enunciado do problema

As operações que congelam uma versão de configuração (publicação, retificação de uma vez e fechamento da sessão editorial) recebem mais de vinte dependências no handler e as repassam, como argumento, a auxiliares estáticos. A ordem das recusas se repete nos três handlers, e a consulta de conformidade repete as leituras do catálogo e do calendário. Os limites medidos pelo analisador (ADR-0161) não cabem sem mudar onde a dependência vive, e agrupar dependências num objeto só para caber na métrica é proibido.

## Drivers da decisão

- Parâmetros e linhas medidos pelo analisador, sem supressão (ADR-0160, ADR-0161).
- Codegen do Wolverine sob `NotAllowed`, sem service location nova (ADR-0098).
- Uma só ordem de recusas para as três operações (ADR-0125).
- Transação e trava do agregado no handler (ADR-0004, ADR-0110).

## Opções consideradas

- **Serviços públicos `Scoped` e orquestrador do congelamento.** Prós: sem service location, uma só ordem de recusas. Contras: mais tipos públicos na Application.
- **Serviços internos resolvidos por service location.** Prós: não amplia a superfície pública. Contras: contraria a ADR-0098 e depende de opt-in por tipo.
- **Objeto que agrupa as dependências.** Prós: poucas mudanças. Contras: só desloca a contagem de parâmetros e esconde a responsabilidade.
- **Manter os estáticos e só dividir os handlers.** Prós: diff menor. Contras: a soma das dependências volta ao handler.

## Resultado da decisão

**Escolhida:** "serviços públicos construídos pelo codegen e orquestrador único", porque reduz as dependências do handler sem service location e sem agrupamento artificial.

1. Dependência de infraestrutura fica no construtor de um serviço de aplicação público e selado, registrado como `Scoped`. Espera-se que o codegen o construa inline, sem opt-in de service location, o que a guarda de service location do host confirma.
2. No que esta decisão altera, auxiliar estático só permanece quando não tem dependência; quem lê porta passa a ser serviço. Os estáticos que recebem porta e não são alterados por esta decisão ficam para uma issue própria, e a decisão não os declara conformes.
3. `record` só para dado com significado de domínio, como a origem do congelamento (primeira publicação ou retificação sobre uma base). Nunca para agrupar dependências.
4. Um orquestrador único concentra a ordem das recusas e o descarte do rastreamento na recusa. O handler carrega o agregado com trava, confere o que precede a base e escolhe a transição do agregado.

## Consequências

### Positivas

- Ordem das recusas num lugar só; a diferença entre as operações é dado, não ramificação.
- Handlers e consulta dentro dos limites, sem supressão.
- A versão vigente é lida uma vez por retificação.

### Negativas

- A Application expõe mais tipos públicos.
- Os testes montam serviços por um construtor de teste.
- As guardas de arquitetura que liam texto viram regras de dependência; as de ordem e de contagem passam a ser provadas por comportamento.

### Neutras

- A ordem e a forma das recusas (`Failure` ou lote de validação) permanecem as de cada operação; a diferença entre elas vem do dado da origem, e não de ramificação por tipo de operação.

## Confirmação

- Guarda de service location do host sob `NotAllowed`.
- Regras de arquitetura: o handler depende do orquestrador; só a conferência do congelamento antecipa os portões; só a gravação apaga o rascunho.
- Analisador sem aviso de parâmetros nem de linhas.

## Mais informações

- [ADR-0004](0004-outbox-transacional-via-wolverine.md), [ADR-0098](0098-politica-de-service-location-do-codegen-wolverine.md), [ADR-0110](0110-retificacao-como-sessao-editorial.md), [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md), [ADR-0160](0160-supressao-de-aviso-do-analisador-proibida.md), [ADR-0161](0161-metricas-medidas-pelos-analisadores.md).
