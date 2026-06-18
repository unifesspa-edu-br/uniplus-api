---
status: "accepted"
date: "2026-06-17"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0090: Módulo Geo como bounded context dedicado de localidades

## Contexto e enunciado do problema

O Uni+ precisa de um catálogo nacional de localidades (país, unidade federativa, município) e, no futuro, de endereçamento e georreferência (CEP, bairro, logradouro, coordenadas), para suportar cidades de prova, campi, bônus regional e demais usos que cruzam vários módulos. Esses dados são **reference data** de origem externa (IBGE, DNE dos Correios), de leitura intensa e baixa escrita.

A [ADR-0054](0054-naming-convention-e-strategy-migrations.md) estabeleceu bancos PostgreSQL **isolados por módulo**, sem chave estrangeira cruzando o limite entre contextos. A questão é **onde** as localidades vivem: dentro de um módulo de negócio existente (ex.: Configuração), espalhadas, ou em um contexto próprio. Localidade não pertence a nenhum módulo de negócio específico — é consumida por todos.

Há ainda a pergunta de **como** os demais módulos consomem localidades: por leitor read-side cross-módulo (como em [ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md)) ou por composição no cliente. Editais congelam os parâmetros que usam (RN08), então o processo seletivo guarda o **snapshot** da localidade no momento do vínculo, não uma referência viva.

## Drivers da decisão

- **Isolamento por contexto** — coerência com a ADR-0054 (cada contexto, seu banco).
- **Localidade é transversal** — não cabe em um módulo de negócio.
- **Reference data read-mostly** — carga por ETL, leitura intensa, escrita rara.
- **Congelamento por edital (RN08)** — o consumidor guarda snapshot, não referência viva.
- **Acoplamento mínimo entre módulos** — evitar dependência de runtime (HTTP/leitor) onde um snapshot resolve.

## Opções consideradas

- **A**: Hospedar localidades dentro de um módulo de negócio existente (ex.: Configuração).
- **B**: **Módulo `Geo` dedicado**, com banco isolado `uniplus_geo`, read-mostly.
- **C**: Banco `Geo` dedicado, mas consumo cross-módulo via leitor read-side (Reader/HTTP), como Configuração.

## Resultado da decisão

**Escolhida:** "B — módulo `Geo` dedicado com banco isolado, consumido por composição no cliente", porque aplica o isolamento por contexto da ADR-0054 a um contexto transversal e elimina acoplamento de runtime desnecessário.

- O `Geo` é um **bounded context próprio**: solution de 5 projetos (Clean Architecture) e banco isolado `uniplus_geo` (ADR-0054), **read-mostly**.
- O **consumo cross-módulo é por composição no cliente**: o front envia o identificador estável da localidade (ex.: código IBGE do município) e um *display cache* já resolvido; o backend do módulo consumidor persiste o **snapshot** necessário (congelamento por edital, RN08). **Não** há leitor read-side nem chamada HTTP cross-módulo do Geo em V1 — o que mantém o módulo `Geo` fora do caminho crítico de runtime dos demais módulos e satisfaz o isolamento de leitura cross-módulo (fitness `CrossModuleReadIsolationTests`, ao qual o `Geo` é adicionado).
- Referências a entidades de **outros contextos** não cruzam por chave estrangeira (ADR-0054); o Geo é dono apenas de localidades.

## Consequências

### Positivas

- Topologia coerente com o isolamento por contexto já adotado no projeto.
- Sem acoplamento de runtime entre o Geo e os módulos de negócio — o snapshot no consumidor é suficiente e auditável.
- Coesão: todo o conhecimento de localidades fica em um único contexto, pronto para evoluir (CEP, georreferência) sem tocar nos módulos de negócio.

### Negativas

- Um banco a mais para provisionar, migrar e operar.
- A composição no cliente exige que o front carregue o *display cache* — um contrato a manter entre front e back.

### Neutras

- A nomenclatura, migrations e configuração do banco seguem a própria ADR-0054; esta ADR fixa apenas **que** localidades têm contexto e banco próprios.

## Confirmação

- **Fitness test**: o `Geo` está no roster de `CrossModuleReadIsolationTests` e nenhum módulo depende de `Geo.Domain`/`Geo.Application`; o `Geo` não expõe leitor read-side consumido por outro módulo em V1.
- **Teste de migração**: o banco `uniplus_geo` é criado e migrado isoladamente.

## Prós e contras das opções

### A — Dentro de um módulo de negócio

- Bom, porque reaproveita um banco existente.
- Ruim, porque localidade é transversal; hospedá-la em um módulo de negócio quebra a coesão e mistura responsabilidades.

### B — Módulo `Geo` dedicado, consumo por composição no cliente (escolhida)

- Bom, porque isola o contexto, mantém coesão e evita acoplamento de runtime entre módulos.
- Ruim, porque adiciona um banco a operar e um contrato de *display cache* com o front.

### C — Banco dedicado com leitor read-side cross-módulo

- Bom, porque centraliza a leitura num contrato de leitor.
- Ruim, porque introduz acoplamento de runtime (resolução viva) onde o snapshot por edital (RN08) já resolve — custo sem benefício para localidades congeladas.

## Mais informações

- Aplica a [ADR-0054](0054-naming-convention-e-strategy-migrations.md) (bancos isolados por módulo) ao contexto transversal de localidades.
- Contrasta com a [ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md): aqui o consumo é por composição no cliente, não por leitor read-side.
- O mecanismo de georreferência do módulo é fixado na [ADR-0091](0091-postgis-georreferencia-nts.md); a isenção de soft-delete do reference data, na [ADR-0092](0092-etl-carga-dne-reference-data.md).
