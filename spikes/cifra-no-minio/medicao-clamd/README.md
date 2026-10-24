# Medição do clamd com PDFs e imagens, 4 CPUs por instância

Complemento da POC (#2045), feito em 23 e 24/10/2026. A POC mediu o fluxo inteiro com conteúdo aleatório e o clamd com 2 CPUs. Ela mostrou que o gargalo é a CPU do clamd. Esta medição vai direto ao clamd, pelo `INSTREAM`, sem MinIO nem verificador. Ela usa PDFs e imagens com a estrutura real dos formatos, até 20 MB (o máximo da UNI-REQ-0027), e um clamd com 4 CPUs.

## Configurações

| Arquivo | O que é |
|---|---|
| `config/hml-atual.conf` | A do chart `clamav-scanner` em HML: `DetectPUA yes`, `MaxThreads 4` |
| `config/aprovada.conf` | `DetectPUA no`, `HeuristicAlerts yes`, `AlertEncrypted no`, `StreamMaxLength 25M`, `MaxFileSize 25M`, `MaxScanSize 100M`, `ConcurrentDatabaseReload no` |
| `config/final.conf` | A aprovada com `ConcurrentDatabaseReload yes` |
| `config/final-alerta.conf` | A final com `AlertExceedsMax yes` |
| `config/final-limites.conf` | A final-alerta com `MaxFileSize 100M` e `MaxScanSize 400M` |
| `config/adotada.conf` | **A adotada:** a final-limites com `MaxFiles 50000`, `DetectPUA yes`, ignorando as duas assinaturas de JavaScript em PDF listadas em `config/pua-ignoradas.ign2`, que vai para o diretório da base, e `AlertEncryptedArchive yes` com `AlertEncryptedDoc no` no lugar do `AlertEncrypted no` |

Todas usam `DisableCache yes`, para que cada envio seja varrido de verdade. Em produção, o cache fica ligado, e um arquivo repetido sai mais rápido.

## Arquivos

`scripts/gerar.py` gera o conjunto sem dependência além do PIL e do Ghostscript:

- **digitalizado:** PDF com páginas JPEG A4 a 200 dpi, como um documento fotografado ou escaneado.
- **cifrado:** o digitalizado regravado pelo Ghostscript com senha de proprietário.
- **texto:** PDF de sistema, com páginas de texto aleatório em streams Flate (~970 páginas em 5 MB).
- **texto-natural:** texto em português, que o Flate comprime ~4 vezes (~3 mil páginas em 5 MB e ~10,6 mil em 20 MB).
- **formulário com JavaScript:** campo com validação em JavaScript e `OpenAction` em objeto próprio ou em linha.
- **JPEG e PNG:** foto de celular.
- **PDFs com o EICAR anexado:** sozinho, depois de 120 MB de anexos e no início de um anexo de 1 ou 30 MB.
- **ZIP cifrado anexado:** PDF que abre sem senha e leva como anexo um ZIP com senha que contém o EICAR.

Os PDFs ficam perto de 2, 6, 11 e 21 MB, porque o PDF cresce de página em página.

## Resultados

### Detecção

`resultados/deteccao.md` (HML × aprovada), `resultados/deteccao-limites.md` (final, final-alerta e final-limites) e `resultados/deteccao-adotada.jsonl` (o conjunto inteiro na adotada).

- **PUA.** A configuração de HML recusou como `PUA.Pdf.Trojan.OpenActionObjectwithJavascript-1` um PDF válido cuja ação de abertura só ajusta o zoom. As outras aceitam. Na base, só 5 assinaturas PUA são de PDF; essa casa o `/OpenAction` com JavaScript escrito em linha no início do objeto. A adotada liga a PUA e ignora só essa assinatura e a irmã (`...OpenActionObjectwithJS-1`): o PDF com JavaScript em linha passa, e o EICAR continua sendo pego.
- **EICAR.** O EICAR anexado dentro do PDF foi pego em todas, inclusive depois de 120 MB de anexos.
- **Limites de varredura.** Sem `AlertExceedsMax`, o que passa de um limite não é varrido, e o arquivo sai `OK`. O anexo de 30 MB de um PDF de 2 MB saiu limpo em 180 ms com `MaxFileSize 25M`, contra 1,7 s varrendo por inteiro com `100M`. Com `AlertExceedsMax yes`, ele é recusado como `Heuristics.Limits.Exceeded.MaxFileSize`.
  - A final-limites liga o alerta e folga os limites de tamanho. Ela recusou por `MaxFiles 10000` o PDF de texto de 20 MB com ~10,6 mil páginas. Por isso, a adotada folga também o `MaxFiles`, para 50000, e esse PDF passa, em 42 s, abaixo do `MaxScanTime` padrão de 120 s. É um caso de milhares de páginas, fora do documento típico do candidato.
  - A EICAR não prova perda de detecção nesse caso, porque a assinatura só casa com o arquivo EICAR exato (o anexo de 1 MB que começa por ela também sai `OK`). O tempo de varredura mostra o salto.

- **Arquivo cifrado dentro do envio.** Com `AlertEncrypted no`, o ZIP com senha anexado a um PDF que abre sem senha sai `OK`, sem ser varrido. A adotada liga só o alerta de arquivo compactado cifrado (`AlertEncryptedArchive yes`) e recusa esse PDF como `Heuristics.Encrypted.Zip`. O alerta de documento cifrado continua desligado (`AlertEncryptedDoc no`), e o PDF com senha só de proprietário segue `OK`. O PDF com senha de abertura é recusado antes, pelo verificador (ADR-0146).

### Vazão e espera

`resultados/carga.md` (configuração aprovada), `resultados/carga-final-limites.jsonl` e `resultados/carga-adotada.jsonl`. A final-limites tem os mesmos números da aprovada a menos de 5%, porque os limites só pesam no arquivo que chega a eles. A adotada, com a PUA ligada, foi medida no PDF digitalizado de 5 MB e no JPEG de 20 MB e repete a final-limites: 9,44 contra 9,43 PDFs por segundo e pico de 1,8 GiB nos dois. O documento típico do candidato tem 2 ou 3 páginas, como o PDF digitalizado de ~6 MB, que sai em ~0,4 s, a ~34 mil por hora por réplica. Com 4 CPUs, o paralelismo 4 leva o clamd a ~3,8 CPUs. Com 8, a vazão sobe de 1% a 10% (o maior ganho é no PDF digitalizado de 20 MB), e a espera de cada arquivo dobra. Com paralelismo 4:

- ~10 PDFs de 5 MB por segundo e ~2,6 de 20 MB;
- ~6 JPEGs de 20 MB por segundo;
- o PDF digitalizado de 20 MB, sozinho, leva de 1,4 a 2,4 s.

O caso caro é o PDF de texto: 1 MB leva 0,8 s; 5 MB com ~970 páginas, 4 s; 5 MB com ~3 mil páginas, 12 s.

### Memória

Em repouso, ~950 MiB. Com JPEG de 20 MB e paralelismo 4, até 1,7 GiB de memória anônima.

### Recarga da base

`resultados/recarga.jsonl` e `resultados/recarga-sob-carga.jsonl`.

- **Com `ConcurrentDatabaseReload no` e verificações em andamento,** o clamd solta a base e trava: não carrega a nova nem responde ao `PING`. São as duas linhas `aprovada` de `recarga.jsonl`. Houve ainda uma terceira ocorrência, interrompida pelo tempo limite, sem registro. Sem carga, a mesma recarga funciona em ~13 s.
- **Com `yes`, sob JPEG de 20 MB e paralelismo 4,** a recarga terminou sem falha: o log do clamd registra de 6 a 7 s entre o início da leitura da base nova e a conclusão. O pico foi de 2,7 GiB, e a espera máxima foi de 0,9 s, contra 0,7 s antes da recarga. Cada verificação conta pelo intervalo em que esteve em andamento, na janela de 60 s depois do `RELOAD`.

### Tempo até a réplica ficar pronta

Com a base já no volume, o clamd responde ao `PING` de 5 a 14 s depois de subir. O download da base, de ~170 MB, vem antes e depende de onde ela é baixada.

## Como reproduzir

```bash
cd spikes/cifra-no-minio
./scripts/subir.sh                          # baixa a base de assinaturas no volume da POC
./medicao-clamd/scripts/subir.sh
python3 -I medicao-clamd/scripts/gerar.py /caminho/dos/arquivos
python3 -I medicao-clamd/scripts/medir.py deteccao /caminho/dos/arquivos saida.jsonl
CONFIGS=final,final-alerta,final-limites python3 -I medicao-clamd/scripts/medir.py deteccao /caminho/dos/arquivos saida.jsonl
python3 -I medicao-clamd/scripts/medir.py carga /caminho/dos/arquivos saida.jsonl
CONFIGS=aprovada python3 -I medicao-clamd/scripts/medir.py recarga /caminho/dos/arquivos saida.jsonl
CONFIG_CARGA=final-limites PARALELISMOS=1,4 python3 -I medicao-clamd/scripts/medir.py carga /caminho/dos/arquivos saida.jsonl
CONFIG_CARGA=adotada PARALELISMOS=1,4 ARQUIVOS=digitalizado-5mb.pdf,jpeg-20mb.jpg python3 -I medicao-clamd/scripts/medir.py carga /caminho/dos/arquivos saida.jsonl
CONFIGS=adotada python3 -I medicao-clamd/scripts/medir.py deteccao /caminho/dos/arquivos saida.jsonl
python3 -I medicao-clamd/scripts/recarga_sob_carga.py /caminho/dos/arquivos saida.jsonl final-limites 13324
```

O `medir.py` lê CPU e memória do cgroup v2 de cada contêiner (`/sys/fs/cgroup/system.slice/docker-<id>.scope`). Sem `CONFIGS`, a detecção e a recarga rodam nas configurações `hml-atual` e `aprovada`, como em `deteccao.jsonl`. A máquina é a mesma da POC (`../resultados/ambiente.md`).
