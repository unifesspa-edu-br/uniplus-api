namespace Unifesspa.UniPlus.Kernel.Domain.Enderecos;

/// <summary>
/// Códigos de erro de domínio da referência de endereço estruturado ao Geo
/// (<see cref="ReferenciaEnderecoGeo"/>, ADR-0096). Compartilhados pelas
/// entidades que adotam o padrão (Campus, LocalOferta, Instituicao). A
/// validação do trio de cidade do snapshot reusa
/// <c>CidadeReferenciaErrorCodes</c> (referência de cidade, ADR-0090).
/// </summary>
public static class EnderecoReferenciaErrorCodes
{
    public const string CepObrigatorio = "EnderecoReferencia.CepObrigatorio";
    public const string CepFormatoInvalido = "EnderecoReferencia.CepFormatoInvalido";
    public const string LogradouroTamanho = "EnderecoReferencia.LogradouroTamanho";
    public const string NumeroTamanho = "EnderecoReferencia.NumeroTamanho";
    public const string ComplementoTamanho = "EnderecoReferencia.ComplementoTamanho";
    public const string BairroTamanho = "EnderecoReferencia.BairroTamanho";
    public const string DistritoTamanho = "EnderecoReferencia.DistritoTamanho";
    public const string NivelResolucaoObrigatorio = "EnderecoReferencia.NivelResolucaoObrigatorio";
    public const string NivelResolucaoInvalido = "EnderecoReferencia.NivelResolucaoInvalido";
    public const string OrigemObrigatoria = "EnderecoReferencia.OrigemObrigatoria";
    public const string OrigemTamanho = "EnderecoReferencia.OrigemTamanho";
    public const string LatitudeForaDeFaixa = "EnderecoReferencia.LatitudeForaDeFaixa";
    public const string LongitudeForaDeFaixa = "EnderecoReferencia.LongitudeForaDeFaixa";

    /// <summary>
    /// O snapshot de cidade do endereço (derivado do CEP) diverge da referência
    /// de cidade da entidade — mesmo <c>codigoIbge</c>/UF são exigidos quando
    /// ambos existem (CA-04 da #726).
    /// </summary>
    public const string CidadeIncoerente = "EnderecoReferencia.CidadeIncoerente";

    /// <summary>
    /// Há endereço estruturado mas a referência de cidade da entidade está
    /// ausente. Como o CEP sempre resolve ao menos a uma cidade, um endereço
    /// presente exige a cidade da entidade preenchida e coerente (relevante para
    /// a Instituicao, cuja cidade é opcional all-or-nothing).
    /// </summary>
    public const string CidadeObrigatoriaComEndereco = "EnderecoReferencia.CidadeObrigatoriaComEndereco";
}
