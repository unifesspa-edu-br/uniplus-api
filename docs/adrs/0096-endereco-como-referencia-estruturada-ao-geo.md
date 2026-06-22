---
status: "accepted"
date: "2026-06-22"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0096: Endereço de entidades institucionais como referência estruturada ao Geo

## Contexto e enunciado do problema

As entidades institucionais que possuem localização física — `Campus` e `LocalOferta` (módulo Configuração) e `Instituicao` (módulo Organização Institucional) — guardam o endereço como **texto livre** (`Endereco`/`EnderecoSede`, `string?` de até 500 caracteres). Apenas o `Campus` acrescenta hoje um `Cep` solto + coordenadas (`latitude`/`longitude`); `LocalOferta` e `Instituicao` têm **só** o texto livre — **sem CEP nem coordenadas**. Em contraste, a **cidade** das três já é uma **referência estruturada ao módulo Geo** (`cidade_codigo_ibge` + display cache), adotada pelo ADR-0090 sob o padrão de composição no cliente.

Agravante na `Instituicao`: o comentário de domínio justifica o texto livre afirmando que *"o Geo não modela endereço pontual"* — premissa **factualmente superada** por #676 (lookup de CEP) e #707 (busca de logradouro), que entregaram exatamente essa modelagem. A justificativa errada está cristalizada no código e deve ser corrigida junto.

Essa assimetria interna gera três problemas:

1. **Endereço opaco** — não é consultável nem integrável por logradouro/bairro/distrito; o front não pode oferecer preenchimento automático por CEP.
2. **Redundância e risco de incoerência** — `cidade_*` e `cep` são blocos independentes, mas no DNE dos Correios **o CEP já determina a cidade**; nada garante hoje que ambos sejam coerentes.
3. **Contrato HTTP achatado** — o DTO expõe o bloco de cidade e o endereço como campos soltos no nível raiz, sem agrupamento semântico.

O módulo Geo **já expõe** endereço estruturado completo via `GET /api/cep/{cep}` (`CepResolvidoDto`: cep, tipo, logradouro, complemento, bairro, distrito, cidade, codigoIbge, uf, latitude, longitude, `nivelResolucao`, `origem`), carregado a partir do DNE dos Correios (ADR-0092). O insumo existe; falta o modelo consumi-lo.

## Drivers da decisão

- **Decisão de engenharia, não de negócio** — as entidades continuam expondo o **mesmo conceito de endereço**: nenhuma regra de negócio, processo ou entidade de domínio muda. Entrega-se o mesmo dado de antes, agora estruturado e ancorado no Geo. A escolha é técnica (modelagem + contrato).
- **Coerência interna** — endereço deve seguir o mesmo padrão de referência-ao-Geo que a cidade já usa.
- **Aproveitamento do DNE** — reusar `CepResolvidoDto` em vez de texto livre.
- **Composição no cliente (ADR-0090)** — o backend consumidor **não** chama o Geo; persiste o snapshot que o front compõe.
- **Referência fraca** — vale só para dado público estável; nunca para invariante de autorização/elegibilidade/financeiro/legal.
- **UX de cadastro** — habilitar autofill por CEP com preenchimento manual dos campos que o CEP não resolve.
- **Padrão cross-entidade** — a decisão vale para todas as entidades com endereço físico, não para uma só.

## Opções consideradas

- **Opção A — Manter texto livre (`Endereco` + `Cep` soltos).** Status quo.
- **Opção B — Endereço estruturado com a cidade DENTRO do endereço (derivada do CEP).** Um único bloco `endereco { ..., cidade { codigoIbge, nome, uf } }`; sem CEP, sem cidade.
- **Opção C — Endereço estruturado opcional + cidade como referência independente no topo (garantida), com invariante de coerência cidade↔CEP quando ambos existem.** (recomendada)

## Resultado da decisão

**Escolhida:** "Opção C — endereço estruturado opcional + cidade como referência independente coerente", porque preserva a cidade garantida mesmo sem CEP (necessária para bônus regional e cidade de prova) e, ao mesmo tempo, estrutura o endereço quando há CEP, com coerência verificável entre ambos.

