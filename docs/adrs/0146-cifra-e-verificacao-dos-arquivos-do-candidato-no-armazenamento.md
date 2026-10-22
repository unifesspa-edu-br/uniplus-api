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

A POC da issue #2045 (`spikes/cifra-no-minio/`) testou essa direção na versão do MinIO do projeto, no KES e no Vault de HML (1.21.2). As provas estão em `resultados/provas.txt` e as medições em `resultados/medicoes.md`.

## Drivers da decisão

- A API não conhece o conteúdo dos arquivos: nem memória proporcional ao arquivo, nem acesso a dado sensível.
- Cada arquivo tem cifra individual, como pede o parecer das encarregadas de dados de 13/10/2026.
- O candidato abre e baixa os próprios arquivos sem a API intermediar o conteúdo.
- Falha fechada: sem a chave, o armazenamento não entrega nem grava conteúdo.
- Não há produção: os arquivos selados em HML podem ser apagados e reenviados.

## Opções consideradas

- **A.** Cifra e verificação na API (o fluxo atual).
- **B.** Verificador dedicado que também cifra com chave da aplicação.
- **C.** Cifra no MinIO (SSE-KMS, chave do KES guardada no Vault) e verificador dedicado acionado pelo bucket.

## Resultado da decisão

**Escolhida:** "C", porque a POC provou a cifra por objeto e a falha fechada no armazenamento, e porque a API deixa de tocar o conteúdo sem perder a leitura do arquivo em claro pelo candidato.

**Armazenamento.** São dois buckets privados, ambos com SSE-KMS como cifra padrão, e o volume é cifrado pela infraestrutura:

- o de envio recebe o PUT pré-assinado e funciona como quarentena; o que ninguém verificou expira;
- o definitivo só recebe o que o verificador promoveu.

A chave `uniplus-arquivos-candidato` vive no KES, que a guarda no KV do Vault cifrada por uma chave Transit. Cada objeto recebe uma chave de dados própria, selada por essa chave. Nenhuma requisição leva cabeçalho de SSE: a URL pré-assinada de envio continua como é hoje.

**Verificador.** É um processo próprio, fora da API, acionado pela notificação de PUT do bucket de envio. Ele:

1. lê o objeto em stream, já decifrado pelo MinIO;
2. confere se o formato corresponde ao declarado;
3. passa o conteúdo ao clamd (`INSTREAM`) e calcula o SHA-256 no caminho.

O arquivo limpo é promovido por cópia no próprio servidor, e a leitura e a cópia exigem o ETag do evento: o que foi verificado é exatamente o que é promovido. O arquivo infectado ou fora do formato é removido. O verificador relata o resultado à API por mensagem com o veredicto, o SHA-256, o tamanho e o formato, nunca o conteúdo nem o nome da ameaça. A entrega do evento precisa ser durável até o veredicto. O webhook com fila em disco garante a entrega ao verificador, mas acrescenta até ~2,3 s. Por isso, o transporte é decidido na implementação, por medição: Kafka, que já está na stack, ou webhook sem fila em disco com varredura do bucket de envio. Em qualquer caso, a varredura reenfileira o que ficou sem veredicto.

**Estado do arquivo.** O arquivo passa por `EM_VERIFICACAO` e termina em `VERIFICADO` ou `RECUSADO`. Com o verificador fora do ar, o arquivo segue em verificação e é verificado quando o serviço volta. Ele não é recusado por indisponibilidade.

**Leitura pelo candidato.** A API assina uma URL de GET curta no bucket definitivo, com `Content-Disposition` (`inline` ou `attachment`). O MinIO entrega o conteúdo em claro e aceita `Range`. Adulterar a URL a invalida.

**Acesso e auditoria.**

| Quem | Permissões no MinIO |
|---|---|
| API | Assina `PutObject` no bucket de envio e `GetObject` no definitivo. Não tem leitura no bucket de envio. |
| Verificador | Lê e remove no bucket de envio. Grava no definitivo. `ListBucket` nos dois, que o SDK exige. |
| Identidade do MinIO no KES | Só `generate` e `decrypt` da chave dos arquivos. |
| Pessoas | Nenhuma tem leitura nos buckets. |

O log de auditoria do MinIO (audit webhook) vai ao coletor de logs e registra cada acesso: objeto, operação, credencial e origem. O GET por URL pré-assinada aparece com a credencial de quem assinou, que é a API. Por isso, a API registra cada emissão de URL de leitura (quem pediu, qual arquivo, quando), e os dois registros se cruzam pelo objeto e pelo horário. Nesta versão, o KES não emitiu auditoria própria.

**Emenda da ADR-0121.** A emenda 2 deixa de valer para o arquivo do candidato: o conteúdo não é cifrado pela aplicação, e a cifra individual do parecer é a chave de dados por objeto do SSE-KMS. A cifra de campo da ADR-0121 continua como está.

### Medições e dimensionamento

O ambiente está descrito em `spikes/cifra-no-minio/resultados/ambiente.md`. O verificador e a API rodaram com os limites do pod de HML: 2 CPUs e 1 GiB.

