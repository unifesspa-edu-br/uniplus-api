namespace Unifesspa.UniPlus.Geo.IntegrationTests.Etl;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

/// <summary>
/// Carga do ETL País/Estado/Cidade (Story #672) sobre Postgres+PostGIS real
/// (<see cref="GeoPostgisFixture"/>): carga inicial, idempotência com Guid preservado,
/// parse tolerante, órfãos, filtro Brasil, dedup intra-fonte e rollback transacional.
/// Cada teste limpa as tabelas antes (a coleção roda sem paralelismo).
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class EtlPaisEstadoCidadeTests
{
    private const string CodigoMaraba = "1500402";
    private const string CodigoSaoPaulo = "3550308";
    private const string CodigoCidadeOrfa = "9999999";

    private readonly GeoPostgisFixture _fixture;

    public EtlPaisEstadoCidadeTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: carga inicial popula País (Brasil), Estados e Cidades com coordenada 4326 e territorial embutido")]
    public async Task CargaInicial_PopulaHierarquia()
    {
        await LimparAsync();
        await ExecutarAsync(FonteCompleta());

        await using GeoDbContext leitura = _fixture.CreateDbContext();

        (await leitura.Paises.CountAsync()).Should().Be(1);
        Pais brasil = await leitura.Paises.SingleAsync();
        brasil.SiglaIso.Should().Be("BRA");

        (await leitura.Estados.CountAsync()).Should().Be(2);
        Estado para = await leitura.Estados.SingleAsync(e => e.Uf == "PA");
        para.PaisId.Should().Be(brasil.Id);
        para.CodigoIbge.Should().Be("15");

        // A cidade órfã (UF inexistente) não entra; restam as 2 válidas.
        (await leitura.Cidades.CountAsync()).Should().Be(2);
        Cidade maraba = await leitura.Cidades.SingleAsync(c => c.CodigoIbge == CodigoMaraba);
        maraba.EstadoId.Should().Be(para.Id);
        maraba.MesorregiaoNome.Should().Be("Sudeste Paraense"); // territorial embutido (join por código IBGE)
        maraba.Coordenada.Should().NotBeNull();
        maraba.Coordenada!.SRID.Should().Be(4326);

        (await leitura.EstadoIndicadores.CountAsync()).Should().Be(2);
        (await leitura.EstadoFaixasCep.CountAsync()).Should().Be(2);
        (await leitura.CidadeIndicadores.CountAsync()).Should().Be(2);
        (await leitura.CidadeFaixasCep.CountAsync()).Should().Be(1);
    }

    [Fact(DisplayName = "CA-05: apenas o registro BRA é carregado, mesmo com países estrangeiros na fonte")]
    public async Task SomenteBrasil_IgnoraEstrangeiros()
    {
        await LimparAsync();
        RelatorioImportacao relatorio = await ExecutarAsync(FonteCompleta());

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Paises.CountAsync()).Should().Be(1);
        (await leitura.Paises.SingleAsync()).SiglaIso.Should().Be("BRA");

        relatorio.Tabelas["pais"].Lidos.Should().Be(3);     // BRA + ARG + USA lidos da fonte
        relatorio.Tabelas["pais"].Inseridos.Should().Be(1); // só BRA persistido
    }

    [Fact(DisplayName = "CA-04: cidade cujo Estado não foi carregado é contada como órfã e não persistida")]
    public async Task CidadeOrfa_NaoInsereERegistra()
    {
        await LimparAsync();
        RelatorioImportacao relatorio = await ExecutarAsync(FonteCompleta());

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Cidades.AnyAsync(c => c.CodigoIbge == CodigoCidadeOrfa)).Should().BeFalse();
        relatorio.Tabelas["cidade"].Orfaos.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact(DisplayName = "CA-03: indicador com mortalidade '-' persiste null e conta como parse degradado, sem abortar")]
    public async Task ParseTolerante_HifenDegradaParaNull()
    {
        await LimparAsync();
        RelatorioImportacao relatorio = await ExecutarAsync(FonteCompleta());

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Cidade maraba = await leitura.Cidades.SingleAsync(c => c.CodigoIbge == CodigoMaraba);
        CidadeIndicador indicador = await leitura.CidadeIndicadores.SingleAsync(i => i.CidadeId == maraba.Id);

        indicador.MortalidadeInfantil.Should().BeNull();
        relatorio.Tabelas["cidade_indicador"].ParsesDegradados.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact(DisplayName = "CA-02: reexecução preserva os Guids (não troca PK) e não duplica; o 2º passe reporta Atualizados")]
    public async Task Idempotencia_PreservaGuidsESemDuplicar()
    {
        await LimparAsync();
        FonteEmMemoria fonte = FonteCompleta();
        await ExecutarAsync(fonte);

        IdsCapturados primeiros = await CapturarIdsAsync();

        RelatorioImportacao segunda = await ExecutarAsync(fonte);
        IdsCapturados segundos = await CapturarIdsAsync();

        // Inclui o Id de uma faixa: o upsert por chave natural não pode trocar a PK de
        // ninguém (país/estado/cidade têm filhos; faixas preservam Id por idempotência).
        segundos.Should().Be(primeiros, "o upsert in place não pode trocar a PK entre cargas");

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Paises.CountAsync()).Should().Be(1);
        (await leitura.Estados.CountAsync()).Should().Be(2);
        (await leitura.Cidades.CountAsync()).Should().Be(2);
        (await leitura.EstadoIndicadores.CountAsync()).Should().Be(2);
        (await leitura.EstadoFaixasCep.CountAsync()).Should().Be(2);
        (await leitura.CidadeFaixasCep.CountAsync()).Should().Be(1);

        segunda.Tabelas["pais"].Atualizados.Should().Be(1);
        segunda.Tabelas["pais"].Inseridos.Should().Be(0);
        segunda.Tabelas["estado"].Atualizados.Should().Be(2);
        segunda.Tabelas["cidade"].Atualizados.Should().Be(2);
        segunda.Tabelas["estado_faixa"].Atualizados.Should().Be(2);
        segunda.Tabelas["estado_faixa"].Inseridos.Should().Be(0);
    }

    [Fact(DisplayName = "Dedup intra-fonte: duas linhas com a mesma UF não disparam unique violation; a 2ª conta como duplicata")]
    public async Task DuplicataIntraFonte_NaoQuebraEContabiliza()
    {
        await LimparAsync();
        FonteEmMemoria fonte = FonteCompleta();
        fonte.Estados.Add(DadosDne.Estado("PA", "Pará (duplicado)", latitude: "-3.0", longitude: "-52.0"));

        RelatorioImportacao relatorio = await ExecutarAsync(fonte);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Estados.CountAsync(e => e.Uf == "PA")).Should().Be(1);
        relatorio.Tabelas["estado"].Duplicados.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact(DisplayName = "Rollback: falha no meio da carga reverte tudo — nem País nem Estado ficam persistidos")]
    public async Task FalhaNoMeio_FazRollbackTotal()
    {
        await LimparAsync();
        FonteComFalhaNasCidades fonte = new(FonteCompleta());

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            GeoImportadorPaisEstadoCidade importador = new(ctx, NullLogger<GeoImportadorPaisEstadoCidade>.Instance);
            Func<Task> carga = () => importador.ImportarAsync(fonte, CancellationToken.None);
            await carga.Should().ThrowAsync<InvalidOperationException>();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Paises.CountAsync()).Should().Be(0);
        (await leitura.Estados.CountAsync()).Should().Be(0);
    }

    private async Task<IdsCapturados> CapturarIdsAsync()
    {
        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Guid brasil = (await leitura.Paises.SingleAsync(p => p.SiglaIso == "BRA")).Id;
        Estado para = await leitura.Estados.SingleAsync(e => e.Uf == "PA");
        Guid maraba = (await leitura.Cidades.SingleAsync(c => c.CodigoIbge == CodigoMaraba)).Id;
        Guid indicadorPara = (await leitura.EstadoIndicadores.SingleAsync(i => i.EstadoId == para.Id)).Id;
        Guid faixaPara = (await leitura.EstadoFaixasCep.SingleAsync(f => f.EstadoId == para.Id)).Id;
        return new IdsCapturados(brasil, para.Id, maraba, indicadorPara, faixaPara);
    }

    private readonly record struct IdsCapturados(Guid Brasil, Guid Para, Guid Maraba, Guid IndicadorPara, Guid FaixaPara);

    private async Task<RelatorioImportacao> ExecutarAsync(IGeoFonteDados fonte)
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        GeoImportadorPaisEstadoCidade importador = new(ctx, NullLogger<GeoImportadorPaisEstadoCidade>.Instance);
        return await importador.ImportarAsync(fonte, CancellationToken.None);
    }

    private async Task LimparAsync()
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        // TRUNCATE ... CASCADE limpa toda a hierarquia (estado → cidade → satélites)
        // por dependência de FK, isolando cada teste no banco compartilhado da coleção.
        await ctx.Database.ExecuteSqlRawAsync("TRUNCATE TABLE pais CASCADE");
    }

    private static FonteEmMemoria FonteCompleta()
    {
        FonteEmMemoria fonte = new() { Versao = "202601" };

        fonte.Paises.Add(DadosDne.Pais("BRA", "Brasil", "BR"));
        fonte.Paises.Add(DadosDne.Pais("ARG", "Argentina", "AR"));
        fonte.Paises.Add(DadosDne.Pais("USA", "Estados Unidos", "US"));

        fonte.Estados.Add(DadosDne.Estado("PA", "Pará", capital: "Belém", latitude: "-1.9981271", longitude: "-54.9306152", faixaIni: "66000000", faixaFim: "68899999"));
        fonte.Estados.Add(DadosDne.Estado("SP", "São Paulo", regiao: "Sudeste", capital: "São Paulo", latitude: "-23.5", longitude: "-46.6", faixaIni: "01000000", faixaFim: "19999999"));

        fonte.EstadoIndicadores.Add(DadosDne.EstadoIndicador("PA", "15", idh: "0.69", populacao: "8120131", receitas: "52747856526.44"));
        fonte.EstadoIndicadores.Add(DadosDne.EstadoIndicador("SP", "35", idh: "0.806", populacao: "44411238"));

        fonte.EstadoFaixas.Add(DadosDne.EstadoFaixa("PA", "66000000", "68899999", "Pará"));
        fonte.EstadoFaixas.Add(DadosDne.EstadoFaixa("SP", "01000000", "19999999", "São Paulo"));

        fonte.Cidades.Add(DadosDne.Cidade(CodigoMaraba, "Marabá", "PA", ddd: "94", latitude: "-5.36867", longitude: "-49.11731"));
        fonte.Cidades.Add(DadosDne.Cidade(CodigoSaoPaulo, "São Paulo", "SP", ddd: "11", latitude: "-23.56287", longitude: "-46.65468"));
        fonte.Cidades.Add(DadosDne.Cidade(CodigoCidadeOrfa, "Cidade Órfã", "ZZ")); // UF sem estado carregado

        fonte.CidadeTerritorios.Add(DadosDne.CidadeTerritorio(CodigoMaraba, mesorregiaoNome: "Sudeste Paraense", microrregiaoNome: "Marabá"));

        fonte.CidadeIndicadores.Add(DadosDne.CidadeIndicador(CodigoMaraba, mortalidadeInfantil: "-", populacao: "283542", aniversario: "05/04"));
        fonte.CidadeIndicadores.Add(DadosDne.CidadeIndicador(CodigoSaoPaulo, mortalidadeInfantil: "11.5", populacao: "11451245"));

        fonte.CidadeFaixas.Add(DadosDne.CidadeFaixa(CodigoMaraba, "68500000", "68519999"));

        return fonte;
    }
}
