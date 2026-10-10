---
status: "accepted"
date: "2026-09-29"
decision-makers:
  - "Tech Lead"
consulted:
  - "CEPS (dono do processo)"
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0136: Catálogo de fatos do candidato administrável, com fatos de sistema protegidos

## Contexto e enunciado do problema

A ADR-0111 fixou o catálogo de fatos do candidato como vocabulário fechado, governado por seed e sem cadastro administrativo. O motivo foi o **fato inoperante**: um fato só é útil se existe código que resolva o seu valor, e cadastrar um fato por tela produziria um dado que nada resolve. Pelo mesmo motivo, ela recusou o domínio texto. A ADR-0116 manteve essa governança ao acrescentar origem, ponto de resolução e vínculo (`binding`).

Com os fatos semeados hoje, o catálogo não cobre o que a Unifesspa precisa coletar. O edital de Medicina 2027 pede identificação, endereço, origem escolar em duas perguntas, bônus regional e composição familiar; o PSIQ 2027 pede opções de curso, povo e etnia, lista de espera e vínculo com o PARFOR. Cada dado novo exigiria uma nova versão do sistema, e cada formulário seria refeito a cada edital. O UNI-REQ-0143 passou a exigir o contrário: os fatos que os formulários coletam são cadastrados pelo administrador no módulo Configuração, sem depender de nova versão, com os fatos definidos pela lei ou pelo motor protegidos.

O argumento do fato inoperante continua certo para um tipo de fato e errado para os outros. Um fato **declarado** não precisa de código que o resolva: o candidato responde num campo do formulário, e a execução guarda a resposta de forma genérica, pelo código do fato. Um fato **derivado por regra** também não: o motor de derivação é genérico e avalia a regra configurada (ADR-0116, emenda 1.5). Só o fato **derivado pelo sistema**, cujo valor é calculado por código próprio (faixa etária, UF e município do endereço, egresso de escola pública), e o fato de **integração** dependem de desenvolvimento.

## Drivers da decisão

- Dado novo no formulário não pode depender de nova versão do sistema (UNI-REQ-0143, UNI-REQ-0065).
- Um fato só existe se algo resolve o seu valor: a resposta do candidato, uma regra configurada, ou código do sistema.
- O que a lei define não pode ser alterado por tela: cor/raça, sexo, autodeclaração de deficiência e as perguntas da origem escolar das cotas (UNI-REQ-0148).
- Dado pessoal não pode entrar no edital congelado por meio de uma regra configurada, e todo dado coletado precisa de classificação de proteção de dados.
- Um predicado publicado congela o código do fato por valor (RN08).

## Opções consideradas

- **Manter o catálogo fechado e governado por seed** (ADR-0111), acrescentando fatos por PR.
- **Catálogo aberto**: o administrador cadastra e altera qualquer fato, inclusive os de sistema.
- **Catálogo administrável com fatos de sistema protegidos**: o administrador cadastra fatos declarados e derivados por regra; os fatos de sistema e de integração continuam entrando só por código.

## Resultado da decisão

**Escolhida:** "Catálogo administrável com fatos de sistema protegidos", porque abre ao administrador exatamente os fatos que não dependem de código para ter valor e mantém sob desenvolvimento os que dependem.

### Governança

