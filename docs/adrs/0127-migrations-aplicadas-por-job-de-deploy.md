---
status: "accepted"
date: "2026-08-26"
decision-makers:
  - "Tech Lead (CTIC)"
informed:
  - "Equipe Uni+"
---

# ADR-0127: Migrations aplicadas por Job de deploy, com o boot do pod como default preservado

## Contexto e enunciado do problema

As migrations EF Core são aplicadas no `StartAsync` do host, por `MigrationHostedService<TContext>`. Isso acopla a alteração de schema ao ciclo de vida do pod, e a consequência aparece só no deploy: **o orquestrador já mexeu nos pods quando descobre que a migration falhou.**

Os dois desfechos possíveis são ruins, e ambos foram observados na promoção da `v0.7.0` para homologação em 26/08/2026:

- Sob `RollingUpdate` com `maxUnavailable: 0`, o pod anterior permanece **pronto e atendendo** enquanto o pod novo aplica a migration. Entre o `ALTER TABLE` e o pod novo ficar pronto, quem responde é uma versão que não conhece o schema vigente.
- Sob `Recreate`, o pod anterior já foi removido: uma migration que falha deixa a API **inteiramente fora do ar**, e não a versão anterior no lugar.

Em ambos, voltar a imagem **não desfaz** a migration — o rollback deixa de ser uma operação disponível exatamente quando é mais necessário.

A tentativa de resolver isso pela estratégia de atualização (adotar `Recreate` no ambiente) trocou um problema por outro: além de exigir intervenção manual no cluster para o primeiro sync, transformou "manter a versão anterior" em "ficar sem serviço".

## Drivers da decisão

- A falha da migration precisa ser conhecida **antes** de qualquer pod ser criado ou removido.
- Nenhum ambiente pode mudar de comportamento por omissão — quem não declarar nada segue como está.
- O modo de execução não deve exigir que cada módulo saiba em que contexto o processo roda.
- A imagem publicada é uma só; o papel do processo é do manifesto, não do build.

## Opções consideradas

- **Manter a migration no boot** e mitigar pela estratégia de atualização do Deployment.
- **Separar por configuração**: a mesma imagem aplica migration e encerra, ou serve requisições sem migrar (esta ADR).
- **Migration fora da aplicação**, por ferramenta dedicada com o SQL versionado à parte.

## Resultado da decisão

**Escolhida:** "Separar por configuração", porque resolve a causa — o acoplamento entre alterar schema e servir requisições — sem duplicar o versionamento das migrations nem tirar da aplicação a fonte de verdade do schema.

A chave `UniPlus:Migrations:Mode` declara o papel do processo:

| Valor | Papel |
|---|---|
| ausente ou `OnStartup` | aplica no boot — **default, comportamento anterior preservado** |
| `ApplyAndExit` | aplica as migrations e encerra; é o modo do Job de deploy |
| `Skip` | não aplica; é o modo do pod quando o Job já cuidou |

`ApplyAndExit` executa **apenas** os `IHostedService` de migration que os módulos já registram, e encerra com código de saída próprio: `0` em sucesso, não-zero em falha. Construir o host resolve o container mas não inicia serviço algum, então mensageria e pipeline HTTP não chegam a subir. `Skip` remove esses mesmos descritores antes do `Build()` — o expediente que os test factories já usavam para subir sem Postgres.

Nenhum módulo precisa saber em que modo o processo está: os três modos operam sobre o mesmo registro existente, a partir do composition root.

Um valor fora do domínio é **recusado no boot**, com a lista dos aceitos. Cair em default silencioso faria um pod destinado a `Skip` aplicar migration por conta própria, que é precisamente o que a separação existe para impedir.

## Consequências

### Positivas

- Migration que falha aborta o rollout com os pods anteriores **intactos e coerentes** com o schema que continuam servindo.
- O código de saída dá ao orquestrador um sinal explícito, em vez de deixá-lo inferir saúde de um pod que reinicia.
- Dispensa `Recreate` como remédio, e com ele a indisponibilidade e a intervenção manual que a mudança de estratégia exige.
- Rollback de imagem volta a ser possível no caso comum, porque a migration deixa de ser efeito colateral de subir pod.

### Negativas

- O deploy ganha um passo: o Job precisa concluir antes do rollout, somando o tempo da migration ao tempo total.
- Ambiente que adotar a separação passa a ter dois manifestos derivados da mesma imagem, e uma incoerência entre eles — Job ausente com pod em `Skip` — deixaria o schema sem aplicar. É o preço de separar os papéis.

### Neutras

- Migration **destrutiva** continua exigindo cuidado próprio: o Job garante que a falha seja limpa, não que o schema novo seja compatível com a versão anterior. Para isso, a resposta é expandir e contrair em duas releases, decisão que esta ADR não toma.

## Confirmação

- Testes cobrem os três modos, a recusa de valor desconhecido e o código de saída em falha.
- Ambiente sem a chave declarada continua aplicando no boot — verificável por inspeção do manifesto e pelo teste que fixa o default.

## Prós e contras das opções

### Manter a migration no boot e mitigar pela estratégia

- Bom, porque não exige mudança na aplicação.
- Ruim, porque nenhuma estratégia resolve: `RollingUpdate` deixa o pod anterior servindo schema alterado, e `Recreate` troca isso por indisponibilidade.
- Ruim, porque mudar a estratégia de um Deployment existente exige intervenção manual no cluster.

### Separar por configuração

- Bom, porque a falha passa a ser conhecida antes de qualquer pod ser tocado.
- Bom, porque reusa o registro de migration existente, sem tocar nos módulos.
- Ruim, porque acrescenta um passo ao deploy e um manifesto ao ambiente.

### Migration fora da aplicação

- Bom, porque desacopla completamente schema de aplicação.
- Ruim, porque duplica o versionamento do schema e tira do EF a fonte de verdade que hoje ele tem.
- Ruim, porque exigiria reescrever o histórico de migrations já aplicado.

## Mais informações

- [ADR-0050](0050-registry-ghcr-e-tagging.md) — publicação por tag explícita; esta ADR muda o que acontece **durante** a promoção, não o que a dispara.
- O Job que consome `ApplyAndExit` vive em `unifesspa-edu-br/uniplus-infra`.
