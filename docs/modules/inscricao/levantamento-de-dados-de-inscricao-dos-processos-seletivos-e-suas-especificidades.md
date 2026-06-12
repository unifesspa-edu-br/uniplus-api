# Etapas comuns nos Concursos

## Etapas

| # | Etapa | Atividades| 
|---|-------|-----------|
| 1 | Cadastramento do edital: (o sistema deverá permitir o cadastramento de informações gerais para geração do edital) ||
| 2 | Cadastramento do processo no sistema: - CEPS ||
| 3 | Inscrição de Candidatos |<ul><li>```Formulários de Inscrição```</li></ul>|
| 4 | Homologação das inscrições |<ul><li>Análise dos documentos obrigatórios enviados pelos candidatos.</li><li>Marcação de Deferido ou Indeferido, com os motivos do indeferimento, de acordo com os itens específicos do edital (apenas marcar nos itens obrigatórios, indicando a ausência), com campo de observação.</li><li>Geração de relação de candidatos deferidos e outra de indeferidos, com número de inscrição, nome do candidato, status (deferido ou indeferido) e motivo.</li></ul>|
| 5 | Preparação para aplicação das etapas |<ul><li>Ensalamento dos candidatos deferidos.</li><li>Geração de listas de frequência, atas de sala, atas de entrevistas, termo de compromisso…</li><li>Impressão de provas.</li></ul>|
| 6 | Lançamento de notas de cada tipo de fase |OPÇÃO 1 CURSO<br>Lançamento de notas de provas objetivas, prova escrita, entrevista ou análise de histórico, de acordo com as etapas definidas no cadastro do processo seletivo.|
| 7 |Processamento de notas/Classificação dos candidatos e Resultado | <ul><li>Processamento das notas, de acordo com as etapas, pesos e notas mínimas.<li>Classificação das pessoas candidatas de acordo com as modalidades, cotas, parâmetros de desempate e eliminação.</li><li>Geração de listas e relatórios para publicação.</li></ul>
|


## Processos Seletivos

| # | **Titulo** | 
|---|------------|
| 1 | **Seleção para Vagas de Programas Especiais(PSE)** |
| 2 | **Seleção para Vagas Remanescentes** |
| 3 | **Transferência Interna (antigo MobIN)** |
| 4 | **Transferência Externa (antigo MobEX)** |
| 5 | **Vestibular** |
| 6 | **SISU** |
| 7 | **Educação No Campo** |

<br>

## Dados Pessoais Comum a todos os Processos

<br>

