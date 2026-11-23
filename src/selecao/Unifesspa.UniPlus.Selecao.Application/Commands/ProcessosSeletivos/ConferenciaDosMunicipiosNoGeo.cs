namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// Consulta o Geo uma vez por município declarado, na ordem da declaração, quando a lista respeita o limite vigente.
/// Toda recusa deixa o município não conferido; a indisponibilidade interrompe a consulta.
/// </summary>
public sealed class ConferenciaDosMunicipiosNoGeo(IConferenciaEnderecoGeo geo, IOptions<ProvaPresencialOptions> options)
{
    public Task<Result<MunicipiosConferidosNoGeo>> ConferirAsync(
        DeclaracaoDaProvaPresencial? declaracao, CancellationToken cancellationToken) =>
        declaracao is { TemProvaPresencial: true } presencial && presencial.Municipios.Count <= options.Value.LimiteDeMunicipios
            ? ConferirDeclaradosAsync(presencial.Municipios, cancellationToken)
            : Task.FromResult(Result<MunicipiosConferidosNoGeo>.Success(MunicipiosConferidosNoGeo.Nenhum));

    private async Task<Result<MunicipiosConferidosNoGeo>> ConferirDeclaradosAsync(
        IReadOnlyList<MunicipioDeProva> municipios, CancellationToken cancellationToken)
    {
        Dictionary<string, ResultadoDoMunicipioNoGeo> resultados = new(StringComparer.Ordinal);
        foreach (MunicipioDeProva municipio in municipios)
        {
            Result<string> resultado = await geo.ConferirMunicipioAsync(municipio.CodigoIbge, municipio.Uf, cancellationToken).ConfigureAwait(false);
            if (resultado.IsFailure && resultado.Error!.Code == ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel)
            {
                return Result<MunicipiosConferidosNoGeo>.Failure(resultado.Error);
            }

            resultados[municipio.CodigoIbge] = Traduzir(resultado);
        }

        return Result<MunicipiosConferidosNoGeo>.Success(MunicipiosConferidosNoGeo.De(resultados));
    }

    private static ResultadoDoMunicipioNoGeo Traduzir(Result<string> resultado) => resultado.IsSuccess
        ? ResultadoDoMunicipioNoGeo.Conferido(resultado.Value!)
        : ResultadoDoMunicipioNoGeo.NaoConferido;
}
