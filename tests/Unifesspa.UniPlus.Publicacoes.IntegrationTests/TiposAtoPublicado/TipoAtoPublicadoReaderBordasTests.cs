namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Readers;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As bordas da janela semiaberta <c>[início, fim)</c> nas duas leituras do catálogo, a individual e a
/// em lote, contra Postgres real: as duas têm de concordar em cada dia.
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
public sealed class TipoAtoPublicadoReaderBordasTests
{
    private static readonly DateOnly Inicio = new(2026, 3, 10);
    private static readonly DateOnly Fim = new(2026, 4, 10);

    private readonly PublicacoesDbFixture _fixture;

    public TipoAtoPublicadoReaderBordasTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "Com fim, a versão vigora do dia do início até a véspera do dia do fim")]
    [InlineData(3, 9, false)]
    [InlineData(3, 10, true)]
    [InlineData(4, 9, true)]
    [InlineData(4, 10, false)]
    public async Task VersaoComFim_AsDuasLeiturasRespeitamAsBordas(int mes, int dia, bool vigente)
    {
        string codigo = await GravarAsync(fim: Fim);

        await ConferirAsync(codigo, new DateOnly(2026, mes, dia), vigente);
    }

    [Theory(DisplayName = "Sem fim, a versão vigora a partir do dia do início, sem limite")]
    [InlineData(2026, 3, 9, false)]
    [InlineData(2026, 3, 10, true)]
    [InlineData(2099, 12, 31, true)]
    public async Task VersaoSemFim_AsDuasLeiturasRespeitamAsBordas(int ano, int mes, int dia, bool vigente)
    {
        string codigo = await GravarAsync(fim: null);

        await ConferirAsync(codigo, new DateOnly(ano, mes, dia), vigente);
    }

    private async Task ConferirAsync(string codigo, DateOnly diaDaPublicacao, bool vigente)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new TipoAtoPublicadoReader(ctx);

        TipoAtoPublicadoView? individual = await reader.ObterVigenteAsync(codigo, diaDaPublicacao, CancellationToken.None);
        IReadOnlyList<TipoAtoPublicadoView> emLote = await reader.ListarVigentesAsync([codigo], diaDaPublicacao, CancellationToken.None);

        (individual is not null).Should().Be(vigente, "a leitura individual");
        emLote.Should().HaveCount(vigente ? 1 : 0, "a leitura em lote");
    }

    private async Task<string> GravarAsync(DateOnly? fim)
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado tipo = new TipoAtoPublicadoDeTeste
        {
            Codigo = codigo,
            VigenciaInicio = Inicio,
            VigenciaFim = fim,
        }.Criar().Value!;

        await using UniPlusDbContext ctx = _fixture.CreateDbContext("admin-publicacoes");
        ctx.TiposAtoPublicado.Add(tipo);
        await ctx.SaveChangesAsync();

        return codigo;
    }

    private static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "TESTE_" + string.Concat(hex.Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
    }
}
