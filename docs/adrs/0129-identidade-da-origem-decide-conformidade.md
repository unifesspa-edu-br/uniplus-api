---
status: "accepted"
date: "2026-09-01"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0129: Identidade da origem decide conformidade; o código é o que se mostra ao humano

## Contexto e enunciado do problema

O [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) estabelece que a referência cross-módulo é feita por snapshot-copy: a entidade consumidora congela os dados do catálogo e guarda `{Catalogo}OrigemId` — que a regra 2 daquele pattern qualifica como **rastreabilidade**, e como **opcional** (`Guid?`). Os usos que ele lista para o campo são UX de admin ("este `LocalProva` veio do `Endereco` X — abre o original para comparar") e relatório de auditoria.

Restou implícito qual dos dois campos congelados — o código legível ou a identidade opaca — decide quando o sistema precisa concluir que **isto é aquilo**. Na prática cada ponto de decisão escolheu por conta própria, e todos escolheram o código.

O caso que expôs o custo foi a avaliação de conformidade legal. Uma `ObrigatoriedadeLegal` exige o documento `LAUDO_MEDICO` para a modalidade `PCD`; a exigência congelada no processo seletivo guarda o mesmo código. Comparar as duas strings parece bastar, e não basta: o código do cadastro é editável, e o índice único que o protege é parcial (`WHERE is_deleted = false`), de modo que o slot é liberado por remoção. Comparar código é comparar duas fotografias de instantes diferentes de algo que muda.

Os dois erros são simétricos, e o silencioso é o que pesa:

- **Falso positivo.** O código é reciclado por outro documento. O gate de publicação aprova o edital como legalmente conforme, satisfeito pelo documento errado, e nada sinaliza.
- **Falso negativo.** Alguém renomeia o tipo e atualiza a regra — faz exatamente o certo. O código novo da regra não casa com o código velho congelado na exigência, e a publicação legítima é recusada com uma mensagem que manda procurar uma exigência que está na tela.

O ADR-0061 não foi ingênuo quanto a isso. Ao justificar por que a referência a `AreaOrganizacional` pode ser feita por código, ele escreve: *"é por código (não por id) e `AreaCodigo` é **imutável post-criação** per Invariante 2 do [ADR-0055](0055-organizacao-institucional-bounded-context.md). Mesma disciplina por mecanismo diferente."* Comparar por código é seguro **quando o código é imutável**. Quando os cadastros de Configuração adotaram código editável, a premissa caiu — e o registro não foi revisitado.

Há ainda uma divergência já consumada entre o registro e o código. O ADR-0061 especifica `Guid?` opcional; `DocumentoExigido.TipoDocumentoOrigemId`, `OfertaTipoDeficiencia.TipoDeficienciaOrigemId`, `ModalidadeSelecionada.ModalidadeOrigemId` e `TipoEtapaSnapshot.OrigemId` são `Guid` obrigatório, e três deles recusam explicitamente o `Guid` vazio na factory. O código já trata a origem como identidade.

## Drivers da decisão

- **Um edital publicado sob obrigação satisfeita pelo documento errado não é defensável** — e o defeito é silencioso, que é a pior combinação.
- **Recusar publicação legítima também custa**: bloqueia quem seguiu o procedimento correto, com mensagem que aponta para o lugar errado.
- **A correção não pode ser ponto a ponto.** Corrigir cinco pontos sem registrar o princípio deixa o sexto nascer errado, porque o documento que o desenvolvedor lê continua dizendo que a origem é rastreabilidade.
- **O código precisa continuar legível para o humano** — na mensagem de erro, na tela e no envelope publicado. Trocar código por `Guid` na comunicação seria regressão de usabilidade e de auditabilidade.
- **Normalização não resolve isto.** `TipoEtapaSnapshot.Criar` já aplica NFC no congelamento, citando `AvaliadorConformidadeLegal` pelo nome; aquilo trata divergência de **grafia** do mesmo código, não troca de código.

## Opções consideradas

- **A**: Manter o registro como está e corrigir cada ponto de decisão à medida que o defeito aparecer.
- **B**: Tornar o código dos cadastros imutável pós-criação, estendendo a disciplina do [ADR-0055](0055-organizacao-institucional-bounded-context.md).
- **C**: Promover `{Catalogo}OrigemId` a chave de decisão obrigatória, mantendo o código como representação legível (escolhida).

## Resultado da decisão

**Escolhida:** "C — a identidade da origem decide; o código é o que se mostra ao humano", porque elimina a classe inteira de defeito sem retirar do operador a capacidade de corrigir e renomear um código, e porque alinha o registro à prática que as entidades já adotaram.

Em uma frase: **decisão de correspondência compara identidade; texto dirigido a pessoa cita o código.**

### Regras

1. Quando o sistema precisa decidir que uma referência congelada e uma referência viva designam **o mesmo item de catálogo**, a comparação é por `{Catalogo}OrigemId`. Comparação por código não decide.
2. `{Catalogo}OrigemId` é **obrigatório e não-anulável** em toda entidade cujo snapshot participe de decisão. A factory recusa o `Guid` vazio — a entidade não aceita uma identidade que não identifica.
3. O código congelado continua sendo persistido, exibido e publicado no envelope. É o que aparece em mensagem de erro, tela e documento oficial.
4. Quando o código casa e a identidade não, a recusa tem **motivo próprio**, que nomeia o que aconteceu: o código foi reatribuído depois que a regra foi escrita. Dizer "não encontrei" seria mandar procurar algo que está visível.
5. A resolução de código para identidade é feita pela camada de aplicação e entregue ao domínio como dado. O serviço de domínio não ganha leitor ([ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md)).
6. Quando gate de escrita e consulta de leitura precisam do mesmo veredicto, ambos consomem **a mesma resolução**, não duas leituras independentes do catálogo — duas leituras podem divergir, e o invariante é que a regra mostrada como reprovada seja a que bloqueia a transição.

