namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

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
/// Todos são fatos de <b>sistema</b>: só nome e descrição são editáveis (ADR-0136). São do escopo
/// do candidato, exceto maior de idade, sem renda e sob guarda, que são campos de membro de um
/// grupo repetível do formulário, como a composição familiar (ADR-0138). A fonte dos valores do categórico está em <see cref="FatoCandidatoSeedItem.FonteValores"/>;
/// os valores do categórico global são os <see cref="Domain.Entities.FatoValorDominio"/> de
/// <see cref="FatoValorDominioSeed"/>. Cor ou raça, autodeclaração quilombola, deficiência,
/// atendimento especializado, os opt-ins que os revelam e a modalidade que deles deriva são
/// <see cref="ClassificacaoProtecaoDado.Sensivel"/>; os demais, <see cref="ClassificacaoProtecaoDado.Pessoal"/>.
/// </para>
/// <para>
/// <see cref="OrigemFato"/> (ADR-0116): <c>FAIXA_ETARIA</c> e <c>RENDA_PER_CAPITA</c>
/// (computados de atributo do candidato), <c>MODALIDADE</c> (derivada das regras
/// congeladas do processo) e <c>MODALIDADE_CONVOCACAO</c> (resultado da classificação) são
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
/// <c>MODALIDADE_CONVOCACAO</c>, que só existe depois do resultado final.
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

    private const string FinalidadeReservaDeVagas =
        "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.";

    private const string FinalidadeAtendimento =
        "Oferta de atendimento especializado ao candidato na realização das etapas do processo seletivo.";

    private const string FinalidadeResidencia =
        "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.";

    private const string FinalidadeComposicaoFamiliar =
        "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.";

    private const string FinalidadeRequisitos =
        "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.";

    private const string FinalidadeIdentificacao =
        "Identificação do candidato no processo seletivo.";

    private const string FinalidadeComunicacao =
        "Comunicação com o candidato sobre o processo seletivo.";

    // Prefixo determinístico próprio do catálogo de fatos (distinto do
    // rol_de_regras, para não confundir identidades entre tabelas).
    private static Guid SeedId(int n) =>
        Guid.Parse($"fa700000-0000-7000-8000-{n:D12}");

    /// <summary>Os fatos de sistema, na ordem canônica de semeadura.</summary>
    public static IReadOnlyList<FatoCandidatoSeedItem> Itens { get; } =
    [
        new(SeedId(1), "COR_RACA", "Cor ou raça", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:COR_RACA",
            ClassificacaoProtecaoDado.Sensivel, FinalidadeReservaDeVagas),

        new(SeedId(2), "QUILOMBOLA", "Quilombola", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:QUILOMBOLA",
            ClassificacaoProtecaoDado.Sensivel, FinalidadeReservaDeVagas),

        new(SeedId(3), "PCD", "Pessoa com deficiência", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:PCD",
            ClassificacaoProtecaoDado.Sensivel, FinalidadeReservaDeVagas),

        new(SeedId(4), "EGRESSO_ESCOLA_PUBLICA", "Egresso de escola pública", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:EGRESSO_ESCOLA_PUBLICA",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeReservaDeVagas),

        new(SeedId(5), "RENDA_PER_CAPITA", "Renda familiar per capita", null,
            DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeReservaDeVagas),

        new(SeedId(6), "FAIXA_ETARIA", "Faixa etária", null,
            DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "ATRIBUTO_CANDIDATO:FAIXA_ETARIA",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeRequisitos),

        new(SeedId(7), "SEXO", "Sexo", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:SEXO",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeRequisitos),

        // MODALIDADE é derivado, não declarado: o candidato declara fatos e opt-ins, e o conjunto
        // de modalidades resulta da avaliação deles contra as regras congeladas do processo. O
        // binding referencia a regra de derivação — o catálogo diz o mecanismo, a config do edital
        // diz o conteúdo (ADR-0116, emenda de 2026-07-22).
        new(SeedId(8), "MODALIDADE", "Modalidade de concorrência", null,
            DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Multivalorado, FonteValoresFato.Modalidade,
            PontoResolucaoInscricao, "REGRA_DERIVACAO:MODALIDADE",
            ClassificacaoProtecaoDado.Sensivel, FinalidadeReservaDeVagas),

        new(SeedId(9), "CONDICAO_ATENDIMENTO", "Condição de atendimento especializado", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Multivalorado, FonteValoresFato.Processo,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:CONDICAO_ATENDIMENTO",
            ClassificacaoProtecaoDado.Sensivel, FinalidadeAtendimento),

        new(SeedId(10), "NACIONALIDADE", "Nacionalidade", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:NACIONALIDADE",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeRequisitos),

        new(SeedId(11), "TIPO_DEFICIENCIA", "Tipo de deficiência", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Processo,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:TIPO_DEFICIENCIA",
            ClassificacaoProtecaoDado.Sensivel, FinalidadeReservaDeVagas),

        // ── Pares elegibilidade + opt-in do formulário de cotas (UNI-REQ-0078) ──
        // A elegibilidade dos quatro blocos já está semeada acima e é REUTILIZADA:
        // COR_RACA (PPI), QUILOMBOLA, PCD e EGRESSO_ESCOLA_PUBLICA. Falta a quinta
        // elegibilidade — a autodeclaração de renda — e os cinco opt-ins.

        new(SeedId(12), "BAIXA_RENDA", "Renda familiar per capita igual ou inferior a um salário mínimo", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:BAIXA_RENDA",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeReservaDeVagas),

        new(SeedId(13), "CONCORRER_PCD", "Deseja concorrer às vagas reservadas a pessoas com deficiência", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:CONCORRER_PCD",
            ClassificacaoProtecaoDado.Sensivel, FinalidadeReservaDeVagas),

        new(SeedId(14), "CONCORRER_EP", "Deseja concorrer às vagas reservadas a egressos de escola pública", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:CONCORRER_EP",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeReservaDeVagas),

        new(SeedId(15), "CONCORRER_PPI", "Deseja concorrer às vagas reservadas a pretos, pardos e indígenas", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:CONCORRER_PPI",
            ClassificacaoProtecaoDado.Sensivel, FinalidadeReservaDeVagas),

        new(SeedId(16), "CONCORRER_Q", "Deseja concorrer às vagas reservadas a quilombolas", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:CONCORRER_Q",
            ClassificacaoProtecaoDado.Sensivel, FinalidadeReservaDeVagas),

        new(SeedId(17), "CONCORRER_RENDA", "Deseja concorrer às vagas reservadas por renda familiar per capita", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:CONCORRER_RENDA",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeReservaDeVagas),

        // ── Residência e nascimento (ADR-0136) ──
        // O endereço e a data de nascimento são declarados; a UF e o município de residência
        // derivam do endereço e são os que uma regra pode citar — o endereço e a data, não.
        new(SeedId(18), "ENDERECO_RESIDENCIAL", "Endereço residencial", null,
            DominioFato.Endereco, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:ENDERECO_RESIDENCIAL",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeResidencia),

        new(SeedId(19), "DATA_NASCIMENTO", "Data de nascimento", null,
            DominioFato.Data, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:DATA_NASCIMENTO",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeRequisitos),

        new(SeedId(20), "UF_RESIDENCIA", "UF de residência", null,
            DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Escalar, FonteValoresFato.GeoUf,
            PontoResolucaoInscricao, "ATRIBUTO_CANDIDATO:UF_RESIDENCIA",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeResidencia),

        new(SeedId(21), "MUNICIPIO_RESIDENCIA", "Município de residência", null,
            DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Escalar, FonteValoresFato.GeoMunicipio,
            PontoResolucaoInscricao, "ATRIBUTO_CANDIDATO:MUNICIPIO_RESIDENCIA",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeResidencia),

        // O grupo de vagas em que o candidato foi convocado, e não as modalidades a que concorreu:
        // os documentos da habilitação seguem a convocação (UNI-REQ-0147). A classificação o produz
        // e ele só é conhecido depois do resultado final.
        new(SeedId(22), "MODALIDADE_CONVOCACAO", "Modalidade da convocação", null,
            DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Escalar, FonteValoresFato.Modalidade,
            PontoResolucaoResultadoFinal, "CLASSIFICACAO:MODALIDADE_CONVOCACAO",
            ClassificacaoProtecaoDado.Sensivel, FinalidadeReservaDeVagas),

        // Campos de membro da composição familiar: a exigência repetida por membro cita cada um
        // na ocorrência do grupo, como a certidão pedida só do membro sob guarda.
        new(SeedId(23), "MAIOR_IDADE", "Maior de idade", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:MAIOR_IDADE",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeComposicaoFamiliar, EscopoFato.MembroGrupo),

        new(SeedId(24), "SEM_RENDA", "Sem renda", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:SEM_RENDA",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeComposicaoFamiliar, EscopoFato.MembroGrupo),

        new(SeedId(25), "SOB_GUARDA", "Sob guarda", null,
            DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, null,
            PontoResolucaoInscricao, "CAMPO_INSCRICAO:SOB_GUARDA",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeComposicaoFamiliar, EscopoFato.MembroGrupo),

        // O parentesco de cada membro identifica, no grupo que inclui o candidato, a ocorrência
        // dele próprio.
        new(SeedId(26), CandidatoComoMembro.FatoParentesco, "Parentesco", null,
            DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            PontoResolucaoInscricao, $"CAMPO_INSCRICAO:{CandidatoComoMembro.FatoParentesco}",
            ClassificacaoProtecaoDado.Pessoal, FinalidadeComposicaoFamiliar, EscopoFato.MembroGrupo),

        // O conjunto básico que todo formulário de inscrição coleta: a identificação, a filiação e
        // os contatos do candidato. A UF do RG e a naturalidade não estão aqui: as opções delas vêm
        // do Geo, que o formulário ainda não coleta. O nome social é público, porque é a
        // identificação que o titular escolhe para aparecer (ADR-0082); os demais são pessoais.
        DadoBasico(27, "NOME", "Nome", DominioFato.Texto, FinalidadeIdentificacao, formato: FormatoTexto.NomePessoa),
        DadoBasico(28, "DESEJA_NOME_SOCIAL", "Deseja usar nome social", DominioFato.Booleano, FinalidadeIdentificacao),
        DadoBasico(29, FatoCandidato.CodigoDoNomeSocial, "Nome social", DominioFato.Texto, FinalidadeIdentificacao,
            formato: FormatoTexto.NomePessoa, classificacao: ClassificacaoProtecaoDado.Publico),
        DadoBasico(30, "CPF", "CPF", DominioFato.Texto, FinalidadeIdentificacao, formato: FormatoTexto.Cpf),
        DadoBasico(31, "RG_NUMERO", "Número do RG", DominioFato.Texto, FinalidadeIdentificacao, formato: FormatoTexto.Livre),
        DadoBasico(32, "RG_ORGAO_EMISSOR", "Órgão emissor do RG", DominioFato.Texto, FinalidadeIdentificacao, formato: FormatoTexto.Livre),
        DadoBasico(33, "RG_DATA_EMISSAO", "Data de emissão do RG", DominioFato.Data, FinalidadeIdentificacao),
        DadoBasico(34, "DOCUMENTO_ESTRANGEIRO_TIPO", "Documento de identificação do estrangeiro", DominioFato.Categorico,
            FinalidadeIdentificacao, fonte: FonteValoresFato.Global),
        DadoBasico(35, "DOCUMENTO_ESTRANGEIRO_NUMERO", "Número do documento do estrangeiro", DominioFato.Texto,
            FinalidadeIdentificacao, formato: FormatoTexto.Livre),
        DadoBasico(36, "NOME_MAE", "Nome da mãe", DominioFato.Texto, FinalidadeIdentificacao, formato: FormatoTexto.NomePessoa),
        DadoBasico(37, "NOME_PAI", "Nome do pai", DominioFato.Texto, FinalidadeIdentificacao, formato: FormatoTexto.NomePessoa),
        DadoBasico(38, "ESTADO_CIVIL", "Estado civil", DominioFato.Categorico, FinalidadeIdentificacao, fonte: FonteValoresFato.Global),
        DadoBasico(39, "EMAIL", "E-mail", DominioFato.Texto, FinalidadeComunicacao, formato: FormatoTexto.Email),
        DadoBasico(40, "TELEFONE", "Telefone", DominioFato.Texto, FinalidadeComunicacao, formato: FormatoTexto.Telefone),
    ];

    /// <summary>Um dado declarado do candidato, escalar e coletado na inscrição.</summary>
    private static FatoCandidatoSeedItem DadoBasico(
        int n,
        string codigo,
        string nome,
        DominioFato dominio,
        string finalidade,
        FonteValoresFato? fonte = null,
        FormatoTexto? formato = null,
        ClassificacaoProtecaoDado classificacao = ClassificacaoProtecaoDado.Pessoal) =>
        new(SeedId(n), codigo, nome, null, dominio, OrigemFato.Declarado, CardinalidadeFato.Escalar, fonte,
            PontoResolucaoInscricao, $"CAMPO_INSCRICAO:{codigo}", classificacao, finalidade, Formato: formato);
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
    string? Descricao,
    DominioFato Dominio,
    OrigemFato Origem,
    CardinalidadeFato Cardinalidade,
    FonteValoresFato? FonteValores,
    string PontoResolucao,
    string Binding,
    ClassificacaoProtecaoDado ClassificacaoProtecao,
    string FinalidadeTratamento,
    EscopoFato Escopo = EscopoFato.Candidato,
    FormatoTexto? Formato = null);
