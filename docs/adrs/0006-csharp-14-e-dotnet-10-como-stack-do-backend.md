---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0006: C# 14 / .NET 10 como linguagem e runtime do backend

## Contexto e enunciado do problema

O `uniplus-api` precisa de uma stack que comporte Clean Architecture, CQRS, processamento de classificação em lote e integrações com Keycloak, PostgreSQL, Kafka e S3. A equipe está se capacitando em .NET, alinhada com o ecossistema institucional da Unifesspa.

O sistema deve absorver picos de inscrição (5000+ candidatos simultâneos), gerar PDFs e planilhas, processar importação de notas em CSV/Excel e executar o motor de classificação com fórmulas configuráveis e desempates encadeados.

## Drivers da decisão

- Suporte LTS de longo prazo (3+ anos) compatível com horizonte de produto.
- Tipagem forte com null safety para reduzir erros em runtime.
- Ecossistema enterprise maduro para todas as integrações previstas.
- Custo zero de licenciamento.
- Capacidade de produzir containers AOT enxutos para Kubernetes.

## Opções consideradas

- C# 14 / .NET 10 + EF Core 10 (Npgsql)
- Java 21 / Spring Boot 3
- Go (com gorm/sqlx)
- Node.js / NestJS
- Python / Django

## Resultado da decisão

**Escolhida:** C# 14 / .NET 10 como linguagem e runtime do backend, com EF Core 10 + provider Npgsql como ORM.

Bibliotecas complementares de mercado:

| Biblioteca | Propósito |
|------------|-----------|
| Wolverine | CQRS in-process e outbox (ver ADR-0003, ADR-0004) |
| FluentValidation | validação de comandos e queries |
| Serilog | logging estruturado com PII masking (ver ADR-0011) |
| Stateless | máquinas de estado de inscrição, edital e recurso |
| QuestPDF | geração de PDFs (resultados, comprovantes) |
| ClosedXML, CsvHelper | importação e exportação de planilhas e CSV |
| MailKit | envio de emails de notificação |
| MinIO .NET SDK | object storage (ver ADR-0009) |
| Asp.Versioning | versionamento de API REST |

`MediatR` é proibido na codebase — a escolha de framework de mediação é Wolverine (ADR-0003), enforçada por fitness test ArchUnitNET (ADR-0012).

## Consequências

### Positivas

- Tipagem forte com null safety reduz classes inteiras de bugs.
- Performance excelente para APIs (Kestrel) — benchmarks consistentemente entre os melhores web frameworks.
- Ecossistema enterprise — todas as integrações previstas têm libs maduras.
- LTS do .NET 10 cobre horizonte de 3 anos.
- AOT compilation possível para containers menores no Kubernetes.
- Integração nativa com OpenTelemetry (ver ADR-0018).

### Negativas

- Pool de desenvolvedores .NET na região Norte é menor — mitigado por capacitação interna.
- Runtime base maior que Go para containers simples — mitigado por AOT e multi-stage Docker builds.
- EF Core pode ser lento para queries muito complexas — mitigado com Dapper para hot paths quando necessário.

### Neutras

- Verbosidade similar a Java — não é vantagem nem desvantagem em projetos longos.

## Confirmação

- Pipeline de CI exige build verde com `--configuration Release` e zero warnings tratados como erros.
- `Directory.Packages.props` centraliza versões — bumps de major exigem PR dedicado com suíte completa de testes.

## Mais informações

- ADR-0002 define a organização de camadas que esta stack viabiliza.
- ADR-0003, ADR-0004, ADR-0005 detalham o backbone CQRS.
- ADR-0007 define PostgreSQL como banco primário.
- **Origem:** revisão da ADR interna Uni+ ADR-005 (não publicada).