### Emenda ao ADR-0061

Esta decisão emenda o pattern de snapshot-copy em dois pontos:

- **Regra 2** — `{Catalogo}OrigemId` deixa de ser "opcional, para rastreabilidade" e passa a ser **obrigatório** nas entidades cujo snapshot participe de decisão. Continua sem FK no banco; o que muda é o papel, não o mecanismo.
- **Regra 5** — o snapshot permanece imutável, e fica explícito que essa imutabilidade é exatamente o motivo de a identidade ser necessária: o código congelado envelhece por construção, e envelhecer é o comportamento desejado do snapshot, não um defeito a corrigir relendo o catálogo.

O restante do ADR-0061 segue valendo integralmente, inclusive a ausência de FK cross-banco e a referência por `AreaCodigo` — que continua legítima porque ali o código é imutável por invariante.

## Consequências

### Positivas

- A reciclagem de código passa a **reprovar** a publicação, com motivo que diz o que houve.
- A renomeação legítima, com a regra atualizada, passa a **aprovar** — o falso negativo desaparece junto com o falso positivo, porque ambos vinham da mesma causa.
- O desenvolvedor que for escrever o próximo ponto de decisão encontra o princípio registrado, em vez de repetir a escolha que parece natural.
- O registro passa a descrever o código que existe: `OrigemId` obrigatório e recusa do `Guid` vazio já eram a prática.

### Negativas

- A camada de aplicação passa a resolver e transportar mapas de identidade que antes não precisava montar, aumentando o insumo que o domínio recebe.
- Entidades de snapshot que hoje não guardam origem — se surgirem — não poderão participar de decisão sem antes ganhar o campo, o que é trabalho de migração.
- O princípio não vale para toda referência: onde o item de catálogo não tem identidade estável exposta, a comparação por código continua sendo o que há. É o caso do critério de desempate, cujo `ReferenciaRegra` guarda código, versão e hash, sem `OrigemId` — aceitável porque o catálogo de regras é seed-governado e append-only, e sem edição não há reciclagem.

### Neutras

- Nada muda no banco: `OrigemId` já é coluna existente e não-anulável nas entidades citadas, e continua sem FK.
- O envelope publicado não muda de forma: já carrega a origem entre as chaves obrigatórias, recusada quando é o `Guid` vazio.

## Confirmação

A verificação é por **tipagem**, não por inspeção de texto.

O serviço de domínio que decide conformidade recebe a identidade como parâmetro obrigatório e **não expõe sobrecarga** que aceite apenas o código. Um chamador que "esqueça" a identidade não compila — o compilador é o gate. Foi assim que a #1372 introduziu o pattern, e é a forma que as extensões devem seguir.

Deliberadamente **não** se propõe scanner de fonte no CI procurando comparação de string. "Comparar código numa decisão" não é detectável por reflexão sobre assembly, e um analisador de texto sobre o fonte produziria ruído sem cobrir os casos que importam. Onde a tipagem não alcançar, a revisão de PR alcança — e o princípio, registrado aqui, é o que ela cita.

Os testes que acompanham cada ponto de decisão vêm em par, e ambos falham sem a correção: renomeação com a regra atualizada **aprova**; reciclagem de código **reprova**.

## Prós e contras das opções

### A — manter o registro e corrigir caso a caso

- Bom, porque não exige decisão nova nem revisitar documento aceito.
- Ruim, porque o desenvolvedor seguinte lê que a origem é rastreabilidade e escolhe o código de novo — o defeito volta em ponto novo, e cada retorno custa um ciclo de descoberta.
- Ruim, porque a descoberta costuma vir do defeito em produção, e aqui o defeito é silencioso.

### B — tornar o código imutável pós-criação

- Bom, porque elimina a classe de defeito na raiz: sem edição não há reciclagem, e comparar código volta a ser correto.
- Bom, porque é disciplina já praticada no projeto, em `AreaCodigo` ([ADR-0055](0055-organizacao-institucional-bounded-context.md)).
- Ruim, porque o custo recai sobre o operador, que erra um código ao cadastrar e passa a precisar recriar o item e migrar o que já o referenciava.
- Ruim, porque reverteria decisão recente de produto — o código semântico editável dos cadastros institucionais é entrega da própria frente que descobriu este problema.

### C — identidade decide, código comunica (escolhida)

- Bom, porque corrige os dois erros simétricos de uma vez, com a mesma mudança.
- Bom, porque preserva a UX de renomear e corrigir código.
- Bom, porque alinha o registro ao que as entidades já fazem.
- Ruim, porque exige que a aplicação monte e transporte a resolução até o domínio, em vez de o domínio comparar dois campos que já tem em mãos.

## Mais informações

- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — pattern de snapshot-copy, emendado nas regras 2 e 5 por esta decisão.
- [ADR-0055](0055-organizacao-institucional-bounded-context.md) — código imutável por invariante, mecanismo alternativo (opção B).
- [ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md) — por que a resolução chega ao domínio como dado, e não como leitor.
- `unifesspa-edu-br/uniplus-api#1372` — a correção que originou o princípio, no predicado de documento por modalidade.
- `unifesspa-edu-br/uniplus-api#1375` — extensão aos demais predicados.
- `unifesspa-edu-br/uniplus-api#1335` — epic de integridade do código de cadastro.
