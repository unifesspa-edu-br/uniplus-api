namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// O tipo de campo segue o tipo do dado e a quantidade de valores do fato no catálogo, no processo e no modelo:
/// sim ou não em campo de sim/não, número em campo numérico, texto, data e endereço de resposta
/// única nos campos deles, e opção em seleção única ou múltipla conforme a quantidade — salvo
/// o município do Geo, de resposta única no campo de município.
/// </summary>
public static class CoerenciaDoCampo
{
    /// <summary>
    /// O único tipo de campo que o tipo do dado, a quantidade de valores e a fonte de valores do fato admitem;
    /// <see langword="null"/> quando nenhum admite — o campo de texto, de data e de endereço recolhe uma resposta só, e o
    /// município do Geo também.
    /// </summary>
    public static TipoDeCampo? TipoDoFato(TipoDoDado tipo, QuantidadeDeValores quantidade, FonteValoresFato? fonteValores)
    {
        bool variosValores = quantidade == QuantidadeDeValores.VariosValores;
        bool municipio = fonteValores == FonteValoresFato.Municipios;
        return tipo switch
        {
            TipoDoDado.SimOuNao => TipoDeCampo.SimOuNao,
            TipoDoDado.Numerico => TipoDeCampo.Numero,
            TipoDoDado.Texto when !variosValores => TipoDeCampo.Texto,
            TipoDoDado.Data when !variosValores => TipoDeCampo.Data,
            TipoDoDado.Endereco when !variosValores => TipoDeCampo.Endereco,
            TipoDoDado.Opcao => TipoDaOpcao(municipio, variosValores),
            _ => null,
        };
    }

    private static TipoDeCampo? TipoDaOpcao(bool municipio, bool variosValores) => (municipio, variosValores) switch
    {
        (true, true) => null,
        (true, false) => TipoDeCampo.Municipio,
        (false, true) => TipoDeCampo.EscolhaMultipla,
        (false, false) => TipoDeCampo.EscolhaUnica,
    };

    /// <summary>Recusa o tipo de campo diferente do que o fato admite (<see cref="TipoDoFato"/>).</summary>
    public static DomainError? Validar(
        string fatoCodigo, TipoDeCampo tipoDeCampo, TipoDoDado tipo, QuantidadeDeValores quantidade, FonteValoresFato? fonteValores) =>
        TipoDoFato(tipo, quantidade, fonteValores) == tipoDeCampo
            ? null
            : new DomainError(
                ItemFormularioErrorCodes.TipoDeCampoIncoerenteComDominio,
                $"O tipo de campo '{tipoDeCampo.Codigo()}' não é coerente com o tipo do dado '{tipo.Codigo()}', a quantidade de valores '{quantidade.Codigo()}' e a fonte de valores '{fonteValores?.Codigo() ?? "nenhuma"}' do dado '{fatoCodigo}'.");
}
