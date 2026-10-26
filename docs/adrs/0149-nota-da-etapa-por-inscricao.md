---
status: "accepted"
date: "2026-10-26"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0149: A nota de cada etapa é apurada por inscrição a partir de lançamentos, qualquer que seja a origem

## Contexto e enunciado do problema

A classificação precisa da nota de cada etapa para cada inscrição. Essa nota chega de formas diferentes conforme a etapa:

- a do ENEM vem do arquivo do INEP, com cinco áreas e a situação em cada prova;
- a da redação e a da carta de intenção vêm de dois ou mais corretores, consolidados pela regra de dupla correção (UNI-REQ-0097);
- a da entrevista vem da banca, às vezes por critério;
- a da prova objetiva vem de um arquivo ou do gabarito.

Se cada origem gravar a nota do seu jeito, a classificação e o recurso ficam obrigados a conhecer todas elas. Além disso, a dupla correção e o recurso precisam guardar cada nota individual, inclusive a que a consolidação descarta.

## Drivers da decisão

- A classificação lê a nota da etapa sem saber de onde ela veio.
- Toda nota individual (de cada corretor, de cada área, de cada arquivo) fica registrada, com autor ou lote, e não é apagada.
- A apuração (média dos corretores, cálculo pelas áreas do ENEM) é reprodutível a partir do que foi lançado.
- Do arquivo importado, só é gravado o que a avaliação usa e o que a conferência com o INEP exige.

## Opções consideradas

- **A.** Uma tabela de notas por origem: ENEM, banca, corretores.
- **B.** Uma linha por inscrição e etapa com a nota final, sobrescrita a cada correção.
- **C.** Lançamentos como evidência e uma avaliação apurada por inscrição e etapa.

## Resultado da decisão

**Escolhida:** "C".

Cada nota individual é um **lançamento**. Ele registra:

- a inscrição e a etapa;
- o item, quando houver: a área do ENEM, o critério da entrevista ou o bloco da prova;
- a nota e a situação do candidato;
- de onde veio: o avaliador e a ordem da correção, ou o lote de importação.

O lançamento só é acrescentado, nunca apagado (ADR-0063). Corrigir um lançamento é acrescentar outro que o substitui, com motivo.

A **avaliação** é a nota da etapa para a inscrição: uma por inscrição e etapa, com a situação e a nota apurada. A apuração pode depender da oferta que a inscrição concorre. É o caso da nota do ENEM, que usa os pesos do grupo de área do curso. Nesse caso, há uma avaliação por oferta concorrida, e a segunda opção de curso tem a sua própria avaliação. Ela é calculada a partir dos lançamentos vigentes, por uma função pura (ADR-0013) que segue a forma de avaliação e a regra de consolidação congeladas na etapa. A avaliação expõe, além da nota da etapa, a nota e a situação de cada item, que as regras de eliminação por área e por falta usam. A classificação lê só a avaliação.

O **lote de importação** registra o processo, a etapa, a edição do ENEM quando o lote é do INEP, o leiaute usado (ADR-0148), o hash do arquivo, quem importou e quando. O que não virou nota fica como ocorrência do lote, com o número da linha e sem o CPF de quem não é inscrito: inscrito sem linha no arquivo, linha de CPF sem inscrito e conflito.

Na etapa do ENEM, vale o lote mais recente de cada processo, etapa e edição, por inteiro, porque o retorno do INEP é o retrato completo da edição: o inscrito que não aparece no lote novo fica sem nota, mesmo que tivesse no anterior. Reimportar o arquivo do lote vigente (mesmo hash, no mesmo processo, etapa e edição) não cria outro lote. Reimportar o arquivo de um lote que já foi substituído cria um lote novo, que volta a valer.

O arquivo importado não é guardado, só o hash dele, porque traz nome e CPF de todos os candidatos. Do retorno do INEP, entram as notas, a situação de cada prova e o nº de inscrição no ENEM, este para conferência com o INEP num recurso.

As notas ficam sem cifra individual. O parecer da encarregada de dados de 13/10/2026 exige cifra individual para dado pessoal sensível, e a nota é dado pessoal, mas não sensível. A proteção da nota está no acesso restrito e na regra de que a nota não vai para log, e a classificação ordena e filtra por ela no banco.

## Consequências

### Positivas

- A classificação e o recurso tratam todas as etapas da mesma forma.
- Toda nota descartada pela consolidação continua registrada, com autor e data.
- Uma origem nova (outro arquivo, outra forma de correção) entra sem mexer na classificação.

### Negativas

- A avaliação precisa ser recalculada a cada lançamento novo.
- Há dois níveis de dado, o lançamento e a avaliação, que precisam ficar coerentes. A coerência é garantida porque a avaliação é sempre derivada dos lançamentos.

## Confirmação

- O teste da apuração parte dos lançamentos e chega à avaliação nos casos da UNI-REQ-0097 e no cálculo pelas áreas do ENEM.
- A classificação não referencia lançamento, lote nem leiaute.
- Nenhum caminho apaga ou altera um lançamento.

## Prós e contras das opções

### A. Uma tabela por origem

- Bom, porque cada origem tem a forma exata que precisa.
- Ruim, porque a classificação e o recurso precisam conhecer cada tabela, e cada origem nova muda os dois.

### B. Nota final sobrescrita

- Bom, porque é o modelo mais simples.
- Ruim, porque perde as notas individuais que a dupla correção e o recurso precisam guardar.

### C. Lançamentos e avaliação apurada

- Bom, porque separa a evidência da nota que vale, e a nota que vale é reprodutível.
- Ruim, porque é preciso manter a avaliação recalculada.

## Mais informações

- [ADR-0148](0148-leiautes-versionados-dos-arquivos-do-inep.md): leiautes dos arquivos de notas.
- [ADR-0150](0150-parametros-de-avaliacao-da-etapa.md): forma de avaliação e regra de consolidação da etapa.
- Issues #1602 (importação das notas do ENEM), #1046 (dupla correção) e #55 (lançamento manual).