|Campo| Descrição | Tipo | Valores| Texto Explicativo|
|-----|-----------|------|--------|------------------|
|01| **Nome** | String |[AZaz] ||
|02|**Deseja Informar o Nome Social**| Bool | [Sim / Nao] ||
|03| **Nome Social** | String |[AZaz] |<div class="alert alert-warning small"><i class="fa fa-exclamation-triangle fa-2x mr-2"></i><span class="font-weight-bold">ATENÇÃO!</span><p>Conforme edital, ficam assegurados às pessoas transexuais e travestis os direitos à-identificação por meio do seu nome social. Entende-se por nome social aquele pelo qual-travestis e transexuais sereconhecem, bem como são identificados por sua comunidade e em seu meio social.</p></div>|
|04| **Data de Nascimento** | Date |[DD/MM/AAAA] ||
|05| **CPF** | String |[0-9] ||
|06| **RG** | String |[0-9] ||
|07| **Sexo Civil** | String | [Mulher / Homem] |O termo sexo civil (também chamado de sexo ou gênero jurídico) é o registro oficial do sexo de uma pessoa perante a lei.|
|08| **RAÇA** | String | Não Informado<br>Amarela<br>Branca<br>Indígena<br>Preta<br>Parda ||
|09|**Você possui alguma necessidade especial?**|Lista Múltipla| - **Sem deficiência**<br> - Cegueira<br> - Baixa Visão<br> - Surdez<br> - Auditiva<br> - Física <br>- Surdocegueira <br>- Intelectual<br> - Autismo<br> - Síndrome de Asperger<br> - Síndrome de RETT <br> - Transtorno desintegrativo da infância<br> - Altas habilidades / Superdotação <br>- Transtorno do espectro autista (TEA) <br>- Visão Monocular||
|10| **Sexo Biológico Cromossômico** | String |Mulher <br> Homem<br>Intersexo|<div class="alert alert-info small"> <i class="fa fa-info-circle fa-2x mr-2"></i> <span class="font-weight-bold">SEXO BIOLOGICO:</span> <hr/> <p>O sexo biológico é considerado pela ciência como o conjunto de informações cromossomiais. Se baseia na identificação genotípica e considera os órgãos sexuais do nascimento, a capacidade de reprodução e as principais características físicas e fisiológicas que diferenciam o masculino do feminino, ou macho da fêmea.</p> </div>|
|11| **Identidade de Gênero** | String |Pessoa Cis<br>Pessoa Transgênero<br>Travesti<br>Transexual<br>Pessoa não-binária<br>Queer<br>**Não Informar** |<div class="alert alert-info small"><i class="fa fa-info-circle fa-2x mr-2"></i><span class="font-weight-bold">IDENTIDADE DE GÊNERO:</span><hr /><p>Identidade de gênero diz respeito à experiência interna e individual relacionada ao gênero com o qual a pessoa se identifica. A identidade de gênero não está necessariamente relacionada com características biológicas tipicamente atribuídas aos sexos masculino e feminino.</p><ul> <li><strong>1 - Pessoa cis </strong> é aquela que nasceu com sexo biológico feminino e se identifica como mulher. Ou que nasceu com o sexo biológico masculino e se identifica como homem. </li><li><strong>2 - Pessoa Transgênero </strong> é a pessoa cuja identidade de gênero difere em diversos graus do sexo biológico.</li><li><strong>3 - Travesti </strong> corresponde a pessoa do sexo masculino que usa roupas e adota formas de expressão de gênero feminino, mas que não necessariamente deseja mudar suas características primárias.</li><li><strong>4 - Transexual </strong> é a pessoa que busca ou passa por uma transição social que pode incluir a transição por tratamentos hormonais ou cirúrgicos a fim de se assemelhar com sua identidade de gênero.</li><li><strong>5 - Pessoa não-binária </strong> é aquela que não se reconhece nem como homem, nem como mulher.</li></ul></div>|
|12| **Orientação Sexual** | String |Heterosexual<br>Homossexual<br>Bisexual<br>Assexual<br>Pansexual<br>**Não Informar** |<div class="alert alert-info small"><i class="fa fa-info-circle fa-2x mr-2"></i><span class="font-weight-bold">ORIENTAÇÃO SEXUAL:</span><hr /><p>Diz respeito à atração que se sente por outros indivíduos. Ela geralmente também envolve questões sentimentais, e não somente sexuais.</p><ul><li><strong>1 - Heterossexual</strong> é a pessoa que se sente atraída e se relaciona com pessoas do sexo oposto.</li> <li><strong>2 - Homossexual </strong> é a pessoa que se sente atraída e se relaciona com pessoas do mesmo sexo.</li> <li><strong>3 - Bisexual </strong> é a pessoa que se sente atraída e se relaciona com pessoas de ambos os sexos. </li> <li><strong>4 - Assexual </strong> é a pessoa que não se sente atraída ou não se relaciona sexualmente.</li> <li><strong>5 - Pansexual </strong> é a pessoa que se sente atraída e se relaciona com pessoas, independente do sexo ou identidade de gênero.</li></ul></div>|
|-----|-----------|------|--------|------------------|
|13| **Telefone** | String |^\(\d{2}\) \d{4,5}-\d{4}$ ||
|14| **E-mail** | String |^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$ ||
|-----|-----------|------|--------|------------------|
|15| **Estado** | String |[AZaz] ||
|16| **Cidade** | String |[AZaz] ||
|17| **Tipo Endereço** | String |URBANO<br>RURAL<br>Aldeia<br>Comunidade<br>Quilombo<br>Vila<br>Outro ||
|18| **Etnia/Comunidade<br>(```se campo 17 = Aldeia OU Comunidade OU Quilombo```)** | String |<table class="table table-sm table-bordered mt-3"><thead><tr><th class="text-center">Se for indígena</th></tr></tr></thead><tbody><tr><td style="vertical-align: top;">Amanayé<br>Anambé<br>Aparai<br>Apiaká<br>Arapiuns<br>Arara<br>Arara da Volta Grande<br>Arara Vermelha<br>Araweté<br>Asurini do Tocantins<br>Asurini do Xingu<br>Atikun<br>Awaeté-Parakanã<br>Borari<br>Cara Preta<br>Galibi-Marworno<br>Gavião Akrãtikatêjê<br>Gavião Kyikatêjê<br>Gavião Parkatêjê<br>Guajajara<br>Guarani<br>Guarani-Mbya<br>Hixkaryana<br>Jaraqui<br>Karajá<br>Katxuyana<br>Kayapó Mebêngôkre<br>Kayapó Xikrin<br>Kraô<br>Kuruaya<br>Munduruku<br>Panará<br>Suruí-Aikewara<br>Tapajó<br>Tembé<br>Ticuna<br>Tiriyó<br>Tunayana<br>Tupaiú<br>Turiwara<br>Waiwai<br>Wajãpi<br>Warao<br>Wayana<br>Xikrin<br>Xipaya<br>Yanomami<br>Zo’e<br><strong>Não encontrei minha etnia/comunidade</strong></td></tr><tr><th class="text-center">Se selecionado quilombola</th><tr><td style="vertical-align: top;">Quilombo Araquembaua<br>Quilombo Baixo Jambuaçu<br>Quilombo Carará<br>Quilombo Comunidade Porto Alegre<br>Quilombo Cupu<br>Quilombo de Anilzinho<br>Quilombo de Calados<br>Quilombo de Engenho Mararia<br>Quilombo de Fugido<br>Quilombo do Engenho<br>Quilombo Igarapé Preto<br>Quilombo Joana Peres<br>Quilombo Remanescentes de quilombo de Varginha<br>Quilombo Santa Luzia do Traquateua<br>Quilombo Teófilo<br>Quilombo Umarizal Beira<br>Quilombo Vila Nova Jutaí<br>Quilombo Santa Maria do Traquateua<br><strong>Não encontrei minha etnia/comunidade</strong></td></tr></tbody></table>|**Não encontrei minha etnia/comunidade**:<br>**```Se esta opção for marcada deve ter a opção de inserção```**|
|-----|-----------|------|--------|------------------|
|19| **Local de Prova** | String |[AZaz] ||
|20| **Necessita Atendimento Especial?** |String |**Não Necessito<br>Prova Ampliada<br>Tempo Adicional ( Até 1 hora)<br>Prova em Braile<br>Intérprete de Língua de Sinais<br>Ledor<br>Lactante<br>Transcritor**||
|-----|-----------|------|--------|------------------|
|21| **Curso Pretendido (1ª Opção)** | String |[AZaz] ||
|22| **Segunda Opção de Curso<br>```Apenas se o edital tiver previsão de SEGUNDA opção```** | String |[AZaz] ||
|23| **Deseja participar da Lista de Espera?** | String |**Não Participar da Lista de Espera<br>1ª Opção<br>2ª Opção(```Apenas se o edital tiver previsão de SEGUNDA opção```)** ||

