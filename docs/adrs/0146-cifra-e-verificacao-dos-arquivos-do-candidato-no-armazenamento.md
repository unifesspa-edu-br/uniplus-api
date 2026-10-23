---
status: "proposed"
date: "2026-10-22"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Uni+"
  - "Infraestrutura"
---

# ADR-0146: O arquivo do candidato é cifrado pelo armazenamento e verificado fora da API

## Contexto e enunciado do problema

Na confirmação do envio de um arquivo do candidato (issue #2036), a API lê o objeto inteiro do MinIO, passa o conteúdo ao clamd, cifra com a chave de dado sensível da Seleção e grava uma cópia selada (ADR-0121, emenda 2). Assim, a API tem em memória o conteúdo de todo arquivo enviado e concentra o acesso a dado sensível, como laudos e documentos de PcD. Em 21/10/2026, o líder técnico definiu outra direção: a cifra fica no armazenamento, com chave por objeto, e a verificação fica com um processo que lê direto do MinIO (issue #2044).

A POC da issue #2045 validou essa direção na versão do MinIO do projeto, com o KES e o Vault de HML (1.21.2), e comparou três formas de acionar o verificador: webhook do bucket, RabbitMQ e a fila do Wolverine no Postgres. Os resultados que sustentam a decisão estão abaixo.

## Drivers da decisão

- A API não conhece o conteúdo dos arquivos: nem memória proporcional ao arquivo, nem acesso a dado sensível.
- Cada arquivo tem cifra individual, como pede o parecer da encarregada de dados de 13/10/2026.
- O candidato abre e baixa os próprios arquivos sem a API intermediar o conteúdo.
- Falha fechada: sem a chave, o armazenamento não entrega nem grava conteúdo.
- Nenhum arquivo confirmado fica sem veredicto, e nenhum é recusado só porque a verificação estava fora do ar.
- Não há produção: os arquivos selados em HML podem ser apagados e reenviados.

## Opções consideradas

- **A.** Cifra e verificação na API (o fluxo atual).
- **B.** Verificador dedicado que também cifra com chave da aplicação.
- **C.** Cifra no MinIO (SSE-KMS, chave do KES guardada no Vault) e verificador dedicado, acionado por fila.

## Resultado da decisão

**Escolhida:** "C", porque a POC provou a cifra por objeto e a falha fechada no armazenamento, e porque a API deixa de tocar o conteúdo sem perder a leitura do arquivo em claro pelo candidato.

**Armazenamento.** São dois buckets privados, ambos com SSE-KMS como cifra padrão, e o volume é cifrado pela infraestrutura:

- o de envio recebe o PUT pré-assinado e funciona como quarentena; o que ninguém verificou expira;
- o definitivo só recebe o que o verificador promoveu.

A chave `uniplus-arquivos-candidato` vive no KES, que a guarda no KV do Vault cifrada por uma chave Transit. Cada objeto recebe uma chave de dados própria, selada por essa chave. Nenhuma requisição leva cabeçalho de SSE: a URL pré-assinada de envio continua como é hoje.

**Acionamento.** Quando o candidato confirma o envio, a API grava o arquivo como `EM_VERIFICACAO` e publica o pedido de verificação pelo outbox do Wolverine (ADR-0004), na mesma transação. Não existe arquivo em verificação sem pedido, nem pedido sem arquivo. O pedido traz a chave do objeto e o ETag que o PUT devolveu ao candidato, que o envia na confirmação (o CORS do bucket de envio expõe o `ETag`). A confirmação fixa assim a versão a verificar.

**Transporte.** RabbitMQ, com quorum queues: uma fila de pedidos e uma de resultados. O verificador consome um pedido por vez em cada slot de paralelismo e só confirma a mensagem depois de publicar o veredicto; se ele cair no meio, a mensagem volta à fila. O RabbitMQ entra aqui como fila de trabalho. A ADR-0014 o recusou como barramento de eventos entre módulos, por não ter replay, o que não pesa neste uso. A POC provou:

- o verificador morto no meio de 40 verificações não perde nenhuma;
- com o RabbitMQ fora do ar, a confirmação continua, e o pedido espera no outbox até ele voltar;
- do fim do PUT ao veredicto, um arquivo de 5 MB leva 0,85 s (p50), contra 3,1 s pelo webhook com fila em disco.

O RabbitMQ foi escolhido pelo isolamento, e não pelo desempenho, que foi equivalente ao da fila no Postgres (abaixo). Com ele, o verificador, que manipula o conteúdo sensível, não tem credencial nem conexão no banco da API. A entrega também não depende de ajuste de polling.

**Reconciliação.** Uma rotina periódica da API, numa instância só e em lotes, republica o que está em verificação sem pedido publicado há mais tempo que o esperado. O prazo conta da última publicação, e não da confirmação, para não republicar a cada execução o pedido que ainda está nas novas tentativas. Ela cobre o pedido perdido, o que foi para a fila de erro e o clamd que voltou. O verificador é idempotente: a reentrega devolve o mesmo veredicto, e a API só registra o primeiro.

**Verificador.** É um processo próprio, fora da API. Ele:

1. consulta o bucket definitivo: se a chave já foi promovida (reentrega), devolve o veredicto gravado, sem promover de novo;
2. recusa sem ler o arquivo acima do tamanho máximo, de 20 MB (UNI-REQ-0027), porque a URL pré-assinada de PUT não limita o tamanho;
3. lê em stream a versão confirmada, exigindo o ETag do pedido, já decifrada pelo MinIO;
4. confere se o formato corresponde ao declarado;
5. passa o conteúdo ao clamd (`INSTREAM`) e calcula o SHA-256 no caminho.

O arquivo limpo é promovido por cópia no próprio servidor, com o mesmo ETag, e o objeto definitivo guarda no metadado o SHA-256 e o ETag do envio: o que foi confirmado é exatamente o que é verificado e promovido, e o promovido nunca é sobrescrito. Se a versão confirmada não estiver mais no bucket de envio, porque outro PUT a substituiu, o arquivo termina em reenvio necessário; substituir o arquivo é um novo envio, com chave nova. O arquivo infectado ou fora do formato não é promovido e fica na quarentena, que só o verificador lê, até expirar; assim a reentrega o verifica de novo e devolve o mesmo veredicto. O resultado vai à API pela fila de resultados, com o veredicto, o SHA-256, o tamanho e o formato, nunca o conteúdo nem o nome da ameaça.

Com o clamd fora do ar, o verificador tenta de novo três vezes, espaçadas, e manda o pedido para a fila de erro. O arquivo segue em verificação até a reconciliação.

A aprovação por etiqueta no próprio bucket de envio, sem cópia, foi recusada. O candidato consegue mandar a etiqueta de aprovado no PUT pela URL pré-assinada e se aprovar sem verificação.

**Estado do arquivo.** O arquivo passa por `EM_VERIFICACAO` e termina em `VERIFICADO`, `RECUSADO` ou `REENVIO_NECESSARIO`. O último vale quando a versão confirmada não está na quarentena nem no bucket definitivo: o candidato confirmou sem enviar, regravou o arquivo depois de confirmar, ou o envio expirou antes de ser verificado. Ele não é recusa da verificação, encerra a reconciliação e pede um novo envio. Nenhum arquivo é recusado por indisponibilidade, e a expiração da quarentena precisa ser maior que a indisponibilidade tolerada da verificação. A mesma expiração encerra a falha que não passa com novas tentativas: sem a versão confirmada na quarentena, o arquivo termina em reenvio necessário.

**Leitura pelo candidato.** A API assina uma URL de GET curta no bucket definitivo, com `Content-Disposition` só `inline` ou `attachment` e o nome do arquivo codificado (`filename*`, RFC 5987), para que o nome não injete parâmetros no cabeçalho. O MinIO entrega o conteúdo em claro e aceita `Range`. Adulterar a URL a invalida.

**Acesso e auditoria.**

| Quem | Permissões no MinIO |
|---|---|
| API | Assina `PutObject` no bucket de envio e `GetObject` no definitivo. Não tem leitura no bucket de envio. |
| Verificador | Lê e remove no bucket de envio. Grava e lê no definitivo; a leitura serve para reconhecer, na reentrega, o arquivo já promovido. `ListBucket` nos dois, que o SDK exige. |
| Identidade do MinIO no KES | Só `generate` e `decrypt` da chave dos arquivos. |
| Pessoas | Nenhuma tem leitura nos buckets. |

O verificador não acessa o banco da API.

O log de auditoria do MinIO (audit webhook) vai ao coletor de logs e registra cada acesso: objeto, operação, credencial e origem. O GET por URL pré-assinada aparece com a credencial de quem assinou, que é a API. Por isso, a API registra cada emissão de URL de leitura (quem pediu, qual arquivo, quando), e os dois registros se cruzam pelo objeto e pelo horário. Nesta versão, o KES não emitiu auditoria própria.

**Emenda da ADR-0121.** A emenda 2 deixa de valer para o arquivo do candidato: o conteúdo não é cifrado pela aplicação, e a cifra individual do parecer é a chave de dados por objeto do SSE-KMS. A cifra de campo da ADR-0121 continua como está.

**Cifra individual.** O parecer da encarregada de dados de 13/10/2026 pede cifra individual. A chave de dados por objeto atende a esse pedido: o mesmo conteúdo não fica igual no bucket, e não há acesso ao conteúdo sem a chave. A encarregada aprovou a solução (repasse do líder técnico, 22/10/2026).

### Transportes descartados

- **Webhook do MinIO com fila em disco.** Garante a entrega ao verificador, mas não até o veredicto: o verificador aceita o evento numa fila em memória e, se cair antes do veredicto, o arquivo fica sem ele. Acrescenta ~2 s de espera.
- **Fila do Wolverine no Postgres** (a da ADR-0044). Passou nas mesmas provas de robustez e, com polling de 100 ms, teve latência e vazão equivalentes às do RabbitMQ. Ficou em segundo lugar por três motivos:
  - o verificador precisaria de credencial e conexões no banco da API;
  - a latência depende do polling: com o padrão do Wolverine, um arquivo esperou ~9,5 s;
  - cada réplica soma dois pools de conexão (o do EF e o do Wolverine) ao `max_connections` do banco, o que limita o número de réplicas.

  Sem componente novo, ela continua sendo a alternativa, se o RabbitMQ não puder entrar na infraestrutura.
- **Kafka** (ADR-0014). Funcionaria, mas, com consumer groups (o modelo do transporte Kafka do Wolverine), o paralelismo fica preso ao número de partições, e uma mensagem que falha repetidamente bloqueia a partição ou precisa de tópico de erro próprio. A fila de trabalho do RabbitMQ entrega um pedido por vez a quem estiver livre e tem fila de erro nativa.

### Medições e dimensionamento

Tudo rodou numa única máquina (AMD Ryzen 9 9950X, NVMe, Debian 13), com MinIO de um nó e um disco. O verificador e a API rodaram com os limites do pod de HML: 2 CPUs e 1 GiB. O conteúdo de cada arquivo era aleatório e único, porque o clamd guarda em cache o veredicto do conteúdo já visto.

Comparação com o fluxo atual (clamd sem limite de CPU, verificador por webhook):

| | Fluxo novo | Fluxo atual |
|---|---|---|
| Verificação de 1 / 5 / 10 MB, um por vez (p50) | 74 / 212 / 444 ms | 59 / 196 / 362 ms (confirmação síncrona) |
| Pico de memória da API | constante (92–98 MiB só assinando; 300–380 MiB com Wolverine e outbox) | 104 MiB (1 envio de 1 MB) a 860 MiB (64 de 10 MB) |
| 64 envios simultâneos de 10 MB | 64 de 64 verificados | 15 de 64 falharam com `OutOfMemoryException` |

Fluxo com fila, cada clamd com 2 CPUs (o tamanho de um sidecar), arquivos de 5 MB:

| | RabbitMQ | Postgres (polling de 100 ms) |
|---|---|---|
| Do fim do PUT ao veredicto, um por vez (p50) | 0,85 s | 0,89 s |
| Vazão com 1 / 2 / 3 réplicas, 64 simultâneos | 11,9 / 21,0 / 28,3 arquivos/s | 11,9 / 20,4 / 26,5 arquivos/s |
| Falhas | nenhuma | nenhuma |
| Pico de memória do verificador | 312–349 MiB, constante | 324–343 MiB, constante |

A vazão é limitada pela CPU do clamd: em toda carga, cada clamd ficou perto do teto das suas 2 CPUs, enquanto o verificador usou 0,1 a 0,7 CPU e a API menos de 1. Com mais paralelismo, cada verificação demora mais, e o total não sobe. A promoção por cópia não pesa: sem cópia, a vazão foi a mesma (11,9 arquivos/s). A escala é horizontal, uma réplica do verificador por clamd, guiada pelo tamanho da fila de pedidos.

Uma réplica (verificador + clamd com 2 CPUs) verifica ~12 arquivos de 5 MB por segundo, ou seja, ~40 mil por hora; três verificam ~28 por segundo. O dimensionamento para a véspera do encerramento é:

```text
réplicas (verificador + clamd) = arquivos na hora de pico ÷ 40 mil
```

A conferência do formato (passo 4) não entrou nas medições.

Para o primeiro processo, o de Medicina, a expectativa é de 10 mil inscrições em 20 dias. Mesmo supondo 5 arquivos por inscrição e 30% das inscrições no último dia, com 20% delas numa única hora, o pico fica em torno de 3 mil arquivos por hora, menos de um décimo de uma réplica. O verificador sobe com duas réplicas: uma basta para a vazão, e a segunda mantém a verificação no ar se um pod cair perto do encerramento das inscrições.

### Plano de migração

Não há produção. Os arquivos selados em HML são apagados e reenviados, sem conversão.

**Sai da API:**

- `IProtecaoDoConteudoSelado` (`VerificarAsync`, `SelarAsync`, `AbrirAsync`) e o `ClamdClient`;
- no arquivo do candidato, a leitura do conteúdo em `ConfirmacaoDoArquivoEnviado` e `LeituraDoArquivoEnviado`, a verificação de formato e o cálculo do SHA-256, que passam ao verificador;
- a chave selada e a sua limpeza.

**Entra na API:** a confirmação registra o arquivo em verificação e publica o pedido pelo outbox; um handler consome o resultado; a reconciliação periódica.

**Entra na infraestrutura:**

- o KES, com autenticação no Vault por Kubernetes;
- a chave Transit e o caminho do KV, com a política do KES;
- o SSE-KMS e as políticas dos dois buckets, e o CORS do bucket de envio expondo o `ETag`;
- o RabbitMQ em cluster de três nós;
- o verificador com o clamd como sidecar, com duas réplicas no mínimo, escalado pelo tamanho da fila;
- o audit webhook;
- o volume cifrado.

## Consequências

### Positivas

- A memória da API não depende mais do tamanho nem do número de arquivos enviados.
- O conteúdo do disco, do backup bruto e do KV do Vault não revela o arquivo nem a chave.
- O candidato abre e baixa o arquivo pela URL pré-assinada, com `Range`, sem passar pela API.
- O verificador escala sozinho, sem escalar a API e sem pesar no banco dela.
- Queda do verificador, do clamd ou do RabbitMQ atrasa a verificação, mas não perde nem recusa arquivo.

### Negativas

- O arquivo passa a ter o estado "em verificação". O documento só atende quando verificado, o envio da inscrição espera o resultado e o prazo vale para o envio (UNI-REQ-0027, UNI-REQ-0028 e UNI-REQ-0029).
- KES e Vault entram no caminho de leitura e gravação. Com o Vault selado ou fora de alcance, depois do cache do KES, o MinIO recusa leitura e envio (falha fechada, provada). O cache do KES é uma escolha entre disponibilidade e revogação.
- Quem tem `GetObject` no MinIO, ou uma URL pré-assinada válida, lê o conteúdo em claro. A proteção contra leitura indevida é o controle de acesso; a cifra protege o disco e o backup.
- Três componentes novos para operar: o KES, o RabbitMQ e o verificador.

## Prós e contras das opções

### A. Cifra e verificação na API

- Bom, porque já existe e responde ao candidato de forma síncrona.
- Ruim, porque a memória da API cresce com o arquivo: com 64 envios simultâneos de 10 MB, ela falhou por falta de memória no limite do pod. Além disso, a API concentra o acesso ao conteúdo sensível.

### B. Verificador que também cifra

- Bom, porque tira o conteúdo da API e mantém a chave na aplicação.
- Ruim, porque refaz no verificador o que o armazenamento já faz, e o candidato não pode ler o arquivo por URL pré-assinada: alguém precisa decifrar e repassar o conteúdo.

### C. SSE-KMS no MinIO e verificador dedicado

- Bom, porque tem chave por objeto, falha fechada, memória constante na API, leitura direta pelo candidato e verificação que sobrevive à queda de qualquer componente.
- Ruim, porque adiciona KES, RabbitMQ e verificador à operação e deixa o arquivo, por um tempo, no estado "em verificação".

## Mais informações

- Issues #2044 (direção), #2045 (POC) e #2036 (fluxo atual).
- [ADR-0121](0121-criptografia-de-dados-sensiveis-em-repouso.md), emendada por esta ADR; [ADR-0132](0132-armazenamento-publico-separado-para-documento-publicado.md), a do acervo público, que não muda.
- [ADR-0004](0004-outbox-transacional-via-wolverine.md) (outbox), [ADR-0014](0014-kafka-como-bus-assincrono-inter-modulos.md) (Kafka entre módulos) e [ADR-0044](0044-roteamento-domain-events-pg-queue-kafka-opcional.md) (fila no Postgres para eventos internos), que não mudam.
