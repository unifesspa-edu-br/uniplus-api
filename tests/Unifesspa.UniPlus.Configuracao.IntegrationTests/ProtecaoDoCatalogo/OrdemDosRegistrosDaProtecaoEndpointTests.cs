namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Os atos e as manifestações do mesmo item gravados no mesmo microssegundo saem na ordem em que foram registrados, mesmo
/// quando o registrado por último tem o identificador menor.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class OrdemDosRegistrosDaProtecaoEndpointTests : ManifestacaoDaProtecaoTestBase
{
    private const string Atos = "configuracao.protecao_do_catalogo_atos";
    private const string Manifestacoes = "configuracao.protecao_do_catalogo_manifestacoes";
    private const string MaiorIdentificador = "'ffffffff-ffff-7fff-bfff-ffffffffffff'::uuid";
    private const string MenorIdentificador = "'00000000-0000-7000-8000-000000000000'::uuid";

    // O primeiro registrado do item passa a ter o maior identificador, e o instante dos dois fica igual.
    private const string InverterOsAtos = $$"""
        UPDATE {{Atos}}
        SET id = CASE WHEN ordem_de_registro = (SELECT min(ordem_de_registro) FROM {{Atos}} WHERE item_id = {0})
                      THEN {{MaiorIdentificador}} ELSE {{MenorIdentificador}} END,
            instante = {1}
        WHERE item_id = {0}
        """;

    private const string InverterAsManifestacoes = $$"""
        UPDATE {{Manifestacoes}}
        SET id = CASE WHEN ordem_de_registro = (SELECT min(ordem_de_registro) FROM {{Manifestacoes}} WHERE item_id = {0})
                      THEN {{MaiorIdentificador}} ELSE {{MenorIdentificador}} END,
            instante = {1}
        WHERE item_id = {0}
        """;

    private static readonly DateTimeOffset MesmoInstante = new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);

    public OrdemDosRegistrosDaProtecaoEndpointTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Com o mesmo instante, as manifestações do item saem pela ordem de registro, e não pelo identificador")]
    public async Task Manifestacoes_MesmoInstante_PelaOrdemDeRegistro()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "TIPO_DOCUMENTO");
        (await ManifestarAsync(client, Kevin, "TIPO_DOCUMENTO", id, "ADEQUADA", texto: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ManifestarAsync(client, Lucia, "TIPO_DOCUMENTO", id, "ADEQUADA", texto: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ExecutarAsync(InverterAsManifestacoes, id);

        IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo> eventos = await RecarregarAsync(
            db => new ManifestacaoDaProtecaoRepository(db).ListarDoItemAsync(new ItemDaProtecao(CadastroDoCatalogo.TipoDocumento, id), CancellationToken.None));

        eventos.Select(static e => e.Autor.Nome).Should().Equal(Kevin.Nome, Lucia.Nome);
    }

    [Fact(DisplayName = "Com o mesmo instante, os atos do item saem pela ordem de registro, e não pelo identificador")]
    public async Task Atos_MesmoInstante_PelaOrdemDeRegistro()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "TIPO_DOCUMENTO");
        (await DecidirComoAsync(client, Betina, "TIPO_DOCUMENTO", id, "APROVAR")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ExecutarAsync(InverterOsAtos, id);

        IReadOnlyList<AtoDaProtecaoDoCatalogo> atos = await RecarregarAsync(
            db => new AtoDaProtecaoDoCatalogoRepository(db).ListarDoItemAsync(CadastroDoCatalogo.TipoDocumento, id, CancellationToken.None));

        atos.Select(static a => a.Ato).Should().Equal(AtoDaProtecao.Solicitacao, AtoDaProtecao.Aprovacao);
    }

    private Task<int> ExecutarAsync(string sql, Guid itemId) =>
        RecarregarAsync(db => db.Database.ExecuteSqlRawAsync(sql, itemId, MesmoInstante));

    private async Task<T> RecarregarAsync<T>(Func<UniPlusDbContext, Task<T>> leitura)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        return await leitura(escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>());
    }
}