| | Fluxo novo | Fluxo atual |
|---|---|---|
| Verificação de 1 / 5 / 10 MB, um por vez (p50) | 74 / 212 / 444 ms | 59 / 196 / 362 ms (confirmação síncrona) |
| Pico de memória da API | 92–98 MiB em todos os cenários | 104 MiB (1 envio de 1 MB) a 860 MiB (64 de 10 MB) |
| Pico de memória do verificador | 109–144 MiB em todos os cenários | — |
| 64 envios simultâneos de 10 MB | 64 de 64 verificados | 15 de 64 falharam com `OutOfMemoryException` |
| Vazão de 5 MB, N = 8 / 32 / 64 | 25 / 25 / 26 arquivos/s (paralelismo 8); 32 arquivos/s (paralelismo 32) | 24 / 46 / 44 arquivos/s |
| Do fim do PUT ao veredicto, 5 MB, sem disputa | ~200 ms sem fila em disco; ~1,4–3 s com fila em disco | — |

A vazão é limitada pelo clamd (10 conexões por padrão), e não por quem lê o arquivo. Com mais paralelismo, cada verificação demora mais e o total quase não sobe. O ganho do fluxo novo é a memória constante e o isolamento, não a vazão bruta.

Um verificador com um clamd verifica ~25–32 arquivos de 5 MB por segundo, ou seja, ~90 mil a 115 mil por hora. O dimensionamento para a véspera do encerramento é:

```text
réplicas (verificador + clamd) = arquivos na hora de pico ÷ 90 mil
```

O número de inscrições esperado, os arquivos por inscrição e a fração enviada na hora de pico ainda não foram levantados. A escala é horizontal: mais réplicas do verificador e do clamd, sem tocar a API.

### Plano de migração

Não há produção. Os arquivos selados em HML são apagados e reenviados, sem conversão.

**Sai da API:**

- `IProtecaoDoConteudoSelado` (`VerificarAsync`, `SelarAsync`, `AbrirAsync`) e o `ClamdClient`;
- no arquivo do candidato, a leitura do conteúdo em `ConfirmacaoDoArquivoEnviado` e `LeituraDoArquivoEnviado`, a verificação de formato e o cálculo do SHA-256, que passam ao verificador;
- a chave selada e a sua limpeza.

A confirmação passa a registrar o arquivo em verificação, e a API consome o resultado do verificador.

**Entra na infraestrutura:** o KES, com autenticação no Vault por Kubernetes; a chave Transit e o caminho do KV, com a política do KES; o SSE-KMS e as políticas dos dois buckets; a notificação; o verificador e o clamd; o audit webhook; e o volume cifrado.

## Consequências

### Positivas

- A memória da API não depende mais do tamanho nem do número de arquivos enviados.
- O conteúdo do disco, do backup bruto e do KV do Vault não revela o arquivo nem a chave.
- O candidato abre e baixa o arquivo pela URL pré-assinada, com `Range`, sem passar pela API.
- O verificador escala sozinho, sem escalar a API.

### Negativas

- O arquivo passa a ter o estado "em verificação", e o produto precisa tratá-lo (pendências abaixo).
- KES e Vault entram no caminho de leitura e gravação. Com o Vault selado ou fora de alcance, depois do cache do KES, o MinIO recusa leitura e envio (falha fechada, provada). O cache do KES é uma escolha entre disponibilidade e revogação.
- Quem tem `GetObject` no MinIO, ou uma URL pré-assinada válida, lê o conteúdo em claro. A proteção contra leitura indevida é o controle de acesso; a cifra protege o disco e o backup.
- Mais um serviço para operar (o verificador) e um componente novo na infraestrutura (o KES).

### Pendências

- **Encarregadas de dados:** confirmar que a chave de dados por objeto do SSE-KMS atende à cifra individual do parecer de 13/10/2026. Ainda não há resposta. **Pendente; não atendida.**
- **PO, com repasse do líder técnico:** UNI-REQ-0027, UNI-REQ-0028, UNI-REQ-0029 e a seção 6.3 do modelo de negócio da Seleção supõem verificação síncrona, por exemplo "verificação fora do ar recusa o envio do arquivo" e "o comprovante nunca mostra arquivo em verificação". É preciso decidir:
  - se o envio da inscrição aguarda o veredicto dos documentos obrigatórios (recomendado: o documento só atende quando verificado, e o comprovante continua sem arquivo em verificação);
  - como o portal mostra "em verificação" e "recusado".

  O SHA-256 e o formato do comprovante passam a vir do verificador.
- **Líder técnico:** o volume de inscrições e de arquivos no pico, para o dimensionamento.

## Prós e contras das opções

### A. Cifra e verificação na API

- Bom, porque já existe e responde ao candidato de forma síncrona.
- Ruim, porque a memória da API cresce com o arquivo: com 64 envios simultâneos de 10 MB, ela falhou por falta de memória no limite do pod. Além disso, a API concentra o acesso ao conteúdo sensível.

### B. Verificador que também cifra

- Bom, porque tira o conteúdo da API e mantém a chave na aplicação.
- Ruim, porque refaz no verificador o que o armazenamento já faz, e o candidato não pode ler o arquivo por URL pré-assinada: alguém precisa decifrar e repassar o conteúdo.

### C. SSE-KMS no MinIO e verificador dedicado

- Bom, porque tem chave por objeto, falha fechada, memória constante na API e leitura direta pelo candidato.
- Ruim, porque adiciona KES e verificador à operação e deixa o arquivo, por um tempo, no estado "em verificação".

## Mais informações

- POC: `spikes/cifra-no-minio/` (README, roteiros e resultados); issues #2044, #2045 e #2036.
- [ADR-0121](0121-criptografia-de-dados-sensiveis-em-repouso.md), emendada por esta ADR; [ADR-0132](0132-armazenamento-publico-separado-para-documento-publicado.md), a do acervo público, que não muda.
