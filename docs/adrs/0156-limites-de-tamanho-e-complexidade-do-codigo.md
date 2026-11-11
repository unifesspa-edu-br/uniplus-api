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

> **Emenda (2026-11-11) — os limites contam linhas físicas e valem para os testes.** O limite de 15 linhas por método conta da linha da assinatura até a chave final, com linhas em branco e comentários, e vale para todo método, de produção e de teste, fora a configuração do EF Core (`IEntityTypeConfiguration<T>`, migrations e snapshot), como já estava decidido. O limite de 400 linhas por arquivo vale para `src/` e `tests/`, inclusive para a configuração do EF escrita à mão; ficam fora só os arquivos gerados. A S138 não é a medida, porque conta só as linhas de código do corpo e não mede projetos de teste: é sinal auxiliar, e a revisão cobra a contagem física. Por isso, onde esta ADR diz que a build mostra o que passa dos limites, leia-se que a build e o job de tamanho dão sinais parciais (a S138 e a S1541 não medem projetos de teste, a S138 não faz a contagem física, e o job de tamanho não mede `tests/`) e que a revisão cobra os limites. O CI não barra a dívida antiga, que é paga por módulo; quando um módulo zera a dívida, o CI passa a barrar aquele módulo. A forma de medir está no `CONTRIBUTING.md`, seção "Métricas de qualidade".

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
