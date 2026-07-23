namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma condição do predicado <c>quando</c> de uma <see cref="RegraDerivacaoConfigurada"/> (Story
/// #927) — a forma <b>relacional</b> (linha com ordinal de cláusula) da tripla
/// <c>{ Fato, Operador, Valor }</c>. Agrupadas por <see cref="Clausula"/> (OU entre cláusulas, E
/// dentro), formam o predicado que ativa a regra.
/// </summary>
/// <remarks>
/// Mesma forma de <see cref="CondicaoPrecondicaoFato"/> e <see cref="CondicaoGatilho"/>, e
/// deliberadamente separada delas: os três ciclos de vida são distintos (pré-condição do campo,
/// gatilho da exigência, regra de derivação), e cada um é substituído com o seu pai. O reúso está
/// onde importa — todas produzem <see cref="CondicaoDnf"/> e são avaliadas pelo mesmo
/// <see cref="ValueObjects.PredicadoDnf"/>.
/// </remarks>
public sealed class CondicaoRegraDerivacao : EntityBase
{
    public Guid RegraDerivacaoConfiguradaId { get; private set; }

    /// <summary>Ordinal da cláusula — OU entre cláusulas, E dentro da mesma cláusula.</summary>
    public int Clausula { get; private set; }

    /// <summary>Código do fato citado no predicado da regra.</summary>
    public string Fato { get; private set; } = string.Empty;

    public Operador Operador { get; private set; }

    public JsonElement Valor { get; private set; }

    private CondicaoRegraDerivacao() { }

    /// <summary>
    /// Cria a condição validando a <b>forma</b> via <see cref="CondicaoDnf.Criar"/>. A validação
    /// semântica — fato no vocabulário fechado, operador × domínio, valor × domínio — é do
    /// <see cref="Services.PredicadoDnfValidador"/>, resolvido pela Application, que tem acesso ao
    /// vocabulário cross-módulo.
    /// </summary>
    public static Result<CondicaoRegraDerivacao> Criar(int clausula, string fato, Operador operador, JsonElement valor)
    {
        if (clausula < 0)
        {
            return Result<CondicaoRegraDerivacao>.Failure(new DomainError(
                CondicaoRegraDerivacaoErrorCodes.ClausulaInvalida,
                "O ordinal da cláusula não pode ser negativo."));
        }

        Result<CondicaoDnf> condicaoResult = CondicaoDnf.Criar(fato, operador, valor);
        if (condicaoResult.IsFailure)
        {
            return Result<CondicaoRegraDerivacao>.Failure(condicaoResult.Error!);
        }

        CondicaoDnf condicao = condicaoResult.Value!;
        return Result<CondicaoRegraDerivacao>.Success(new CondicaoRegraDerivacao
        {
            Clausula = clausula,
            Fato = condicao.Fato,
            Operador = condicao.Operador,
            Valor = condicao.Valor,
        });
    }

    internal void VincularRegra(Guid regraDerivacaoConfiguradaId) =>
        RegraDerivacaoConfiguradaId = regraDerivacaoConfiguradaId;

    /// <summary>
    /// Reconstrói o VO a partir da linha persistida. A forma foi provada em <see cref="Criar"/>, mas
    /// o banco não a impõe: propaga o <see cref="Result{T}"/> para que uma linha corrompida recuse
    /// como falha de domínio, nunca lance ao reidratar.
    /// </summary>
    internal Result<CondicaoDnf> ParaCondicaoDnf() => CondicaoDnf.Criar(Fato, Operador, Valor);
}

/// <summary>Códigos de erro de <see cref="CondicaoRegraDerivacao"/>.</summary>
public static class CondicaoRegraDerivacaoErrorCodes
{
    public const string ClausulaInvalida = "CondicaoRegraDerivacao.ClausulaInvalida";
}
