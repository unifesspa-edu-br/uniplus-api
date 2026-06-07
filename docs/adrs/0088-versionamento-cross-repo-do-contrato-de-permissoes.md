---
status: "proposed"
date: "2026-06-02"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0088: Versionamento e publicação cross-repo do contrato de permissões

## Contexto e enunciado do problema

O catálogo declarativo de permissões ([ADR-0080](0080-catalogo-declarativo-de-permissoes-e-codegen.md)) gera, entre outros artefatos, as **constantes de permissão consumidas pelo frontend** (`uniplus-web`). Backend (`uniplus-api`) e frontend são **repositórios distintos**.

Se o frontend consome esse contrato sem disciplina de versão, uma mudança no catálogo de permissões do backend pode **quebrar o frontend silenciosamente** (uma permissão renomeada ou removida) ou deixá-lo **dessincronizado** (o frontend referencia permissões que não existem mais, ou ignora permissões novas). O problema é **como o contrato gerado é publicado e versionado** para que o frontend o consuma de forma **estável e verificável**.

> Esta é uma decisão **distinta** da existência da fonte única e do gerador (ADR-0080), em respeito à regra "1 ADR = 1 decisão": a 0080 decide que o contrato **é gerado**; esta ADR decide **como ele é publicado e versionado entre repositórios**.

## Drivers da decisão

- **Consumo estável** pelo frontend — sem quebra silenciosa por mudança no backend.
- **Mudança incompatível visível** — uma alteração que quebra o contrato precisa ser explícita.
- **Verificação de sincronização** — detectar, na integração contínua, que o frontend está desalinhado do contrato publicado.

## Opções consideradas

- **A**: O frontend copia as constantes manualmente do backend.
- **B**: O contrato é publicado como **pacote versionado** (versionamento semântico); o frontend o consome com **versão fixa**; a integração contínua valida o casamento entre o que o frontend consome e o que o backend publicou.
- **C**: O frontend referencia o arquivo do backend diretamente (submódulo ou caminho compartilhado).

## Resultado da decisão

**Escolhida:** "B — contrato publicado como pacote versionado, consumido com versão fixa, validado na integração contínua", porque torna a evolução do contrato explícita e detectável, sem acoplar fisicamente os dois repositórios.

- O contrato gerado é publicado como um **pacote versionado**, seguindo **versionamento semântico**: uma mudança incompatível (remover ou renomear uma permissão) é um **salto de versão maior**, visível.
- O frontend consome o pacote com **versão fixa** (sem faixa aberta), de modo que atualizar o contrato é um **ato deliberado** — uma mudança no backend não chega ao frontend sem revisão.
- A **integração contínua valida** que a versão do contrato consumida pelo frontend corresponde à versão publicada pelo backend, detectando deriva entre os repositórios.

## Consequências

### Positivas

- O frontend consome um contrato estável; mudanças no backend não o quebram sem aviso.
- Uma mudança incompatível é explícita (salto de versão maior), não silenciosa.
- A deriva entre os repositórios é detectada pela integração contínua.

### Negativas

- Há um fluxo de publicação e versionamento do pacote a operar.
- A atualização do frontend para uma versão nova do contrato é um passo deliberado, não automático.

### Neutras

- O ecossistema de empacotamento, o nome do pacote e o mecanismo concreto de publicação são detalhes de implementação; esta ADR fixa a **disciplina** (pacote versionado por versionamento semântico, consumo com versão fixa, validação na integração contínua).

## Confirmação

- **Validação na integração contínua (cross-repo)**: a versão do contrato consumida pelo frontend bate com a versão publicada pelo backend; divergência falha a verificação.
- **Política de versão**: uma mudança incompatível no catálogo de permissões exige um salto de versão maior; o frontend só passa a consumi-la ao atualizar a versão fixa deliberadamente.

## Prós e contras das opções

### A — Cópia manual das constantes

- Bom, porque não exige publicação de pacote.
- Ruim, porque a cópia se desatualiza sem aviso e não há verificação de sincronização — a divergência é descoberta tarde.

### B — Pacote versionado, versão fixa, validação na integração contínua (escolhida)

- Bom, porque torna a evolução explícita e detectável, sem acoplar fisicamente os repositórios.
- Ruim, porque exige operar a publicação e tratar a atualização do frontend como ato deliberado.

### C — Referência direta ao arquivo do backend (submódulo/caminho)

- Bom, porque há uma única cópia do contrato.
- Ruim, porque acopla fisicamente os repositórios, dificulta a versão fixa e mistura os ciclos de build dos dois projetos.

## Mais informações

- O artefato versionado é um dos produtos do gerador decidido na [ADR-0080](0080-catalogo-declarativo-de-permissoes-e-codegen.md); esta ADR cobre **apenas** a sua publicação e versionamento entre repositórios.
- O consumo desse contrato pelo frontend é implementado no repositório `uniplus-web`.
