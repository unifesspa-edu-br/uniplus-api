---
status: "proposed"
date: "2026-10-28"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Encarregada de Proteção de Dados (DPO) — a confirmar"
informed:
  - "Equipe Uni+"
---

# ADR-0152: Governança das concessões de acesso com dupla aprovação para dado pessoal

## Contexto e enunciado do problema

O modelo de autorização decide por concessão efetiva ([ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md)). As concessões chegam por vínculo de grupo, que associa a um grupo um perfil, um conjunto de permissões e uma unidade ([ADR-0083](0083-grupos-oidc-governados-pela-aplicacao.md)), ou por concessão excepcional, temporária e com escopo ([ADR-0084](0084-concessao-excepcional-e-atuacao-institucional-server-side.md)). Cada permissão do catálogo declara a sua sensibilidade.

Nenhuma dessas decisões define **quem concede**. Hoje, na prática, o acesso inteiro é um único papel administrativo atribuído no provedor de identidade, sem pedido, sem aprovação e sem registro de quem decidiu. Com o primeiro processo seletivo real, servidores de unidades diferentes vão precisar de acesso a dados pessoais e sensíveis de candidatos (deficiência, saúde, autodeclarações, renda). O parecer da encarregada de dados de 13/10/2026 exige acesso restrito por padrão a esses dados.

O problema é definir quem pede, quem aprova e quem revoga uma concessão, de modo que o acesso a dado pessoal tenha justificativa funcional e de proteção de dados, sem transformar a encarregada, que é uma pessoa só, em gargalo da operação.

## Drivers da decisão

- **Segregação de funções:** quem fiscaliza o acesso não deve ser quem o concede no dia a dia, e ninguém aprova o próprio pedido.
- **Proteção de dados:** acesso a dado pessoal ou sensível exige finalidade, base legal e minimização conferidas por quem responde por elas (LGPD, art. 41).
- **Continuidade:** pedidos sem dado pessoal não podem depender da disponibilidade da encarregada.
- **Retirada de acesso nunca espera fila.**
- **Rastreabilidade:** todo pedido, aprovação, recusa e revogação fica na trilha de auditoria ([ADR-0086](0086-trilha-de-auditoria-com-hmac-e-cofre.md)).

## Opções consideradas

- **A**: Atribuição direta no provedor de identidade por quem administra a plataforma, sem pedido nem aprovação no Uni+.
- **B**: A encarregada de dados opera todas as concessões.
- **C**: Pedido e aprovação no Uni+, com aprovação do gestor de acessos para toda concessão e segunda aprovação da encarregada quando há dado pessoal ou sensível.

## Resultado da decisão

**Escolhida:** "C — pedido e aprovação no Uni+, com dupla aprovação para dado pessoal", porque separa a adequação funcional da adequação à proteção de dados, põe cada uma com quem responde por ela e limita a fila da encarregada ao que de fato envolve dado pessoal.

**Papéis.** O **gestor de acessos** mantém os perfis e aprova a adequação funcional: a pessoa precisa daquele acesso para o trabalho na unidade. A **encarregada de dados** aprova a adequação à proteção de dados (finalidade, base legal e minimização), faz a revisão periódica de quem tem acesso a dado pessoal ou sensível e consulta a trilha.

**Permissão é código, perfil é dado.** O catálogo em código ([ADR-0151](0151-catalogo-de-permissoes-em-codigo-por-modulo.md)) declara só permissões: o que se pode fazer, em qual módulo e sobre qual recurso, com código, nome, descrição e os requisitos da operação. Nenhum membro do catálogo representa perfil, cargo, papel ou unidade. O perfil é um conjunto nomeado de permissões, montado e mantido pelo gestor de acessos no Uni+, e muda sem nova versão do sistema. A mudança no perfil vale para todas as concessões dele; por isso, acrescentar a um perfil uma permissão de sensibilidade pessoal ou sensível passa também pela aprovação da encarregada, como um pedido.

**Fluxo.** Toda concessão começa por um pedido e passa pelo gestor de acessos. Quando o perfil ou a concessão inclui permissão de sensibilidade pessoal ou sensível, segue para a encarregada, sempre depois do gestor. A concessão só passa a valer na decisão de acesso depois da última aprovação exigida.

**Regras:**

