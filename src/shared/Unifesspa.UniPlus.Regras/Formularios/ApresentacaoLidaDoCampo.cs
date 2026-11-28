namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A apresentação do campo como a leitura da entrada a devolve, quando alguma parte pode ter sido recusada: o tipo
/// de campo nulo não foi lido, já tem a recusa dele, e o que depende do tipo não é conferido. O rótulo e a ajuda
/// ausentes chegam nulos; o rótulo ausente é recusado pela forma do item.
/// </summary>
/// <param name="Rotulo">O rótulo informado; nulo quando a apresentação não veio.</param>
/// <param name="TipoDeCampo">Nulo quando recusado.</param>
/// <param name="Ajuda">O texto de apoio informado, se houver.</param>
public sealed record ApresentacaoLidaDoCampo(string? Rotulo, TipoDeCampo? TipoDeCampo, string? Ajuda)
{
    /// <summary>A apresentação de um campo lido por inteiro.</summary>
    public static ApresentacaoLidaDoCampo De(ApresentacaoDoCampo apresentacao)
    {
        ArgumentNullException.ThrowIfNull(apresentacao);
        return new(apresentacao.Rotulo, apresentacao.TipoDeCampo, apresentacao.Ajuda);
    }

    /// <summary>A apresentação do pedido, com o tipo de campo que a leitura dele devolveu; a apresentação ausente não tem rótulo nem ajuda.</summary>
    public static ApresentacaoLidaDoCampo DaEntrada(ApresentacaoDoCampoInput? entrada, TipoDeCampo? tipoLido) =>
        new(entrada?.Rotulo, tipoLido, entrada?.Ajuda);
}
