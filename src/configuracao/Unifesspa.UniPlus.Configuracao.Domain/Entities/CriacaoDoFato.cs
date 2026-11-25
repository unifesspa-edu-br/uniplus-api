namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.Entities.ConferenciaDaDefinicaoDoFato;

/// <summary>
/// Monta o fato do candidato novo: a factory canônica e os três cadastros do administrador (declarado,
/// derivado por regra e agregado sobre grupo repetível), com os eixos que cada um infere e a
/// conferência do agregado contra o fato de membro. As violações independentes acumulam (ADR-0125).
/// </summary>
public static class CriacaoDoFato
{
    /// <summary>
    /// Factory canônica: valida o código, o nome, o domínio, a origem, a cardinalidade, a fonte
    /// dos valores, o formato, o ponto de resolução, o vínculo, o escopo e a proteção de dados. As
    /// violações independentes são acumuladas (ADR-0125); a que depende de outro campo só é
    /// conferida quando esse campo é válido.
    /// </summary>
    public static Result<FatoCandidato> Criar(
        DescritivoDoFato descritivo,
        OrigemFato? origem,
        string binding,
        DefinicaoDoFato definicao,
        bool sistema)
    {
        ArgumentNullException.ThrowIfNull(descritivo);
        ArgumentNullException.ThrowIfNull(definicao);

        return new ConferenciaDoNovoFato(descritivo, origem, binding, definicao, sistema).Conferir();
    }

    /// <summary>
    /// Cadastro de fato pelo administrador (ADR-0136): sempre declarado, com o vínculo ao campo de
    /// formulário gerado a partir do código, e nunca de sistema.
    /// </summary>
    public static Result<FatoCandidato> CriarDoAdministrador(DescritivoDoFato descritivo, DefinicaoDoFato definicao)
    {
        ArgumentNullException.ThrowIfNull(descritivo);

        return Criar(
            descritivo, OrigemFato.Declarado, VinculoDeFato.De(MecanismoDoVinculo.CampoFormulario, CodigoNormalizado(descritivo)),
            definicao, sistema: false);
    }

    /// <summary>
    /// Cadastro de derivado por regra pelo administrador (ADR-0136): booleano, verdadeiro quando
    /// alguma regra ativa, ou categórico de valores próprios, com a união das contribuições. A
    /// cardinalidade e a fonte saem do domínio, então a definição não as traz. As regras padrão vêm
    /// depois, por <see cref="FatoCandidato.DefinirRegrasPadrao"/>, porque citam os valores que o
    /// categórico ainda vai receber.
    /// </summary>
    public static Result<FatoCandidato> CriarDerivadoDoAdministrador(DescritivoDoFato descritivo, DefinicaoDoFato definicao)
    {
        ArgumentNullException.ThrowIfNull(descritivo);
        ArgumentNullException.ThrowIfNull(definicao);

        Result<FatoCandidato> criado = Criar(
            descritivo, OrigemFato.Calculado, VinculoDeFato.De(MecanismoDoVinculo.RegraDeDerivacao, CodigoNormalizado(descritivo)),
            ComEixosInferidosDoDerivado(definicao), sistema: false);
        List<FieldError> recusasDoDominio = [];

        // Com o domínio recusado, o que depende dele (formato, fonte) não é conferido.
        return ConferirDominioDoDerivado(recusasDoDominio, definicao.Dominio)
            ? criado
            : Result<FatoCandidato>.ValidationFailure(
                [.. recusasDoDominio, .. criado.Errors.Where(static e => e.Field is not ("dominio" or "formato" or "fonteValores"))]);
    }

    private static DefinicaoDoFato ComEixosInferidosDoDerivado(DefinicaoDoFato definicao)
    {
        (QuantidadeDeValores cardinalidade, FonteValoresFato? fonteValores) = EixosInferidosDoDerivado(definicao.Dominio.Valor);
        return definicao with { Cardinalidade = cardinalidade, FonteValores = fonteValores, Formato = null };
    }

