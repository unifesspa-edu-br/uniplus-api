namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// Alcance pedido para a concessão. A coerência entre o tipo e a unidade é conferida por
/// quem concede, para que a incoerência volte como recusa e não como exceção.
/// </summary>
public sealed record EscopoDaConcessao(TipoDeEscopo Tipo, Guid? UnidadeId)
{
    public static EscopoDaConcessao DaUnidade(Guid unidadeId) => new(TipoDeEscopo.Unidade, unidadeId);

    public static EscopoDaConcessao DaInstituicao() => new(TipoDeEscopo.Instituicao, null);
}
