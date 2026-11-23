---
status: "proposed"
date: "2026-11-16"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0174: Conferência única da configuração do processo, com todas as causas

## Contexto e enunciado do problema

A conferência de que um processo pode ser congelado era uma cadeia de verificações que devolvia a primeira causa. O painel de conformidade, a retificação e a publicação tinham listas próprias de regras, com ordens e textos que podiam divergir. O diagnóstico precisa mostrar todas as causas independentes de uma vez ([ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md)) e as declarações parciais já produzem recusas próprias ([ADR-0173](0173-regras-da-configuracao-sobre-declaracoes-parciais.md)).

## Drivers da decisão

- Uma ordem única de conferência para gravação, painel e congelamento.
- Todas as causas independentes numa só resposta, sem causa duplicada nem exceção.
- Uma regra só deixa de avaliar a unidade cujo dado foi recusado; o item que não pôde ser verificado nunca fica aprovado por omissão.
- O agregado não guarda um estado derivado da própria conferência.

## Opções consideradas

- Manter as cadeias de primeira causa e acrescentar os assuntos novos ao lado.
- Conferir a configuração por acumulação, com uma configuração apta a congelar que só existe sem causas.

## Resultado da decisão

**Escolhida:** "Conferir a configuração por acumulação, com uma configuração apta a congelar que só existe sem causas", porque é a única forma de dar a mesma ordem e o mesmo texto a todos os consumidores.

1. A configuração apta a congelar só existe quando a conferência não tem causa. A conferência devolve a configuração apta ou o conjunto de causas, nunca os dois, e a configuração apta carrega apenas o identificador do processo.
2. Os cadastros que a conferência lê (catálogos, referências, tipos de documento e fatos protegidos) entram como um único valor, lidos uma vez, num instante único, pela camada de aplicação. O domínio não lê porta. Os motivos de decisão escolhidos entram nesse valor com a situação do motivo de origem no cadastro e o número da versão que nasce: o processo congela a cópia feita na escolha, e a conferência só lê do cadastro se o motivo continua ativo, sem refazer a cópia.
3. A ordem dos assuntos de regra é única: identificação, conformidade por item, declarações, referências de regra, conjunto, completude, procedimentos de recurso, bancas, correção independente, tipos de ato, cascata, as condições que a canonicalização exige e os motivos de decisão escolhidos. No assunto dos motivos, a ordem é o motivo desativado no cadastro, o motivo de um tipo que a fase ou a etapa não declara e a cobertura por lugar; o motivo recusado por uma das duas primeiras não conta para a cobertura. Quando a leitura recusou uma fase ou etapa inteira, ou a lista dos tipos de decisão de uma delas, não se sabe quais lugares declaram um tipo: o assunto não recusa o motivo desse lugar nem aponta que nenhum lugar declara o tipo, e os itens do painel que dependem disso ficam pendentes. Cada assunto é uma função que devolve todas as suas causas, e a ordem é a da concatenação delas em `CausasQueImpedemCongelar`; uma regra que lê o dado que outra recusou deixa de avaliar dentro do próprio predicado. A conferência do estado (status, motivo, versão anterior) é conflito, e não causa da configuração: fica fora dessa ordem, em `OrigemDoCongelamento`, e impede todo o resto quando recusa.

   As referências de regra são conferidas nesta ordem: algoritmo de contagem de prazo na identificação; distribuição e ajuste por oferta; cascata de remanejamento; prazo e procedimentos de recurso por fase; consolidação, prazo e procedimentos de recurso por etapa; cálculo, arredondamento, ordem de alocação, eliminação e desempate na classificação; bônus regional. Nas coleções vale a posição no documento. Cada ocorrência cujo código e versão não existam ou cujo hash divirja produz uma recusa no campo correspondente, mesmo quando outra ocorrência cita a mesma regra. Referências opcionais ausentes não produzem essa recusa; declarações recusadas conservam suas próprias causas. A conferência usa o catálogo vigente sem alterar o hash gravado.
4. O congelamento confere nesta ordem: o estado, o ato e o documento no mesmo instante, a configuração, a montagem, a transição e a gravação. As recusas das etapas da aplicação se acumulam às da configuração, e só deixa de rodar a etapa que lê um dado que outra recusou. A aprovação da proteção é regra sobre uma leitura que nunca falha, e não falha da leitura: a recusa dela sai na frente, antes da configuração e da operação, e a recópia e a conferência da configuração rodam sempre. O corte é por exigência: a exigência que a aprovação recusa (tipo pendente, tipo excluído ou fato comprovado pendente) fica fora das pendências que leem a proteção, e as demais exigências seguem conferidas.
5. A recusa reúne todas as causas numa só resposta 422, em que o erro principal é a primeira causa e cada causa traz o campo do documento da configuração quando aponta o documento. A lista `errors[]` segue a regra da [ADR-0023](0023-wire-formato-erro-rfc-9457.md) (emenda de 17/11/2026): vai na resposta quando há mais de uma causa ou quando alguma causa aponta um campo, e uma causa única sem campo não a leva. O painel de conformidade tem um item por assunto de declaração e de conjunto (`cronograma_fases_declaradas`, `cronograma_fases_em_conjunto`, `etapas_declaradas`, `etapas_em_conjunto`), cada um derivado da mesma conferência que a configuração chama, de modo que o painel e a conferência concordam em tudo. O painel tem também um item por regra dos motivos de decisão escolhidos.

