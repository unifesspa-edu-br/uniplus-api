namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// O tipo de campo segue o domínio e a cardinalidade do fato no catálogo, no processo e no modelo:
/// booleano em campo de sim/não, numérico em campo numérico, texto, data e endereço de resposta
/// única nos campos deles, e categórico em seleção única ou múltipla conforme a cardinalidade.
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
    /// Recusa o tipo de campo incoerente com o domínio e a cardinalidade do fato, dados pelos
    /// tokens do catálogo. O campo de texto recolhe uma resposta só: não há várias respostas de texto.
    /// </summary>
    public static DomainError? Validar(string fatoCodigo, TipoRenderizacao tipoRenderizacao, string dominio, string cardinalidade)
    {
        bool multivalorado = string.Equals(cardinalidade, Multivalorado, StringComparison.Ordinal);
        bool coerente = dominio switch
        {
            Booleano => tipoRenderizacao == TipoRenderizacao.Booleano,
            Numerico => tipoRenderizacao == TipoRenderizacao.Numero,
            Texto => tipoRenderizacao == TipoRenderizacao.Texto && !multivalorado,
            Data => tipoRenderizacao == TipoRenderizacao.Data && !multivalorado,
            Endereco => tipoRenderizacao == TipoRenderizacao.Endereco && !multivalorado,
            Categorico => tipoRenderizacao == (multivalorado ? TipoRenderizacao.SelecaoMultipla : TipoRenderizacao.SelecaoUnica),
            _ => false,
        };

        return coerente
            ? null
            : new DomainError(
                ItemFormularioErrorCodes.TipoRenderizacaoIncoerenteComDominio,
                $"O tipo de campo '{tipoRenderizacao}' não é coerente com o domínio '{dominio}'/cardinalidade '{cardinalidade}' do fato '{fatoCodigo}'.");
    }
}
