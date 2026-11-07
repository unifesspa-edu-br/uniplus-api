namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Fonte única do seed do catálogo <c>rol_de_fatos_candidato</c> (UNI-REQ-0077,
/// ADR-0111, refinada pela ADR-0116; ampliada pela UNI-REQ-0078 e pela ADR-0136): os
/// fatos do vocabulário fechado do candidato. Consumida tanto pela configuração EF Core (que materializa as linhas
/// via <c>HasData</c> na migration) quanto pelos testes (que conferem o seed do
/// banco contra esta lista), garantindo uma única definição por fato.
/// </summary>
/// <remarks>
/// <para>
/// O conteúdo é a modelagem da ADR-0111/ADR-0116 (autoridade), portada linha a
/// linha. Os <see cref="Guid"/> são fixos determinísticos (não
/// <c>Guid.CreateVersion7</c>) porque seed precisa de identidade estável entre
/// execuções — o mesmo molde de <c>RegraCatalogoSeed</c>.
/// </para>
/// <para>
/// Todos são fatos de <b>sistema</b>: só nome, descrição, componente de exibição e proteção de dados são editáveis (ADR-0136). Todos trazem a descrição que explica o dado a quem analisa a inscrição. São do escopo
/// do candidato, exceto maior de idade, sem renda e sob guarda, que são campos de membro de um
/// grupo repetível do formulário, como a composição familiar (ADR-0138). A fonte dos valores do categórico está em <see cref="FatoCandidatoSeedItem.FonteValores"/>;
/// os valores do categórico global são os <see cref="Domain.Entities.FatoValorDominio"/> de
/// <see cref="FatoValorDominioSeed"/>. Cor ou raça, autodeclaração quilombola e indígena, deficiência,
/// seu tipo e atendimento especializado, que dizem a condição da pessoa, são
/// <see cref="ClassificacaoDeProtecao.Sensivel"/>. A modalidade e as opções de concorrer são
/// <see cref="ClassificacaoDeProtecao.Interno"/> (ADR-0154): dizem em que reserva a pessoa concorre, o que
/// o resultado publica por obrigação legal, e não qual é a condição dela, que só a homologação confirma.
/// Os demais são <see cref="ClassificacaoDeProtecao.Pessoal"/>, ou a classificação própria do dado.
/// </para>
/// <para>
/// <see cref="OrigemFato"/> (ADR-0116): <c>FAIXA_ETARIA</c> e <c>RENDA_PER_CAPITA</c>
/// (computados de atributo do candidato), <c>MODALIDADE</c>, <c>EGRESSO_ESCOLA_PUBLICA</c> e
/// <c>OPTOU_PELAS_COTAS_DA_LEI</c> (derivados das regras congeladas do processo), <c>MODALIDADE_SEGUNDA_OPCAO</c> (a modalidade na 2ª opção de curso, calculada pelo sistema) e <c>MODALIDADE_CONVOCACAO</c> (resultado da classificação) são
/// <see cref="OrigemFato.Derivado"/>; todos os demais são
/// <see cref="OrigemFato.Declarado"/> (resposta/seleção direta do candidato), inclusive
/// os cinco opt-ins <c>CONCORRER_*</c> — que são seleção direta, ainda que expressem
/// vontade e não afirmação de elegibilidade. <see cref="OrigemFato.Integracao"/> fica
/// reservada, sem fato semeado (fonte externa futura, ex.: SIGAA #874).
/// </para>
/// <para>
/// <c>BAIXA_RENDA</c> <b>não</b> substitui <c>RENDA_PER_CAPITA</c>: são fatos distintos e
/// coexistem. O primeiro é a autodeclaração booleana feita na inscrição (item 5.1 do
/// formulário de cotas); o segundo é o valor numérico derivado, cuja comprovação ocorre em
/// fase posterior.
/// </para>
/// <para>
/// Os fatos semeados resolvem em <c>PontoResolucao = "INSCRICAO"</c> — são
/// respondidos/derivados no cadastro de inscrição do candidato —, exceto
/// <c>MODALIDADE_CONVOCACAO</c>, que só existe depois do resultado final, e a renda declarada no
/// pedido de isenção da taxa, que resolve na solicitação de isenção.
/// </para>
/// </remarks>
public static class FatoCandidatoSeed
{
    private const string PontoResolucaoInscricao = "INSCRICAO";
    private const string PontoResolucaoResultadoFinal = "RESULTADO_FINAL";

    /// <summary>
    /// Todo fato semeado trata o dado por cumprimento de obrigação legal: a Lei nº 12.711/2012 e
    /// o edital regem cada um deles (LGPD, art. 7º, II, e art. 11, II, a).
    /// </summary>
    public const HipoteseLegalTratamento HipoteseLegal = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    internal const string FinalidadeReservaDeVagas =
        "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.";

    internal const string FinalidadeAtendimento =
        "Oferta de atendimento especializado ao candidato na realização das etapas do processo seletivo.";

    internal const string FinalidadeResidencia =
        "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.";

    internal const string FinalidadeComposicaoFamiliar =
        "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.";

    internal const string FinalidadeRequisitos =
        "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.";

    internal const string FinalidadeIsencaoDaTaxa =
        "Análise do pedido de isenção da taxa de inscrição do processo seletivo.";

    private const string FinalidadeIdentificacao =
        "Identificação do candidato no processo seletivo.";

    private const string FinalidadeComunicacao =
        "Comunicação com o candidato sobre o processo seletivo.";

    internal const string FinalidadeImportacaoDeNotasDoEnem =
        "Importação das notas do ENEM da edição escolhida pelo candidato, na classificação do processo seletivo.";

    // Prefixo determinístico próprio do catálogo de fatos (distinto do
    // rol_de_regras, para não confundir identidades entre tabelas).
    private static Guid SeedId(int n) =>
        Guid.Parse($"fa700000-0000-7000-8000-{n:D12}");

    /// <summary>Os fatos de sistema, na ordem canônica de semeadura.</summary>
    public static IReadOnlyList<FatoCandidatoSeedItem> Itens { get; } =
    [
        new(SeedId(1), "COR_RACA", "Cor ou raça", "Cor ou raça que o candidato declara. É o dado de que depende a concorrência às vagas reservadas a pretos, pardos e indígenas.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:COR_RACA",
            ClassificacaoDeProtecao.Sensivel, FinalidadeReservaDeVagas),

        new(SeedId(2), "QUILOMBOLA", "Quilombola", "Se o candidato se declara quilombola. É o dado de que depende a concorrência às vagas reservadas a quilombolas.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:QUILOMBOLA",
            ClassificacaoDeProtecao.Sensivel, FinalidadeReservaDeVagas),

        new(SeedId(3), "PCD", "Pessoa com deficiência", "Se o candidato declara ser pessoa com deficiência. É o dado de que depende a concorrência às vagas reservadas a pessoas com deficiência.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:PCD",
            ClassificacaoDeProtecao.Sensivel, FinalidadeReservaDeVagas),

        // O egresso de escola pública é derivado das duas perguntas da origem escolar, com a regra das
        // cotas da Lei nº 12.711/2012 (UNI-REQ-0148) como regra padrão, que o processo copia.
        new(SeedId(4), OrigemEscolar.FatoEgresso, "Egresso de escola pública", "Se o candidato cursou o ensino médio em escola pública. O sistema o calcula a partir das duas respostas sobre a origem escolar, pela regra da Lei nº 12.711/2012.",
            DominioFato.Booleano, OrigemFato.Derivado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, $"REGRA_DERIVACAO:{OrigemEscolar.FatoEgresso}",
            ClassificacaoDeProtecao.Pessoal, FinalidadeReservaDeVagas, RegrasPadrao: OrigemEscolar.RegrasDoEgresso),

        new(SeedId(5), "RENDA_PER_CAPITA", "Renda familiar per capita", "Renda mensal da família do candidato dividida pelo número de integrantes da composição familiar, em reais. Define o enquadramento nas vagas reservadas por renda.",
            DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA",
            ClassificacaoDeProtecao.Pessoal, FinalidadeReservaDeVagas, ComponenteExibicao: ComponenteDeExibicao.Moeda),

        new(SeedId(6), "FAIXA_ETARIA", "Idade", "Idade em anos completos do candidato na data de referência dos fatos do processo, calculada pelo sistema a partir da data de nascimento, para verificar os requisitos de participação.",
            DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "ATRIBUTO_CANDIDATO:FAIXA_ETARIA",
            ClassificacaoDeProtecao.Pessoal, FinalidadeRequisitos),

        new(SeedId(7), "SEXO", "Sexo biológico", "Sexo biológico do candidato, conforme ele declara na inscrição.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:SEXO",
            ClassificacaoDeProtecao.Pessoal, FinalidadeRequisitos),

        // MODALIDADE é derivado, não declarado: o candidato declara fatos e opt-ins, e o conjunto
        // de modalidades resulta da avaliação deles contra as regras congeladas do processo. O
        // binding referencia a regra de derivação — o catálogo diz o mecanismo, a config do edital
        // diz o conteúdo (ADR-0116, emenda de 2026-07-22).
        new(SeedId(8), ModalidadeDoCandidato.Fato, "Modalidade de concorrência", "Modalidades de concorrência em que o candidato concorre, resultantes das regras de reserva de vagas do processo aplicadas às respostas dele.",
            DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Multivalorado, FonteValoresFato.Modalidade,
            PontoResolucaoInscricao, $"REGRA_DERIVACAO:{ModalidadeDoCandidato.Fato}",
            ClassificacaoDeProtecao.Interno, FinalidadeReservaDeVagas),

        new(SeedId(9), "CONDICAO_ATENDIMENTO", "Condição de atendimento especializado", "Condições do candidato pelas quais ele pede atendimento especializado nas etapas do processo.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Multivalorado, FonteValoresFato.Processo,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:CONDICAO_ATENDIMENTO",
            ClassificacaoDeProtecao.Sensivel, FinalidadeAtendimento),

        new(SeedId(10), "NACIONALIDADE", "Nacionalidade", "Nacionalidade do candidato, que define os documentos de identificação que ele informa.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:NACIONALIDADE",
            ClassificacaoDeProtecao.Pessoal, FinalidadeRequisitos),

        new(SeedId(11), "TIPO_DEFICIENCIA", "Tipo de deficiência", "Tipo de deficiência do candidato com deficiência, entre os do cadastro de tipos de deficiência.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.TiposDeficiencia,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:TIPO_DEFICIENCIA",
            ClassificacaoDeProtecao.Sensivel, FinalidadeReservaDeVagas),

        // ── Pares elegibilidade + opt-in do formulário de cotas (UNI-REQ-0078) ──
        // A elegibilidade dos quatro blocos já está semeada acima e é REUTILIZADA:
        // COR_RACA (PPI), QUILOMBOLA, PCD e EGRESSO_ESCOLA_PUBLICA. Falta a quinta
        // elegibilidade — a autodeclaração de renda — e os cinco opt-ins.

        new(SeedId(12), "BAIXA_RENDA", "Renda familiar per capita igual ou inferior a um salário mínimo", "Autodeclaração de que a renda familiar per capita é igual ou inferior a um salário mínimo, feita na inscrição para as vagas reservadas por renda.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:BAIXA_RENDA",
            ClassificacaoDeProtecao.Pessoal, FinalidadeReservaDeVagas),

        new(SeedId(13), "CONCORRER_PCD", "Deseja concorrer às vagas reservadas a pessoas com deficiência", "Opção do candidato por concorrer às vagas reservadas a pessoas com deficiência.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:CONCORRER_PCD",
            ClassificacaoDeProtecao.Interno, FinalidadeReservaDeVagas),

        new(SeedId(14), "CONCORRER_EP", "Deseja concorrer às vagas reservadas a egressos de escola pública", "Opção do candidato por concorrer às vagas reservadas a egressos de escola pública.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:CONCORRER_EP",
            ClassificacaoDeProtecao.Interno, FinalidadeReservaDeVagas),

        new(SeedId(15), "CONCORRER_PPI", "Deseja concorrer às vagas reservadas a pretos, pardos e indígenas", "Opção do candidato por concorrer às vagas reservadas a pretos, pardos e indígenas.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:CONCORRER_PPI",
            ClassificacaoDeProtecao.Interno, FinalidadeReservaDeVagas),

        new(SeedId(16), "CONCORRER_Q", "Deseja concorrer às vagas reservadas a quilombolas", "Opção do candidato por concorrer às vagas reservadas a quilombolas.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:CONCORRER_Q",
            ClassificacaoDeProtecao.Interno, FinalidadeReservaDeVagas),

        new(SeedId(17), "CONCORRER_RENDA", "Deseja concorrer às vagas reservadas por renda familiar per capita", "Opção do candidato por concorrer às vagas reservadas por renda familiar per capita.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:CONCORRER_RENDA",
            ClassificacaoDeProtecao.Interno, FinalidadeReservaDeVagas),

        // ── Residência e nascimento (ADR-0136) ──
        // O endereço e a data de nascimento são declarados; a UF e o município de residência
        // derivam do endereço e são os que uma regra pode citar — o endereço e a data, não.
        new(SeedId(18), "ENDERECO_RESIDENCIAL", "Endereço residencial", "Endereço em que o candidato reside, usado para verificar os requisitos regionais do processo.",
            DominioFato.Endereco, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:ENDERECO_RESIDENCIAL",
            ClassificacaoDeProtecao.Pessoal, FinalidadeResidencia),

        new(SeedId(19), "DATA_NASCIMENTO", "Data de nascimento", "Data de nascimento do candidato, usada para verificar a idade e os requisitos de participação.",
            DominioFato.Data, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:DATA_NASCIMENTO",
            ClassificacaoDeProtecao.Pessoal, FinalidadeRequisitos),

        new(SeedId(20), "UF_RESIDENCIA", "UF de residência", "Unidade da Federação em que o candidato reside, derivada do endereço residencial.",
            DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Escalar, FonteValoresFato.GeoUf,
            PontoResolucaoInscricao, "ATRIBUTO_CANDIDATO:UF_RESIDENCIA",
            ClassificacaoDeProtecao.Pessoal, FinalidadeResidencia),

        new(SeedId(21), "MUNICIPIO_RESIDENCIA", "Município de residência", "Município em que o candidato reside, derivado do endereço residencial.",
            DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Escalar, FonteValoresFato.GeoMunicipio,
            PontoResolucaoInscricao, "ATRIBUTO_CANDIDATO:MUNICIPIO_RESIDENCIA",
            ClassificacaoDeProtecao.Pessoal, FinalidadeResidencia),

        // O grupo de vagas em que o candidato foi convocado, e não as modalidades a que concorreu:
        // os documentos da habilitação seguem a convocação (UNI-REQ-0147). A classificação o produz
        // e ele só é conhecido depois do resultado final.
        new(SeedId(22), "MODALIDADE_CONVOCACAO", "Modalidade da convocação", "Grupo de vagas em que o candidato foi convocado, conhecido só depois do resultado final. Os documentos da habilitação seguem a convocação.",
            DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Escalar, FonteValoresFato.Modalidade,
            PontoResolucaoResultadoFinal, "CLASSIFICACAO:MODALIDADE_CONVOCACAO",
            ClassificacaoDeProtecao.Interno, FinalidadeReservaDeVagas),

        // Campos de membro da composição familiar: a exigência repetida por membro cita cada um
        // na ocorrência do grupo, como a certidão pedida só do membro sob guarda.
        new(SeedId(23), "MAIOR_IDADE", "Maior de idade", "Se o membro da composição familiar é maior de idade.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:MAIOR_IDADE",
            ClassificacaoDeProtecao.Pessoal, FinalidadeComposicaoFamiliar, EscopoFato.MembroGrupo),

        new(SeedId(24), "SEM_RENDA", "Sem renda", "Se o membro da composição familiar não tem renda.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:SEM_RENDA",
            ClassificacaoDeProtecao.Pessoal, FinalidadeComposicaoFamiliar, EscopoFato.MembroGrupo),

        new(SeedId(25), "SOB_GUARDA", "Sob guarda", "Se o membro da composição familiar está sob guarda.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:SOB_GUARDA",
            ClassificacaoDeProtecao.Pessoal, FinalidadeComposicaoFamiliar, EscopoFato.MembroGrupo),

        // O parentesco de cada membro identifica, no grupo que inclui o candidato, a ocorrência
        // dele próprio.
        new(SeedId(26), CandidatoComoMembro.FatoParentesco, "Parentesco", "Parentesco do membro da composição familiar com o candidato.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, $"CAMPO_FORMULARIO:{CandidatoComoMembro.FatoParentesco}",
            ClassificacaoDeProtecao.Pessoal, FinalidadeComposicaoFamiliar, EscopoFato.MembroGrupo),

        // O conjunto básico que todo formulário de inscrição coleta: a identificação, a filiação e
        // os contatos do candidato. A UF do RG e a naturalidade têm as UFs e os municípios do Geo
        // como opções; o órgão emissor do RG, uma lista fechada, e o nome do órgão que falta nela. O
        // nome social é pessoal na consulta administrativa, como os demais (ADR-0082, ADR-0153).
        // O nome civil vem do login (gov.br) e é igual ao do token: não passa por regra de formato.
        DadoBasico(27, "NOME", "Nome", "Nome civil do candidato, como vem do login gov.br.", DominioFato.Texto, FinalidadeIdentificacao, formato: FormatoTexto.Livre),
        DadoBasico(28, "DESEJA_NOME_SOCIAL", "Deseja usar nome social", "Se o candidato deseja ser identificado pelo nome social no processo seletivo.", DominioFato.Booleano, FinalidadeIdentificacao),
        DadoBasico(29, FatoCandidato.CodigoDoNomeSocial, "Nome social", "Nome pelo qual o candidato prefere ser identificado e que aparece no lugar do nome civil.", DominioFato.Texto, FinalidadeIdentificacao,
            formato: FormatoTexto.NomePessoa),
        DadoBasico(30, "CPF", "CPF", "Número do CPF do candidato.", DominioFato.Texto, FinalidadeIdentificacao, formato: FormatoTexto.Cpf,
            classificacao: ClassificacaoDeProtecao.Identificador),
        DadoBasico(31, "RG_NUMERO", "Número do RG", "Número do RG do candidato.", DominioFato.Texto, FinalidadeIdentificacao, formato: FormatoTexto.Livre,
            classificacao: ClassificacaoDeProtecao.Identificador),
        DadoBasico(32, "RG_ORGAO_EMISSOR", "Órgão emissor do RG", "Órgão que emitiu o RG do candidato, entre os de uma lista fechada.", DominioFato.Categorico, FinalidadeIdentificacao, fonte: FonteValoresFato.Global),
        DadoBasico(56, "RG_ORGAO_EMISSOR_OUTRO", "Nome do órgão emissor do RG", "O órgão emissor do RG que não está na lista.", DominioFato.Texto, FinalidadeIdentificacao,
            formato: FormatoTexto.Livre),
        DadoBasico(33, "RG_DATA_EMISSAO", "Data de emissão do RG", "Data em que o RG do candidato foi emitido.", DominioFato.Data, FinalidadeIdentificacao),
        DadoBasico(51, "PASSAPORTE_NUMERO", "Número do passaporte", "Número do passaporte do candidato.", DominioFato.Texto, FinalidadeIdentificacao,
            formato: FormatoTexto.Livre, classificacao: ClassificacaoDeProtecao.Identificador),
        DadoBasico(52, "RNM_NUMERO", "Número do Registro Nacional Migratório (RNM)", "Número do Registro Nacional Migratório (RNM) do candidato.", DominioFato.Texto, FinalidadeIdentificacao,
            formato: FormatoTexto.Livre, classificacao: ClassificacaoDeProtecao.Identificador),
        DadoBasico(36, "NOME_MAE", "Nome da mãe", "Nome da mãe do candidato.", DominioFato.Texto, FinalidadeIdentificacao, formato: FormatoTexto.NomePessoa),
        DadoBasico(37, "NOME_PAI", "Nome do pai", "Nome do pai do candidato.", DominioFato.Texto, FinalidadeIdentificacao, formato: FormatoTexto.NomePessoa),
        // Sexo civil, identidade de gênero e orientação sexual (uniplus-developers#316): vocabulários
        // normativos declarados pelo candidato. A identidade de gênero e a orientação sexual são
        // dados pessoais sensíveis (LGPD, art. 5º, II) e se gravam com cifra individual.
        DadoBasico(57, "SEXO_CIVIL", "Sexo civil", "Sexo que consta do registro civil do candidato.", DominioFato.Categorico, FinalidadeIdentificacao, fonte: FonteValoresFato.Global),
        DadoBasico(58, "IDENTIDADE_GENERO", "Identidade de gênero", "Identidade de gênero que o candidato declara.", DominioFato.Categorico, FinalidadeIdentificacao,
            fonte: FonteValoresFato.Global, classificacao: ClassificacaoDeProtecao.Sensivel),
        DadoBasico(59, "ORIENTACAO_SEXUAL", "Orientação sexual", "Orientação sexual que o candidato declara.", DominioFato.Categorico, FinalidadeIdentificacao,
            fonte: FonteValoresFato.Global, classificacao: ClassificacaoDeProtecao.Sensivel),
        DadoBasico(38, "ESTADO_CIVIL", "Estado civil", "Estado civil do candidato.", DominioFato.Categorico, FinalidadeIdentificacao, fonte: FonteValoresFato.Global),
        DadoBasico(39, "EMAIL", "E-mail", "Endereço de e-mail para comunicação com o candidato.", DominioFato.Texto, FinalidadeComunicacao, formato: FormatoTexto.Email),
        DadoBasico(40, "TELEFONE", "Telefone", "Telefone, com DDD, para comunicação com o candidato.", DominioFato.Texto, FinalidadeComunicacao, formato: FormatoTexto.Telefone),
        DadoBasico(41, "RG_UF", "UF de emissão do RG", "Unidade da Federação que emitiu o RG do candidato.", DominioFato.Categorico, FinalidadeIdentificacao, fonte: FonteValoresFato.GeoUf),
        DadoBasico(42, "NATURALIDADE_UF", "UF de nascimento", "Unidade da Federação em que o candidato nasceu.", DominioFato.Categorico, FinalidadeIdentificacao, fonte: FonteValoresFato.GeoUf),
        DadoBasico(43, "NATURALIDADE_MUNICIPIO", "Município de nascimento", "Município em que o candidato nasceu.", DominioFato.Categorico, FinalidadeIdentificacao,
            fonte: FonteValoresFato.GeoMunicipio),

        // O tipo de localidade da residência, perguntado antes do endereço, e o nome da aldeia, da
        // comunidade tradicional ou do quilombo de quem mora em um deles.
        DadoBasico(44, "TIPO_ENDERECO", "Tipo de localidade", "Tipo de localidade em que o candidato reside, perguntado antes do endereço.", DominioFato.Categorico, FinalidadeResidencia, fonte: FonteValoresFato.Global),
        DadoBasico(45, "NOME_COMUNIDADE", "Nome da aldeia, comunidade ou quilombo", "Nome da aldeia, da comunidade tradicional ou do quilombo em que o candidato reside.", DominioFato.Texto, FinalidadeResidencia,
            formato: FormatoTexto.Livre),

        // A origem escolar em duas perguntas (UNI-REQ-0148): vocabulários normativos, de que o egresso
        // de escola pública deriva.
        DadoBasico(46, OrigemEscolar.FatoFormaDeConclusao, "Forma de conclusão do ensino médio",
            "Como o candidato concluiu o ensino médio: curso regular, Educação de Jovens e Adultos, ENCCEJA, exame de proficiência ou ENEM.", DominioFato.Categorico,
            FinalidadeReservaDeVagas, fonte: FonteValoresFato.Global),
        DadoBasico(47, OrigemEscolar.FatoOndeCursou, "Onde cursou o ensino médio",
            "Em que rede de ensino o candidato cursou o ensino médio, ou se o concluiu por certificação sem frequentá-lo.", DominioFato.Categorico,
            FinalidadeReservaDeVagas, fonte: FonteValoresFato.Global),

        // A renda declarada no pedido de isenção da taxa, conhecida na fase dele, que a condição da Lei nº 12.799/2013 cita
        // ao lado da origem escolar (UNI-REQ-0149).
        new(SeedId(48), IsencaoPorCarenciaSocioeconomica.FatoRenda, "Renda familiar per capita de até um salário mínimo e meio",
            "Se a renda familiar per capita do candidato é igual ou inferior a um salário mínimo e meio, como ele declara no pedido de isenção da taxa.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            FaseCanonicaCatalogo.CodigoSolicitacaoIsencao, $"CAMPO_FORMULARIO:{IsencaoPorCarenciaSocioeconomica.FatoRenda}",
            ClassificacaoDeProtecao.Pessoal, FinalidadeIsencaoDaTaxa),

        // A edição do ENEM cujas notas serão importadas (issue #1620) — as opções são as
        // edições que o processo admite, declaradas por ele (ADR-0136, fonte PROCESSO), nunca
        // uma lista fixa no código. Só existe em processo com classificação baseada em ENEM
        // (ProcessoSeletivo.DefinirOpcoesDeclaradas recusa fora desse caso).
        new(SeedId(49), EdicaoDoEnem.Fato, "Edição do ENEM", "Edição do ENEM cujas notas são importadas para a classificação do candidato.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Processo,
            PontoResolucaoInscricao, $"CAMPO_FORMULARIO:{EdicaoDoEnem.Fato}",
            ClassificacaoDeProtecao.Pessoal, FinalidadeImportacaoDeNotasDoEnem),

        // A condição de indígena é dado próprio, e não só um valor da cor ou raça: exige declaração de
        // pertencimento e passa só por análise de documentos, sem heteroidentificação (UNI-REQ-0147).
        new(SeedId(50), "INDIGENA", "Indígena", "Se o candidato se declara indígena. A condição exige declaração de pertencimento e passa só por análise de documentos.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:INDIGENA",
            ClassificacaoDeProtecao.Sensivel, FinalidadeReservaDeVagas),

        // A opção pelas cotas da Lei nº 12.711/2012, que cada cota da lei exige e cada ação afirmativa
        // recusa (UNI-REQ-0076, UNI-REQ-0142). O processo copia a regra padrão recortada pela oferta:
        // sem cota da lei ofertada, ninguém optou por elas.
        new(SeedId(54), OpcaoPelasCotasDaLei.Fato, "Optou pelas cotas da Lei nº 12.711/2012",
            "Se o candidato é egresso de escola pública e optou por concorrer às vagas de escola pública.",
            DominioFato.Booleano, OrigemFato.Derivado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, $"REGRA_DERIVACAO:{OpcaoPelasCotasDaLei.Fato}",
            ClassificacaoDeProtecao.Pessoal, FinalidadeReservaDeVagas, RegrasPadrao: OpcaoPelasCotasDaLei.RegrasDaOpcao),

        // A opção pelas vagas por acréscimo do PSIQ, uma só para as duas: a autodeclaração indígena
        // ou quilombola decide qual delas se aplica (UNI-REQ-0096).
        new(SeedId(55), "CONCORRER_VAGA_POR_ACRESCIMO", "Deseja concorrer às vagas por acréscimo para indígenas e quilombolas", "Opção do candidato por concorrer às vagas por acréscimo para indígenas e quilombolas.",
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:CONCORRER_VAGA_POR_ACRESCIMO",
            ClassificacaoDeProtecao.Interno, FinalidadeReservaDeVagas),

        // O recurso de que o candidato precisa nas etapas (sala para amamentação, ledor, intérprete de
        // Libras…), escolhido entre os que a oferta de atendimento do processo declara (UNI-REQ-0026).
        new(SeedId(53), "RECURSO_ACESSIBILIDADE", "Recurso de acessibilidade", "Recursos de acessibilidade de que o candidato precisa nas etapas, entre os que o processo oferece.",
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Multivalorado, FonteValoresFato.Processo,
            PontoResolucaoInscricao, "CAMPO_FORMULARIO:RECURSO_ACESSIBILIDADE",
            ClassificacaoDeProtecao.Sensivel, FinalidadeAtendimento),

        // A modalidade em que o candidato concorre na 2ª opção de curso, que tem a própria distribuição de
        // vagas: o sistema a calcula na inscrição, como a da 1ª opção.
        new(SeedId(60), ModalidadeDoCandidato.FatoSegundaOpcao, "Modalidade de concorrência da 2ª opção de curso", "Modalidades de concorrência do candidato na segunda opção de curso, que tem distribuição de vagas própria.",
            DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Multivalorado, FonteValoresFato.Modalidade,
            PontoResolucaoInscricao, $"ATRIBUTO_CANDIDATO:{ModalidadeDoCandidato.FatoSegundaOpcao}",
            ClassificacaoDeProtecao.Interno, FinalidadeReservaDeVagas),
    ];

    /// <summary>Um dado declarado do candidato, escalar e coletado na inscrição.</summary>
    private static FatoCandidatoSeedItem DadoBasico(
        int n,
        string codigo,
        string nome,
        string descricao,
        DominioFato dominio,
        string finalidade,
        FonteValoresFato? fonte = null,
        FormatoTexto? formato = null,
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal) =>
        new(SeedId(n), codigo, nome, descricao, dominio, OrigemFato.Declarado, CardinalidadeFato.Escalar, fonte,
            PontoResolucaoInscricao, $"CAMPO_FORMULARIO:{codigo}", classificacao, finalidade, Formato: formato);
}

/// <summary>
/// Definição de um fato do seed (fonte única), na forma da entidade
/// <c>FatoCandidato</c>. Não passa pela factory (seed materializa linhas
/// diretamente); a coerência com as invariantes de domínio é garantida por
/// teste de unidade sobre a factory e por CHECKs de banco sobre a tabela.
/// </summary>
public sealed record FatoCandidatoSeedItem(
    Guid Id,
    string Codigo,
    string Nome,
    string Descricao,
    DominioFato Dominio,
    OrigemFato Origem,
    CardinalidadeFato Cardinalidade,
    FonteValoresFato? FonteValores,
    string PontoResolucao,
    string Binding,
    ClassificacaoDeProtecao ClassificacaoProtecao,
    string FinalidadeTratamento,
    EscopoFato Escopo = EscopoFato.Candidato,
    FormatoTexto? Formato = null,
    IReadOnlyList<RegraDerivacao>? RegrasPadrao = null,
    ComponenteDeExibicao? ComponenteExibicao = null);
