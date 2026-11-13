---
status: "proposed"
date: "2026-11-13"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0165: Instantes do sistema em microssegundos, relógio único e ordem de registro em coluna própria

## Contexto e enunciado do problema

O `DateTimeOffset` do .NET tem 7 casas decimais (100 ns) e o `timestamptz` do PostgreSQL guarda 6 (1 µs). O valor lido do relógio na memória e o valor relido do banco divergiam na sétima casa. Onde a regra compara um instante da requisição com um instante gravado (prazo, divulgação, expiração de chave de idempotência, expiração de cursor), a comparação decidia com um número que ninguém havia gravado. O mesmo vale para o instante que sai em mensagem: o JSON mantinha as 7 casas, o Avro declarava milissegundos e descartava a fração que o banco guarda, e uma mensagem sem o instante era lida como `DateTimeOffset.MinValue` e processada.

Com instantes em microssegundos, dois fatos do mesmo escopo podem ter o mesmo instante. O `Id` não os ordena: o Guid v7 ([ADR-0032](0032-guid-v7-para-identidade-de-entidades.md)) é aleatório depois do milissegundo e, por isso, "o último" entre dois fatos empatados saía em ordem arbitrária. Havia consultas de negócio nessa situação: o ato divulgado do processo, o lote de notas do ENEM vigente, a janela de migração e as decisões do pedido, da migração e da proteção do catálogo; `PedidoDeConcessao.UltimaAprovacaoDoGestor` usava `MaxBy`, que devolve a primeira entre as empatadas.

A [ADR-0068](0068-relogio-via-timeprovider-injetado.md) tornou o relógio injetado e obrigatório, mas deixou três pontos abertos: cada extensão de registro podia acrescentar o próprio `TimeProvider.System` com `TryAdd`, nada garantia que os três processos (Host, Portal e Verificador) usassem a mesma fonte, e a regra de arquitetura lia o código-fonte como texto e deixava passar a leitura do relógio estático dentro de lambda, função local, inicializador e construtor estático.

## Drivers da decisão

- **O instante comparado é o instante gravado.** Nenhuma decisão de prazo ou de ordem lê um valor que o banco não guarda.
- **Uma só fonte de tempo por processo**, testável e verificável.
- **Ordem de registro é fato do banco, não do relógio.** Fatos do mesmo instante têm ordem definida, gravada junto do fato.
- **Fronteira estrita.** Instante que entra por HTTP ou por mensagem é normalizado na entrada; instante ausente é recusado, nunca assumido.
- **Regra de arquitetura que enxerga o código**, não o texto.

## Opções consideradas

- **A**: Relógio decorador que trunca em microssegundos, registrado uma vez por processo; truncamento nas fronteiras e onde se soma duração; coluna de identidade `OrdemDeRegistro` nos fatos que decidem prazo ou ordem; regra de arquitetura por chamada de membro.
- **B**: Truncar por convenção do EF Core, na gravação, e deixar a memória com 7 casas.
- **C**: Aumentar a precisão do banco para 7 casas (`timestamp` do PostgreSQL não tem).
- **D**: Manter o `Id` como desempate universal e ordenar por ele.

## Resultado da decisão

**Escolhida:** "A", porque é a única que alinha a memória, o banco, a mensagem e a resposta no mesmo valor e dá ordem real aos fatos do mesmo instante.

**Relógio único.** `RelogioEmMicrossegundos` (Kernel) decora um `TimeProvider` e trunca só `GetUtcNow()`; timestamp, frequência, fuso e timers passam à fonte. `RelogioEmMicrossegundos.DoSistema` é o único uso de `TimeProvider.System` em produção, e cada processo o registra com `Replace` na primeira linha após o builder (Host, Portal e Verificador). As extensões de registro não acrescentam relógio: dependem do que o host registrou. O cliente do Schema Registry, criado antes do contêiner, recebe o relógio por parâmetro.

**Fronteiras.** O instante do corpo JSON é truncado por conversor, na leitura e na escrita; o de query, rota e cabeçalho, por um binder que delega ao padrão e trunca só o valor válido; o das mensagens JSON, pelas opções de serialização compartilhadas, que também tornam obrigatório todo `DateTimeOffset` não anulável que o serializador preenche, isto é, com `set` ou `init`, o que inclui o parâmetro de um `record` posicional (a mensagem sem ele vai para a carta morta). A propriedade só de leitura preenchida pelo construtor fica fora: marcá-la como obrigatória faz o serializador recusar o tipo inteiro, e os contratos de mensagem em uso são `record` posicionais. O schema Avro do processo publicado passa a `timestamp-micros`. O transformer do OpenAPI publica todo instante como `string`/`date-time`, e nenhum contrato publicado muda por causa do conversor. O instante derivado de soma com duração configurável ou parametrizada é truncado no ponto da soma; durações constantes em unidades inteiras já são múltiplas de 10 ticks e ficam como estão.

**Ordem de registro.** Os fatos que decidem prazo ou ordem ganham `OrdemDeRegistro`, coluna de identidade (`UseIdentityAlwaysColumn`, índice único), como já faz `ImplantacaoDasPermissoes.Sequencia`: atos do processo, lotes de notas do ENEM, janelas e decisões da migração, decisões do pedido, atos e manifestações da proteção do catálogo. A coluna é do banco, não entra em DTO nem em contrato, e o número reflete a ordem real porque cada escopo já é serializado (trava do processo, controle de concorrência do pedido e do item, índice único parcial da janela, `FOR UPDATE` nas concessões); quem perde o conflito refaz e recebe número maior. O buraco por rollback é irrelevante, pois a coluna nunca é exibida. A entidade ainda não gravada tem ordem zero e conta como a mais recente do seu instante.