- O pedido pode ser feito para si ou para outra pessoa, como a coordenação de uma unidade que pede acesso para um servidor da equipe. Ninguém aprova pedido em que é solicitante ou beneficiário, e o gestor que aprova não é a encarregada que aprova.
- A instituição mantém no mínimo dois gestores de acessos ativos. O sistema recusa remover ou desativar o penúltimo.
- Pedido em que a encarregada é a beneficiária passa só pela aprovação do gestor de acessos, porque a finalidade é a própria função dela. Pedido que ela faz para outra pessoa e que inclui permissão pessoal ou sensível espera a substituição formal dela nessa aprovação, porque ela não aprova o pedido que fez. O acesso a dados de candidatos para atender um caso (direitos do titular, incidente) é concessão com escopo e validade. A revisão periódica dos acessos dela fica com os gestores, e a trilha marca essas concessões como aprovadas sem segunda aprovação por serem da encarregada.
- Revogar não exige aprovação: o gestor de acessos ou a encarregada retiram um acesso a qualquer momento, com efeito imediato na decisão de acesso: enquanto não houver o cache da [ADR-0085](0085-cache-e-revogacao-diferenciados-por-sensibilidade.md), a decisão lê as concessões gravadas a cada requisição; quando houver, a marca de revogação versionada daquela ADR o invalida a cada revogação. O mínimo de dois gestores é conferido também na revogação.
- O dono de um recurso institucional, como o processo seletivo, é sempre uma unidade, nunca uma pessoa, porque a pessoa pode ser substituída. O escopo da concessão é a unidade, e quem atua sobre o recurso o faz por concessão naquela unidade, não por tê-lo criado.
- O escopo de uma concessão é uma unidade ou, para o cadastro de alcance institucional (campi, motivos, catálogos), a própria instituição. A concessão numa unidade não se estende às unidades abaixo dela na hierarquia ([ADR-0079](0079-hierarquia-institucional-sem-heranca-de-permissao.md)). A concessão excepcional da [ADR-0084](0084-concessao-excepcional-e-atuacao-institucional-server-side.md) mantém os escopos mais restritos dela (processo, chamada ou tipo de recurso).
- Concessão não se edita: para mudar o perfil ou o escopo, revoga-se a concessão e faz-se um pedido novo.
- O dono dos dados de uma inscrição é o próprio candidato, titular dos dados, que não precisa ter vínculo com unidade alguma, mesmo quando é servidor da Unifesspa. O acesso do titular aos próprios dados não depende de concessão; o acesso de um servidor aos dados de outra pessoa depende sempre de concessão aprovada por este fluxo. Ser servidor não dá acesso a dados de candidatos.

**O token identifica, não concede.** O token do provedor de identidade informa quem é a pessoa (identificador estável, CPF verificado, nome civil, nome social e e-mail institucional verificado) e com que força ela se autenticou. Ele não concede acesso: toda permissão é satisfeita por concessão gravada no Uni+ e aprovada por este fluxo, inclusive a de plataforma, cujo escopo é a instituição. Atribuir papel diretamente no provedor de identidade não abre acesso a nada. Os direitos do candidato vêm de ele ser titular dos próprios dados, e não de um papel no token: nenhum papel serve para distinguir candidato de servidor.

O CPF nunca trafega em URL. Nas operações do titular, ele vem do token; nas administrativas, no corpo da requisição. Toda resposta o mascara por padrão, e o CPF completo só é revelado ao titular ou, um registro por vez, a quem tem a permissão de revelá-lo, com registro na trilha.

Durante a transição, os papéis que o token já carrega continuam valendo até que cada módulo migre para concessões gravadas. Nenhum papel novo é criado no provedor de identidade para conceder acesso.

**Instalação.** A única exceção à regra acima é o papel de instalação, que habilita só a operação de instalar os gestores de acessos de um ambiente. A operação:

- é recusada sempre que há ao menos um gestor de acessos ativo; por isso, o papel não tem efeito na operação normal, e a mesma operação serve de recuperação se a instituição ficar sem gestores;
- instala dois gestores de uma vez, cumprindo o mínimo, e nunca quem a executa;
- exige o número do documento formal registrado no sistema de protocolo da instituição que designa os gestores, e o próprio documento em PDF, que fica guardado no armazenamento privado e cifrado do Uni+, com o resumo criptográfico registrado na trilha;
- é feita em duas etapas. Na primeira, quem executa informa os gestores, o documento e o PDF; o sistema valida tudo, guarda a instalação como pendente e envia um código para o e-mail de quem executou. Na segunda, a mesma pessoa informa o código em até duas horas, e só então a instalação passa a valer;
- aceita a confirmação só da mesma pessoa que iniciou a instalação. A tentativa de outra pessoa é recusada e registrada, não consome as tentativas de quem iniciou e não revela se há instalação pendente;
- envia o código só para o e-mail institucional verificado que vem do token, nunca para um e-mail informado na requisição;
- prende o código à instalação iniciada, com os mesmos gestores e o mesmo documento, aceita poucas tentativas e vale uma única vez. Iniciar de novo cancela a pendência anterior da mesma pessoa, e esgotar as tentativas ou o prazo obriga a recomeçar;
- registra na trilha quem executou, quando, o envio do código, as tentativas, a confirmação, os gestores instalados e o documento, sem gravar o código.

O papel de instalação é atribuído a poucas pessoas da equipe de infraestrutura. Todas as demais concessões, inclusive o papel da encarregada e os novos gestores de acessos, seguem o fluxo deste documento a partir da instalação. A concessão do papel de gestor de acessos exige o documento de designação anexado ao pedido; nas demais concessões, o anexo é opcional.