    /// <summary>
    /// Cadastro do agregado sobre grupo repetível pelo administrador (ADR-0138, UNI-REQ-0146): o
    /// vínculo <c>RESUMO_DE_GRUPO:</c> aponta o fato de membro, que precisa existir, estar ativo e ser
    /// fato declarado de membro de grupo de domínio que agrega. O domínio, a cardinalidade e a fonte
    /// dos valores saem do fato de membro — booleano dá "existe membro que…", categórico dá "valores
    /// presentes" —, e o agregado não tem valores próprios; a definição traz só o ponto de resolução e a
    /// proteção de dados. Não protege menos nem resolve antes do fato de membro. As recusas se acumulam
    /// (ADR-0125); sem fato de membro válido, as que dependem dele não são conferidas.
    /// </summary>
    public static Result<FatoCandidato> CriarAgregadoDoAdministrador(
        DescritivoDoFato descritivo,
        string fatoDeMembroCodigo,
        CatalogoDeFatos catalogo,
        DefinicaoDoFato definicao)
    {
        ArgumentNullException.ThrowIfNull(descritivo);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(definicao);

        string membroCodigo = fatoDeMembroCodigo?.Trim() ?? string.Empty;
        FatoCandidato? membro = catalogo.Fatos.FirstOrDefault(f => string.Equals(f.Codigo, membroCodigo, StringComparison.Ordinal));
        return CriarAgregadoSobre(descritivo, membro, membroCodigo, catalogo, definicao);
    }

    private static Result<FatoCandidato> CriarAgregadoSobre(
        DescritivoDoFato descritivo, FatoCandidato? membro, string membroCodigo, CatalogoDeFatos catalogo, DefinicaoDoFato definicao)
    {
        List<FieldError> erros = [];
        OperacaoAgregado? operacao = AgregadoDeGrupo.OperacaoDoDominio(membro?.Dominio);
        bool membroValido = ConferirFatoDeMembro(erros, membro, operacao);
        Result<FatoCandidato> criado = Criar(
            descritivo, OrigemFato.Calculado, VinculoDeFato.De(MecanismoDoVinculo.ResumoDeGrupo, membroCodigo),
            ComEixosInferidosDoAgregado(definicao, operacao, membro?.FonteValores), sistema: false);
        erros.AddRange(RecusasDoCadastroDoAgregado(criado, membroValido));
        if (membroValido)
        {
            ConferirContraOMembro(erros, membro!, definicao.PontoResolucao, definicao.ClassificacaoProtecao, catalogo);
        }

        return erros.Count > 0 ? Result<FatoCandidato>.ValidationFailure(erros) : criado;
    }

    // As recusas que dependem do fato de membro só valem com ele válido; sem ele, o domínio e a fonte
    // não foram inferidos e as recusas desses campos não orientam. A proteção e o ponto de resolução se
    // comparam com os do fato de membro sempre que o campo é válido, independentemente dos outros.
    private static IEnumerable<FieldError> RecusasDoCadastroDoAgregado(Result<FatoCandidato> criado, bool membroValido) =>
        membroValido ? criado.Errors : criado.Errors.Where(static e => e.Field is not ("dominio" or "fonteValores" or "binding"));

    private static string CodigoNormalizado(DescritivoDoFato descritivo) => descritivo.Codigo?.Trim() ?? string.Empty;

    private static DefinicaoDoFato ComEixosInferidosDoAgregado(DefinicaoDoFato definicao, OperacaoAgregado? operacao, FonteValoresFato? fonteDoMembro)
    {
        (TipoDoDado dominio, QuantidadeDeValores cardinalidade, FonteValoresFato? fonteValores) = EixosInferidosDoAgregado(operacao, fonteDoMembro);
        return definicao with { Dominio = dominio, Cardinalidade = cardinalidade, FonteValores = fonteValores, Formato = null, Escopo = EscopoFato.Candidato };
    }

    private static bool ConferirFatoDeMembro(List<FieldError> erros, FatoCandidato? membro, OperacaoAgregado? operacao)
    {
        if (!RegistrarRecusa(erros, "fatoDeMembro", RecusaDoFatoDeMembro(membro)))
        {
            return false;
        }

        RegistrarRecusa(erros, "fatoDeMembro", RecusaDoFatoDeMembroDesativado(membro!));
        return RegistrarRecusa(erros, "fatoDeMembro", RecusaDoAgregadoSobreDominioQueNaoAgrega(operacao));
    }

