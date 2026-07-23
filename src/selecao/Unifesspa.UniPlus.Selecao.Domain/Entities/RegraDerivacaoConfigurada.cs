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

    public static Result<RegraDerivacaoConfigurada> Criar(
        int ordem,
        string contribui,
        IReadOnlyList<CondicaoRegraDerivacao>? condicoes)
    {
        if (ordem < 0)
        {
            return Result<RegraDerivacaoConfigurada>.Failure(new DomainError(
                RegraDerivacaoConfiguradaErrorCodes.OrdemInvalida,
                "A ordem da regra não pode ser negativa."));
        }

        if (string.IsNullOrWhiteSpace(contribui))
        {
            return Result<RegraDerivacaoConfigurada>.Failure(new DomainError(
                RegraDerivacaoConfiguradaErrorCodes.ContribuiObrigatorio,
                "Uma regra de derivação precisa contribuir um código."));
        }

        RegraDerivacaoConfigurada regra = new() { Ordem = ordem, Contribui = contribui.Trim() };
        foreach (CondicaoRegraDerivacao condicao in condicoes ?? [])
        {
            condicao.VincularRegra(regra.Id);
            regra._condicoes.Add(condicao);
        }

        return Result<RegraDerivacaoConfigurada>.Success(regra);
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
