---
status: "accepted"
date: "2026-09-02"
decision-makers:
  - "Tech Lead"
consulted:
  - "Equipe da API do SIGAA (DISI)"
informed: []
---

# ADR-0130: Integração com o SIGAA por consulta HTTP paginada, não por tópico de mensageria

## Contexto e enunciado do problema

O módulo Discentes mantém uma réplica dos vínculos de discentes de graduação, reconciliada
diariamente a partir do SIGAA, o sistema acadêmico de referência. A réplica já existe; o
que falta é o meio de recuperar os dados na origem.

A [ADR-0014](0014-kafka-como-bus-e-transporte-para-integracoes.md) escolheu Kafka como bus
assíncrono entre módulos e como transporte para integrações com sistemas externos, mas
deixou uma pendência explícita: o consumo de tópicos produzidos por sistemas externos
"fica fora do escopo desta ADR — cada integração deve ser objeto de ADR específica quando
aparecer". Esta é a primeira dessas integrações a aparecer.

Some-se a isso que o repositório não tem precedente algum de cliente HTTP para serviço de
terceiro: não há cliente tipado, não há política de resiliência configurada e não existe
nenhum manipulador de autenticação na cadeia de envio. O que se decide aqui vira o molde
das próximas integrações.

## Drivers da decisão

- A origem não publica tópico de mensageria; expõe uma API de consulta.
- O que se quer é reconciliação diária de um conjunto inteiro, não um fluxo de eventos.
- A origem é um sistema institucional em uso, sujeito a indisponibilidade momentânea e a
  limitação de vazão — o cliente precisa suportar isso sem derrubar a sincronização.
- Os dados trafegados incluem CPF: o meio precisa ser auditável quanto ao que registra.
- É o primeiro cliente do tipo no repositório; a escolha precisa ser reprodutível.

## Opções consideradas

- Consulta HTTP paginada contra a API da origem
- Consumo de tópico de mensageria alimentado pela origem
- Troca de arquivo em armazenamento compartilhado

## Resultado da decisão

**Escolhida:** "consulta HTTP paginada contra a API da origem", porque é o único meio que
a origem oferece e porque corresponde à natureza do trabalho — reconciliar um conjunto
conhecido uma vez por dia, e não reagir a acontecimentos.

A recuperação usa um cliente tipado gerado em tempo de compilação, sobre a fábrica de
clientes HTTP da plataforma, com uma política de resiliência declarada. A cadeia de envio
tem a resiliência por fora e a autenticação por dentro, de modo que cada nova tentativa
releia o token vigente em vez de repetir um cabeçalho carimbado uma única vez. O corte de
circuito fica dentro da política de repetição: assim, quando abre, encerra o laço em vez
de deixá-lo insistir contra uma origem que já se declarou indisponível.

A autenticação é por usuário e senha de serviço, trocados por um token de acesso. A origem
não informa a validade do token junto com ele, então a validade é lida do próprio token —
preferindo a diferença entre os instantes de emissão e expiração que ele declara, porque
ambos vêm do relógio da origem e a subtração cancela qualquer divergência com o relógio
local. **Essa leitura serve exclusivamente para agendar a renovação, nunca como decisão de
segurança:** quem valida o token é a origem, a cada requisição, e um valor adulterado ali
só provocaria uma renovação desnecessária ou uma recusa que o cliente já sabe tratar.

## Consequências

### Positivas

- A sincronização não depende de a origem passar a publicar mensageria.
- Instabilidade momentânea da origem é absorvida sem intervenção.
- O molde — cliente tipado, política declarada, autenticação por dentro da resiliência —
  fica disponível para a próxima integração externa.
- A omissão do cabeçalho de autorização nos registros impede que o token apareça no log
  quando alguém aumenta o detalhamento para investigar um incidente.

### Negativas

- A réplica reflete o estado da origem no momento de cada varredura, não continuamente:
  uma alteração feita logo após a leitura só aparece no dia seguinte.
- A paginação por deslocamento sobre uma base em uso pode fazer um registro aparecer duas
  vezes ou nenhuma, se a origem for alterada durante a varredura. A reconciliação é por
  chave natural do vínculo, o que absorve a duplicidade; a ausência é corrigida na
  execução seguinte.
- Três dependências novas entram no repositório, e com elas a responsabilidade de
  acompanhar seus avisos de segurança.

### Neutras

- A origem recusa qualquer parâmetro de consulta que não conheça, em vez de ignorá-lo.
  Isso torna a interface de consulta rígida — acrescentar um parâmetro não previsto quebra
  toda chamada —, mas também torna impossível um filtro silenciosamente ignorado.

## Confirmação

A cadeia de envio é exercitada por testes que substituem apenas a rede, preservando
resiliência e autenticação reais: um deles falha se a ordem dos manipuladores for
invertida, porque nesse caso a renovação do token deixa de ocorrer a cada tentativa. Outro
falha se o cabeçalho de autorização deixar de ser omitido do registro, e roda com o nível
de detalhe mais alto justamente porque é só nele que o vazamento apareceria.

## Prós e contras das opções

### Consulta HTTP paginada

- Bom, porque é o que a origem oferece hoje, sem exigir trabalho dela.
- Bom, porque reconciliar o conjunto inteiro é naturalmente idempotente.
- Ruim, porque a leitura é um retrato do instante, e não acompanha alterações contínuas.
- Ruim, porque a paginação sobre base em uso não é um retrato atômico.

### Tópico de mensageria

- Bom, porque entregaria alterações à medida que acontecem.
- Bom, porque seguiria o transporte já escolhido para integrações externas.
- Ruim, porque a origem não publica tópico algum — dependeria de trabalho de outra equipe,
  em outro sistema, sem previsão.
- Ruim, porque um fluxo de eventos não dá, sozinho, a garantia de conjunto completo que a
  reconciliação diária precisa.

### Troca de arquivo

- Bom, porque é simples e não exige disponibilidade simultânea dos dois lados.
- Ruim, porque acrescenta um intermediário a operar e vigiar.
- Ruim, porque desloca para fora do código a definição de formato e de recorte, que hoje
  está num contrato versionado.

## Mais informações

- O contrato de resposta publicado pela origem está copiado em
  `tests/Unifesspa.UniPlus.Discentes.UnitTests/Recursos/`, e é usado como corpo real nos
  testes de decodificação.
- O recorte de requisição não tem contrato publicado. Três características da origem
  governam o cliente e estão registradas aqui porque não estão em lugar nenhum além do
  código dela: parâmetro desconhecido é recusado com erro de requisição em vez de
  ignorado; o filtro de situação aceita vários valores de uma vez, o que permite pedir
  todas as situações de interesse numa única chamada; e a paginação é por número de página
  com tamanho limitado a duzentos, sem possibilidade de o cliente desligá-la.
- [ADR-0014](0014-kafka-como-bus-e-transporte-para-integracoes.md) — escolheu Kafka para
  integrações externas e deixou o consumo de sistemas externos para ADR específica.
- [ADR-0121](0121-criptografia-de-dados-sensiveis-em-repouso.md) — cifra do CPF na
  fronteira do repositório, que é o destino dos dados recuperados aqui.