- **Fato de sistema** é o que a lei ou o motor define, qualquer que seja a origem: entra por código e seed e é marcado como de sistema no cadastro. São de sistema os fatos já semeados, os vocabulários normativos, todo fato **derivado pelo sistema** por código próprio daquele fato, os fatos de que esses derivados dependem (como o endereço de residência e a data de nascimento) e todo fato de **integração**. As dependências de um derivado de sistema são declaradas no seed e passam pelas mesmas conferências de ordem e ciclo dos derivados por regra. A classificação de um derivado de sistema é fixada no seed com a sua justificativa legal, como a da modalidade, que é publicada nas listas de resultado.
- **Fato do administrador** é o que o papel `plataforma-admin` cadastra: sempre `Declarado`, ou `Derivado` com o vínculo `REGRA_DERIVACAO:`. Os vínculos `ATRIBUTO_CANDIDATO:`, `CLASSIFICACAO:` e `INTEGRACAO:` só existem em fato de sistema, porque dependem de código que calcule ou traga o valor. Os demais módulos leem o catálogo por leitor síncrono (ADR-0056).
- A única exceção em aberto é o agregado sobre um grupo repetível (`AGREGACAO_GRUPO:`): ele é derivado pelo sistema (UNI-REQ-0075), mas por mecanismo genérico, sem código próprio do fato. Se ele entra como fato de sistema, por seed, ou é declarado pelo administrador sobre o grupo que compõe, é decidido na ADR do grupo repetível. Até lá, uma regra configurada de fato de escopo `CANDIDATO` não cita fato de escopo `MEMBRO_GRUPO`, porque o motor genérico resolve um valor por código e não sabe escolher entre os membros. Um derivado de sistema, com código próprio, pode depender da lista inteira, como a renda per capita, que divide a renda da família pelos seus integrantes.
- Num fato de sistema, o administrador edita só o nome e a descrição: não o desativa, não acrescenta nem desativa valores e não mexe nas regras padrão dele no catálogo; a lista de valores só cresce por nova versão do sistema. É o caso dos **vocabulários normativos** (cor/raça, sexo, autodeclaração de deficiência, e as duas perguntas da origem escolar). A regra que um processo configura para um fato de sistema, como a matriz de regras da modalidade, continua sendo configuração daquele processo (ADR-0116, emenda 1.2).
- **Desativação no lugar da remoção**, para o fato do administrador e para cada valor, como na ADR-0122: desativar recusa vínculos novos e preserva os existentes (UNI-REQ-0143), e o fato ou o valor pode ser reativado. Um vínculo copiado de um modelo de formulário ou de uma regra padrão que cite fato ou valor desativado é vínculo novo: a cópia não o leva, e o processo recebe a lista do que ficou de fora. O processo congela os valores ativos e os que as suas regras já citam, de modo que um valor desativado depois não impede a publicação de quem já o tinha nem deixa regra congelada citando valor ausente.
- O nome e a descrição do fato e dos seus valores são texto de catálogo. O que o candidato lê é o rótulo do item do formulário, que congela com o formulário (UNI-REQ-0145).

### Identidade e imutabilidade

- O **código** do fato é imutável desde o cadastro, em todo fato, e único, inclusive entre os desativados. O código de um valor de domínio nunca é renomeado nem removido, só desativado. Um predicado publicado cita o fato e o valor por código, e mudar a semântica de um código reinterpretaria o edital congelado (ADR-0111).
- **Colisão de código:** o cadastro recusa, num fato do administrador, código reservado a fato de sistema, isto é, os já semeados e os que o sistema declara como reservados antes de semeá-los. Um fato de sistema novo nunca assume um código ocupado por fato do administrador: a migration que o semeia confere a colisão e falha sem gravar, e o fato de sistema recebe outro código, o que é livre porque nada o cita ainda.
- **Eixos do fato** são tudo o que define o significado, a validação ou a proteção do valor: domínio, origem, cardinalidade, escopo, fonte dos valores, formato do texto, ponto de resolução, vínculo, e classificação de proteção de dados com a finalidade e a hipótese legal de tratamento. Mudança num eixo, depois do marco, é um fato novo.
- No fato do administrador, o marco é o **cadastro**: os eixos não se editam. Continuam editáveis o nome, a descrição, os valores (acrescentar, desativar e reativar, com a descrição e a ordem de cada um) e as regras padrão do derivado. O marco é o cadastro, e não a publicação, porque a troca feita por tela não passa por revisão de PR, e um fato já citado por um processo em rascunho ou por um modelo de formulário seria reinterpretado sem aviso.
- No fato de sistema, o marco é o da emenda 1.1 da ADR-0116, estendido a todos os eixos: eles se tornam imutáveis a partir da primeira publicação de um processo seletivo que cite o fato. Até lá, a reclassificação é feita por código, em PR, mantendo o código, e a migration que a aplica confere as citações ao fato nas regras padrão e nos modelos do catálogo e falha sem gravar se alguma deixar de ser válida. Quando a reclassificação invalidaria uma regra do administrador, que não se edita, a saída é um fato de sistema novo, com código novo. Nos processos, que vivem em outro módulo, a conferência é refeita pela Seleção na configuração e na publicação.

