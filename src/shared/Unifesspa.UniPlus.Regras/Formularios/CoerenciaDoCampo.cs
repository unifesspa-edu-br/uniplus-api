namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// O tipo de campo segue o domínio e a cardinalidade do fato no catálogo, no processo e no modelo:
/// booleano em campo de sim/não, numérico em campo numérico, texto, data e endereço de resposta
/// única nos campos deles, e categórico em seleção única ou múltipla conforme a cardinalidade — salvo
/// o município do Geo, de resposta única no campo de município.
/// </summary>
public static class CoerenciaDoCampo
{
    private const string Booleano = DominioDoCatalogo.Booleano;
    private const string Numerico = DominioDoCatalogo.Numerico;
    private const string Categorico = DominioDoCatalogo.Categorico;
    private const string Texto = DominioDoCatalogo.Texto;
    private const string Data = DominioDoCatalogo.Data;
    private const string Endereco = DominioDoCatalogo.Endereco;
    private const string Multivalorado = DominioDoCatalogo.Multivalorado;

    /// <summary>
    /// O único tipo de campo que o domínio, a cardinalidade e a fonte de valores do fato admitem, dados
    /// pelos tokens do catálogo; <see langword="null"/> quando nenhum admite — o campo de texto, de data
    /// e de endereço recolhe uma resposta só, e o município do Geo também.
    /// </summary>
    public static TipoRenderizacao? TipoDoFato(string dominio, string cardinalidade, string? fonteValores)
    {
        bool multivalorado = string.Equals(cardinalidade, Multivalorado, StringComparison.Ordinal);
        bool municipio = string.Equals(fonteValores, DominioDoCatalogo.FonteGeoMunicipio, StringComparison.Ordinal);
        return dominio switch
        {
            Booleano => TipoRenderizacao.Booleano,
            Numerico => TipoRenderizacao.Numero,
            Texto when !multivalorado => TipoRenderizacao.Texto,
            Data when !multivalorado => TipoRenderizacao.Data,
            Endereco when !multivalorado => TipoRenderizacao.Endereco,
            Categorico when municipio => multivalorado ? null : TipoRenderizacao.Municipio,
            Categorico => multivalorado ? TipoRenderizacao.SelecaoMultipla : TipoRenderizacao.SelecaoUnica,
            _ => null,
        };
    }

    /// <summary>Recusa o tipo de campo diferente do que o fato admite (<see cref="TipoDoFato"/>).</summary>
    public static DomainError? Validar(string fatoCodigo, TipoRenderizacao tipoRenderizacao, string dominio, string cardinalidade, string? fonteValores) =>
        TipoDoFato(dominio, cardinalidade, fonteValores) == tipoRenderizacao
            ? null
            : new DomainError(
                ItemFormularioErrorCodes.TipoRenderizacaoIncoerenteComDominio,
                $"O tipo de campo '{tipoRenderizacao}' não é coerente com o domínio '{dominio}', a cardinalidade '{cardinalidade}' e a fonte de valores '{fonteValores ?? "nenhuma"}' do fato '{fatoCodigo}'.");
}
