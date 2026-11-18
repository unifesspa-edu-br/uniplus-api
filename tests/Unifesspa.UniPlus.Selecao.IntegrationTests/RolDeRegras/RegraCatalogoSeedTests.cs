namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da biblioteca
/// <c>regras_do_catalogo</c> (Story #772): o seed das regras <c>v1</c>, a
/// content-addressability do hash sobrevivendo à normalização jsonb do
/// Postgres, a coexistência de versões (<c>UNIQUE (codigo, versao)</c>) e o
/// <see cref="RegraCatalogoReader"/>.
/// </summary>
public sealed class RegraCatalogoSeedTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public RegraCatalogoSeedTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Seed materializa exatamente as regras declaradas, únicas por código e versão")]
    public async Task Seed_MaterializaAsRegrasDeclaradas()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();

        List<RegraCatalogo> regras = await context.RolDeRegras
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        regras.Should().HaveCount(RegraCatalogoSeed.Itens.Count);

        ConferirRegrasSemDuplicacao(regras);
    }

    private static void ConferirRegrasSemDuplicacao(List<RegraCatalogo> regras)
    {
        regras.Select(r => (r.Codigo, r.Versao)).Should().OnlyHaveUniqueItems();
        regras.Should().Contain(r => r.Codigo == "DISTRIBUICAO-LEI-12711" && r.Tipo == TipoRegra.RegraDistribuicaoVagas);
        regras.Should().Contain(r => r.Codigo == "DISTRIBUICAO-LEI-12711-COM-AMPLA-CONCORRENCIA-E-PESSOA-COM-DEFICIENCIA" && r.Tipo == TipoRegra.RegraDistribuicaoVagas);
        regras.Should().Contain(r => r.Codigo == "DISTRIBUICAO-POR-ACRESCIMO-INDIGENA-E-QUILOMBOLA" && r.Tipo == TipoRegra.RegraDistribuicaoVagas);
        regras.Should().Contain(r => r.Codigo == "DISTRIBUICAO-COM-AMPLA-CONCORRENCIA-E-PESSOA-COM-DEFICIENCIA" && r.Tipo == TipoRegra.RegraDistribuicaoVagas);
        regras.Should().NotContain(r => r.Codigo == "DISTRIB-VAGAS-COM-PCD-PURO",
            "a reserva de pessoa com deficiência sem as cotas da lei passou a ser AC_PCD");
        regras.Should().Contain(r => r.Codigo == "FORMULA-MEDIA-PONDERADA" && r.Tipo == TipoRegra.RegraCalculo);
        regras.Should().Contain(r => r.Codigo == "REMANEJ-CASCATA-LEI-12711" && r.Tipo == TipoRegra.CriterioRemanejamento);
        regras.Should().Contain(r => r.Codigo == AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial && r.Tipo == TipoRegra.AlgoritmoContagemPrazo);
        regras.Should().Contain(r => r.Codigo == AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora && r.Tipo == TipoRegra.AlgoritmoContagemPrazo);
        regras.Should().Contain(r => r.Codigo == AlgoritmoContagemPrazoCodigo.AvancaDataUtil && r.Tipo == TipoRegra.AlgoritmoContagemPrazo);
    }

    [Fact(DisplayName = "Hash gravado é content-addressable: recomputar após round-trip jsonb bate")]
    public async Task Seed_HashContentAddressable_RecomputeBate()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();

        List<RegraCatalogo> regras = await context.RolDeRegras
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        ConferirHashes(regras);
    }

    private static void ConferirHashes(List<RegraCatalogo> regras)
    {
        foreach (RegraCatalogo regra in regras)
        {
            regra.RecomputeHash().Should().Be(regra.Hash);
        }

        foreach (RegraCatalogoSeedItem item in RegraCatalogoSeed.Itens)
        {
            RegraCatalogo persistida = regras.Single(r => r.Codigo == item.Codigo && r.Versao == item.Versao);
            persistida.Hash.Should().Be(item.ComputarHash());
        }
    }

    [Fact(DisplayName = "declaracao_da_regra/invariantes preservam o conteúdo semântico após o round-trip jsonb")]
    public async Task Seed_PayloadJsonb_PreservaConteudo()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();

        RegraCatalogo bonus = await context.RolDeRegras
            .AsNoTracking()
            .SingleAsync(r => r.Codigo == "BONUS-MULTIPLICATIVO", CancellationToken.None);

        bonus.DeclaracaoDaRegra.ValueKind.Should().Be(JsonValueKind.Object);
        bonus.DeclaracaoDaRegra.EnumerateObject().Should().BeEmpty();
        bonus.Invariantes.ValueKind.Should().Be(JsonValueKind.Array);
        bonus.Invariantes.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "v1 e v2 de um mesmo código coexistem (UNIQUE por codigo+versao)")]
    public async Task Codigo_V1EV2_Coexistem()
    {
        using JsonDocument declaracao = JsonDocument.Parse("""{"fonte_pesos":["etapa"]}""");
        using JsonDocument invariantes = JsonDocument.Parse("""["divisor = Σ(peso)"]""");
        RegraCatalogo v2 = RegraCatalogo.Criar(
            "FORMULA-MEDIA-PONDERADA", "v2", TipoRegra.RegraCalculo,
            declaracao.RootElement, invariantes.RootElement, "Revisão da fórmula (nova versão)");
        try
        {
            await GravarRegraAsync(v2);
            await ConferirVersoesAsync();
        }
        finally
        {
            await RemoverRegraAsync(v2.Id);
        }
    }

    private async Task GravarRegraAsync(RegraCatalogo regra)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        context.RolDeRegras.Add(regra);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private async Task ConferirVersoesAsync()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        List<RegraCatalogo> versoes = await context.RolDeRegras.AsNoTracking()
            .Where(r => r.Codigo == "FORMULA-MEDIA-PONDERADA")
            .OrderBy(r => r.Versao).ToListAsync(CancellationToken.None);
        versoes.Select(r => r.Versao).Should().Equal("v1", "v2");
    }

    private async Task RemoverRegraAsync(Guid id)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        await context.RolDeRegras.Where(r => r.Id == id).ExecuteDeleteAsync(CancellationToken.None);
    }

    [Fact(DisplayName = "Reader resolve regra por (codigo, versao) e filtra por tipo")]
    public async Task Reader_ObterEListar()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        RegraCatalogo? formula = await reader.ObterAsync("FORMULA-MEDIA-PONDERADA", "v1", CancellationToken.None);
        formula.Should().NotBeNull();
        formula!.Tipo.Should().Be(TipoRegra.RegraCalculo);

        RegraCatalogo? inexistente = await reader.ObterAsync("FORMULA-MEDIA-PONDERADA", "v99", CancellationToken.None);
        inexistente.Should().BeNull();

        IReadOnlyList<RegraCatalogo> desempates = await reader.ListarPorTipoAsync(TipoRegra.CriterioDesempate, CancellationToken.None);
        desempates.Should().HaveCount(5);
        desempates.Should().OnlyContain(r => r.Tipo == TipoRegra.CriterioDesempate);
        desempates.Should().Contain(r => r.Codigo == CriterioDesempateCodigo.MaiorNotaAreaEnem && r.Versao == "v1");
    }

    [Fact(DisplayName = "A eliminação por falta em dia de prova do ENEM é semeada sem parâmetros, com as duas invariantes")]
    public async Task Seed_EliminacaoPorFaltaEmDiaDeProvaEnem()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        RegraCatalogo? regra = await reader.ObterAsync(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "v1", CancellationToken.None);

        regra.Should().NotBeNull();
        regra!.Tipo.Should().Be(TipoRegra.RegraEliminacao);
        regra.DeclaracaoDaRegra.EnumerateObject().Should().BeEmpty("a regra não tem parâmetro: o edital só a declara ou não");
        regra.Invariantes.EnumerateArray().Select(i => i.GetString()).Should().Equal(
            "falta em pelo menos um dia de prova da edição do ENEM usada no processo → elimina",
            "sem participação registrada na edição do ENEM indicada pelo candidato equivale a falta");
    }

    [Fact(DisplayName = "O corte em área é semeado com área e mínimo no lugar do corte de Redação")]
    public async Task Seed_CorteEmArea_SubstituiOCorteDeRedacao()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        RegraCatalogo? regra = await reader.ObterAsync(RegraEliminacaoCodigo.ElimCorteEmArea, "v1", CancellationToken.None);
        RegraCatalogo? antiga = await reader.ObterAsync("ELIM-CORTE-REDACAO", "v1", CancellationToken.None);

        regra.Should().NotBeNull();
        regra!.Tipo.Should().Be(TipoRegra.RegraEliminacao);

        regra.Invariantes.EnumerateArray().Select(i => i.GetString()).Should().Equal(
            "nota na área < mínimo → elimina",
            "no máximo um corte por área");
        antiga.Should().BeNull("o corte em área substitui o de Redação, sem convivência das duas");
    }

    [Fact(DisplayName = "A eliminação por ausência na etapa é semeada com a etapa como único parâmetro e a base legal do edital")]
    public async Task Seed_EliminacaoPorAusenciaNaEtapa()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        RegraCatalogo? regra = await reader.ObterAsync(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, "v1", CancellationToken.None);

        regra.Should().NotBeNull();
        regra!.Tipo.Should().Be(TipoRegra.RegraEliminacao);

        regra.Invariantes.EnumerateArray().Select(i => i.GetString()).Should().Equal(
            "falta à etapa referenciada → elimina",
            "não se aplica à etapa de nota do ENEM");
        regra.BaseLegal.Should().NotBeNullOrWhiteSpace();
    }

    [Theory(DisplayName = "Código ou versão com o caractere nulo não é encontrado, sem erro do banco")]
    [InlineData("FORMULA-MEDIA-PONDERADA\0", "v1")]
    [InlineData("FORMULA-MEDIA-PONDERADA", "v\01")]
    public async Task Reader_ObterAsync_CaractereNulo_NaoEncontrado(string codigo, string versao)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        RegraCatalogo? regra = await reader.ObterAsync(codigo, versao, CancellationToken.None);

        regra.Should().BeNull();
    }

    [Fact(DisplayName = "Versão nula não é encontrada, sem exceção")]
    public async Task Reader_ObterAsync_VersaoNula_NaoEncontrado()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        RegraCatalogo? regra = await reader.ObterAsync("FORMULA-MEDIA-PONDERADA", null!, CancellationToken.None);

        regra.Should().BeNull();
    }

    [Fact(DisplayName = "CA-07 — o reader resolve a regra nova e não resolve mais a antiga")]
    public async Task Reader_ObterAsync_RegraNova_Resolve()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        RegraCatalogo? nova = await reader.ObterAsync("RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", CancellationToken.None);
        nova.Should().NotBeNull();
        nova!.Tipo.Should().Be(TipoRegra.RegraPrazoRecurso);

        RegraCatalogo? antiga = await reader.ObterAsync("RECURSO-MULTI-INSTANCIA", "v1", CancellationToken.None);
        antiga.Should().BeNull("a regra que gere a segunda instância deixou de existir");
    }

    [Fact(DisplayName = "CA-08 — há exatamente uma regra de prazo de recurso — a nova")]
    public async Task Reader_ListarPorTipo_PrazoRecurso_ExatamenteUma()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        IReadOnlyList<RegraCatalogo> regras = await reader.ListarPorTipoAsync(
            TipoRegra.RegraPrazoRecurso, CancellationToken.None);

        regras.Should().ContainSingle()
            .Which.Codigo.Should().Be("RECURSO-PRAZO-ANCORADO-EM-ATO");
    }

    [Theory(DisplayName = "O leitor resolve cada regra de distribuição apenas em v1")]
    [InlineData("DISTRIBUICAO-LEI-12711")]
    [InlineData("DISTRIBUICAO-LEI-12711-COM-AMPLA-CONCORRENCIA-E-PESSOA-COM-DEFICIENCIA")]
    [InlineData("DISTRIBUICAO-INSTITUCIONAL")]
    [InlineData("DISTRIBUICAO-POR-ACRESCIMO-INDIGENA-E-QUILOMBOLA")]
    [InlineData("DISTRIBUICAO-COM-AMPLA-CONCORRENCIA-E-PESSOA-COM-DEFICIENCIA")]
    public async Task Reader_ObterAsync_DistribuicaoVagas_SoResolveV1(string codigo)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);
        RegraCatalogo? v1 = await reader.ObterAsync(codigo, "v1", CancellationToken.None);
        RegraCatalogo? v2 = await reader.ObterAsync(codigo, "v2", CancellationToken.None);
        v1.Should().NotBeNull();
        v2.Should().BeNull();
    }

    [Theory(DisplayName = "A base legal das regras de ação afirmativa é a Res. 532/2021, sem resolução revogada")]
    [InlineData(RegraDistribuicaoVagasCodigo.ComAcPcd, "532/2021-CONSEPE, art. 1º")]
    [InlineData(RegraDistribuicaoVagasCodigo.Psiq, "532/2021-CONSEPE, art. 2º")]
    public async Task RegraDeAcaoAfirmativa_FundamentadaNaResolucao532(string codigo, string normaEsperada)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();

        RegraCatalogo regra = await context.RolDeRegras.AsNoTracking()
            .SingleAsync(r => r.Codigo == codigo && r.Versao == RegraCatalogoSeed.VersaoV1, CancellationToken.None);

        // A base legal entra no hash e congela com a regra no edital: citar resolução revogada
        // pelo art. 3º da 532/2021 fundamentaria a vaga numa norma sem vigência.
        regra.BaseLegal.Should().Contain(normaEsperada)
            .And.NotContain("22/2014")
            .And.NotContain("64/2015");
    }

    [Fact(DisplayName = "Toda regra de consolidação do rol se lê pelos dois leitores, o dos corretores e o das naturezas")]
    public async Task RegraDeConsolidacao_LidaPelosDoisLeitores()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();

        List<RegraCatalogo> consolidacoes = await context.RolDeRegras.AsNoTracking()
            .Where(r => r.Tipo == TipoRegra.RegraConsolidacao)
            .ToListAsync(CancellationToken.None);

        consolidacoes.Select(r => r.Codigo).Should().BeEquivalentTo(RegraConsolidacaoCodigo.Rol);
        foreach (RegraCatalogo regra in consolidacoes)
        {
            CorretoresDaRegraDeConsolidacao.De(regra).Should().BePositive(regra.Codigo);
            NaturezasDaConsolidacao.De(regra).Should().NotBeEmpty(regra.Codigo);
        }
    }

    [Fact(DisplayName = "O rol de procedimentos de recurso semeia os quatro procedimentos, na versão v1, cada um com o hash recomputável")]
    public async Task ProcedimentosDeRecurso_SemeadosComHashRecomputavel()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();

        List<RegraCatalogo> procedimentos = await context.RolDeRegras.AsNoTracking()
            .Where(r => r.Tipo == TipoRegra.RegraProcedimentoRecurso)
            .ToListAsync(CancellationToken.None);

        procedimentos.Select(r => r.Codigo).Should().BeEquivalentTo(RegraProcedimentoRecursoCodigo.Rol);
        procedimentos.Should().OnlyContain(r => r.Versao == RegraCatalogoSeed.VersaoV1);
        foreach (RegraCatalogo regra in procedimentos)
        {
            RegraCatalogoSeed.Itens.Single(i => i.Codigo == regra.Codigo).ComputarHash().Should().Be(regra.Hash, regra.Codigo);
        }
    }

    [Fact(DisplayName = "A avaliação única exige um corretor e aceita as duas naturezas; a correção independente exige três e só a numérica")]
    public async Task RegraDeConsolidacao_DeclaraCorretoresENaturezas()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        RegraCatalogo unica = (await reader.ObterAsync(RegraConsolidacaoCodigo.AvaliacaoUnica, RegraCatalogoSeed.VersaoV1, CancellationToken.None))!;
        RegraCatalogo independente = (await reader.ObterAsync(RegraConsolidacaoCodigo.CorrecaoIndependente, RegraCatalogoSeed.VersaoV1, CancellationToken.None))!;

        CorretoresDaRegraDeConsolidacao.De(unica).Should().Be(1);
        NaturezasDaConsolidacao.De(unica).Should().BeEquivalentTo([NaturezaConceito.Numerico, NaturezaConceito.Categorico]);
        CorretoresDaRegraDeConsolidacao.De(independente).Should().Be(3);
        NaturezasDaConsolidacao.De(independente).Should().Equal(NaturezaConceito.Numerico);
    }

    [Theory(DisplayName = "A regra categórica declara critério, corretores, tratamento e natureza")]
    [InlineData(RegraConsolidacaoCodigo.ConceitoUnanimidade, CriterioDeConsolidacaoDoConceito.Unanimidade, 2, TratamentoDaFaltaDeConsolidacao.DecisaoDaBanca)]
    [InlineData(RegraConsolidacaoCodigo.ConceitoMaioriaSimples, CriterioDeConsolidacaoDoConceito.MaioriaSimples, 3, TratamentoDaFaltaDeConsolidacao.NovoCorretor)]
    [InlineData(RegraConsolidacaoCodigo.ConceitoMaioriaAbsoluta, CriterioDeConsolidacaoDoConceito.MaioriaAbsoluta, 3, TratamentoDaFaltaDeConsolidacao.NovoCorretor)]
    public async Task RegraDeConceitoCategorico_DeclaraCriterioCorretoresETratamento(
        string codigo, CriterioDeConsolidacaoDoConceito criterio, int corretores, TratamentoDaFaltaDeConsolidacao tratamento)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);
        RegraCatalogo regra = (await reader.ObterAsync(codigo, RegraCatalogoSeed.VersaoV1, CancellationToken.None))!;
        DeclaracaoDaConsolidacaoDoConceito.De(regra).Should().Be(new DeclaracaoDaConsolidacaoDoConceito(criterio, corretores, tratamento));
        NaturezasDaConsolidacao.De(regra).Should().Equal([NaturezaConceito.Categorico]);
    }

    [Fact(DisplayName = "As regras de consolidação numéricas não declaram critério de conceito")]
    public async Task RegraDeConsolidacaoNumerica_NaoDeclaraCriterioDeConceito()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        foreach (string codigo in new[] { RegraConsolidacaoCodigo.AvaliacaoUnica, RegraConsolidacaoCodigo.CorrecaoIndependente })
        {
            RegraCatalogo regra = (await reader.ObterAsync(codigo, RegraCatalogoSeed.VersaoV1, CancellationToken.None))!;

            DeclaracaoDaConsolidacaoDoConceito.Tentar(regra, out _).Should().BeFalse(codigo);
        }
    }

    [Fact(DisplayName = "As regras de consolidação do seed têm código único e hash distinto")]
    public void RegraDeConsolidacao_CodigoUnicoEHashDistinto()
    {
        List<RegraCatalogoSeedItem> consolidacoes =
            [.. RegraCatalogoSeed.Itens.Where(item => item.Tipo == TipoRegra.RegraConsolidacao)];

        consolidacoes.Select(item => item.Codigo).Should().BeEquivalentTo(RegraConsolidacaoCodigo.Rol).And.OnlyHaveUniqueItems();
        consolidacoes.Select(item => item.ComputarHash()).Should().OnlyHaveUniqueItems();
    }
}
