namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Como um fato que o formulário cita sem perguntar se apresenta ao candidato: o nome do fato no
/// catálogo e o tipo de campo que o domínio dele admite. Nulo o tipo quando o catálogo não admite
/// nenhum (<see cref="CoerenciaDoCampo.TipoDoFato"/>).
/// </summary>
public sealed record ApresentacaoDoFato(string FatoCodigo, string Rotulo, string? TipoRenderizacao)
{
    /// <summary>A apresentação a partir dos tokens do catálogo.</summary>
    public static ApresentacaoDoFato DoCatalogo(string fatoCodigo, string nome, string dominio, string cardinalidade, string? fonteValores) =>
        new(fatoCodigo, nome, CoerenciaDoCampo.TipoDoFato(dominio, cardinalidade, fonteValores)?.ToCodigo());
}