### Identidade por código, como a ADR-0129 admite

A Seleção referencia o fato pelo **código**, e não por uma identidade de origem. A ADR-0129 exige comparar pela identidade quando um código pode ser reatribuído, e admite a referência por código quando o código é imutável por invariante, como o `AreaCodigo` da ADR-0055. O código do fato é imutável e nunca reutilizado, então ele **é** a identidade, e o predicado congelado já o carrega por valor (ADR-0111).

### Forma do fato

- **Domínio:** além de `Booleano`, `Categorico` e `Numerico`, o catálogo aceita `Texto`, `Data` e `Endereco`.
- **Endereço do candidato:** é uma `ReferenciaEnderecoGeo` (ADR-0096), mas, ao contrário do endereço institucional, decide elegibilidade, como o bônus regional por município. Por isso o CEP e a cidade do endereço do candidato são conferidos no servidor contra o Geo, e não aceitos como retrato enviado pelo frontend.
- **Citação em regra:** texto e endereço **nunca** são citados em regra configurada, para que dado pessoal não entre no edital congelado. O endereço entra só por meio dos derivados de sistema de UF e município. A data entra só por meio de um derivado relativo à data de referência, como a faixa etária.
- **Formato do texto:** `LIVRE`, `CPF`, `EMAIL`, `TELEFONE`, `CEP` ou `NOME_PESSOA`. A validação e a máscara de exibição parcial de cada formato vivem num tipo de valor do Kernel: o de CPF já tem as duas, com a máscara `***.999.999-**`; o de e-mail tem a validação; os demais nascem com o formato.
- **Classificação de proteção de dados**, obrigatória em todo fato, na escala da ADR-0081: `PUBLICO`, `INTERNO`, `PESSOAL` ou `SENSIVEL`. `SENSIVEL` diz a natureza do dado — o dado sensível do art. 5º, II da LGPD, como origem racial ou saúde — e cabe a quem cadastra reconhecê-la; os demais níveis dizem a exposição permitida a um dado que é sempre pessoal, porque toda resposta ligada a um candidato identificado é dado pessoal (LGPD, art. 5º, I). O "não pessoal" do UNI-REQ-0143 corresponde a `PUBLICO` ou `INTERNO`.
- **Finalidade e hipótese legal:** todo fato declara a finalidade e a hipótese legal de tratamento, porque toda resposta ligada a um candidato identificado é dado pessoal, qualquer que seja o nível de exposição. A hipótese é escolhida de um vocabulário fechado: as do art. 11 da LGPD para fato `SENSIVEL` e as do art. 7º para os demais.
- **Classificação mínima:** os domínios texto (em todo formato, inclusive o livre, que pode conter qualquer coisa), endereço e data exigem `PESSOAL` ou `SENSIVEL`, e o cadastro recusa classificação mais fraca. A exceção é o nome social preferido pelo titular, público pela ADR-0082: é fato de sistema, e a classificação e a regra da preferência vêm daquela ADR.
- **Classificação do derivado do administrador:** um derivado por regra nunca tem classificação mais fraca que a mais restritiva das suas dependências, porque o valor derivado revela o fato de que depende — um derivado de cor/raça é sensível como ela. A conferência vale ao cadastrar o derivado, ao editar as suas regras padrão e, na publicação, sobre a regra copiada para o processo.
- **Escopo:** `CANDIDATO`, ou `MEMBRO_GRUPO` para os fatos de cada membro de uma lista do formulário. O grupo repetível, inclusive a proteção dos dados de terceiros que ele coleta, é decidido na ADR própria.
- **Fonte dos valores**, para o fato categórico: `GLOBAL` (valores no catálogo); `PROCESSO` (opções que o processo declara, inclusive a partir de um cadastro institucional, como o tipo de deficiência e a condição de atendimento especializado ofertados); `MODALIDADE`; `MUNICIPIOS_BONUS`; e `GEO` (UF e municípios do módulo Geo, para os derivados de residência). A fonte substitui a distinção pela nulidade da lista de valores da ADR-0111. Uma fonte nova é decisão de desenvolvimento, porque exige o código que a lê.
- **Ponto de resolução** declarado no cadastro, validado contra as fases canônicas. O de um derivado nunca é anterior ao de nenhuma dependência: a recusa vale ao cadastrar o derivado e ao editar as suas regras padrão. Na publicação, a conferência de ordem e de ciclo é refeita sobre a regra copiada para o processo, com a fase efetiva de cada fato no processo (UNI-REQ-0077, UNI-REQ-0144).

