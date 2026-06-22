namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;

using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Converte o <see cref="EnderecoGeoInput"/> do payload no value object
/// <see cref="ReferenciaEnderecoGeo"/>, carimbando server-side o instante do
/// display cache (ADR-0090/ADR-0096). A validação de formato é delegada ao VO.
/// </summary>
public static class EnderecoGeoInputMapping
{
    public static Result<ReferenciaEnderecoGeo> ParaReferencia(this EnderecoGeoInput input, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(input);
        return ReferenciaEnderecoGeo.Criar(
            input.Cep,
            input.Logradouro,
            input.Numero,
            input.Complemento,
            input.Bairro,
            input.Distrito,
            input.Cidade?.CodigoIbge,
            input.Cidade?.Nome,
            input.Cidade?.Uf,
            input.Latitude,
            input.Longitude,
            input.NivelResolucao,
            input.Origem,
            agora);
    }

    /// <summary>
    /// Resolve o endereço a persistir a partir do payload: ausente vira
    /// <see langword="null"/>; presente é validado e carimbado com
    /// <paramref name="agora"/>. Na atualização, preserva o instante do display
    /// cache de <paramref name="existente"/> quando o conteúdo não muda (espelha a
    /// semântica de re-carimbo da cidade). Retorna o erro de domínio na falha de
    /// formato, sem lançar.
    /// </summary>
    public static (DomainError? Error, ReferenciaEnderecoGeo? Endereco) Resolver(
        EnderecoGeoInput? input,
        ReferenciaEnderecoGeo? existente,
        DateTimeOffset agora)
    {
        if (input is null)
        {
            return (null, null);
        }

        Result<ReferenciaEnderecoGeo> resultado = input.ParaReferencia(agora);
        if (resultado.IsFailure)
        {
            return (resultado.Error, null);
        }

        ReferenciaEnderecoGeo novo = resultado.Value!;
        return existente is not null && novo.ConteudoEquivale(existente)
            ? (null, existente)
            : (null, novo);
    }
}
