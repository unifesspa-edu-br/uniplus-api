---
status: "accepted"
date: "2026-10-23"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Uni+"
  - "Infraestrutura"
---

# ADR-0147: A base de assinaturas do antivírus chega aos verificadores por um espelho interno

## Contexto e enunciado do problema

Pela [ADR-0146](0146-cifra-e-verificacao-dos-arquivos-do-candidato-no-armazenamento.md), o arquivo do candidato é verificado por um clamd que roda como sidecar em cada pod do verificador, com duas réplicas no mínimo e escala pelo tamanho da fila. O clamd só reconhece a ameaça que consta na sua base de assinaturas, de cerca de 170 MB compactados, atualizada todo dia pelo freshclam.

Na POC da issue #2045, os clamd compartilhavam o mesmo volume, e só um deles atualizava a base. No Kubernetes, o volume do clamd é `ReadWriteOnce` e não é montado por vários pods. É preciso decidir como a base chega a cada sidecar.

## Drivers da decisão

- A réplica nova fica pronta em segundos, porque ela sobe justamente no pico, perto do encerramento das inscrições. Com a base no volume, o clamd responde de 5 a 14 s depois de subir; o resto é o download da base.
- Subir uma réplica não depende do servidor público do ClamAV, que bloqueia quem baixa demais.
- Só um componente tem saída para a internet.
- A falha da atualização não interrompe a verificação nem impede uma réplica nova de subir.

## Opções consideradas

- **A.** Cada sidecar com o seu freshclam, baixando do servidor público para um volume próprio.
- **B.** Um volume `ReadWriteMany` compartilhado, gravado por um freshclam e lido por todos os clamd.
- **C.** Um espelho interno no cluster, do qual o freshclam de cada sidecar baixa a base.

## Resultado da decisão

**Escolhida:** "C". Um único componente, o espelho (por exemplo, o `cvdupdate`, ferramenta oficial do ClamAV), baixa a base do servidor público e a serve dentro do cluster. O freshclam de cada sidecar aponta para o espelho (`PrivateMirror`), e a base fica num volume local do pod. O verificador não tem saída para a internet.

A imagem do sidecar traz uma base de partida, gerada no build, fora do diretório da base. Na subida, ela é copiada para o volume local, junto com a lista de assinaturas ignoradas da ADR-0146, que o espelho não serve, e o pod busca no espelho a base atual; se o espelho não responder, o clamd sobe com a cópia da imagem, e o freshclam a atualiza quando o espelho voltar.

Se o espelho cair, cada clamd continua verificando com a base que tem, a que já carregou ou a da imagem, e o envio do candidato não é recusado nem bloqueado por isso. Um alerta operacional dispara quando a base em uso passa de três dias.

## Consequências

### Positivas

- A réplica nova baixa a base pela rede interna e fica pronta sem depender de terceiros.
- O servidor público recebe um acesso por atualização, e não um por pod.
- A saída para a internet fica restrita a um componente.

### Negativas

- Mais um componente para operar.
- Com o espelho fora do ar, a base envelhece até o alerta ser atendido.
- Com o espelho fora do ar, o pod que inicia sobe com a base da imagem, que pode ser mais velha que a dos pods em execução; o alerta de três dias vale também para ela.
- A imagem do sidecar cresce cerca de 170 MB e precisa ser reconstruída de tempos em tempos, para a base de partida não envelhecer.

### Neutras

- O arquivo já aprovado não é verificado de novo quando a base é atualizada.

## Confirmação

- A NetworkPolicy do verificador não permite saída para a internet, só o acesso ao espelho.
- Com o espelho fora do ar, um pod novo do verificador fica pronto com a base da imagem.
- O verificador expõe a idade da base em uso, que alimenta o alerta de três dias.

## Prós e contras das opções

### A. freshclam por sidecar

- Bom, porque não tem componente novo.
- Ruim, porque cada réplica nova baixa a base inteira de fora ao subir e leva minutos para ficar pronta, e o número de réplicas multiplica os acessos ao servidor público.

### B. Volume `ReadWriteMany`

- Bom, porque a base é baixada uma vez só.
- Ruim, porque depende de o armazenamento do cluster oferecer `ReadWriteMany` e mistura a gravação do freshclam com a leitura de vários clamd.

### C. Espelho interno

- Bom, porque a réplica sobe rápido, sem depender de fora, e a saída para a internet fica num componente só.
- Ruim, porque acrescenta um componente e, para a réplica subir com o espelho fora do ar, uma base de partida na imagem.

## Mais informações

- [ADR-0146](0146-cifra-e-verificacao-dos-arquivos-do-candidato-no-armazenamento.md), que define o verificador com o clamd sidecar. Se ela deixar de usar o clamd como sidecar, esta ADR é revista.
- Issues #2067 (esta decisão), #2054 (implementação do verificador) e unifesspa-edu-br/uniplus-infra#679 (espelho).
