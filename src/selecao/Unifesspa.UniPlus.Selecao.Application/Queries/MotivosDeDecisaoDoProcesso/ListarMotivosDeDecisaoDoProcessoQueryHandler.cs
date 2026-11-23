namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisaoDoProcesso;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Handler convention-based da leitura dos motivos escolhidos pelo processo, na escolha atual ou numa versão publicada.</summary>
public static class ListarMotivosDeDecisaoDoProcessoQueryHandler
{
    public static async Task<Result<IReadOnlyList<MotivoDeDecisaoDoProcessoDto>>> Handle(
        ListarMotivosDeDecisaoDoProcessoQuery query,
        IMotivoDeDecisaoDoProcessoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        IReadOnlyList<MotivoDeDecisaoDoProcesso>? escolhidos = await ListarAsync(query, repository, cancellationToken).ConfigureAwait(false);
        return escolhidos is null
            ? Result<IReadOnlyList<MotivoDeDecisaoDoProcessoDto>>.Failure(VersaoNaoPublicada(query.NumeroDaVersao!.Value))
            : Result<IReadOnlyList<MotivoDeDecisaoDoProcessoDto>>.Success([.. Ordenados(escolhidos).Select(MotivoDeDecisaoMapping.ToDto)]);
    }

    // A versão anterior à primeira nunca existiu, e a consulta dispensa o banco para dizê-lo.
    private static async Task<IReadOnlyList<MotivoDeDecisaoDoProcesso>?> ListarAsync(
        ListarMotivosDeDecisaoDoProcessoQuery query, IMotivoDeDecisaoDoProcessoRepository repository, CancellationToken cancellationToken) =>
        query.NumeroDaVersao switch
        {
            null => await repository.ListarEscolhaAtualAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false),
            < VersaoConfiguracao.NumeroDaAbertura => null,
            { } numero => await repository.ListarDaVersaoPublicadaAsync(query.ProcessoSeletivoId, numero, cancellationToken).ConfigureAwait(false),
        };

    private static IOrderedEnumerable<MotivoDeDecisaoDoProcesso> Ordenados(IEnumerable<MotivoDeDecisaoDoProcesso> escolhidos) =>
        escolhidos
            .OrderBy(static e => e.Tipo)
            .ThenBy(static e => e.Codigo.Valor, StringComparer.Ordinal)
            .ThenBy(static e => e.Lugar.FaseCanonicaOrigemId ?? e.Lugar.EtapaProcessoId);

    private static DomainError VersaoNaoPublicada(int numeroDaVersao) =>
        new(MotivoDeDecisaoErrorCodes.VersaoNaoPublicada, $"O processo não tem a versão {numeroDaVersao} do edital publicada.");
}
