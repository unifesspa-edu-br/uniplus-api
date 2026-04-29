---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0009: MinIO como object storage S3-compatible

## Contexto e enunciado do problema

O `uniplus-api` precisa de object storage para:

- documentos enviados pelos candidatos (RG, laudos, comprovantes de renda) durante inscrição e recursos administrativos;
- documentos gerados pelo sistema (PDFs de resultados, planilhas de classificação, comprovantes de inscrição);
- versionamento e audit trail de arquivos substituídos pelo candidato.

Filesystem local não escala em Kubernetes com múltiplas réplicas. Cloud storage gerenciado (AWS S3, Azure Blob) tem custo recorrente incompatível com universidade pública e move dados pessoais para fora da infraestrutura institucional, com implicações sob a LGPD.

## Drivers da decisão

- API S3-compatible para portabilidade futura.
- Self-hosted, dados sob a infraestrutura da Unifesspa.
- Custo zero de licenciamento.
- Equipe da Unifesspa tem experiência operacional acumulada com MinIO em sistema legado.

## Opções consideradas

- MinIO (última versão estável)
- Filesystem local
- AWS S3
- Azure Blob Storage

## Resultado da decisão

**Escolhida:** MinIO como object storage S3-compatible do `uniplus-api`.

Práticas obrigatórias:

- **Buckets separados por módulo e tipo de documento** com políticas de retenção configuráveis (ex.: documentos de processo seletivo encerrado há mais de 5 anos podem ser arquivados).
- **Erasure coding** habilitado para redundância distribuída entre discos.
- **Backup regular** para storage secundário institucional.
- **Versionamento de objetos** ativo para audit trail de substituições pelo candidato.
- **Acesso via MinIO .NET SDK** (compatível com AWS S3 SDK) — abstração sobre `IObjectStorage` em `Application.Abstractions/Storage/` mantém o cliente substituível.

## Consequências

### Positivas

- API S3-compatible — portabilidade para AWS S3 ou outro provedor sem alteração de código de aplicação.
- Dados permanecem na infraestrutura institucional (alinhado com LGPD).
- Open source, custo zero de licenciamento.
- Versionamento nativo cobre o requisito de audit trail de documentos.

### Negativas

- Mais um serviço para operar — exige dimensionamento de disco e backup ativos.
- Crescimento de storage ao longo dos processos seletivos exige lifecycle rules.
- Recuperação de desastre depende da disciplina de backup secundário.

### Riscos

- **Perda de dados.** Mitigado por erasure coding + backup secundário institucional.
- **Crescimento descontrolado de storage.** Mitigado com políticas de retenção e lifecycle rules para processos seletivos encerrados há mais de 5 anos.

## Confirmação

- Health check `/health/storage` valida conectividade e operação de read/write em bucket de teste.
- Métricas de uso de bucket (size, object count) emitidas para observabilidade (ver ADR-0018).

## Mais informações

- ADR-0017 define K8s + Helm para o deploy.
- **Origem:** revisão da ADR interna Uni+ ADR-010 (não publicada) — split: a parte "cache distribuído" foi extraída para a ADR-0008.
