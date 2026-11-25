namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

internal sealed class ConsultaDaVitrineDeTeste(ProcessoSeletivoDbFixture fixture)
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Limiar = TimeSpan.FromDays(7);
    private const string VersaoServida = "1";
    private readonly ProcessoSeletivoDbFixture _fixture = fixture;

    internal async Task<IReadOnlyList<string>> ListarNumerosAsync(RecorteDaVitrine recorte)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        PaginaDaVitrine pagina =
            await repository.ListarVitrineAsync(
                Agora, recorte, [], Limiar, null, null, 20, PaginationDirection.Next, incluirContadores: false, VersaoServida, CancellationToken.None);

        return [.. pagina.Itens.Select(static c => c.Numero!)];
    }

    internal async Task<IReadOnlyList<string>> ListarAsync(
        RecorteDaVitrine recorte,
        IReadOnlyList<SortField>? ordenacao = null,
        string? afterSortKey = null,
        Guid? afterId = null)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        PaginaDaVitrine pagina =
            await repository.ListarVitrineAsync(
                Agora, recorte, ordenacao ?? [], Limiar, afterSortKey, afterId, 20,
                PaginationDirection.Next, incluirContadores: false, VersaoServida, CancellationToken.None);

        return [.. pagina.Itens.Select(static c => c.Nome)];
    }

    internal async Task<(string SortKey, Guid Id)?> PrimeiraPaginaAsync(
        RecorteDaVitrine recorte,
        int limite,
        IReadOnlyList<SortField>? ordenacao = null)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        PaginaDaVitrine pagina =
            await repository.ListarVitrineAsync(
                Agora, recorte, ordenacao ?? [], Limiar, null, null, limite,
                PaginationDirection.Next, incluirContadores: false, VersaoServida, CancellationToken.None);

        return pagina.Proximo;
    }

    internal async Task<(IReadOnlyList<CertameDivulgado> Itens, (string SortKey, Guid Id)? Proximo)> PaginaAsync(
        IReadOnlyList<SortField> ordenacao,
        (string SortKey, Guid Id)? cursor,
        PaginationDirection direcao)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        PaginaDaVitrine pagina =
            await repository.ListarVitrineAsync(
                Agora, new RecorteDaVitrine(), ordenacao, Limiar, cursor?.SortKey, cursor?.Id, 1,
                direcao, incluirContadores: false, VersaoServida, CancellationToken.None);

        return (pagina.Itens, direcao == PaginationDirection.Prev ? pagina.Anterior : pagina.Proximo);
    }

    internal async Task<(IReadOnlyList<CertameDivulgado> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        PaginaCompletaAsync((string SortKey, Guid Id)? cursor, PaginationDirection direcao)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        PaginaDaVitrine pagina =
            await repository.ListarVitrineAsync(
                Agora, new RecorteDaVitrine(), [], Limiar, cursor?.SortKey, cursor?.Id, 1,
                direcao, incluirContadores: false, VersaoServida, CancellationToken.None);

        return (pagina.Itens, pagina.Anterior, pagina.Proximo);
    }

}