## Consequências

### Positivas

- O diagnóstico, o painel e o congelamento mostram as mesmas causas, na mesma ordem e com o mesmo texto.
- Sai do agregado o código que repetia as regras de conformidade.

### Negativas

- Algumas primeiras causas mudam de ordem, e cada mudança tem teste que a registra.
- O painel ganha os itens de cada assunto novo, entre eles os três dos motivos de decisão escolhidos, e as mensagens de erro do congelamento ganham ponto final.

### Neutras

- O estado derivado sai da raiz, e o campo das causas passa a ponteiro JSON.

## Confirmação

Testes de unidade fixam a ordem dos assuntos, a paridade do painel com a conferência e o bloco de cada causa no documento. Testes de arquitetura garantem que a configuração apta a congelar não tem construtor público.

## Prós e contras das opções

### Manter as cadeias de primeira causa

- Bom, porque não altera o que já funciona.
- Ruim, porque painel e congelamento divergem e o diagnóstico mostra uma causa por vez.

### Conferir por acumulação

- Bom, porque a ordem, o texto e a regra de quando deixar de avaliar vivem num lugar só.
- Ruim, porque muda a primeira causa de alguns estados.

## Emenda 1 (2026-11-23) — prova presencial

Decisão do líder técnico (23/11/2026), com a uniplus-api#2767. O processo declara se tem prova presencial e, se tem, em que municípios a aplica (UNI-REQ-0407). O município é a cidade em que o candidato pode fazer a prova; o local exato, com endereço e sala, sai no ensalamento (UNI-REQ-0042, uniplus-api#2779) e não é conferido aqui. A declaração passa a ser conferida pela conferência única, com todas as causas.

1. **Ordem dos assuntos.** O item 3 passa a terminar assim: "… as condições que a canonicalização exige, os motivos de decisão escolhidos e a prova presencial." No assunto da prova presencial, a ordem é:
   1. o processo que ainda não declarou (`ProvaPresencial.PorDeclarar`);
   2. o processo com prova presencial sem município (`ProvaPresencial.SemMunicipio`);
   3. o processo sem prova presencial que declara município (`ProvaPresencial.MunicipiosSemProvaPresencial`);
   4. as recusas de cada município, pela posição declarada (a primeira posição é 0): a forma do código do IBGE, a coerência com a UF, o município repetido (`ProvaPresencial.MunicipioRepetido`) e o município que o Geo não conhece ou recusa, por exemplo o de outra UF (`ProvaPresencial.MunicipioNaoConferidoNoGeo`), com um erro por município, no campo `/provaPresencial/municipios/{i}/codigoIbge`;
   5. por último, a lista acima do limite, que é configuração da API, com o padrão de 100 municípios (`ProvaPresencial.MunicipiosAcimaDoLimite`).

   O processo por declarar não passa pelos demais itens do assunto.

   A gravação da declaração só grava e não lê o Geo. Ela segue a mesma ordem, sem os itens 1 e 2 e sem o Geo: o município declarado sem prova presencial; as recusas de cada município pela posição (forma do código do IBGE, coerência com a UF e município repetido); o limite por último. O processo por declarar (`ProvaPresencial.PorDeclarar`), a falta de município (`ProvaPresencial.SemMunicipio`), o município que o Geo não conhece ou recusa (`ProvaPresencial.MunicipioNaoConferidoNoGeo`) e o Geo indisponível são da conferência, na publicação, na retificação e no painel. O rascunho incompleto é salvo, e a resposta lista o que falta (UNI-REQ-0416).

2. **O Geo entra no valor único dos cadastros.** O item 2 passa a incluir os municípios de prova conferidos no Geo: a aplicação lê o Geo uma vez, no mesmo instante dos demais cadastros, e entrega à conferência, para cada município declarado, se o Geo o conhece e o nome que devolveu. O nome congelado na versão é esse; o nome informado pelo documento não o substitui.

3. **Geo indisponível não é causa da configuração.** Quando o Geo não responde, a publicação e a retificação respondem indisponibilidade (503), e nada é gravado (UNI-REQ-0407). Essa resposta fica fora da ordem dos assuntos, como a conferência do estado no item 3. No painel, com o Geo indisponível, o item da prova presencial fica pendente, e não aprovado.

4. **Painel de conformidade.** O painel ganha o item `prova_presencial`, derivado da mesma conferência que a configuração chama.

Consequência: as primeiras causas não mudam para quem já declarava tudo, porque o assunto novo vem depois dos existentes. A ordem nova tem teste que a registra, e a paridade do painel com a conferência cobre o item novo.

## Mais informações

- Emendada pela [ADR-0175](0175-o-processo-publicado-nao-tem-edicao-direta-nem-sessao-editorial.md): saem a restauração, o rascunho de retificação aberto e o fechamento da sessão, e a retificação passa a ser conferida só por esta conferência.
- [ADR-0110](0110-retificacao-como-sessao-editorial.md), [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md), [ADR-0167](0167-servicos-de-aplicacao-com-dependencia-no-construtor-e-orquestrador-do-congelamento.md), [ADR-0173](0173-regras-da-configuracao-sobre-declaracoes-parciais.md).
