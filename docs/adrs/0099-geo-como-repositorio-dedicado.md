---
status: "accepted"
date: "2026-06-26"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0099: Geo como repositório e serviço transversal dedicado

## Contexto e enunciado do problema

A [ADR-0090](0090-modulo-geo-localidades.md) definiu Geo como bounded context
transversal de localidades, endereçamento e georreferência. A implementação
inicial ficou dentro do `uniplus-api`, com cinco projetos locais, migrations,
baseline `contracts/openapi.geo.json`, testes de integração e deployable
proprio.

Esse arranjo resolve a separação conceitual, mas ainda prende uma API
institucional transversal ao ciclo de release do Uni+. Outros serviços
UNIFESSPA podem precisar do mesmo contrato sem depender de paths, pacotes ou
decisões internas do monólito modular.

## Drivers da decisão

- Geo é capability institucional transversal, não exclusiva do Uni+.
- O contrato HTTP V1 deve continuar estável para consumidores atuais.
- Mudanças, CI, imagem, migrations e rollback do Geo precisam de ownership
  independente.
- O `uniplus-api` não deve compilar, testar nem publicar internals Geo.
- Consumidores Uni+ continuam usando snapshots por contrato, sem FK cross-banco
  e sem chamada backend obrigatória ao Geo.

## Opções consideradas

- Manter Geo como módulo local do `uniplus-api` com CODEOWNERS mais forte.
- Criar submódulo Git dentro do `uniplus-api`.
- Extrair Geo para repositório dedicado com codigo e namespace proprios.

## Resultado da decisão

**Escolhida:** "Extrair Geo para repositório dedicado com codigo e namespace
proprios", porque separa ownership e release sem mudar o contrato público V1.

O repositório dedicado é
[`unifesspa-geo-api`](https://github.com/unifesspa-edu-br/unifesspa-geo-api).
Ele passa a ser a fonte de verdade para código, migrations, testes, baseline
OpenAPI, CI, Docker image, documentação operacional e release/rollback do Geo.
O namespace raiz do novo serviço é `Unifesspa.Geo`.

O `uniplus-api` mantém apenas modelos de consumo e snapshots necessários aos
módulos consumidores. Não deve existir `ProjectReference`, `PackageReference`
ou uso de namespace de internals Geo neste repositório, seja o namespace antigo
`Unifesspa.UniPlus.Geo.*` ou o novo namespace do serviço dedicado.

## Consequências

### Positivas

- O Geo pode evoluir e publicar imagem sem acoplar release ao Uni+.
- Outros serviços UNIFESSPA podem consumir a API por contrato publicado.
- O monólito modular reduz superfície de build, teste e publish.

### Negativas

- Há duplicação intencional de código fundacional no repositório Geo.
- Operação precisa coordenar cutover para evitar dois runtimes Geo executando
  migrations, ETL, seed ou reconciliação no mesmo banco.

### Neutras

- O `service.name` operacional permanece compatível durante a extração; eventual
  renomeação fica para mudança posterior.

## Confirmação

- `UniPlus.slnx` não referencia projetos `Unifesspa.UniPlus.Geo.*`.
- `contracts/openapi.geo.json` foi removido deste repositório; a baseline canônica
  vive no repositório dedicado.
- Nenhum projeto, pacote ou namespace `Unifesspa.UniPlus.Geo.*` /
  `Unifesspa.Geo.*` é referenciado pelo `uniplus-api` (verificável em build e nos
  fitness tests do `ArchTests`, que deixaram de arrolar o Geo).
- O repositório `unifesspa-geo-api` executa restore locked, build, testes,
  drift OpenAPI, Spectral e forbidden-deps próprios.

## Prós e contras das opções

### Manter Geo local com CODEOWNERS

- Bom, porque reduz custo inicial.
- Ruim, porque mantém CI, release e imagem presos ao `uniplus-api`.

### Submódulo Git

- Bom, porque separa histórico e ownership parcialmente.
- Ruim, porque aumenta atrito de checkout, revisão e CI sem entregar autonomia
  operacional completa.

### Repositório dedicado

- Bom, porque torna Geo uma API institucional consumível por contrato.
- Ruim, porque exige duplicação controlada de fundações e coordenação de
  cutover.

## Mais informações

- Repositório dedicado: <https://github.com/unifesspa-edu-br/unifesspa-geo-api>
- Esta ADR refina a [ADR-0090](0090-modulo-geo-localidades.md).
- Contrato Geo canônico: `contracts/openapi.geo.json` no repositório dedicado e
  endpoint runtime `GET /openapi/geo.json`.