### Derivado por regra

- O derivado por regra é **categórico ou booleano**, avaliado por **união** das contribuições (ADR-0116, emenda 1.5). Com alguma dependência ainda indeterminada, o derivado é indeterminado, nunca falso nem vazio.
- O categórico é sempre multivalorado, porque a união pode produzir mais de um código; o cadastro recusa derivado categórico escalar.
- No booleano, a regra não contribui código de valor: a regra ativa contribui verdadeiro. O valor é verdadeiro se alguma regra ativa e falso se nenhuma, congelado e trafegado como booleano, nunca como conjunto. O motor de derivação hoje só representa o categórico, e o booleano entra com a implementação desta decisão.
- As regras cadastradas no catálogo são o **padrão**, copiado para o processo quando ele passa a usar o fato. **A cópia é a que manda**: alterar o padrão depois não altera o processo (ADR-0061). A cópia leva só as regras válidas naquele processo: fica de fora a regra padrão que contribui código fora dos valores do processo (a modalidade não ofertada, a opção que o processo não declara, o município fora do bônus) ou que cita fato ou valor desativado, e o processo recebe a lista das regras que ficaram de fora. Depois da cópia, uma regra que passe a fazer falta, porque a oferta do processo mudou, é acrescentada na configuração do próprio processo: a cópia não é refeita a partir do catálogo. Na publicação, todo código contribuído é conferido contra os valores do processo.
- O catálogo recusa ciclo entre derivados.

### Fatos de sistema reclassificados e acrescentados

Pelo marco acima, sem publicação que os cite. Como não há produção, os processos de homologação que citem esses fatos são recriados com a forma nova, em vez de preservados:

- o egresso de escola pública deixa de ser declarado e passa a ser **derivado pelo sistema** a partir das duas perguntas da origem escolar, que entram como fatos de sistema declarados (UNI-REQ-0148);
- o endereço de residência e a data de nascimento entram como fatos de sistema declarados;
- a UF e o município de residência entram como fatos **derivados pelo sistema** a partir do endereço, com a fonte `GEO`.

### Proteção dos valores

- **Cifra:** todo valor de fato gravado com classificação `PESSOAL` ou `SENSIVEL` é cifrado, seja resposta, seja derivado.
- **Log:** nenhum valor de fato do candidato é registrado em log, em nenhum domínio e em nenhuma classificação; o log cita só o código do fato.
- **Pendências da execução da inscrição, que ainda não existe:**
  - onde e como os valores são armazenados e cifrados;
  - como a projeção por permissão e a decisão de acesso da ADR-0081 alcançam fatos cadastrados em tempo de execução, cuja classificação e hipótese legal vêm do catálogo, e não de um DTO escrito à mão.

### Emendas

**À ADR-0116, mapa de prefixos do vínculo:**

| Origem | Prefixos aceitos | Referência |
|---|---|---|
| `Declarado` | `CAMPO_FORMULARIO:` | código do próprio fato, coletado por um campo de algum formulário do processo; no processo, cada fato tem um único campo que o produz, em uma única finalidade, e as demais finalidades o reaproveitam (UNI-REQ-0144); substitui `CAMPO_INSCRICAO:`, porque o processo passa a ter um formulário por finalidade |
| `Derivado` | `ATRIBUTO_CANDIDATO:` | atributo calculado por código do sistema |
| `Derivado` | `CLASSIFICACAO:` | resultado da classificação, como o grupo de vagas da convocação (UNI-REQ-0147); só em fato de sistema |
| `Derivado` | `REGRA_DERIVACAO:` | código do próprio fato, cuja regra é copiada do padrão do catálogo para o processo |
| `Derivado` | `AGREGACAO_GRUPO:` | reservado para o agregado sobre a lista de membros de um grupo repetível; só é aceito depois da ADR do grupo repetível (ver a exceção em aberto na governança) |
| `Integracao` | `INTEGRACAO:` | sistema externo |