<br>

## Seleção de Vagas Remanescentes - PSVR

<br>

|Campo| Descrição | Tipo | Valores| Texto Explicativo|
|-----|-----------|------|--------|------------------|
|PSVR-24|**Sua nota do ENEM é referente ao ano de**|Inteiro|**[Anos Válidos]**|<div class="alert alert-warning small"><div class="mb-1"><i class="fa fa-exclamation-triangle fa-2x mr-2"></i><span class="font-weight-bold ">LEIA COM ATENÇÃO!</span></div><hr/><ul><li><p>Selecione o ano que você fez a prova do ENEM. Tenha atenção ao selecionar, pois você concorrerá com a nota correspondente ao ano selecionado.</p></li><li><p>Caso você informe o ano de referência do ENEM errado, inviabilizará o resgate de suas notas, resultando em sua eliminação no processo.</p></li></ul></div>|
|-----|**Perfil Social / Modalidades**|------|--------|----------------|
|PSVR-25|**Deseja concorrer as vagas destinadas a PCD<br>(```se campo 09 NÃO CONTER "Sem deficiência"```)**|Bool |**[Sim / Não]**|Vagas previstas na Lei A Lei nº 13.146, de 6 de julho de 2015 e na Resolução Nº 532/2021 CONSEPE/Unifesspa|
|PSVR-26|**Você estudou, durante todo o ensino médio, no Brasil, em escola pública ou em escola comunitária que atua no âmbito da educação do campo conveniada com o poder público?**|Bool |**[Sim / Não]**||
|PSVR-27|**Você deseja concorrer às vagas destinadas às pessoas candidatas que cursaram todo o ensino médio, exclusivamente, em escolas públicas brasileiras da rede municipal, estadual ou federal ou em escolas comunitárias que atuam no âmbito da educação do campo conveniadas com o poder público?<br>(```se campo 26 = "Sim"```)**|Bool |**[Sim / Não]**||
|PSVR-28|**Você Deseja concorrer às vagas destinadas às pessoas candidatas autodeclaradas pretas, pardas ou indígenas?<br>(```se campo 08 = "Indígena OU Preta OU Parda"```)**|Bool |**[Sim / Não]**||
|PSVR-29|**Você deseja concorrer às vagas reservadas às pessoas originárias de comunidades quilombolas??<br>(```se campo 17 = "Quilombo"```)**|Bool |**[Sim / Não]**||
|PSVR-30|**Você e sua família possuem renda mensal bruta por pessoa (antes dos descontos) menor ou igual a um salário mínimo?**|Bool |**[Sim / Não]**||
|PSVR-31|**Deseja concorrer às vagas reservadas às pessoas com renda mensal bruta por pessoa (antes dos descontos) menor ou igual a um salário mínimo?<br>(```se campo 30 = "Sim"```)**|Bool|**[Sim / Não]**||
|-----|**Especificidade PSVR**|------|--------|----------------|
|PSVR-32|Sua renda individual (Renda Bruta)|Numérico|||
|PSVR-33|Grupo Familiar| String ||<table><thead><tr><th>CPF</th><th>Grau de Parentesco</th><th>Nome</th><th>Renda Individual</th><th>Ações</th></tr></thead></table>|


