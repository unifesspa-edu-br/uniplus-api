---
status: "accepted"
date: "2026-11-09"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0157: Perfis de acesso versionados, com a concessão presa à versão aprovada e a ampliação por migração em lote

## Contexto e enunciado do problema

A [ADR-0152](0152-governanca-das-concessoes-de-acesso.md) faz toda concessão nascer de um pedido aprovado. Ela diz também que a mudança no perfil vale para todas as concessões dele, e que acrescentar permissão pessoal ou sensível passa pela proteção de dados "como um pedido". Esse fluxo nunca foi implementado: hoje, acrescentar permissão Interno a um perfil já concedido amplia na hora o acesso de todas as pessoas, com uma única aprovação da gestão de acessos. Acrescentar permissão protegida é recusado, e o caminho é criar outro perfil.

Há mais quatro lacunas:

- o perfil não tem origem formal;
- um perfil sem concessões se edita sem controle, mesmo com pedido em andamento;
- uma versão nova do sistema que reclassifica uma permissão para cima passa a dar esse acesso sem aprovação;
- desativar o perfil não contém um incidente.

O problema é ampliar e mudar perfis sem que ninguém ganhe permissão sem uma aprovação daquela concessão naquela composição, e sem travar o perfil quando todas as pessoas da gestão de acessos o têm.

## Drivers da decisão

- **Nenhuma ampliação sem aprovação da concessão.** Aprovar o perfil não basta: cada pessoa que ganha a permissão precisa ser avaliada.
- **Segregação sem travamento.** Ninguém aprova a mudança que propôs nem a própria concessão, mas ter o perfil não pode impedir a operação.
- **Uma só versão em uso.** O perfil não pode ficar com várias versões valendo sem prazo.
- **Contenção imediata.** A retirada de acesso nunca espera aprovação nem tarefa agendada.
- **Rastreabilidade por decisão** ([ADR-0086](0086-trilha-de-auditoria-com-hmac-e-cofre.md)).

## Opções consideradas

- **A**: A versão aprovada vale para todas as concessões do perfil.
- **B**: A versão nova exige um pedido novo de cada pessoa.
- **C**: A concessão fica presa à versão aprovada, e a ampliação chega a quem já tem o perfil por migração em lote, numa janela de prazo fixo.

## Resultado da decisão

**Escolhida:** "C — concessão presa à versão e migração em lote". A versão aprovada não amplia o acesso de ninguém por si; cada concessão é decidida uma a uma, e a janela de prazo fixo impede que a versão anterior continue em uso para sempre.

**Perfil versionado.**

- Cada versão é imutável e guarda nome, descrição, permissões, o documento instituidor (protocolo e hash do PDF), quem a criou e o instante.
- A vigência é um registro separado.
- O perfil tem no máximo uma versão em vigor e uma pendente. Com a janela de migração aberta, não aceita versão pendente.
- O perfil novo nasce pendente e não pode ser pedido antes da aprovação.
- O escopo não muda entre versões.

**Concessão presa à versão.**

- A concessão guarda a versão aprovada para ela.
- A decisão de acesso usa a interseção entre essa versão e a versão em vigor, então a remoção de permissão vale na hora para todos.
- O pedido registra a versão em vigor. Se entra em vigor uma versão sem acréscimo antes da última aprovação, o pedido volta à gestão de acessos; se a versão tem acréscimo, o pedido é devolvido ao solicitante.
- Fora da janela, a versão sem acréscimo migra todas as concessões automaticamente. Dentro da janela, a remoção cria na hora uma versão reduzida: as concessões já migradas passam para ela, as migrações seguintes também apontam para ela, e as demais continuam sujeitas à decisão do lote, no mesmo prazo.
- A versão com acréscimo abre a janela de migração.

**Aprovação de versão.**

| Mudança | Aprovação |
|---|---|
| Perfil novo só com Público ou Interno, ou acréscimo só de permissões Público ou Interno | outra pessoa da gestão de acessos |
| Perfil novo com permissão Pessoal, Identificador ou Sensível, ou acréscimo de ao menos uma permissão dessas classes | outra pessoa da gestão de acessos e, depois, a proteção de dados |
| Só nome, descrição ou documento | outra pessoa da gestão de acessos |
| Só remoção | sem aprovação, com motivo e aviso |