**À ADR-0116, emenda 1.5:** o derivado por regra também pode ser booleano, com a semântica acima, e o derivado categórico por regra é multivalorado. O recorte pela oferta do item 4 passa a acontecer na cópia da regra padrão para o processo, e não só no último passo da avaliação, porque a regra que contribui código fora dos valores do processo não tem significado nele.

**À ADR-0096:** o endereço do candidato é conferido no servidor contra o Geo, como descrito em "Forma do fato".

**Decisões sucedidas:**

- **ADR-0111:**
  - a governança "seed-governado pelo time de desenvolvimento, não é CRUD administrativo" passa a valer só para os fatos de sistema;
  - a restrição a três domínios deixa de valer: entram texto, data e endereço, com a restrição de citação acima;
  - a distinção entre fato categórico estático e de escopo-processo passa a ser a fonte dos valores;
  - o derivado categórico por regra é multivalorado mesmo com fonte `GLOBAL`; o categórico estático declarado continua podendo ser escalar;
  - a imutabilidade dos eixos segue os marcos acima: cadastro para os fatos do administrador, primeira publicação que cite o fato para os de sistema.
  - O resto continua valendo: a matriz operador × domínio, a imutabilidade do código, a proibição de renomear ou remover valor citado e o congelamento por valor.
- **ADR-0116:**
  - a governança somente por seed passa a valer só para os fatos de sistema;
  - o marco da emenda 1.1 continua valendo nos fatos de sistema, estendido a todos os eixos; o código é imutável desde o cadastro em todo fato, e nos fatos do administrador o marco dos eixos é o cadastro;
  - o mapa de prefixos e a emenda 1.5 são os desta ADR.

## Consequências

### Positivas

- Dado novo no formulário deixa de exigir nova versão do sistema, e o formulário de cada edital é composto pelo administrador.
- O que a lei define continua protegido, e o dado pessoal não entra no edital congelado.
- Todo fato tem classificação de proteção de dados, finalidade e hipótese legal antes de existir resposta.

### Negativas

- O catálogo passa a ter dois regimes de governança no mesmo cadastro, e cada operação precisa saber se o fato é de sistema.
- Um fato de sistema novo cujo código já foi usado pelo administrador faz o deploy falhar até que se escolha outro código.
- O administrador pode cadastrar um fato que nenhum formulário usa. Não é inoperante, porque a resposta tem onde ser guardada, mas é ruído que só a desativação limpa.
- A execução da inscrição passa a guardar respostas de forma genérica, por código do fato, e a proteção por permissão da ADR-0081 fica pendente de um desenho que alcance fatos dinâmicos.

### Neutras

- A troca de `CAMPO_INSCRICAO:` por `CAMPO_FORMULARIO:` alcança o seed, o código que confere o prefixo e as fixtures de envelope, na mesma mudança. Não há resposta de candidato a preservar, porque não há produção.

## Confirmação

- Teste de domínio do fato de sistema: recusa edição fora de nome e descrição, desativação e valor novo ou desativado, em todo fato de sistema, e não só nos normativos.
- Teste de domínio do fato do administrador:
  - recusa troca de qualquer eixo;
  - recusa código reservado a fato de sistema e código repetido, inclusive de fato desativado;
  - recusa origem `Integracao` e vínculo diferente de `REGRA_DERIVACAO:` no derivado (o agregado sobre grupo repetível fica pendente da ADR do grupo);
  - recusa regra configurada de fato de escopo `CANDIDATO` que cite fato de escopo `MEMBRO_GRUPO`;
  - recusa derivado categórico escalar e derivado de domínio diferente de categórico ou booleano;
  - recusa ponto de resolução fora das fases canônicas e derivado resolvido antes de alguma dependência, no cadastro e na edição das regras padrão.