> Decisão de **engenharia** aprovada pelo Tech Lead — não envolve negócio (mesmos dados entregues, melhor estruturados). Os **detalhes de implementação** ao final desta seção são resolvidos na story de implementação (#726), dentro da opção escolhida.

Modelar o endereço de `Campus`, `LocalOferta` e `Instituicao` como um value object compartilhado no Kernel — `ReferenciaEnderecoGeo`, espelhando `ReferenciaCidadeGeo` — composto por:

- **Âncoras do DNE** (preenchidas pela resolução do CEP, read-only no front quando presentes): `cep`, `logradouro`, `bairro`, `distrito`, e a referência de `cidade` (`codigoIbge` + `nome` + `uf`).
- **Dados próprios da entidade** (sempre manuais): `numero`, `complemento`.
- **Georreferência**: `latitude`, `longitude` (pré-preenchidas, ajustáveis).
- **Metadados de cache/proveniência**: `nivelResolucao` (`logradouro|bairro|distrito|cidade`), `origem`, `displayAtualizadoEm`.

A **cidade permanece como referência independente garantida** (Opção C): um campus pode ter cidade conhecida sem CEP. Quando há endereço com CEP, vale a **invariante de coerência**: a cidade do endereço deve bater com a referência de cidade da entidade (mesmo `codigoIbge`/UF).

O DTO expõe `endereco` como **sub-objeto aninhado**, e a cidade também aninhada (`cidade { codigoIbge, nome, uf }`), padronizado nas entidades afetadas.

### Comportamento de preenchimento (front)

O `nivelResolucao` governa o que é read-only vs. editável: campo resolvido pelo Geo é âncora (read-only); campo nulo é entrada manual. `numero`/`complemento` são sempre editáveis. `codigoIbge`/cidade permanecem âncora mesmo no nível "cidade" (faixa de CEP), preservando a coerência cidade↔CEP.

### Limites (escopo da referência fraca)

A validação server-side é **apenas de formato e coerência** (CEP com 8 dígitos; `codigoIbge` com 7 dígitos coerente com a UF). O backend **não** consulta o Geo e **confia** no snapshot + `nivelResolucao` informados pelo front — mesma garantia (fraca) já aceita para a referência de cidade. Aceitável porque endereço institucional é dado público estável; **proibido** para qualquer invariante de autorização, elegibilidade, financeiro ou legal.

### Detalhes de implementação (resolver na #726)

São escolhas técnicas internas à opção aprovada — não reabrem a decisão:

1. **Cidade dentro do endereço vs. independente** — a Opção C mantém a cidade independente; tratar os casos de borda (campus sem CEP).
2. **Granularidade da proveniência** — `origem` por bloco + `nivelResolucao`, ou flag de override por campo.
3. **Read-only rígido vs. override explícito** — o DNE erra/atrasa; definir se campos resolvidos são imutáveis ou corrigíveis.
4. **Migração dos dados existentes** (dev/seed) — descartar o texto livre atual ou tentar parse.

## Consequências

### Positivas

- Endereço consultável/integrável e exibível campo a campo; autofill por CEP no cadastro.
- Elimina a redundância cidade↔cep e introduz coerência verificável.
- Contrato HTTP com agrupamento semântico (`endereco`, `cidade`), alinhado entre entidades.
- `nivelResolucao` + `origem` + `displayAtualizadoEm` habilitam reconciliação posterior sem sobrescrever entradas manuais.

### Negativas / custos

- Refactor cross-entidade (Campus, LocalOferta, Instituicao) — domínio, migration, DTOs, mapeamentos, validators, baseline OpenAPI (configuracao + organizacao), testes. Em `LocalOferta` e `Instituicao` é preciso **adicionar** `cep`/`latitude`/`longitude` (inexistentes hoje); só o `Campus` já os possui.
- **Ruptura de contrato de um front já entregue** — a tela da Instituição (uniplus-web `web#386`) consome o shape flat atual; o novo contrato a quebra, exigindo issue de **atualização** dela (não só telas novas de Campus/LocalOferta).
- Estado de formulário no front mais elaborado (read-only condicional por `nivelResolucao`, dois fluxos de cidade).
- A garantia de que "o endereço veio mesmo do DNE" continua sendo confiança no front (referência fraca), não verificação no back.
- Migração dos dados existentes em dev/seed (descartar texto livre ou tentar parse).

## Confirmação

- Testes de domínio do VO `ReferenciaEnderecoGeo` (formato, coerência cidade↔CEP, tolerância a resolução parcial).
- Testes de integração de round-trip (persistência + CHECKs) e do contrato OpenAPI (sub-objeto aninhado).
- ArchTests de isolamento de leitura cross-módulo (ADR-0056) — o consumo do Geo permanece composição no cliente, sem leitor read-side nem HTTP cross-módulo no backend.

## Prós e contras das opções

### Opção A — texto livre (status quo)

- Bom: zero esforço; flexível para qualquer string.
- Ruim: endereço opaco; sem autofill; redundância cidade↔cep sem coerência; assimetria com o tratamento de cidade.

### Opção B — cidade dentro do endereço

- Bom: modelo mais enxuto; o CEP determina tudo, sem duplicar cidade.
- Ruim: quebra o caso "cidade sem CEP" (campus sem logradouro no DNE / sem endereço cadastrado); perde a cidade como referência garantida usada por bônus regional e cidade de prova.

### Opção C — endereço opcional + cidade independente coerente (recomendada)

- Bom: cidade garantida mesmo sem endereço; endereço estruturado opcional; coerência explícita quando ambos existem; evolução incremental sobre o modelo atual de cidade.
- Ruim: dois lugares com informação de cidade (a referência da entidade e a do endereço) exigindo invariante de coerência; ligeiramente mais verboso.

## Mais informações

- ADR-0090 (módulo Geo como bounded context; composição no cliente; display cache) — este ADR **evolui** o padrão de referência de cidade para referência de endereço.
- ADR-0092 (ETL de carga do DNE) — fonte do endereçamento estruturado.
- ADR-0056 (isolamento de leitura cross-módulo) — preservado.
- ADR-0029 (HATEOAS Level 1) — `_links` mantido no DTO.
- Insumo: `CepResolvidoDto` / `CepResolucao` e `GET /api/cep/{cep}` (módulo Geo).
- Modelo a espelhar: `ReferenciaCidadeGeo` (`src/shared/Unifesspa.UniPlus.Kernel/Domain/Cidades/`).
- Story de implementação (backend): #726. Ajuste de frontend: uniplus-web#412.