    private static (string Codigo, string Mensagem)? RecusaDoFatoDeMembro(FatoCandidato? membro) => membro switch
    {
        null => (FatoCandidatoErrorCodes.AgregadoSemFatoDeMembro, "O fato de membro não pertence ao catálogo."),
        { Escopo: not EscopoFato.MembroGrupo } or { Origem: not OrigemFato.Declarado } => (
            FatoCandidatoErrorCodes.AgregadoSobreFatoQueNaoEhCampoDeGrupo,
            "O fato deve ser declarado de membro de grupo: o agregado resume o que cada membro respondeu."),
        _ => null,
    };

    private static (string Codigo, string Mensagem)? RecusaDoAgregadoSobreDominioQueNaoAgrega(OperacaoAgregado? operacao) =>
        operacao is null
            ? (FatoCandidatoErrorCodes.AgregadoSobreDominioQueNaoAgrega, "O agregado resume fato de membro booleano (existe membro que…) ou categórico (valores presentes).")
            : null;

    private static (string Codigo, string Mensagem)? RecusaDoFatoDeMembroDesativado(FatoCandidato membro) =>
        membro.Ativo
            ? null
            : (VinculoCatalogoErrorCodes.FatoDesativado, "O fato de membro está desativado no catálogo e não aceita vínculo novo.");

    /// <summary>A quantidade de valores e a fonte que o derivado por regra infere do próprio tipo do dado.</summary>
    internal static (QuantidadeDeValores Cardinalidade, FonteValoresFato? FonteValores) EixosInferidosDoDerivado(TipoDoDado? tipo) =>
        tipo == TipoDoDado.Opcao
            ? (QuantidadeDeValores.VariosValores, FonteValoresFato.Catalogo)
            : (QuantidadeDeValores.ValorUnico, null);

    /// <summary>
    /// O domínio, a cardinalidade e a fonte que o agregado infere da operação sobre o fato de membro:
    /// valores presentes dá categórico multivalorado com a fonte do membro, e o restante dá booleano.
    /// </summary>
    internal static (TipoDoDado Dominio, QuantidadeDeValores Cardinalidade, FonteValoresFato? FonteValores) EixosInferidosDoAgregado(
        OperacaoAgregado? operacao, FonteValoresFato? fonteDoMembro) =>
        operacao == OperacaoAgregado.ValoresPresentes
            ? (TipoDoDado.Opcao, QuantidadeDeValores.VariosValores, fonteDoMembro)
            : (TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, null);

    /// <summary>
    /// O agregado não protege menos nem resolve antes do fato de membro: a recusa vai no campo do
    /// eixo, e só quando o eixo informado é válido.
    /// </summary>
    internal static void ConferirContraOMembro(
        List<FieldError> erros,
        FatoCandidato membro,
        string pontoResolucao,
        CodigoInformado<ClassificacaoDeProtecao> classificacaoProtecao,
        CatalogoDeFatos catalogo)
    {
        RegistrarRecusa(erros, "classificacaoProtecao", RecusaDaProtecaoAbaixoDoMembro(membro, classificacaoProtecao));
        string ponto = pontoResolucao?.Trim() ?? string.Empty;
        RegistrarRecusa(erros, "pontoResolucao", RecusaDoPontoAnteriorAoMembro(membro, ponto, catalogo));
    }

    private static (string Codigo, string Mensagem)? RecusaDaProtecaoAbaixoDoMembro(
        FatoCandidato membro, CodigoInformado<ClassificacaoDeProtecao> classificacaoProtecao) =>
        classificacaoProtecao.Valor is { } valor && valor < membro.ClassificacaoProtecao
            ? (FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia,
                $"O agregado revela o que se sabe de '{membro.Codigo}' e não pode ter proteção de dados mais fraca que a dele.")
            : null;

    private static (string Codigo, string Mensagem)? RecusaDoPontoAnteriorAoMembro(FatoCandidato membro, string ponto, CatalogoDeFatos catalogo) =>
        FaseCanonicaCatalogo.EhCanonico(ponto) && ValidadorRegrasPadrao.Precede(ponto, membro.PontoResolucao, catalogo.Precedencias)
            ? (FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia,
                $"O agregado resolve em {ponto}, antes de '{membro.Codigo}', que só é conhecido em {membro.PontoResolucao}.")
            : null;
}