<br>

## Seleção para Vagas de Programas Especiais (PSE)

<br>

|Campo| Descrição | Tipo | Valores| Texto Explicativo|
|-----|-----------|------|--------|------------------|
|PSE-24| **Curso (exceto PSE Indigena e Quilombola segunda opção)** | String |**[AZaz]**||
|PSE-25| **Modalidades** | String |**[AZaz]** ||
|PSE-26| **Anexos da Inscrição** | Blob |**[Arquivos]** |<div class="alert alert-info small"><i class="fa fa-comment  fa-2x"></i><span class="font-weight-bold m-2">ATENÇÃO!</span><p>Antes de inserir anexos, verifique se os mesmos estão em conformidade com as orientações-do edital do processo Clique no botão abaixo para acessar o edital ou a página do CEPS.</p><div class=" w-100 text-center"><a class="btn-sm btn btn-info" target="_blank" href="#"> Abrir Edital</a> / <a class="btn-sm btn btn-info" target="_blank" href="https://ceps.unifesspa.edu.br">Página do CEPS</a></div></div>|
|PSE-27| **Deseja optar pela utilização do bônus?** | Bool |**[Sim / Não]** |<div class="alert alert-warning small"><i class="fa fa-info-circle fa-2x mr-2"></i><span class="font-weight-bold">LEIA COM ATENÇÃO!</span><hr /><p>Os candidatos que optarem pela utilização do bônus deverão estar cientes e cumprir todas as regras para sua concessão, conforme determina o edital deste processo de seleção e deverão, obrigatoriamente, apresentar toda a documentação comprobatória para concessão do bônus.Os candidatos que solicitarem o bônus e não comprovarem o direito, no período da habilitação, serão eliminados sumariamente.</p>-</div>|
|PSE-28| **Autodenominação** | String |**Agricultores familiares<br>Extrativistas<br>Pescadores artesanais<br>Ribeirinhos<br>Assentados da Reforma Agrária<br>Quilombolas<br>Caiçaras<br>Indígenas<br>Quebradeiras de côco babaçu<br>Outros povos tradicionais<br>Acampados da Reforma Agrária<br>** ||


<br>

## Vestibular

<br>

|Campo| Descrição | Tipo | Valores| Texto Explicativo|
|-----|-----------|------|--------|------------------|
|Vestibular-24| **Modalidades** | String |**[AZaz]** |


<br>

## SISU

<br>