- Teste de domínio da proteção: recusa fato de texto, endereço ou data classificado como `PUBLICO` ou `INTERNO`, salvo o nome social preferido, que o seed classifica como público pela ADR-0082; recusa derivado do administrador mais fraco que alguma dependência; recusa fato sem finalidade ou sem hipótese legal, ou com hipótese legal fora do vocabulário da sua classificação.
- Teste de domínio da desativação: o fato ou valor desativado recusa vínculo novo, e a cópia de modelo ou de regra padrão não o leva; os vínculos existentes são preservados, e o fato ou valor pode ser reativado.
- Teste de domínio das regras: regra que cita fato de texto, de endereço ou de data diretamente é recusada, e os derivados de UF, município e faixa etária são citáveis; vínculo com prefixo incoerente com a origem é recusado; o catálogo recusa ciclo entre derivados; o derivado booleano resolve verdadeiro, falso ou indeterminado como acima.
- Teste da publicação: sobre a regra copiada para o processo, recusa ordem, ciclo, classificação ou código contribuído inválidos; a cópia deixa de fora, e lista, a regra padrão de código fora dos valores do processo ou que cite fato ou valor desativado; alterar o padrão do catálogo depois da cópia não altera o processo; valor desativado depois de citado continua no vocabulário congelado.
- Teste de integração: escrita no catálogo sem o papel `plataforma-admin` é recusada com 403, e o seed dá classificação, finalidade e hipótese legal a todo fato semeado, com a justificativa legal da classificação de cada derivado de sistema.
- Teste das migrations: a que semeia fato de sistema novo falha sem gravar com o código já ocupado, e a que reclassifica fato de sistema falha sem gravar se alguma citação no catálogo deixar de ser válida.
- Teste de log: os componentes que recebem, validam, derivam ou publicam valores de fato registram o código do fato e nunca o valor.

## Prós e contras das opções

### Manter o catálogo fechado e governado por seed

- Bom, porque nenhum fato existe sem revisão de código.
- Ruim, porque trata como inoperante o fato declarado, que tem valor sem código, e deixa cada edital dependente de uma nova versão do sistema.

### Catálogo aberto

- Bom, porque dá ao administrador liberdade total.
- Ruim, porque permitiria alterar o que a lei define e cadastrar derivado de sistema sem o código que calcula o valor — este sim um fato inoperante.

### Catálogo administrável com fatos de sistema protegidos

- Bom, porque abre ao administrador os fatos que têm valor sem código e protege os que dependem de código ou de lei.
- Ruim, porque o cadastro passa a ter dois regimes de governança.

## Mais informações

