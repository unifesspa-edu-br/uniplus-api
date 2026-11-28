namespace Unifesspa.UniPlus.Kernel.Domain.Cidades;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os nomes que o pedido dá aos três dados da cidade. A regra de cidade (<see cref="ReferenciaCidadeGeo.Validar"/>)
/// devolve as causas sem campo; este tipo põe em cada uma o campo do pedido que ela descreve (ADR-0023), sem o qual
/// toda causa de cidade apontaria o código do IBGE, mesmo quando é o nome ou a UF que está errado.
/// </summary>
public sealed record CamposDaCidade(string CodigoIbge, string Nome, string Uf)
{
    public IReadOnlyList<FieldError> Apontar(IReadOnlyList<FieldError> causas)
    {
        ArgumentNullException.ThrowIfNull(causas);

        return [.. causas.Select(causa => causa with { Field = CampoDaCausa(causa.Error.Code) })];
    }

    private string CampoDaCausa(string codigoDoErro) => codigoDoErro switch
    {
        CidadeReferenciaErrorCodes.NomeObrigatorio
            or CidadeReferenciaErrorCodes.NomeCaractereInvalido
            or CidadeReferenciaErrorCodes.NomeTamanho => Nome,
        CidadeReferenciaErrorCodes.UfObrigatoria
            or CidadeReferenciaErrorCodes.UfIncoerente => Uf,
        _ => CodigoIbge,
    };
}
