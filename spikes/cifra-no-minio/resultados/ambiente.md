# Ambiente das medições (22/10/2026)

Uma única máquina, com todos os contêineres e o gerador de carga no mesmo host.

| Item | Valor |
|---|---|
| CPU | AMD Ryzen 9 9950X, 16 núcleos / 32 threads |
| Memória | 60 GiB |
| Disco | NVMe (Kingston), volumes Docker locais |
| Sistema | Debian 13, kernel 7.2.9 |
| Docker / Compose | 29.9.1 / v5.6.0 |
| .NET | SDK 10.0.402, runtime ASP.NET 10 (imagem `mcr.microsoft.com/dotnet/aspnet:10.0`), GC padrão (Server GC) |
| MinIO | RELEASE.2025-09-07T16-13-09Z (imagem do projeto), um nó, um disco |
| KES | 2025-03-12T09-35-18Z |
| Vault | 1.21.2, armazenamento em arquivo |
| ClamAV | 1.4 (imagem do projeto), configuração padrão do clamd (10 conexões simultâneas), sem limite de CPU |
| Verificador e API | 2 CPUs e 1 GiB cada, os limites do pod da API em HML |

Condições:

- O conteúdo de cada arquivo é aleatório e único, porque o clamd guarda em cache o veredicto do conteúdo já visto. Arquivo aleatório não tem estrutura de PDF, então o clamd o varre como binário genérico. Um PDF real pode levar mais ou menos tempo.
- O fluxo atual foi reproduzido no papel `api-atual` com os mesmos passos da confirmação da API, sem o resto da API (banco, transação, autenticação). A cifra é o AES-GCM local. Em HML, se o provedor for o Vault Transit, a cópia selada passa pela rede até o Vault, e o custo do fluxo atual seria maior que o medido.
- O pico de memória é o VmHWM do processo, zerado antes de cada cenário (`/proc/self/clear_refs`).
- No fluxo novo, o candidato é liberado ao fim do PUT. "Até o veredicto" vai do fim do PUT ao registro do verificador. "Notificação" vai do fim do PUT à chegada do evento ao verificador. No fluxo atual, "até o veredicto" é a própria confirmação síncrona.
- A vazão (arq/s) é o total de arquivos dividido pelo tempo de parede, do primeiro envio ao último veredicto.

## Medições do fluxo com fila (`medicoes-fila.md`)

Mesma máquina, com estas diferenças e acréscimos:

| Item | Valor |
|---|---|
| ClamAV | 1.4, **2 CPUs por clamd** (o tamanho de um sidecar), `StreamMaxLength` padrão de 100 MB, 10 conexões |
| Verificador | uma a três réplicas, cada uma com o seu clamd; 2 CPUs e 1 GiB; paralelismo 8 por réplica |
| API (`api-fila`) | 2 CPUs e 1 GiB; Wolverine 5.41.0 com outbox durável sobre EF Core |
| RabbitMQ | 4.1.4, um nó, quorum queues |
| Postgres | 18.6, `max_connections` 100; dois pools por processo (EF e Wolverine), de até 10 conexões cada |
| Fila no Postgres | polling de 100 ms (`postgres`) ou o padrão do Wolverine (`postgres-padrao`) |

Condições:

- "Espera na fila" vai da confirmação ao início da verificação; no webhook, do fim do PUT à chegada do evento. "Até o veredicto" vai da confirmação ao registro do veredicto no banco da API, lido pelo gerador de carga.
- A CPU é a média das amostras de `docker stats` durante a carga, somada por família de contêiner (por exemplo, os três clamd juntos).
- O pico de memória do verificador e da API inclui o host do Wolverine e o cliente do transporte; não muda com o tamanho nem com o número de arquivos.
- As medições são anteriores a três ajustes do código: o veredicto passou a ser gravado por `UPDATE` atômico, o envio só é removido depois de uma leitura de metadado que confere o ETag, e o infectado deixou de ser removido. Nenhum deles toca o clamd, que é o gargalo.
- Antes de cada cenário, o roteiro confere o disco e, abaixo de 3 GiB livres, reinicia o MinIO para esvaziar a lixeira. Isso aconteceu três vezes na rodada publicada, sempre entre cenários.
