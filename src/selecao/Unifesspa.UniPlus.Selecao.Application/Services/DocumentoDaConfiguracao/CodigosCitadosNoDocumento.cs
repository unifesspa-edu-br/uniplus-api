namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// Os códigos que o documento cita em cada cadastro, sem repetição e na ordem em que aparecem: o que a leitura dos cadastros precisa
/// consultar, uma vez por código. O tipo de processo e a localidade são um só código no documento. A regra do catálogo é citada por código e versão, e a citação sem um dos dois não consulta nada.
/// </summary>
internal sealed record CodigosCitadosPorCadastro(
    IReadOnlyList<string> Unidades,
    string? TipoDeProcesso,
    IReadOnlyList<string> FasesCanonicas,
    IReadOnlyList<string> TiposDeBanca,
    IReadOnlyList<string> CategoriasDeDocumento,
    string? Localidade,
    IReadOnlyList<(string Codigo, string Versao)> Regras,
    IReadOnlyList<string> TiposDeAto)
{
    /// <summary>Os códigos dos tipos de etapa que as etapas citam.</summary>
    public IReadOnlyList<string> TiposDeEtapa { get; init; } = [];

    /// <summary>A resolução dos pesos por área do ENEM que a classificação cita; ausente é nulo.</summary>
    public string? ResolucaoDePesos { get; init; }

    /// <summary>Os códigos das ofertas de curso que a distribuição de vagas cita.</summary>
    public IReadOnlyList<string> OfertasDeCurso { get; init; } = [];

    /// <summary>Os códigos das modalidades que a distribuição, o ajuste do quadro e a cascata de remanejamento citam.</summary>
    public IReadOnlyList<string> Modalidades { get; init; } = [];

    /// <summary>Os códigos das referências demográficas que a distribuição cita.</summary>
    public IReadOnlyList<string> ReferenciasDemograficas { get; init; } = [];

    /// <summary>Os códigos das condições de atendimento que o atendimento especializado cita.</summary>
    public IReadOnlyList<string> CondicoesDeAtendimento { get; init; } = [];

    /// <summary>Os códigos dos recursos de acessibilidade que o atendimento especializado cita.</summary>
    public IReadOnlyList<string> RecursosDeAcessibilidade { get; init; } = [];

    /// <summary>Os códigos dos tipos de deficiência que o atendimento especializado cita.</summary>
    public IReadOnlyList<string> TiposDeDeficiencia { get; init; } = [];

    /// <summary>Os códigos dos tipos de documento que as exigências citam.</summary>
    public IReadOnlyList<string> TiposDeDocumento { get; init; } = [];

    /// <summary>O código da base legal do FADA que a declaração cita; ausente é nulo.</summary>
    public string? BaseLegalDoFada { get; init; }
}

/// <summary>
/// Junta, nas declarações da identificação, do cronograma e da avaliação, os códigos distintos que o documento cita em cada cadastro: a unidade
/// administradora e as responsáveis pelas fases compartilham o cadastro de unidades, e a regra vem do algoritmo de contagem, da regra
/// de recurso e dos procedimentos. As etapas e a classificação entram pela união com o que elas citam
/// (<see cref="CodigosCitadosNaAvaliacao"/>). Não confere o que cada código é.
/// </summary>
internal static class CodigosCitadosNoDocumento
{
    internal static CodigosCitadosPorCadastro Em(DeclaracoesDoDocumento declaracoes) =>
        Unir(DaIdentificacaoEDasFases(declaracoes), CodigosCitadosNaAvaliacao.Em(declaracoes), CodigosCitadosNasVagasENasExigencias.Em(declaracoes));

    // A avaliação, as vagas e as exigências citam regras e outros códigos dos mesmos cadastros que as fases, e cada código vale uma vez.
    private static CodigosCitadosPorCadastro Unir(
        CodigosCitadosPorCadastro dasFases, CodigosDaAvaliacaoCitados avaliacao, CodigosDasVagasEDasExigenciasCitados dasVagas) => dasFases with
        {
            TiposDeBanca = CodigosDistintos.De([.. dasFases.TiposDeBanca, .. avaliacao.TiposDeBanca]),
            Regras = [.. dasFases.Regras.Concat(avaliacao.Regras).Concat(dasVagas.Regras).Distinct()],
            TiposDeAto = CodigosDistintos.De([.. dasFases.TiposDeAto, .. avaliacao.TiposDeAto]),
            TiposDeEtapa = avaliacao.TiposDeEtapa,
            ResolucaoDePesos = avaliacao.ResolucaoDePesos,
            OfertasDeCurso = dasVagas.OfertasDeCurso,
            Modalidades = dasVagas.Modalidades,
            ReferenciasDemograficas = dasVagas.ReferenciasDemograficas,
            CondicoesDeAtendimento = dasVagas.CondicoesDeAtendimento,
            RecursosDeAcessibilidade = dasVagas.RecursosDeAcessibilidade,
            TiposDeDeficiencia = dasVagas.TiposDeDeficiencia,
            TiposDeDocumento = dasVagas.TiposDeDocumento,
            BaseLegalDoFada = dasVagas.BaseLegalDoFada,
        };

    private static CodigosCitadosPorCadastro DaIdentificacaoEDasFases(DeclaracoesDoDocumento declaracoes)
    {
        IdentificacaoNoDocumento identificacao = declaracoes.Identificacao;
        FaseNoDocumento[] fases = [.. declaracoes.Fases];
        BancaNoDocumento[] bancas = [.. fases.SelectMany(static fase => fase.Bancas)];

        return new(
            CodigosDistintos.De([identificacao.UnidadeAdministradoraCodigo, .. fases.Select(static fase => fase.Referencias.UnidadeResponsavelCodigo)]),
            identificacao.TipoProcessoCodigo,
            CodigosDistintos.De(fases.Select(static fase => fase.Referencias.FaseCanonicaCodigo)),
            CodigosDistintos.De(bancas.Select(static banca => banca.TipoBancaCodigo)),
            CodigosDistintos.De(bancas.SelectMany(static banca => banca.CategoriasDocumentoCodigos)),
            identificacao.LocalidadeCodigoIbge,
            CodigosDistintos.DasRegras(ReferenciasDeRegra(identificacao, fases)),
            CodigosDistintos.De(fases.SelectMany(static fase => fase.Publicacoes.Produtos).Select(static produto => produto.AtoCodigo)));
    }

    private static IEnumerable<ReferenciaDeRegraNoDocumento> ReferenciasDeRegra(IdentificacaoNoDocumento identificacao, FaseNoDocumento[] fases)
    {
        RegraDeRecursoNoDocumento[] regrasDeRecurso = [.. fases.Select(static fase => fase.RegraDeRecurso).OfType<RegraDeRecursoNoDocumento>()];

        return
        [
            .. new[] { identificacao.AlgoritmoContagemPrazo }.OfType<ReferenciaDeRegraNoDocumento>(),
            .. regrasDeRecurso.Select(static regra => regra.Regra),
            .. regrasDeRecurso.SelectMany(static regra => regra.Procedimentos).Select(static procedimento => procedimento.Regra),
        ];
    }
}
