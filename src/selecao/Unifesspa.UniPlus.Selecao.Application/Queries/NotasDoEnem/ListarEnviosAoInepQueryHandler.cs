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
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IGeracaoDeEnvioAoInepRepository geracoes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(geracoes);

        Result<EscopoDeNotasDoEnem> escopo = await ResolvedorDoEscopoDeNotasDoEnem
            .ResolverAsync(
                query.ProcessoSeletivoId, query.EtapaProcessoId, query.Edicao, exigeNotaDoEnem: false,
                certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (escopo.IsFailure)
        {
            return Result<IReadOnlyList<GeracaoDeEnvioDto>>.Failure(escopo.Error!);
        }

        IReadOnlyList<GeracaoDeEnvioAoInep> historico = await geracoes
            .ListarAsync(query.ProcessoSeletivoId, query.EtapaProcessoId, query.Edicao, cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<GeracaoDeEnvioDto>>.Success([.. historico.Select(g => new GeracaoDeEnvioDto(
            g.Id, g.EdicaoCodigo, g.LeiauteCodigo, g.LeiauteVersao, g.GeradoPor, g.GeradoEm, g.TotalDeCpfs))]);
    }
}
