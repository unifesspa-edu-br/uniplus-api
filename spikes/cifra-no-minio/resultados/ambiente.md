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
