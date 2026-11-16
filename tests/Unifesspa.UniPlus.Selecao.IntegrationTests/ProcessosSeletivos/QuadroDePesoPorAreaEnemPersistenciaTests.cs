namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CenarioDeClassificacaoPersistida;

/// <summary>
/// Quadro de pesos por área do ENEM congelado na classificação (Postgres real): persiste e volta
/// pelo carregamento do agregado, é descartado junto com a classificação que o congelou e tem a
/// integridade (grupo único, peso e corte na faixa) garantida pelo próprio banco.
/// </summary>
[Collection(ClassificacaoPersistenciaColecao.Nome)]
public sealed class QuadroDePesoPorAreaEnemPersistenciaTests
{
    private readonly CenarioDeClassificacaoPersistida _cenario;

    public QuadroDePesoPorAreaEnemPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeClassificacaoPersistida(fixture);
    }

    [Fact(DisplayName = "Quadro de pesos por área congelado persiste e volta pelo carregamento do agregado, com peso e corte exatos")]
    public async Task QuadroPesoAreaEnem_PersisteERecarregaPeloAgregado()
    {
        ProcessoSeletivo processo = NovoProcessoComClassificacaoEnem();

        await _cenario.PersistirAsync(processo);

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        ProcessoSeletivo recarregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(readContext)
            .ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

        ConfiguracaoClassificacao classificacao = recarregado.Classificacao!;
        classificacao.ResolucaoPesoAreaEnem.Should().Be(QuadroPesoAreaEnemDeTeste.Resolucao);
        classificacao.QuadroPesoAreaEnem.Select(g => g.GrupoAreaEnem.Codigo).Should().BeEquivalentTo(
            ["HUMANISTICA_I", "HUMANISTICA_II", "SAUDE_E_BIOLOGICAS", "TECNOLOGICA"]);

        GrupoPesoAreaEnemCongelado humanisticaI = classificacao.QuadroPesoAreaEnem.Single(g => g.GrupoAreaEnem.Codigo == "HUMANISTICA_I");
        humanisticaI.GrupoAreaEnem.Rotulo.Should().Be("Humanística I");
        humanisticaI.BaseLegal.Should().Be("Resolução nº 805/2024/Consepe – Anexo I");
        humanisticaI.Areas.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)).Should().BeEquivalentTo(
        [
            ("REDACAO", "Redação", 2.00m, (decimal?)400m),
            ("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias", 1.50m, (decimal?)null),
            ("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias", 2.50m, (decimal?)null),
            ("LINGUAGENS", "Linguagens e suas Tecnologias", 3.00m, (decimal?)null),
            ("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, (decimal?)null),
        ]);
    }

    [Fact(DisplayName = "Desmarcar BaseadoEmEnem descarta a resolução e o quadro congelado, sem grupo nem área órfãos")]
    public async Task DesmarcarBaseadoEmEnem_DescartaQuadroSemOrfaos()
    {
        ProcessoSeletivo processo = NovoProcessoComClassificacaoEnem();
        Guid classificacaoOriginalId = processo.Classificacao!.Id;
        Guid[] gruposOriginais = [.. processo.Classificacao.QuadroPesoAreaEnem.Select(g => g.Id)];

        await _cenario.PersistirAsync(processo);

        (await ContarGruposAsync(classificacaoOriginalId)).Should().Be(4);
        (await ContarAreasAsync(gruposOriginais)).Should().Be(20);

        await using (UniPlusDbContext configureContext = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(configureContext);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

            ConfiguracaoClassificacao semEnem = ConfiguracaoClassificacao.Criar(
                Regra(RegraCalculoCodigo.FormulaMediaPonderada, "a"),
                Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "b"),
                2,
                Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "c"),
                [],
                baseadoEmEnem: false,
                resolucaoPesoAreaEnem: null,
                quadroPesoAreaEnem: [], escalaNotaFinal: 100m).Value!;
            carregado.DefinirClassificacao(semEnem, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (UniPlusDbContext readContext = _cenario.CriarDbContext())
        {
            ProcessoSeletivo recarregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(readContext)
                .ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            recarregado.Classificacao!.BaseadoEmEnem.Should().BeFalse();
            recarregado.Classificacao.ResolucaoPesoAreaEnem.Should().BeNull();
            recarregado.Classificacao.QuadroPesoAreaEnem.Should().BeEmpty();
        }

        (await ContarGruposAsync(classificacaoOriginalId)).Should().Be(0, "o quadro vai embora com a classificação que o congelou");
        (await ContarAreasAsync(gruposOriginais)).Should().Be(0, "as áreas vão embora com o grupo");
    }

    [Fact(DisplayName = "O banco recusa o mesmo grupo duas vezes no quadro de uma classificação")]
    public async Task QuadroPesoAreaEnem_GrupoRepetidoNaMesmaClassificacao_BancoRecusa()
    {
        GrupoPesoAreaEnemCongelado grupo = await PersistirEObterUmGrupoAsync();

        await using UniPlusDbContext ctx = _cenario.CriarDbContext();
        Func<Task> duplicar = () => ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO selecao.grupos_do_quadro_de_pesos (id, configuracao_classificacao_id, grupo_area_enem_codigo, grupo_area_enem_rotulo, base_legal, created_at) VALUES ({Guid.CreateVersion7()}, {grupo.ConfiguracaoClassificacaoId}, {grupo.GrupoAreaEnem.Codigo}, {grupo.GrupoAreaEnem.Rotulo}, {grupo.BaseLegal}, {DateTimeOffset.UtcNow})");

        (await duplicar.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Theory(DisplayName = "O banco recusa peso negativo e corte fora de 0 a 1000 nas áreas do quadro")]
    [InlineData(-0.0001, null)]
    [InlineData(1.0, -0.0001)]
    [InlineData(1.0, 1000.0001)]
    public async Task QuadroPesoAreaEnem_ValorForaDaFaixa_BancoRecusa(double peso, double? corte)
    {
        GrupoPesoAreaEnemCongelado grupo = await PersistirEObterUmGrupoAsync();

        await using UniPlusDbContext ctx = _cenario.CriarDbContext();
        Func<Task> inserir = () => ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO selecao.areas_do_quadro_de_pesos (id, grupo_peso_area_enem_congelado_id, codigo, rotulo, peso, corte, created_at) VALUES ({Guid.CreateVersion7()}, {grupo.Id}, {"AREA_EXTRA"}, {"Área extra"}, {(decimal)peso}, {(decimal?)corte}, {DateTimeOffset.UtcNow})");

        (await inserir.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    private async Task<GrupoPesoAreaEnemCongelado> PersistirEObterUmGrupoAsync()
    {
        ProcessoSeletivo processo = NovoProcessoComClassificacaoEnem();
        await _cenario.PersistirAsync(processo);

        return processo.Classificacao!.QuadroPesoAreaEnem.First();
    }

    private static ProcessoSeletivo NovoProcessoComClassificacaoEnem()
    {
        ProcessoSeletivo processo = NovoProcesso("PS 2027 — Medicina", TiposDeProcessoDeTeste.PSVR);

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "a"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "b"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "c"),
            [],
            baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: 100m).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private async Task<int> ContarGruposAsync(Guid classificacaoId)
    {
        await using UniPlusDbContext ctx = _cenario.CriarDbContext();
        return await ctx.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM selecao.grupos_do_quadro_de_pesos WHERE configuracao_classificacao_id = {classificacaoId}")
            .SingleAsync();
    }

    private async Task<int> ContarAreasAsync(IEnumerable<Guid> grupoIds)
    {
        await using UniPlusDbContext ctx = _cenario.CriarDbContext();
        int total = 0;
        foreach (Guid grupoId in grupoIds)
        {
            total += await ctx.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM selecao.areas_do_quadro_de_pesos WHERE grupo_peso_area_enem_congelado_id = {grupoId}")
                .SingleAsync();
        }

        return total;
    }
}