**Aprovação da concessão e aprovação da operação.** A dupla aprovação desta decisão aprova a **concessão**, antes de ela valer: a decisão de acesso só enxerga concessões já aprovadas. Ela não se confunde com a aprovação de uma **operação** em sessão delegada ([ADR-0084](0084-concessao-excepcional-e-atuacao-institucional-server-side.md)), que acontece a cada execução da operação.

## Consequências

### Positivas

- O acesso a dado pessoal passa a ter duas justificativas registradas, a funcional e a de proteção de dados.
- A encarregada recebe só os pedidos que envolvem dado pessoal, já validados do ponto de vista funcional.
- A trilha responde quem pediu, quem aprovou, quando e por quê, para toda concessão.

### Negativas

- Conceder acesso a dado pessoal fica mais lento: depende de duas pessoas.
- Com só dois gestores ativos, o pedido de um em favor do outro espera um terceiro gestor, porque nenhum dos dois pode aprová-lo.
- Enquanto não houver substituição formal da encarregada, a ausência dela paralisa os pedidos com dado pessoal ou sensível.
- O Uni+ precisa de fluxo de pedido e aprovação, filas por aprovador e telas de administração de acesso.

### Neutras

- O papel administrativo atual continua existindo como perfil, mas sua atribuição passa pelo mesmo fluxo.

## Confirmação

- Teste da regra de pessoas distintas: pedido aprovado pelo solicitante, pelo beneficiário ou pela mesma pessoa nas duas etapas é recusado.
- Teste da dupla aprovação: concessão com permissão pessoal ou sensível não vale na decisão de acesso com uma só aprovação, salvo o pedido em que a encarregada é a beneficiária.
- Teste do mínimo de gestores: desativar o penúltimo gestor ativo é recusado.
- Teste da revogação: concessão revogada deixa de valer na próxima decisão de acesso.
- Teste da fonte: no módulo já migrado para concessões gravadas, permissão é negada quando a única origem é um papel no token.
- Teste da instalação: a operação é recusada com um gestor ativo, sem documento ou quando quem executa está entre os instalados; depois da confirmação, grava dois gestores e o documento com o resumo na trilha.
- Teste da confirmação: a instalação só passa a valer com o código certo, informado pela mesma pessoa que a iniciou, dentro de duas horas; código de outra pessoa, expirado, já usado ou de uma instalação cancelada é recusado.

## Prós e contras das opções

### A — Atribuição direta no provedor de identidade

- Bom, porque não exige nada novo no Uni+.
- Ruim, porque não registra pedido nem justificativa, e a proteção de dados nunca é conferida.

### B — Encarregada opera todas as concessões

- Bom, porque toda concessão passa por quem responde pela proteção de dados.
- Ruim, porque quem fiscaliza passa a conceder, e uma pessoa só vira gargalo de toda a operação, inclusive do que não tem dado pessoal.

### C — Dupla aprovação para dado pessoal

- Bom, porque cada aprovação confere o que o aprovador sabe avaliar, e a fila da encarregada fica restrita ao necessário.
- Ruim, porque exige fluxo e telas próprios, e a ausência da encarregada precisa de substituição formal.

## Mais informações

- **Emenda** a [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md): o token deixa de ser fonte de concessão efetiva, salvo o papel de instalação e os papéis da transição até a migração de cada módulo.
- [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md) — ponto de decisão único e concessões efetivas.
- [ADR-0083](0083-grupos-oidc-governados-pela-aplicacao.md) — vínculo de grupo com perfil, permissões e unidade.
- [ADR-0084](0084-concessao-excepcional-e-atuacao-institucional-server-side.md) — concessão excepcional com escopo e validade.
- [ADR-0085](0085-cache-e-revogacao-diferenciados-por-sensibilidade.md) — revogação com efeito imediato.
- [ADR-0086](0086-trilha-de-auditoria-com-hmac-e-cofre.md) — trilha de auditoria.
- Parecer da encarregada de dados de 13/10/2026 sobre proteção dos dados da inscrição.
- **Pendente:** confirmação da encarregada sobre o papel e definição da substituição formal nas ausências dela.
- **Pendente:** prazo de guarda dos documentos de designação, conforme a tabela de temporalidade.
- **Pendente:** multifator. O modelo de autorização prevê que uma permissão exija multifator, mas a força da autenticação ainda não é lida do token, e o provedor de identidade não tem fluxo que obrigue o segundo fator. Até isso existir, nenhuma permissão pode exigir multifator, porque seria sempre recusada. Também falta verificar se a verificação em duas etapas do gov.br chega ao token do provedor institucional. A instalação não usa o multifator do provedor de identidade: o segundo fator dela é o código enviado ao e-mail institucional de quem executa.
- **Pendente:** recuperação quando os gestores ativos registrados não podem mais atuar (desligamento, perda da conta): a instalação só vale sem gestor ativo, e nenhum deles aprova o substituto.
- **Pendente:** o Uni+ ainda não envia e-mail. A confirmação da instalação depende dessa capacidade, que também atende às comunicações com o candidato.
