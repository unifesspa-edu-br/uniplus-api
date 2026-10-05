---
status: "accepted"
date: "2026-09-29"
decision-makers:
  - "Tech Lead"
consulted:
  - "CEPS (dono do processo)"
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0137: Formulários por finalidade no processo e modelos de formulário por tipo de processo

## Contexto e enunciado do problema

Hoje o processo seletivo tem um único formulário, o de inscrição, montado do zero em cada edital. O edital de Medicina 2027 exige ao menos dois formulários estruturalmente diferentes no mesmo processo, inscrição e habilitação, e processos que cobram taxa exigem também o de isenção. O UNI-REQ-0144 passou a exigir um formulário por finalidade e modelos de formulário por tipo de processo, montados no módulo Configuração e aplicados ao processo. O UNI-REQ-0145 põe as regras de exibição, obrigatoriedade e opções no item do formulário, e não no fato.

## Drivers da decisão

- O mesmo processo tem formulários diferentes por finalidade (UNI-REQ-0144).
- O formulário de cada edital não pode ser refeito do zero; ele parte de um modelo.
- O mesmo fato pode ser obrigatório para um candidato e opcional para outro (UNI-REQ-0145).
- Não há produção: a forma do envelope ainda muda a cada entrega.

## Opções consideradas

- **Um formulário por processo**, com seções por finalidade.
- **Um formulário por finalidade no processo**, com modelos por tipo de processo e finalidade aplicados por cópia.
- **Formulário referenciado ao vivo pelo processo**, sem cópia.

## Resultado da decisão

**Escolhida:** "Um formulário por finalidade no processo, com modelos aplicados por cópia", porque separa o que cada finalidade coleta, permite reaproveitar o formulário entre editais e preserva o processo de mudanças posteriores no modelo.

- **Finalidades:** `INSCRICAO`, `ISENCAO_TAXA` e `HABILITACAO`, cada uma com a sua fase canônica e os blocos de sistema que admite e que exige. Uma finalidade nova entra por ADR.
- **Regras no item:** exibição, obrigatoriedade (sempre, nunca ou quando) e restrição de valor, inclusive opções condicionadas às respostas anteriores, filtradas por fato anterior ou formadas pelas respostas de itens anteriores; essa dependência entra no grafo, e a resposta que deixa de valer é invalidada. Etapas são seções ou blocos de sistema, e a ordem é a de etapa e item. Uma regra só cita fato anterior, fato de finalidade anterior ou derivado com dependências anteriores.
- **Produtor único:** cada fato tem um único campo que o produz no processo. A fase efetiva de um fato no processo é a mais tardia entre o ponto de resolução do catálogo, a fase do formulário que o produz e a fase efetiva das suas dependências.
- **Modelos:** o administrador compõe modelos por tipo de processo e finalidade no módulo Configuração. O processo, em rascunho, recebe uma cópia por valor (ADR-0061), que pode ajustar; alterar o modelo depois não altera o processo. Um fato já produzido por outra finalidade continua na mais cedo.
- **Termos:** referenciados do catálogo por termo e versão, e congelados com o conteúdo que o UNI-REQ-0086 exige.
- **Contrato:** as rotas passam a ser por finalidade, com nova versão do media type (ADR-0028), inclusive no portal público (ADR-0131).

### Emenda às ADRs 0109 e 0110

O codec único do envelope e a golden fixture só da forma corrente (ADR-0109, Emenda 2), e o regime transitório da ADR-0110, valem até a primeira publicação **em produção**, e não em qualquer ambiente. Os processos de homologação são descartáveis: quando a forma muda, são recriados. A preservação por versão começa na produção.

### Emenda de 2026-10-04: a exigência documental declara o formulário a que pertence

Inscrição e isenção podem dividir a mesma fase do cronograma, e a fase sozinha não diz em qual dos dois formulários o documento é apresentado.

- **Decisão:** a exigência documental guarda, além da fase, a finalidade do formulário a que pertence. A finalidade é declarada por quem configura, nunca deduzida da fase. Ela é obrigatória na fase que responde algum formulário, tem de ser uma das finalidades que essa fase responde e fica nula só fora de formulário. Todos os documentos de um grupo pertencem ao mesmo formulário.
- **Efeito:** o bloco de comprovação de cada formulário lista só as exigências da sua fase e da sua finalidade, e o fato coletado só pelo formulário de isenção só é citado por exigência da isenção.
- **Opção rejeitada:** proibir inscrição e isenção na mesma fase, porque o cronograma do edital admite as duas juntas.
- **Consequência:** a forma do envelope muda e as exigências gravadas antes precisam declarar a finalidade; os processos de homologação são recriados, como prevê a emenda acima.

## Consequências

### Positivas

- Cada finalidade tem o formulário que precisa, e o formulário de um edital parte de um modelo.
- As regras do item servem a qualquer formulário, e o mesmo avaliador as executa na pré-visualização e na execução.

### Negativas

- O processo passa a ter vários formulários, e as exigências documentais têm de conferir a fase efetiva dos fatos de todos eles.
- Processos de homologação precisam ser recriados a cada mudança de forma até a produção.

## Confirmação

- Teste de domínio: dois campos que produzem o mesmo fato no processo são recusados; regra que cita fato posterior é recusada.
- Teste de domínio: aplicar um modelo gera cópia, e editar o modelo depois não altera o processo.

## Prós e contras das opções

### Um formulário por processo

- Bom, porque mantém o modelo atual.
- Ruim, porque mistura finalidades com fases e públicos diferentes num único formulário.

### Um formulário por finalidade, com modelos por cópia

- Bom, porque separa as finalidades e reaproveita o formulário entre editais sem acoplar o processo ao modelo.
- Ruim, porque exige conferir a coerência de fase entre formulários.

### Formulário referenciado ao vivo

- Bom, porque evita duplicação.
- Ruim, porque uma mudança no modelo alteraria processos já configurados ou publicados.

## Mais informações

- UNI-REQ-0017, UNI-REQ-0086, UNI-REQ-0144 e UNI-REQ-0145.
- ADR-0028 (versionamento por media type), ADR-0061 (cópia por valor), ADR-0109 (envelope do congelamento), ADR-0110 (retificação), ADR-0131 (portal como BFF público), ADR-0135 (projeto compartilhado de regras) e ADR-0136 (catálogo de fatos administrável).