Quem cria a versão, quem a aprova pela gestão de acessos e quem a aprova pela proteção de dados são três pessoas diferentes. Na versão que remove e acrescenta, a remoção vale na hora para todos, e o acréscimo segue a tabela. A remoção cancela a versão pendente. A versão pendente expira em 15 dias.

**Janela de migração.**

- Dura 72 horas corridas, sem prorrogação. Termina antes quando não resta concessão pendente, ou quando a gestão de acessos encerra a versão anterior, com motivo e confirmação, depois de concluída a aprovação da versão nova. Quem não migrou perde o acesso.
- Nela, a versão anterior não recebe atribuição, a versão nova não recebe pessoa nova e os pedidos em andamento são devolvidos.
- A gestão de acessos decide cada concessão da versão anterior, migrando ou recusando, num lote completo e atômico.
- No acréscimo protegido, a proteção de dados decide em seguida, individualmente ou em lote, e não migra quem a gestão de acessos recusou.
- A recusa revoga na hora.
- A concessão que não concluir todas as etapas exigidas até o fim do prazo cai, inclusive a aprovada pela gestão de acessos que ainda aguarda a proteção de dados, e a pessoa volta por pedido novo.
- A decisão de acesso confere o prazo gravado, sem depender da tarefa agendada.

**Segregação.**

- Quem criou a versão não a aprova.
- Ninguém decide a própria concessão: as concessões de quem envia o lote ficam retidas para outra pessoa da gestão de acessos, que tenha o perfil ou não.
- As duas etapas de uma mesma concessão são de pessoas diferentes.
- Ter concessão do perfil não impede ninguém de criar nem de aprovar versões dele.

**Contenção.**

- **Suspender** o perfil bloqueia todas as concessões na hora. Retomar exige outra pessoa, que não seja quem suspendeu, e também a proteção de dados quando o perfil é protegido.
- **Desativar** só impede pedidos e versões novas. Reativar exige outra pessoa da gestão de acessos.
- **Reclassificar uma permissão para cima**, ou mudar o escopo dela, a suspende nas versões em uso. A suspensão é aplicada numa etapa de migration, antes de servir tráfego.
  - A liberação é perfil por perfil, lendo todas as permissões do perfil e o documento instituidor: a proteção de dados libera quando a classificação sobe, e a gestão de acessos, quando o escopo muda. Se os dois mudam, são duas decisões, de pessoas diferentes.
  - Dentro do perfil, a proteção de dados revisa as concessões individualmente ou em lote. Quem não for liberado continua sem a permissão reclassificada, e a concessão cujo escopo não comporta o novo alcance só volta por pedido novo.
- Os perfis de governança ficam fora da suspensão por reclassificação, que neles só gera aviso. Também não se suspendem pela API, como já não se desativam: suspender a gestão de acessos travaria a própria retomada, e a contenção neles é a revogação individual, com o mínimo de duas pessoas. Também não têm versão, lote nem janela, e a composição deles só muda por PR com migration. Por isso só reúnem permissões do módulo de autorização, e toda permissão reservada à governança é desse módulo: uma permissão de outro módulo chegaria a todos os gestores e à proteção de dados sem a aprovação que os perfis comuns exigem.

**Avisos e trilha.**

- Os avisos vão por e-mail, pelo outbox, sem texto livre nem dado pessoal.
- Cada decisão grava, por concessão, um evento imutável na trilha. O evento registra quem decidiu, a etapa, a dispensa aplicada, a composição avaliada e se quem decide tem concessão do perfil.

## Emendas à ADR-0152

- A mudança no perfil deixa de valer sozinha para todas as concessões. A remoção vale para todas na hora, e o acréscimo só vale por migração.
- A migração dispensa pedido novo e aceite do beneficiário.
  - Continuam valendo os impedimentos contra autobenefício: ninguém decide a própria concessão, as etapas são de pessoas diferentes e quem criou a versão não a aprova.
  - O impedimento de quem solicitou a concessão original não se aplica, porque não há pedido novo.
