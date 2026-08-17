namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma regra de derivação configurada num processo (Story #927): quando o predicado
/// <c>quando</c> — formado pelas <see cref="Condicoes"/> — é verdadeiro, a regra contribui o código
/// <see cref="Contribui"/> para o conjunto derivado. É a forma persistida de
/// <see cref="ValueObjects.RegraDerivacao"/>.
/// </summary>
/// <remarks>
/// A regra <b>âncora</b> (incondicional) é a que não tem condição alguma: predicado de DNF vazio,
/// que resolve verdadeiro sempre. Não há sentinela textual — a ausência de condições é a âncora.
/// </remarks>
public sealed class RegraDerivacaoConfigurada : EntityBase
{
    private readonly List<CondicaoRegraDerivacao> _condicoes = [];

    public Guid ConfiguracaoDerivacaoFatoId { get; private set; }

    /// <summary>Ordinal da regra na configuração — total e único, para serialização determinística.</summary>
    public int Ordem { get; private set; }

    /// <summary>Código de valor do domínio do fato que a regra contribui quando ativa.</summary>
    public string Contribui { get; private set; } = string.Empty;

    public IReadOnlyCollection<CondicaoRegraDerivacao> Condicoes => _condicoes.AsReadOnly();

    private RegraDerivacaoConfigurada() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — ordem e
    /// contribuição não dependem uma da outra.
    /// </summary>
    public static Result<RegraDerivacaoConfigurada> Criar(
        int ordem,
        string contribui,
        IReadOnlyList<CondicaoRegraDerivacao>? condicoes)
    {
        List<FieldError> erros = ValidarFormaBasica(ordem, contribui);
        if (erros.Count > 0)
        {
            return Result<RegraDerivacaoConfigurada>.ValidationFailure(erros);
        }

        RegraDerivacaoConfigurada regra = new() { Ordem = ordem, Contribui = contribui.Trim() };
        foreach (CondicaoRegraDerivacao condicao in condicoes ?? [])
        {
            condicao.VincularRegra(regra.Id);
            regra._condicoes.Add(condicao);
        }

        return Result<RegraDerivacaoConfigurada>.Success(regra);
    }

    /// <summary>
    /// Ordem e contribuição não dependem do vocabulário cross-módulo nem de condições já
    /// resolvidas. Existe separada para o handler poder confirmar a forma de TODAS as regras do
    /// payload numa primeira passada, antes de resolver o catálogo (mesmo padrão de
    /// <c>FatoColetado.ValidarFormaBasica</c>, PR #1214 — evita que um <c>contribui</c> vazio
    /// caia num erro semântico menos específico, e que uma violação de forma seja mascarada pelo
    /// erro semântico de outra regra do mesmo payload).
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(int ordem, string? contribui)
    {
        List<FieldError> erros = [];

        if (ordem < 0)
        {
            erros.Add(new("ordem", new DomainError(
                RegraDerivacaoConfiguradaErrorCodes.OrdemInvalida,
                "A ordem da regra não pode ser negativa.")));
        }

        if (string.IsNullOrWhiteSpace(contribui))
        {
            erros.Add(new("contribui", new DomainError(
                RegraDerivacaoConfiguradaErrorCodes.ContribuiObrigatorio,
                "Uma regra de derivação precisa contribuir um código.")));
        }

        return erros;
    }

    internal void VincularConfiguracao(Guid configuracaoDerivacaoFatoId) =>
        ConfiguracaoDerivacaoFatoId = configuracaoDerivacaoFatoId;

    /// <summary>
    /// Reconstrói o VO <see cref="ValueObjects.RegraDerivacao"/> — o predicado é montado das
    /// condições agrupadas por cláusula; sem condições, o predicado é vazio (âncora).
    /// </summary>
    internal Result<RegraDerivacao> ParaRegraDerivacao()
    {
        List<(int Clausula, CondicaoDnf Condicao)> agrupadas = new(_condicoes.Count);
        foreach (CondicaoRegraDerivacao condicao in _condicoes)
        {
            Result<CondicaoDnf> condicaoResult = condicao.ParaCondicaoDnf();
            if (condicaoResult.IsFailure)
            {
                return Result<RegraDerivacao>.Failure(condicaoResult.Error!);
            }

            agrupadas.Add((condicao.Clausula, condicaoResult.Value!));
        }

        Result<PredicadoDnf> quandoResult = PredicadoDnf.CriarDeCondicoesAgrupadas(agrupadas);
        if (quandoResult.IsFailure)
        {
            return Result<RegraDerivacao>.Failure(quandoResult.Error!);
        }

        return RegraDerivacao.Criar(quandoResult.Value!, Contribui);
    }
}

/// <summary>Códigos de erro de <see cref="RegraDerivacaoConfigurada"/>.</summary>
public static class RegraDerivacaoConfiguradaErrorCodes
{
    public const string OrdemInvalida = "RegraDerivacaoConfigurada.OrdemInvalida";
    public const string ContribuiObrigatorio = "RegraDerivacaoConfigurada.ContribuiObrigatorio";
}
