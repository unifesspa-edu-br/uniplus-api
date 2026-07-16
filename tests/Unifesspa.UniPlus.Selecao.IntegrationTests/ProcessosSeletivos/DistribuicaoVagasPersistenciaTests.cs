namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da distribuição
/// de vagas (Story #773, modelagem P-A): persiste e recarrega
/// <c>ConfiguracaoDistribuicaoVagas</c> + <c>ModalidadeSelecionada</c>,
/// provando o mapeamento EF (owned type <c>ReferenciaRegra</c>, snapshot de
/// referência demográfica opcional, jsonb de critérios cumulativos) contra
/// Postgres real, e a reconfiguração sobre o agregado tracked (mesma
/// regressão de <c>ValueGeneratedNever</c> validada na F0).
/// </summary>
public sealed class DistribuicaoVagasPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public DistribuicaoVagasPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static ModalidadeSelecionada NovaModalidade(
        string codigo, NaturezaLegalModalidade natureza, ComposicaoVagasModalidade composicao,
        string? composicaoOrigemCodigo = null,
        RegraRemanejamentoModalidade regraRemanejamento = RegraRemanejamentoModalidade.Nenhuma,
        int? quantidadeDeclarada = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, $"Modalidade {codigo}", natureza, composicao,
            composicaoOrigemCodigo, regraRemanejamento, null, null, null,
            ["CRITERIO_A", "CRITERIO_B"], "RECLASSIFICA_AC", "Lei 12.711/2012", quantidadeDeclarada).Value!;

    [Fact(DisplayName = "Persiste e recarrega distribuição Lei 12.711 com referência demográfica e modalidades")]
    public async Task PersisteERecarrega_Lei12711ComDemografica()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)], PrecondicaoIfMatch.Ausente);

        ReferenciaRegra regra = ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Lei12711, "v1", new string('a', 64)).Value!;
        ReferenciaReservaDemograficaSnapshot demografica = ReferenciaReservaDemograficaSnapshot.Criar(
            Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022 (IBGE)").Value!;

        // INV-6: a Lei 12.711 exige as 8 modalidades federais + AC. "V" (PcD
        // retirada da AC) é uma 10ª modalidade além das obrigatórias.
        List<ModalidadeSelecionada> modalidades =
        [
            .. ModalidadesFederaisLei12711.Codigos.Select(codigo =>
                NovaModalidade(codigo, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr, regraRemanejamento: RegraRemanejamentoModalidade.SegueCascata)),
            NovaModalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo),
            NovaModalidade("V", NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe, composicaoOrigemCodigo: "AC", quantidadeDeclarada: 2),
        ];

        ReferenciaRegra regraAjuste = ReferenciaRegra.Criar(
            RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico, "v1", new string('d', 64)).Value!;

        Guid ofertaCursoId = Guid.CreateVersion7();
        Result<ConfiguracaoDistribuicaoVagas> configResult = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoId, voBase: 50, pr: 0.5m, regra, regraAjuste, demografica, modalidades);
        configResult.IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([configResult.Value!], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.VagasOfertadas)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConfiguracaoDistribuicaoVagas distribuicao = recarregado!.DistribuicaoVagas.Single();
        distribuicao.OfertaCursoOrigemId.Should().Be(ofertaCursoId);
        distribuicao.VoBase.Should().Be(50);
        distribuicao.Pr.Should().Be(0.5m);
        distribuicao.RegraDistribuicao.Codigo.Should().Be(RegraDistribuicaoVagasCodigo.Lei12711);
        distribuicao.RegraDistribuicao.Hash.Should().Be(new string('a', 64));
        distribuicao.RegraAjuste.Should().NotBeNull();
        distribuicao.RegraAjuste!.Codigo.Should().Be(RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico);
        distribuicao.ReferenciaDemografica.Should().NotBeNull();
        distribuicao.ReferenciaDemografica!.CensoReferencia.Should().Be("2022");
        distribuicao.ReferenciaDemografica.PpiPercentual.Should().Be(79m);
        distribuicao.Modalidades.Should().HaveCount(10);

        ModalidadeSelecionada retiraDe = distribuicao.Modalidades.Single(m => m.Codigo == "V");
        retiraDe.ComposicaoVagas.Should().Be(ComposicaoVagasModalidade.RetiraDe);
        retiraDe.ComposicaoOrigemCodigo.Should().Be("AC");
        retiraDe.CriteriosCumulativos.Should().BeEquivalentTo(["CRITERIO_A", "CRITERIO_B"]);
        retiraDe.QuantidadeDeclarada.Should().Be(2);

        ModalidadeSelecionada cotaReservada = distribuicao.Modalidades.Single(m => m.Codigo == "LB_PPI");
        cotaReservada.RegraRemanejamento.Should().Be(RegraRemanejamentoModalidade.SegueCascata);
        cotaReservada.QuantidadeDeclarada.Should().BeNull("sub-reserva federal é calculada, não fixada pelo edital");

        distribuicao.VagasOfertadas.Should().HaveCount(10);
        distribuicao.VagasOfertadas.Single(v => v.ModalidadeCodigo == "V").Quantidade.Should().Be(2);
    }

    [Fact(DisplayName = "Persiste e recarrega distribuição institucional sem referência demográfica")]
    public async Task PersisteERecarrega_InstitucionalSemDemografica()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — PSIQ", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([EtapaProcesso.Criar("Entrevista", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)], PrecondicaoIfMatch.Ausente);

        ReferenciaRegra regra = ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('b', 64)).Value!;
        Result<ConfiguracaoDistribuicaoVagas> configResult = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 60, pr: 1m, regra, regraAjuste: null, referenciaDemografica: null,
            [NovaModalidade("IND", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 60)]);
        configResult.IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([configResult.Value!], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConfiguracaoDistribuicaoVagas distribuicao = recarregado!.DistribuicaoVagas.Single();
        distribuicao.RegraDistribuicao.Codigo.Should().Be(RegraDistribuicaoVagasCodigo.Institucional);
        distribuicao.ReferenciaDemografica.Should().BeNull();
    }

    [Fact(DisplayName = "Reconfigurar distribuição sobre o agregado tracked insere os filhos novos, não falha em UPDATE")]
    public async Task ReconfigurarDistribuicaoSobreAgregadoTracked_InsereFilhos()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — PSE Campo", TipoProcesso.PSECampo, OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)], PrecondicaoIfMatch.Ausente);

        ReferenciaRegra regra = ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('c', 64)).Value!;
        Guid ofertaCursoId = Guid.CreateVersion7();
        ConfiguracaoDistribuicaoVagas original = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoId, voBase: 40, pr: 1m, regra, null, null,
            [NovaModalidade("QUIL", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 40)]).Value!;
        processo.DefinirDistribuicaoVagas([original], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (SelecaoDbContext configureContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(configureContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

            ConfiguracaoDistribuicaoVagas nova = ConfiguracaoDistribuicaoVagas.Criar(
                ofertaCursoId, voBase: 45, pr: 1m, regra, null, null,
                [
                    NovaModalidade("QUIL", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 20),
                    NovaModalidade("IND", NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, quantidadeDeclarada: 25),
                ]).Value!;

            Result result = carregado.DefinirDistribuicaoVagas([nova], PrecondicaoIfMatch.Ausente);
            result.IsSuccess.Should().BeTrue();

            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.VagasOfertadas)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConfiguracaoDistribuicaoVagas distribuicao = recarregado!.DistribuicaoVagas.Single();
        distribuicao.VoBase.Should().Be(45);
        distribuicao.Modalidades.Should().HaveCount(2);

        // CA-15: redefinir substitui o quadro por inteiro, na mesma transação —
        // nenhuma linha do quadro anterior ("QUIL" sozinho, quantidade 40) sobrevive.
        distribuicao.VagasOfertadas.Should().HaveCount(2);
        distribuicao.VagasOfertadas.Should().Contain(v => v.ModalidadeCodigo == "QUIL" && v.Quantidade == 20);
        distribuicao.VagasOfertadas.Should().Contain(v => v.ModalidadeCodigo == "IND" && v.Quantidade == 25);
    }
}