- **Emenda (03/10/2026, #1726):** as fontes `GEO_UF` e `GEO_MUNICIPIO` servem também ao fato declarado, como a UF do RG e a naturalidade. A UF é enumerada pelo Kernel e congelada; o município é referência fraca ao Geo, listado no cliente entre os da UF respondida antes e conferido no servidor por forma e prefixo do código IBGE — município criado pelo IBGE depois da publicação muda só a listagem. Quando um município declarado decidir elegibilidade, vale a regra do endereço do candidato: conferência contra o Geo no servidor, na execução.
- **Emendada pela ADR-0138:** o agregado sobre grupo repetível é declarado pelo administrador com o vínculo `AGREGACAO_GRUPO:`, o que fecha a exceção em aberto na governança.
- **Emenda (2026-10-10, #1856):** a escala de classificação de proteção de dados citada em "Forma do
  fato" (`PUBLICO`, `INTERNO`, `PESSOAL` ou `SENSIVEL`, na escala da ADR-0081) ganha um quinto nível,
  `IDENTIFICADOR`, entre `PESSOAL` e `SENSIVEL` — o fato que isola, por si só, um indivíduo entre os
  demais (CPF, passaporte, RNM), sem revelar nenhum atributo substantivo sobre ele; `PESSOAL` e
  `SENSIVEL` continuam reservados a fatos que revelam algo sobre o candidato além de diferenciá-lo.

  **A regra de cifra muda de sentido, não só de abrangência.** A regra citada em "Proteção dos
  valores" ("todo valor de fato gravado com classificação `PESSOAL` ou `SENSIVEL` é cifrado") é
  **substituída**: a partir desta emenda, só o fato que **declara** a classificação `IDENTIFICADOR`
  é cifrado — `PESSOAL` e `SENSIVEL` deixam de ser cifrados automaticamente. O motivo é prático:
  cifrar toda a base impediria classificar candidato e gerar relatório sobre cor/raça, deficiência,
  renda e demais fatos pessoais/sensíveis, que são exatamente os que a classificação e a elegibilidade
  precisam ler. A exposição desses fatos continua controlada pela projeção por permissão (ADR-0081) e
  pelo mascaramento em log (`PiiMaskingEnricher`), não pela cifra em repouso. Um derivado ou agregado
  que dependa de um fato `IDENTIFICADOR` não herda a cifra só por essa dependência: a regra de
  "Classificação do derivado do administrador" continua herdando `PESSOAL`/`SENSIVEL` quando alguma
  dependência tiver essa classificação, mas `IDENTIFICADOR` isolado numa dependência não eleva a
  classificação do derivado, porque o derivado deixa de identificar sozinho quem respondeu.

  **Marco de imutabilidade dos eixos do fato de sistema.** A frase acima, em "Identidade e
  imutabilidade" ("eles se tornam imutáveis a partir da primeira publicação de um processo seletivo
  que cite o fato"), é qualificada: essa publicação é a de um ambiente de **produção** — publicação
  em homologação nunca trava eixo nenhum, porque não há dado de homologação que precise ser
  preservado (a mesma lógica que já leva este documento a recriar, em vez de preservar, os processos
  de homologação afetados por uma reclassificação, na seção "Fatos de sistema reclassificados e
  acrescentados"). Até existir a primeira publicação em produção, todo eixo de todo fato de sistema
  continua reclassificável por código, em PR.

  **Controles de produção exigidos porque `PESSOAL` e `SENSIVEL` deixam de ser cifrados em
  repouso** (só `IDENTIFICADOR` continua cifrado): volume do banco e backup cifrados no nível de
  infraestrutura; acesso direto ao banco de produção restrito; relatório e exportação só por visão
  controlada ou réplica, nunca pela tabela base; registro de todo acesso administrativo a inscrição
  identificada; máscara de CPF (`***.999.999-**`) em toda exibição fora do DTO autorizado pela
  permissão (ADR-0081).

  O valor novo do enum `ClassificacaoProtecaoDado.Identificador` e a migração que reclassifica os
  fatos de sistema existentes (CPF, passaporte/RNM) ficam para a issue #1857 — esta emenda registra
  só a decisão. A mesma escala e os mesmos controles de produção são emendados na ADR-0081, que é
  dona da projeção por permissão, não da cifra — a cifra em repouso é desta ADR e da ADR-0121.
- **Emenda (2026-10-10, #1857):** a frase de "Classificação mínima" ("os domínios texto [...],
  endereço e data exigem `PESSOAL` ou `SENSIVEL`") é qualificada: o domínio **texto** passa a
  aceitar também `IDENTIFICADOR` — número de documento é sempre texto, nunca data nem endereço.
  Data e endereço continuam exigindo só `PESSOAL` ou `SENSIVEL`; `IDENTIFICADOR` neles seria
  recusado pelo cadastro, porque nenhum documento de identificação é uma data ou um endereço.
- UNI-REQ-0143 (catálogo de fatos administrável), UNI-REQ-0065 (vocabulário extensível por configuração), UNI-REQ-0074 (estados do fato), UNI-REQ-0075 (derivados pelo sistema), UNI-REQ-0077 (ordem de coleta), UNI-REQ-0144 (formulário por finalidade), UNI-REQ-0145 (regras do item) e UNI-REQ-0148 (origem escolar).
- ADR-0055 (código imutável por invariante), ADR-0056 (leitor cross-módulo), ADR-0061 (cópia por valor), ADR-0081 (classificação e base legal do dado pessoal), ADR-0082 (nome social público quando preferido), ADR-0096 (endereço estruturado), ADR-0111 (vocabulário de fatos), ADR-0116 (origem, ponto de resolução e vínculo), ADR-0122 (desativação prospectiva), ADR-0129 (identidade da origem) e ADR-0135 (projeto compartilhado de regras).
- Lei 13.709/2018 (LGPD), arts. 5º, I e II, 7º e 11.
