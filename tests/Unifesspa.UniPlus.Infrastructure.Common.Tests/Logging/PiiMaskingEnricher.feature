# language: pt
# Story: #115 — PiiMaskingEnricher.Enrich para mascaramento efetivo de CPF em logs
# Referências: LGPD Art. 6º, ADR-012, RN-LGPD
# Arquivo-alvo: src/shared/Unifesspa.UniPlus.Infrastructure.Common/Logging/PiiMaskingEnricher.cs

Funcionalidade: Mascaramento de CPF em logs estruturados do Serilog
  Como operador do sistema Uni+
  Quero que CPFs sejam automaticamente mascarados em todos os registros de log
  Para garantir conformidade com a LGPD e evitar vazamento de dados pessoais em saídas estruturadas (stdout, Loki, arquivos)

  Contexto:
    Dado que o pipeline do Serilog está configurado com o enricher "PiiMaskingEnricher"
    E que o padrão de mascaramento definido pela LGPD é "***.***.***-XX", preservando os dois últimos dígitos verificadores
    E que um sink capturador de eventos está anexado ao logger para inspeção dos LogEvents emitidos

  # ── Cenários felizes ──────────────────────────────────────

  Cenário: CA-01 — Mascarar CPF formatado em propriedade estruturada
    Dado um LogEvent com a propriedade "CpfCandidato" contendo o valor "123.456.789-01"
    Quando o enricher processar o evento
    Então a propriedade "CpfCandidato" deve conter o valor "***.***.***-01"
    E nenhum dígito verificador do meio deve aparecer em texto claro

  Cenário: CA-02 — Mascarar CPF não-formatado (somente dígitos)
    Dado um LogEvent com a propriedade "CpfCandidato" contendo o valor "12345678901"
    Quando o enricher processar o evento
    Então a propriedade "CpfCandidato" deve conter o valor "***.***.***-01"
    E o formato do retorno deve seguir o padrão SERPRO/Gov.br

  Cenário: CA-04 — Mascarar múltiplas ocorrências na mesma string
    Dado um LogEvent com a propriedade "Mensagem" contendo o texto "candidatos 123.456.789-01 e 987.654.321-02 homologados"
    Quando o enricher processar o evento
    Então a propriedade "Mensagem" deve conter o texto "candidatos ***.***.***-01 e ***.***.***-02 homologados"
    E nenhum CPF em texto claro deve permanecer na saída

  # ── Cenários de borda ─────────────────────────────────────

  Cenário: CA-03 — Log sem CPF preserva valor original sem alocação extra
    Dado um LogEvent com a propriedade "Mensagem" contendo o valor "processo seletivo iniciado com sucesso"
    Quando o enricher processar o evento
    Então a propriedade "Mensagem" deve conter o valor "processo seletivo iniciado com sucesso"
    E a referência da string retornada deve ser a mesma instância original (sem realocação)

  Cenário: CPF com 11 dígitos inválido (fora do padrão regex) não é alterado
    Dado um LogEvent com a propriedade "Texto" contendo o valor "codigo-ABC-123"
    Quando o enricher processar o evento
    Então a propriedade "Texto" deve conter o valor "codigo-ABC-123"
    E o enricher não deve emitir alertas ou warnings

  Cenário: Propriedade com valor nulo é ignorada silenciosamente
    Dado um LogEvent com a propriedade "CpfCandidato" contendo valor nulo
    Quando o enricher processar o evento
    Então a propriedade "CpfCandidato" deve permanecer com valor nulo
    E nenhuma exceção deve ser lançada

  # ── Cenários compostos (recursivos) ───────────────────────

  Cenário: CA-05 — Mascaramento recursivo em StructureValue aninhado
    Dado um LogEvent com a propriedade estruturada "Candidato" contendo os campos:
      | campo | valor            |
      | Nome  | João da Silva    |
      | Cpf   | 123.456.789-01   |
    Quando o enricher processar o evento
    Então o campo "Cpf" do StructureValue "Candidato" deve conter "***.***.***-01"
    E o campo "Nome" deve permanecer inalterado com o valor "João da Silva"

  Cenário: Mascaramento recursivo em SequenceValue de strings
    Dado um LogEvent com a propriedade "CpfsHomologados" contendo a sequência ["123.456.789-01", "987.654.321-02"]
    Quando o enricher processar o evento
    Então a sequência "CpfsHomologados" deve conter os valores ["***.***.***-01", "***.***.***-02"]

  Cenário: Mascaramento recursivo em estruturas profundamente aninhadas (Structure dentro de Sequence)
    Dado um LogEvent com a propriedade "Lote" contendo uma sequência de objetos "Candidato", cada um com o campo "Cpf" preenchido
    Quando o enricher processar o evento
    Então cada campo "Cpf" de cada elemento da sequência deve estar mascarado no padrão "***.***.***-XX"

  # ── Cenários parametrizados ───────────────────────────────

  Esquema do Cenário: Mascarar CPF em variações de formatação
    Dado um LogEvent com a propriedade "CpfCandidato" contendo o valor "<entrada>"
    Quando o enricher processar o evento
    Então a propriedade "CpfCandidato" deve conter o valor "<saida>"

    Exemplos:
      | entrada           | saida            |
      | 123.456.789-01    | ***.***.***-01   |
      | 12345678901       | ***.***.***-01   |
      | 123.45678901      | ***.***.***-01   |
      | 123456789-01      | ***.***.***-01   |
      | 000.000.000-00    | ***.***.***-00   |
      | 999.999.999-99    | ***.***.***-99   |

  # ── Cenário de integração (end-to-end Serilog) ────────────

  Cenário: CA-07 — Integração com ILogger real e sink capturador
    Dado um host configurado com Serilog usando "PiiMaskingEnricher" no pipeline
    E um sink em memória anexado para capturar os LogEvents emitidos
    Quando o código da aplicação invocar um método "[LoggerMessage]" passando o CPF "123.456.789-01" como parâmetro estruturado "CpfCandidato"
    Então o LogEvent capturado deve conter a propriedade "CpfCandidato" com o valor "***.***.***-01"
    E a saída renderizada do template (`RenderMessage()`) não deve conter o CPF em texto claro
    E nenhum dígito intermediário do CPF deve aparecer na mensagem final

  # ── Cenário de conformidade LGPD ──────────────────────────

  Cenário: Todos os sinks herdam o mascaramento (stdout, Loki, arquivo)
    Dado um host configurado com múltiplos sinks Serilog (Console, Arquivo, Loki)
    E o enricher "PiiMaskingEnricher" registrado no pipeline raiz
    Quando um log for emitido contendo CPF em qualquer propriedade estruturada
    Então o CPF mascarado deve aparecer em todos os sinks configurados
    E nenhum sink deve receber o valor original em texto claro

---

## Mapeamento para implementação

**Projeto de teste unitário:**
- Arquivo: `tests/Unifesspa.UniPlus.Infrastructure.Common.Tests/Logging/PiiMaskingEnricherTests.cs`
- Framework: xUnit + FluentAssertions
- Cobertura-alvo: CA-01, CA-02, CA-03, CA-04, CA-05, CA-06 (≥90%)

**Projeto de teste de integração:**
- Arquivo: `tests/Unifesspa.UniPlus.Infrastructure.Common.Tests/Logging/PiiMaskingEnricherIntegrationTests.cs`
- Setup: `LoggerConfiguration` real + sink capturador (`InMemorySink` ou implementação própria de `ILogEventSink`)
- Cobertura-alvo: CA-07

**Fixtures necessárias:**
- Factory de `LogEvent` com propriedades variadas (scalar, structure, sequence, aninhadas)
- Helper para construir `StructureValue` e `SequenceValue` com Serilog.Events

**Referências cruzadas:**
- `MascararCpf` (método estático existente) — **corrigir bug**: usa ponto no separador final; deve ser hífen conforme padrão SERPRO/Gov.br
- `[LoggerMessage]` source generator (CLAUDE.md do uniplus-api) — obrigatório no teste de integração