|Campo| Descrição | Tipo | Valores| Texto Explicativo|
|-----|-----------|------|--------|------------------|
|SISU-24| **Confirmação de Interesse** |String |**[AZaz]**|
|-----|**Perfil Social / Modalidades**|------|--------|----------------|
|SISU-25|**Deseja concorrer as vagas destinadas a PCD<br>(```se campo 09 NÃO CONTER "Sem deficiência"```)**|Bool |**[Sim / Não]**|Vagas previstas na Lei A Lei nº 13.146, de 6 de julho de 2015 e na Resolução Nº 532/2021 CONSEPE/Unifesspa|
|SISU-26|**Você estudou, durante todo o ensino médio, no Brasil, em escola pública ou em escola comunitária que atua no âmbito da educação do campo conveniada com o poder público?**|Bool |**[Sim / Não]**||
|SISU-27|**Você deseja concorrer às vagas destinadas às pessoas candidatas que cursaram todo o ensino médio, exclusivamente, em escolas públicas brasileiras da rede municipal, estadual ou federal ou em escolas comunitárias que atuam no âmbito da educação do campo conveniadas com o poder público?<br>(```se campo 26 = "Sim"```)**|Bool |**[Sim / Não]**||
|SISU-28|**Você Deseja concorrer às vagas destinadas às pessoas candidatas autodeclaradas pretas, pardas ou indígenas?<br>(```se campo 08 = "Indígena OU Preta OU Parda"```)**|Bool |**[Sim / Não]**||
|SISU-29|**Você deseja concorrer às vagas reservadas às pessoas originárias de comunidades quilombolas??<br>(```se campo 17 = "Aldeia OU Comunidade OU Quilombo"```)**|Bool |**[Sim / Não]**||
|SISU-30|**Você e sua família possuem renda mensal bruta por pessoa (antes dos descontos) menor ou igual a um salário mínimo?**|Bool |**[Sim / Não]**||
|SISU-31|**Deseja concorrer às vagas reservadas às pessoas com renda mensal bruta por pessoa (antes dos descontos) menor ou igual a um salário mínimo?<br>(```se campo 30 = "Sim"```)**|Bool|**[Sim / Não]**||
|-----|-----------|------|--------|------------------|
|SISU-25|Sua renda individual (Renda Bruta)|Numérico|||
|SISU-26|Outro(s) membros (s) familiar (es)| String ||<table><thead><tr><th>ID</th><th>CPF</th><th>Grau de Parentesco</th><th>Nome</th><th>Renda Individual</th><th>Ações</th></tr></thead></table>|
|SISU-27|Total de pessoas em sua família|Numérico|||
|SISU-28|Renda mensal total da sua família|Numérico|||
|SISU-29|Renda mensal por pessoa|Numérico|```Campo Calculável```|Para concorrer a cotas por renda, você e sua família tem que receber, no máximo, um salário mínimo (R4 1.518,00) por pessoa.|


<br>

## Educação no Campo(EC)

<br>

- ```Não possui segunda opção de Curso```

|Campo| Descrição | Tipo | Valores| Texto Explicativo |
|-----|-----------|------|--------|-------------------|
|EC-24| **Confirmação de Interesse** | String |**[AZaz]**|
|EC-25| **Modalidades** | String |**§ Ampla Concorrência<br>§ Pessoa Candidata com Deficiência (Resolução nº 64/2015 Consepe/Unifesspa)**|
|EC-26| **Autodenominação** | String |**Agricultores familiares<br>Extrativistas<br>Pescadores artesanais<br>Ribeirinhos<br>Assentados da Reforma Agrária<br>Quilombolas<br>Caiçaras<br>Indígenas<br>Quebradeiras de côco babaçu<br>Outros povos tradicionais<br>Acampados da Reforma Agrária<br>** ||
|EC-27| **Anexos da Inscrição** | Blob |**Arquivos**||

<br>

## Transferência Interna(TI)

<br>

- ```Não possui segunda opção de Curso```

|Campo| Descrição | Tipo | Valores| Texto Explicativo|
|-----|-----------|------|--------|------------------|
|24|**Histórico Escolar Graduação**|Blob|**Arquivos**|


<br>

## Transferência Externa(TE)

<br>

- ```Não possui segunda opção de Curso```


|Campo| Descrição | Tipo | Valores| Texto Explicativo|
|-----|-----------|------|--------|------------------|
|TE-24| **Instituição de Origem** | String |**[AZaz]** |
|TE-25| **Curso de Origem** | String |**[AZaz]** |
|TE-26| **CRG** | String |**[AZaz]** |
|TE-27| **Histórico Escolar ( GRADUAÇÃO )** | String |<table><thead><tr><th>ID</th><th>Período</th><th>Disciplina</th><th>Conceito</th></tr></thead></table>|<table><thead><tr><th>ID</th><th>Período</th><th>Disciplina</th><th>Conceito</th><th>Ações</th></tr></thead><tbody><tr><td>1</td><td>1º</td><td>Algoritmos</td><td>Excelente</td><td><button class="btn btn-sm btn-danger">remover</button><br><button class="btn btn-sm btn-danger">corrigir</button></td></tr><tr><td>2</td><td>2º</td><td>Banco de Dados</td><td>Bom</td><td><button class="btn btn-sm btn-danger">remover</button><br><button class="btn btn-sm btn-danger">corrigir</button></td></tr></tbody></table>|
|TE-28| **Concluiu o Curso?** | Bool |**[Sim OU Não]** |
|TE-29| **Histórico Escolar da Graduação** | Blob |**[Arquivo]** |
|TE-30| **Matriz Curricular da Graduação** | Blob |**[Arquivo]** |


## Confirmação da Inscrição

Após a conclusão do formulário:

1. O sistema valida os campos obrigatórios.
2. O sistema gera número de inscrição.
3. O sistema registra data e hora da inscrição.
4. O sistema envia comprovante por e-mail.
5. O candidato pode emitir o comprovante em PDF.
6. O status inicial da inscrição será "Aguardando Homologação".