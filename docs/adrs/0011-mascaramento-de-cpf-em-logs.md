---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0011: Mascaramento de CPF em logs via enricher Serilog

## Contexto e enunciado do problema

A LGPD (Lei 13.709/2018, Art. 6º inciso VII) exige medidas técnicas para que processamento acessório — incluindo logs, stdout, arquivos e sinks downstream — não exponha dados pessoais. Princípios mais detalhados de conformidade LGPD permanecem em política institucional interna.

Antes da entrega da Story `uniplus-api#115`, o `PiiMaskingEnricher` existia registrado no pipeline Serilog mas com implementação no-op — o enforcement era apenas nominal.

## Drivers da decisão

- Conformidade LGPD enforçada por construção, não por disciplina manual.
- Performance adequada no hot path de logging (zero alocação no caso comum).
- Defesa em profundidade — masking permissivo no log mesmo quando o domínio rejeita CPFs malformados.
- Encapsulamento — não expor superfície pública desnecessária.

## Opções consideradas

- Enricher Serilog (`ILogEventEnricher`) com regex compilada
- Acoplar masking ao value object `Cpf.Mascarado` do SharedKernel
- Mascarar somente no sink (template JSON, formatter)
- Biblioteca de terceiros (`Serilog.Enrichers.Sensitive`)
- Expor `IPiiMaskingService` como porta pública de mascaramento

## Resultado da decisão

**Escolhida:** `PiiMaskingEnricher` como `ILogEventEnricher` no pipeline Serilog, com as propriedades:

- **Padrão de mascaramento** `***.***.***-XX` (SERPRO/Gov.br), preservando os dois dígitos verificadores.
- **Detecção por regex compilada** (`[GeneratedRegex]`) com word boundaries ASCII — evita falsos positivos em timestamps/IDs longos e cobre CPFs formatados e não formatados.
- **Aplicação recursiva** em todos os tipos de `LogEventPropertyValue` do Serilog (`ScalarValue`, `StructureValue`, `SequenceValue`, `DictionaryValue` — chaves e valores).
- **Preservação de referência** quando nenhum CPF é encontrado — sem realocação de containers para logs sem PII (zero alocação no hot path).
- **API encapsulada** via `internal` + `[InternalsVisibleTo]` no `.csproj`.
- **Decoupling do value object `Cpf`** — VO valida estritamente para uso no domínio; enricher mascara permissivamente para defesa em profundidade.

O enricher é thread-safe por construção: regex compilada é imutável, helpers são funções puras, pipeline Serilog processa cada `LogEvent` sequencialmente.

## Consequências

### Positivas

- Conformidade LGPD por construção — não é possível "esquecer de mascarar".
- Performance adequada no hot path (regex compilada via `[GeneratedRegex]`, spans no replace, zero alocação sem PII).
- Camadas independentes — VO e enricher evoluem separadamente.
- Auditável via testes automatizados que servem de evidência de enforcement em cada deploy.

### Negativas

- Falso negativo raro quando um CPF é concatenado a outros dígitos sem delimitador. Trade-off deliberado: preferimos preservar timestamps e IDs íntegros a mascarar dígitos ambíguos.
- Uma alocação de array por `LogEvent` para snapshot seguro das propriedades antes da iteração — aceitável para o volume típico do Serilog.
- Escopo limitado a CPF — outros PII (email, telefone, RG) ficam para issues dedicadas que estenderão o mesmo enricher.

### Riscos

- **Novo tipo de `LogEventPropertyValue`** em versão futura do Serilog não seria coberto pela recursão. Mitigação: revisar ao atualizar major version da dependência.
- **Bypass do pipeline** (`Console.WriteLine` direto, por exemplo). Mitigação: convenção `[LoggerMessage]` obrigatória no projeto (`CA1848` como erro de build) e code review.

## Confirmação

- Suíte de testes do enricher cobre cenários de scalar, structure, sequence, dictionary e composição recursiva.
- Pipeline de CI executa `dotnet test` com a categoria do enricher e falha o build em caso de regressão.

## Prós e contras das opções

### Acoplar ao VO `Cpf.Mascarado`

- Bom, porque reutiliza a regra do domínio.
- Ruim, porque o VO falha validação para CPFs com checksum incorreto — exatamente o tipo de entrada que precisa ser mascarada em logs.

### Mascarar só no sink

- Bom, porque é mais simples por sink.
- Ruim, porque não captura propriedades estruturadas antes da serialização e precisa ser replicado por sink.

### `Serilog.Enrichers.Sensitive`

- Bom, porque é dependência pronta.
- Ruim, porque é generalista, sem regex calibrada para padrões brasileiros, adiciona dependência externa em infraestrutura crítica.

### `IPiiMaskingService` público

- Bom, porque permite reuso fora do pipeline.
- Ruim, porque cria ambiguidade sobre qual é a porta oficial de mascaramento sem consumidor concreto definido — YAGNI.

## Mais informações

- ADR-0018 define OpenTelemetry e Loki como sinks downstream — masking é aplicado antes da exportação.
- LGPD (Lei 13.709/2018), Art. 6º inciso VII e Art. 46.
- [Serilog wiki — Enrichment](https://github.com/serilog/serilog/wiki/Enrichment)
- **Origem:** revisão da ADR interna Uni+ ADR-020 (não publicada).
