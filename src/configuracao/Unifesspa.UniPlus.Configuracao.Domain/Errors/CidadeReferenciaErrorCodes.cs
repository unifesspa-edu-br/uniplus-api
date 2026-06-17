namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Códigos de erro da validação da referência de cidade do Geo (ADR-0090):
/// compartilhados por <c>Campus</c> e <c>LocalOferta</c>, que guardam
/// <c>cidade_codigo_ibge</c> + display cache em vez de uma FK cross-banco.
/// A validação é apenas de formato + coerência de UF (sem dígito verificador,
/// sem consultar o Geo).
/// </summary>
public static class CidadeReferenciaErrorCodes
{
    public const string CodigoIbgeObrigatorio = "CidadeReferencia.CodigoIbgeObrigatorio";
    public const string CodigoIbgeFormatoInvalido = "CidadeReferencia.CodigoIbgeFormatoInvalido";
    public const string UfObrigatoria = "CidadeReferencia.UfObrigatoria";
    public const string UfIncoerente = "CidadeReferencia.UfIncoerente";
    public const string NomeObrigatorio = "CidadeReferencia.NomeObrigatorio";
}
