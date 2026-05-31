---
status: "accepted"
date: "2026-05-31"
decision-makers:
  - "Tech Lead"
---

# ADR-0070: A validação de documentos em runtime avalia o snapshot congelado, não a configuração viva

## Contexto e enunciado do problema

A validação documental decide, no momento do envio da inscrição e no acompanhamento pós-envio, quais documentos uma inscrição ainda precisa apresentar. Originalmente essa validação resolvia as exigências lendo as tabelas vivas da configuração do processo (`documento_exigido`, `condicao_gatilho`).

Essas tabelas permanecem editáveis depois da publicação do edital. Como consequência, mutar a configuração de um processo já publicado mudava **retroativamente** o resultado da validação para inscrições já enviadas — uma exigência inserida ou alterada após a publicação passava a valer para atos passados. Isso fere a RN08 (congelamento dos parâmetros do edital na publicação) e a defensibilidade jurídica do resultado.

A questão a decidir é: **a partir de qual fonte a validação documental em runtime resolve as exigências de um processo publicado** — a configuração viva (mutável) ou o snapshot congelado na publicação.

## Drivers da decisão

- **RN08 e reprodutibilidade legal.** O resultado de um ato deve ser reproduzível a partir do estado congelado na publicação, imune a edições posteriores da configuração.
- **Disciplina de snapshot já vigente.** A [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) (snapshot-copy cross-módulo) e a [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) (snapshot na publicação para governança) já estabelecem o snapshot congelado como disciplina do projeto.
- **Auditabilidade.** Separar claramente a configuração viva (tempo de configuração) do que governa o runtime (congelado) torna o resultado auditável.

## Opções consideradas

- **A. Avaliar o snapshot congelado combinado com os fatos vivos do candidato.** A validação em runtime resolve as exigências a partir do snapshot da publicação e as cruza (join) com os fatos vivos da inscrição.
- **B. Manter a leitura da configuração viva.** Simples, mas viola a RN08: edições pós-publicação afetam atos passados.
- **C. Travar toda mutação pós-publicação por triggers na configuração viva.** Não resolve a retroatividade do que já foi avaliado e engessa correções legítimas feitas antes da publicação.

## Resultado da decisão

**Escolhida:** "A — avaliar o snapshot congelado combinado com os fatos vivos", porque é a única opção que honra a RN08 no runtime sem engessar a configuração antes da publicação.

A validação documental em runtime resolve as exigências a partir do snapshot da publicação (`configuracao_congelada → documentos_exigidos → exigencias[]`), cruzando-as por join com os fatos vivos do candidato; **não** lê as tabelas vivas de configuração de documentos (`documento_exigido`, `condicao_gatilho`). A avaliação do predicado de aplicabilidade de cada exigência passa a ter **fonte única** sobre o gatilho congelado no snapshot.

O avaliador que lê a configuração viva permanece existindo, porém restrito a **pré-visualização e coerência em tempo de configuração** — fora do caminho de runtime. Runtime lê o congelado; tempo de configuração lê o vivo.

**Invariante.** Para um processo já publicado, mutar, inserir ou retratar a configuração viva **não altera** o resultado da validação em runtime.

## Consequências

### Positivas

- RN08 honrada no runtime: o resultado fica imune a edições posteriores da configuração.
- Separação nítida entre tempo de configuração (lê o vivo) e runtime (lê o congelado).
- Reprodução histórica fiel do resultado de cada ato.

### Negativas

- O runtime passa a depender da integridade do snapshot (tratada na [ADR-0076](0076-contrato-snapshot-runtime-espelha-publicacao.md)) e da resolução explícita de qual snapshot governa cada ato (tratada na [ADR-0075](0075-snapshot-do-ato-resolvido-no-instante.md)).

### Neutras

- O avaliador sobre a configuração viva permanece disponível para coerência e pré-visualização em tempo de configuração; deixa apenas de participar do runtime.

## Confirmação

- **Fitness test de imunidade comportamental.** Para um processo publicado, mutar, inserir ou retratar (soft-delete) a configuração viva não altera o resultado da validação — verificado por uma matriz que percorre cada superfície de configuração e cada atributo lido, com contraprova de que a leitura viva divergiria (tornando a imunidade observável, não vácua).
- **Verificação estática.** O fecho de chamadas da validação em runtime não referencia as tabelas vivas de configuração (por nome de tabela, execução dinâmica, view ou operador).

## Mais informações

- Requisito de rastreabilidade: **UNI-REQ-0062**.
- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — snapshot-copy cross-módulo, disciplina de snapshot que esta ADR estende ao runtime documental.
- [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) — snapshot na publicação para governança.
- [ADR-0075](0075-snapshot-do-ato-resolvido-no-instante.md) — qual snapshot governa cada ato.
- [ADR-0076](0076-contrato-snapshot-runtime-espelha-publicacao.md) — contrato e validação do snapshot lido em runtime.
- Regra de negócio **RN08** — congelamento dos parâmetros do edital na publicação.
