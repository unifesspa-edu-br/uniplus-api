namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As marcas que dizem o que uma <see cref="Entities.FaseCanonica"/> faz no certame: se agrupa as
/// etapas pontuadas, se admite complementação documental, se coleta inscrição e se coleta a
/// solicitação de isenção da taxa. Andam juntas porque a coerência entre elas e o código da fase
/// é regra do conjunto: nenhuma marca se confere sozinha.
/// </summary>
/// <remarks>
/// O valor não se recusa ao ser criado: a fase o confere contra o código que a identifica, e o
/// cadastro o grava como colunas da própria linha da fase. A Seleção decide pelas marcas que
/// copia para o snapshot da fase, nunca pelo código (ADR-0113).
/// </remarks>
/// <param name="AgrupaEtapas">Verdadeiro quando a fase agrupa as etapas pontuadas — só a de avaliação.</param>
/// <param name="PermiteComplementacao">Verdadeiro quando a fase admite complementação documental — só onde a lei permite.</param>
/// <param name="ColetaInscricao">Verdadeiro quando a fase coleta inscrições — decide o piso mínimo do cronograma quando a origem dos candidatos é inscrição própria.</param>
/// <param name="ColetaSolicitacaoIsencao">Verdadeiro quando a fase abre a janela em que o candidato pede isenção da taxa — exclusiva da fase de solicitação de isenção e incompatível com a coleta de inscrição.</param>
public sealed record SinalizadoresDaFase(
    bool AgrupaEtapas,
    bool PermiteComplementacao,
    bool ColetaInscricao,
    bool ColetaSolicitacaoIsencao)
{
    /// <summary>
    /// As recusas das marcas, acumuladas na ordem: agrupamento de etapas, complementação, marca de
    /// solicitação de isenção e exclusividade entre coletar inscrição e coletar solicitação de
    /// isenção. As três primeiras conferem as marcas contra o código e só valem para um
    /// <paramref name="codigoReconhecido"/> (formato válido e pertencente ao conjunto canônico);
    /// com <see langword="null"/> o código já foi recusado, e relatá-las seria um segundo erro
    /// derivado do primeiro. A exclusividade não depende do código e é sempre conferida.
    /// </summary>
    internal IReadOnlyList<FieldError> Recusas(CodigoFase? codigoReconhecido)
    {
        FieldError?[] recusas =
        [
            .. RecusasQueDependemDoCodigo(codigoReconhecido),
            RecusaDaColetaSimultaneaDeInscricaoEIsencao(),
        ];

        return [.. recusas.OfType<FieldError>()];
    }

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
        AgrupaEtapas && !FaseCanonicaCatalogo.AdmiteAgruparEtapas(codigo)
            ? new("agrupaEtapas", new DomainError(
                FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao,
                "Apenas a fase de avaliação agrupa etapas pontuadas."))
            : null;

    private FieldError? RecusaDaComplementacao(string codigo) =>
        PermiteComplementacao && !FaseCanonicaCatalogo.AdmiteComplementacao(codigo)
            ? new("permiteComplementacao", new DomainError(
                FaseCanonicaErrorCodes.ComplementacaoApenasFasesPermitidas,
                "Complementação documental só é permitida nas fases de homologação e recursos."))
            : null;

    /// <remarks>
    /// Bidirecional: a marca só existe na fase de isenção, e nela é obrigatória. Aceitá-la
    /// falsa ali deixaria a fase indistinguível das demais no cronograma, e a janela publicaria
    /// sem passar por nenhuma das regras que existem para ela.
    /// </remarks>
    private FieldError? RecusaDaMarcaDeSolicitacaoIsencao(string codigo) =>
        ColetaSolicitacaoIsencao != string.Equals(
            codigo, FaseCanonicaCatalogo.CodigoSolicitacaoIsencao, StringComparison.Ordinal)
            ? new("coletaSolicitacaoIsencao", new DomainError(
                FaseCanonicaErrorCodes.SolicitacaoIsencaoApenasNaFaseDeIsencao,
                "A marca de solicitação de isenção pertence à fase de isenção, e só a ela."))
            : null;

    private FieldError? RecusaDaColetaSimultaneaDeInscricaoEIsencao() =>
        ColetaInscricao && ColetaSolicitacaoIsencao
            ? new("coletaSolicitacaoIsencao", new DomainError(
                FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas,
                "Uma fase não coleta inscrição e solicitação de isenção ao mesmo tempo — são períodos distintos, cada um com o seu prazo."))
            : null;
}
