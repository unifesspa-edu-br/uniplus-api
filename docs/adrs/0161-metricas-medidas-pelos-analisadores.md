---
status: "accepted"
date: "2026-11-12"
decision-makers:
  - "Tech Lead"
consulted: []
informed: []
---

# ADR-0161: Métricas de qualidade medidas pelos analisadores

## Contexto e enunciado do problema

A [ADR-0156](0156-limites-de-tamanho-e-complexidade-do-codigo.md), emendada em 2026-11-11, passou a contar linhas físicas e a incluir os testes, mas nenhum analisador conta assim. A S138 conta só linhas com código, não roda em projeto de teste e não mede acessor de propriedade, e o limite de arquivo dependia de um script do próprio repositório que só mede `src/`. A regra escrita e o aviso da build divergiam, e só a revisão sabia qual valia.

## Drivers da decisão

- A regra registrada é a que a build mostra, sem contagem paralela.
- Medir com ferramenta de mercado, como já pede a [ADR-0155](0155-analisadores-de-qualidade-na-build.md), e não com script próprio.
- Medir produção e testes.

## Opções consideradas

- Manter a contagem física cobrada na revisão, com os analisadores como sinal auxiliar.
- Medir método pela S138 e arquivo pela S104, deixando os testes sem limite por método.
- Medir método pela MA0051 e arquivo pela S104.

## Resultado da decisão

**Escolhida:** medir método pela MA0051 e arquivo pela S104, porque a MA0051 roda em projeto de teste e mede acessor, e a S104 substitui o script próprio. O número que vale é o do aviso do analisador, mesmo quando a contagem dele deixa o método ou o arquivo com mais linhas físicas que a contagem anterior.

| Medida | Limite | Analisador |
|---|---|---|
| Linhas por método | 15 | MA0051 |
| Linhas com código por arquivo | 400 | S104 |
| Parâmetros | 7 | S107 |
| Complexidade cognitiva | 15 | S3776 |
| Complexidade ciclomática | 10 | S1541 (não roda em projeto de teste) |

A MA0051 passa a ser a dona do tamanho de método, e a S138 fica desligada. O script próprio de tamanho de arquivo e a lista de exceções dele saem. As regras continuam como aviso, e cada módulo passa a erro quando zerar a dívida. Esta ADR emenda a ADR-0155 (dono do tamanho de método e gate próprio de arquivo) e a ADR-0156 (forma de contar e de cobrar os limites). A forma de contar de cada analisador está no `CONTRIBUTING.md`, seção "Métricas de qualidade".

## Consequências

### Positivas

- O aviso da build é a medida: autor, revisão e CI veem o mesmo número.
- Os testes passam a ter limite de método medido.

### Negativas

- A complexidade ciclomática não é medida em projeto de teste; ali vale a cognitiva.
- Sem o script, arquivo novo acima do limite deixa de barrar o CI até o módulo zerar a dívida; até lá, a revisão cobra.

### Neutras

- Configuração do EF e migrations continuam fora do limite de método, e migrations, fora do de arquivo.

## Confirmação

`dotnet build UniPlus.slnx -c Release` termina sem erro, e os únicos avisos são MA0051, S104, S107, S3776 e S1541.

## Mais informações

- [ADR-0155](0155-analisadores-de-qualidade-na-build.md) e [ADR-0156](0156-limites-de-tamanho-e-complexidade-do-codigo.md), emendadas aqui.
