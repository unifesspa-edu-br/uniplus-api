namespace Unifesspa.UniPlus.Selecao.Application.Queries.NotasDoEnem;

using Abstractions;

using Commands.NotasDoEnem;

using Domain.Entities;
using Domain.Interfaces;

using DTOs.NotasDoEnem;

using Kernel.Results;

/// <summary>Histórico das gerações do envio ao INEP: quem gerou, quando, com que leiaute e quantos CPFs.</summary>
public static class ListarEnviosAoInepQueryHandler
{
    public static async Task<Result<IReadOnlyList<GeracaoDeEnvioDto>>> Handle(
        ListarEnviosAoInepQuery query,
        ResolvedorDoEscopoDeNotasDoEnem escopos,
        IGeracaoDeEnvioAoInepRepository geracoes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(geracoes);

        Result<EscopoDeNotasDoEnem> escopo = await escopos
            .ResolverAsync(
                query.ProcessoSeletivoId, query.EtapaProcessoId, query.Edicao, exigeNotaDoEnem: false,
                cancellationToken)
            .ConfigureAwait(false);
        return escopo.IsFailure ? Result<IReadOnlyList<GeracaoDeEnvioDto>>.Failure(escopo.Error!)
            : await ConsultarAsync(query, geracoes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<IReadOnlyList<GeracaoDeEnvioDto>>> ConsultarAsync(ListarEnviosAoInepQuery query,
        IGeracaoDeEnvioAoInepRepository geracoes, CancellationToken cancellationToken)
    {

        IReadOnlyList<GeracaoDeEnvioAoInep> historico = await geracoes
            .ListarAsync(query.ProcessoSeletivoId, query.EtapaProcessoId, query.Edicao, cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<GeracaoDeEnvioDto>>.Success([.. historico.Select(g => new GeracaoDeEnvioDto(
            g.Id, g.EdicaoCodigo, g.LeiauteCodigo, g.LeiauteVersao, g.GeradoPor, g.GeradoPorNome, g.GeradoEm, g.TotalDeCpfs))]);
    }
}