**Onde o `Id` continua.** Nas consultas que ordenam só por instante e não decidem nada pela ordem, o `Id` fica como desempate de determinismo, e não como ordem de registro: arquivos verificados da inscrição, fila de versões da etapa e a fila de providências, paginada por cursor keyset `(aberta_em, id)` ([ADR-0089](0089-navegacao-bidirecional-cursor-keyset-reverso.md)). A providência de uma concessão tem no máximo uma aberta por vez (índice único parcial), então não há duas providências concorrentes cuja ordem relativa importe.

**Conferência por chamada de membro.** A regra de arquitetura (ArchUnitNET) reprova, em todo assembly de produto, a leitura de `DateTime`/`DateTimeOffset` `UtcNow`, `Now` e `Today`, o uso de `TimeProvider.System` fora de `RelogioEmMicrossegundos` e o de `RelogioEmMicrossegundos.DoSistema` fora do `Program`. Ela é provada contra fixtures de cada forma violadora (método comum, lambda, closure, função local, `async`, iterador, construtor estático, inicializadores de campo e de propriedade). Limitação: o ArchUnitNET não enxerga as instruções de nível superior do `Program`; o que o `Program` chama por conta própria fica fora da conferência, e ele entra apenas como a exceção que registra o relógio.

### Esta ADR não decide

- **Instantes de token OAuth em memória** (Schema Registry, SIGAA e Geo): nunca são gravados nem publicados, e continuam com a fonte injetada sem truncamento próprio.
- **Janelas do envelope congelado**, que têm granularidade canônica de segundos ([ADR-0100](0100-canonicalizacao-hash-snapshot-publicacao.md)).

## Consequências

### Positivas

- O instante em memória, no banco, na mensagem e na resposta é o mesmo valor.
- Fatos empatados no mesmo microssegundo têm ordem definida e testada em transação real; a última aprovação do gestor deixa de depender da ordem em que o banco devolve linhas empatadas.
- Mensagem sem instante não é mais processada com o primeiro dia do calendário.
- A regra de arquitetura passa a pegar o relógio estático onde a leitura textual não pegava.

### Negativas

- Sete tabelas ganham uma coluna e um índice único, e o índice da vigência dos lotes do ENEM passa a terminar nela.
- O `[JsonRequired]` do instante do envio simulado muda o comportamento em runtime (ausência vira 400) e a baseline OpenAPI da Configuração em `envio.instante`.
- A troca do tipo lógico do Avro pode exigir remover o subject no Apicurio de HML na implantação.
- O `Program` fica fora da conferência de arquitetura; a revisão cobre o que ele chama.

### Neutras

- `CreatedAt`, `UpdatedAt` e `DeletedAt` passam a vir do relógio único e saem em microssegundos, e não em 7 casas.

## Confirmação

- Teste de arquitetura `RelogioDoSistemaTests`, com fixtures violadoras e conformes, sobre os assemblies de produto, inclusive o Verificador.
- Testes de integração de ida e volta no PostgreSQL (memória igual ao relido) para a auditoria, a chave de idempotência e o agendamento de republicação, e de unicidade do relógio registrado nos três processos.
- Testes de integração da ordem dos registros por escopo (atos do processo, lotes do ENEM, decisões do pedido e da migração, janelas, atos e manifestações da proteção), gravando primeiro o de maior `Id` para falhar de forma determinística, e testes unitários da última aprovação do gestor, das decisões do ciclo e da fila de versões.
- Teste de integração da mensagem sem instante na fila durável (carta morta, com a mensagem seguinte processada) e do Avro em `timestamp-micros`.

## Prós e contras das opções

### A — Relógio truncado, fronteiras normalizadas e coluna de ordem

- Bom, porque a divergência some na origem e a ordem do empate é um fato gravado.
- Ruim, porque acrescenta coluna nas tabelas de fatos e exige disciplina nas fronteiras novas.

### B — Truncar na gravação pelo EF Core

- Bom, porque não toca o relógio.
- Ruim, porque a entidade em memória, o `jsonb` e a mensagem continuam com 7 casas, e a comparação feita antes da gravação continua divergindo.

### C — Precisão de 7 casas no banco

- Bom, porque eliminaria a divergência sem truncar.
- Ruim, porque o PostgreSQL não oferece essa precisão.

### D — `Id` como desempate universal

- Bom, porque não acrescenta coluna.
- Ruim, porque o Guid v7 é aleatório abaixo do milissegundo: a ordem do empate seria arbitrária, o que é pior do que não ter desempate quando a regra depende de "o último".

## Mais informações

- **Emenda** a [ADR-0068](0068-relogio-via-timeprovider-injetado.md): o relógio de produção é único e truncado em microssegundos, as extensões de registro não registram relógio e a conferência é por chamada de membro, não por texto.
- **Emenda** a [ADR-0032](0032-guid-v7-para-identidade-de-entidades.md): o `Id` não ordena fatos do mesmo microssegundo; a ordem de registro vive em coluna própria.
- [ADR-0089](0089-navegacao-bidirecional-cursor-keyset-reverso.md) e [ADR-0164](0164-tramitacao-da-providencia-sobre-concessao.md): a fila de providências mantém o `Id` no desempate do cursor.
- [ADR-0160](0160-supressao-de-aviso-do-analisador-proibida.md): as supressões do registro Avro foram removidas no mesmo trabalho.
- Issue unifesspa-edu-br/uniplus-api#2380.
