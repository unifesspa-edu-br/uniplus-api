namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As cinco marcas do tipo de fase como chegam do cadastro, antes de conferidas: cada uma pode estar
/// ausente, e a ausência é recusada. Só depois de zero recusas o conjunto vira
/// <see cref="SinalizadoresDaFase"/>, o valor gravado.
/// </summary>
/// <remarks>
/// A coerência das marcas com o código do tipo de fase e entre si é regra do conjunto: nenhuma marca
/// se confere sozinha. A Seleção decide pelas marcas que copia para o snapshot do tipo de fase,
/// nunca pelo código (ADR-0113).
/// </remarks>
/// <param name="AgrupaEtapas">Informa se o tipo de fase agrupa as etapas pontuadas — só o de avaliação.</param>
/// <param name="PermiteComplementacao">Informa se o tipo de fase admite complementação documental — só onde a lei permite.</param>
/// <param name="ColetaInscricao">Informa se o tipo de fase coleta inscrições.</param>
/// <param name="ColetaSolicitacaoIsencao">Informa se o tipo de fase coleta a solicitação de isenção da taxa — exclusiva do tipo de fase de solicitação de isenção e incompatível com a coleta de inscrição.</param>
/// <param name="OrganizadaEmChamadas">Informa se o tipo de fase se repete em chamadas, cada uma com os seus convocados.</param>
public sealed record SinalizadoresInformados(
    bool? AgrupaEtapas,
    bool? PermiteComplementacao,
    bool? ColetaInscricao,
    bool? ColetaSolicitacaoIsencao,
    bool? OrganizadaEmChamadas)
{
    /// <summary>
    /// As recusas das marcas, acumuladas na ordem: a ausência de cada uma, a coerência com o código
    /// (agrupamento de etapas, complementação e marca de solicitação de isenção) e a exclusividade
    /// entre coletar inscrição e coletar solicitação de isenção. A coerência com o código só vale
    /// para um <paramref name="codigoReconhecido"/> (formato válido e pertencente ao conjunto
    /// canônico); com <see langword="null"/> o código foi recusado (relatá-la seria um segundo erro
    /// derivado do primeiro) ou ainda não é conhecido, como na validação do pedido de alteração, que
    /// roda antes de buscar o código gravado. Cada coerência só é conferida quando as marcas que ela
    /// compara foram informadas.
    /// </summary>
    internal IReadOnlyList<FieldError> Recusas(CodigoFase? codigoReconhecido)
    {
        FieldError?[] recusas =
        [
            .. RecusasDeAusencia(),
            .. RecusasQueDependemDoCodigo(codigoReconhecido),
            RecusaDaColetaSimultaneaDeInscricaoEIsencao(),
        ];

        return [.. recusas.OfType<FieldError>()];
    }

    /// <summary>As marcas como valor gravado. Só vale depois de <see cref="Recusas"/> devolver vazio.</summary>
    internal SinalizadoresDaFase Confirmados() =>
        new(
            AgrupaEtapas!.Value,
            PermiteComplementacao!.Value,
            ColetaInscricao!.Value,
            ColetaSolicitacaoIsencao!.Value,
            OrganizadaEmChamadas!.Value);

    private FieldError?[] RecusasDeAusencia() =>
    [
        RecusaDeAusencia(AgrupaEtapas, "agrupaEtapas", "Informe se o tipo de fase agrupa as etapas pontuadas."),
        RecusaDeAusencia(PermiteComplementacao, "permiteComplementacao", "Informe se o tipo de fase admite complementação documental."),
        RecusaDeAusencia(ColetaInscricao, "coletaInscricao", "Informe se o tipo de fase coleta inscrições."),
        RecusaDeAusencia(ColetaSolicitacaoIsencao, "coletaSolicitacaoIsencao", "Informe se o tipo de fase coleta a solicitação de isenção da taxa."),
        RecusaDeAusencia(OrganizadaEmChamadas, "organizadaEmChamadas", "Informe se o tipo de fase é organizada em chamadas."),
    ];

    private static FieldError? RecusaDeAusencia(bool? marca, string campo, string orientacao) =>
        marca is null
            ? new(campo, new DomainError(CodigosDeValidacao.Obrigatorio, orientacao))
            : null;

    private FieldError?[] RecusasQueDependemDoCodigo(CodigoFase? codigoReconhecido)
    {
        if (codigoReconhecido is null)
        {
            return [];
        }

        string codigo = codigoReconhecido.Valor;

        return
        [
            RecusaDoAgrupamentoDeEtapas(codigo),
            RecusaDaComplementacao(codigo),
            RecusaDaMarcaDeSolicitacaoIsencao(codigo),
        ];
    }

    private FieldError? RecusaDoAgrupamentoDeEtapas(string codigo) =>
        AgrupaEtapas is true && !FaseCanonicaCatalogo.AdmiteAgruparEtapas(codigo)
            ? new("agrupaEtapas", new DomainError(
                FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao,
                "Apenas a fase de avaliação agrupa etapas pontuadas."))
            : null;

    private FieldError? RecusaDaComplementacao(string codigo) =>
        PermiteComplementacao is true && !FaseCanonicaCatalogo.AdmiteComplementacao(codigo)
            ? new("permiteComplementacao", new DomainError(
                FaseCanonicaErrorCodes.ComplementacaoApenasFasesPermitidas,
                "Complementação documental só é permitida na fase de homologação das inscrições."))
            : null;

    /// <remarks>
    /// Bidirecional: a marca só existe na fase de isenção, e nela é obrigatória. Aceitá-la
    /// falsa ali deixaria a fase indistinguível das demais no cronograma, e a janela publicaria
    /// sem passar por nenhuma das regras que existem para ela.
    /// </remarks>
    private FieldError? RecusaDaMarcaDeSolicitacaoIsencao(string codigo) =>
        ColetaSolicitacaoIsencao is { } marca
        && marca != string.Equals(codigo, FaseCanonicaCatalogo.CodigoSolicitacaoIsencao, StringComparison.Ordinal)
            ? new("coletaSolicitacaoIsencao", new DomainError(
                FaseCanonicaErrorCodes.SolicitacaoIsencaoApenasNaFaseDeIsencao,
                "A marca de solicitação de isenção pertence à fase de isenção, e só a ela."))
            : null;

    private FieldError? RecusaDaColetaSimultaneaDeInscricaoEIsencao() =>
        ColetaInscricao is true && ColetaSolicitacaoIsencao is true
            ? new("coletaSolicitacaoIsencao", new DomainError(
                FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas,
                "Uma fase não coleta inscrição e solicitação de isenção ao mesmo tempo — são períodos distintos, cada um com o seu prazo."))
            : null;
}
