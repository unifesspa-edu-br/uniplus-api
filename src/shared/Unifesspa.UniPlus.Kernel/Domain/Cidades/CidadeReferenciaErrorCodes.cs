namespace Unifesspa.UniPlus.Kernel.Domain.Cidades;

/// <summary>
/// Códigos de erro da validação da referência de cidade do Geo (ADR-0090):
/// compartilhados por qualquer módulo que guarde <c>cidade_codigo_ibge</c> +
/// display cache em vez de uma FK cross-banco (ex.: <c>Campus</c>/
/// <c>LocalOferta</c> em Configuração, <c>Instituicao</c> em Organização).
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
    public const string NomeTamanho = "CidadeReferencia.NomeTamanho";
}