- A migração de versão é a única exceção à regra de que a concessão não se edita. Ela troca só a versão e mantém pessoa, escopo e validade.
- A aprovação da versão e da migração segue a classificação do **acréscimo**, e não a do perfil. A concessão nova continua seguindo a classificação da versão pedida. Um acréscimo só Interno num perfil já protegido passa só pela gestão de acessos, porque a proteção de dados já aprovou aquela concessão.
- Continuam valendo:
  - as dispensas `designacao_de_protecao_de_dados` e `beneficiario_ja_tem_protecao_de_dados`;
  - o mínimo de duas pessoas na gestão de acessos;
  - a revogação imediata.
- A pendência do envio de e-mail está resolvida: o Uni+ já envia pelo outbox.

## Consequências

### Positivas

- Nenhuma pessoa ganha permissão sem uma aprovação daquela concessão naquela composição.
- O perfil não trava quando todas as pessoas da gestão de acessos o têm.
- A contenção é imediata.
- A reclassificação falha fechada.
- Toda função passa a ter origem documental.

### Negativas

- Ampliar um perfil obriga a revisar todas as concessões em 72 horas. Sem decisão no prazo, o acesso cai, e isso inclui a concessão retida, se ninguém mais da gestão de acessos agir. Os avisos de 48 e 24 horas reduzem o risco.
- Durante a janela, o perfil não recebe pessoa nova.
- Toda mudança de nome ou descrição passa por outra pessoa da gestão de acessos.
- **Risco aceito:** duas pessoas da gestão de acessos com o mesmo perfil podem migrar uma a concessão da outra. A trilha registra quando quem decide tem o perfil.

### Neutras

- A proteção contra escrita direta no banco fica fora desta decisão e vai para uma decisão própria, que emendará a ADR-0086.

## Confirmação

- Testes de API das recusas de segregação, da janela, do prazo conferido na decisão de acesso, da idempotência e da concorrência.
- Teste de arquitetura de que a decisão de acesso não lê as permissões do perfil vivo.
- Implementação acompanhada pela épica unifesspa-edu-br/uniplus-api#2274.

## Prós e contras das opções

### A — Versão aprovada vale para todas as concessões

- Bom, porque é simples.
- Ruim, porque amplia o acesso de todas as pessoas sem avaliar cada uma.

### B — Pedido novo por pessoa

- Bom, porque cada pessoa é avaliada.
- Ruim, porque cria uma fila de pedidos repetidos e deixa duas versões em uso sem prazo.

### C — Concessão presa à versão e migração em lote

- Bom, porque avalia cada concessão e encerra a versão anterior num prazo fixo.
- Ruim, porque falha fechada se ninguém agir no prazo.

## Mais informações

- **Alternativas descartadas:**
  - janela em horas úteis ou prorrogável;
  - manter o acesso reclassificado até a resposta da proteção de dados, porque falharia aberto;
  - escopo por permissão.
- Relacionadas:
  - [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md);
  - [ADR-0085](0085-cache-e-revogacao-diferenciados-por-sensibilidade.md): o cache futuro deve respeitar o prazo da janela e ser invalidado em toda mudança que reduz acesso;
  - [ADR-0086](0086-trilha-de-auditoria-com-hmac-e-cofre.md);
  - [ADR-0151](0151-catalogo-de-permissoes-em-codigo-por-modulo.md);
  - [ADR-0152](0152-governanca-das-concessoes-de-acesso.md);
  - [ADR-0153](0153-projecao-por-protecao.md).
- Emendada pela [ADR-0162](0162-separacao-das-atribuicoes-de-governanca.md): o aprovador pela proteção de dados passa a ser o Representante do controlador; a dispensa `beneficiario_ja_tem_protecao_de_dados` deixa de valer, também na migração em lote; os perfis de governança passam a três e admitem permissões de outros módulos reservadas à governança.
- Emendada pela [ADR-0158](0158-recurso-quem-recorre-ciclo-banca-e-decisoes.md): a designação da banca de análise de recursos é ato do processo, distinto da concessão de perfil.
