---
status: "accepted"
date: "2026-11-06"
decision-makers:
  - "Tech Lead"
consulted: []
informed: []
---

# ADR-0156: Limites de tamanho e complexidade do código

## Contexto e enunciado do problema

Arquivos de milhares de linhas e métodos longos dificultam revisar regra de negócio. O projeto precisa de limites objetivos, e parte do código atual ainda não os cumpre.

## Drivers da decisão

- Limite objetivo, medido na build, e não opinião de revisão.
- Dividir por assunto: código escrito só para caber na métrica não resolve o problema.
- A refatoração completa não cabe no prazo do projeto agora.

## Opções consideradas

- Limites valendo como erro, depois de refatorar todo o código.
- Limites publicados agora, com o que falta tratado como dívida registrada.
- Limite maior de método (30 linhas), mais fácil de cumprir.

## Resultado da decisão

**Escolhida:** limites publicados agora, com o que falta tratado como dívida registrada, porque refatorar tudo antes atrasaria o projeto e um limite maior aceitaria métodos que misturam assuntos.

| Medida | Limite | Como é cobrada |
|---|---|---|
| Linhas físicas por arquivo de `src/` | 400 | Erro no gate `tools/tamanho-de-arquivo/` |
| Linhas por método | 15 | Aviso (S138) |
| Complexidade cognitiva | 15 | Aviso (S3776) |
| Complexidade ciclomática | 10 | Aviso (S1541) |
| Parâmetros | 7 | Aviso (S107) |

Mapeamentos do EF ficam fora do limite por método, conforme a [ADR-0155](0155-analisadores-de-qualidade-na-build.md). Reprovam na revisão, mesmo dentro dos limites: sobrecarga ou serviço que só repassa, estado fatiado, vista ou cópia da entidade e coleção viva exposta.

A lista `tools/tamanho-de-arquivo/excecoes.txt` não recebe arquivo novo, e cada arquivo nela só pode encolher. `ProcessoSeletivo.cs` parte de 739 linhas, por decisão pontual ao concentrar na raiz a aplicação das mudanças; daí em diante só encolhe. As métricas passam a erro quando as ocorrências acabarem.

## Consequências

### Positivas

- Código novo tem limite claro, e a build mostra o que passa dele.

### Negativas

- Enquanto forem aviso, nada barra o aumento de ocorrências de método e complexidade; cabe à revisão não aceitar novas.
- O código atual não cumpre os limites, e a dívida precisa de prioridade própria.

## Confirmação

`tools/tamanho-de-arquivo/check.sh` passa, e a build grava em `artifacts/analise/` o relatório das quatro métricas.

## Mais informações

- [ADR-0155](0155-analisadores-de-qualidade-na-build.md): analisadores na build.
