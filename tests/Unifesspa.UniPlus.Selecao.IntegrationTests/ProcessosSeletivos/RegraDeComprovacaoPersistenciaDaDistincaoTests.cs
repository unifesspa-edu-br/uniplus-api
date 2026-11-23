namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RegraDeComprovacaoPersistenciaDaDistincaoTests(ProcessoSeletivoDbFixture fixture) : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Theory]
    [InlineData(null)]
    [InlineData(CriterioDeDistincaoDasApresentacoes.CompetenciaMensal)]
    [InlineData(CriterioDeDistincaoDasApresentacoes.ExercicioAnual)]
    [InlineData(CriterioDeDistincaoDasApresentacoes.Ocorrencia)]
    public async Task ObterParaMutacao_DistincaoDaRegraDeDocumento_ConservaValoresEmOutroContexto(CriterioDeDistincaoDasApresentacoes? criterio)
    {
        ProcessoSeletivo processo = ProcessoComDistincao(criterio);
        await PersistirAsync(processo);
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        ProcessoSeletivo recarregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(contexto)
            .ObterParaMutacaoAsync(processo.Id, CancellationToken.None))!;

        RegraDeComprovacao original = processo.RegrasDeComprovacao.Single();
        RegraDeComprovacao regraDeDocumento = recarregado.RegrasDeComprovacao.Should().ContainSingle().Which;
        regraDeDocumento.Id.Should().Be(original.Id);
        regraDeDocumento.Distincao.Should().BeEquivalentTo(original.Distincao);
        regraDeDocumento.QuantidadeMinima.Should().Be(2);
        regraDeDocumento.DocumentoExigido!.Id.Should().Be(original.DocumentoExigidoId!.Value);
    }

    private async Task PersistirAsync(ProcessoSeletivo processo)
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        await ProcessoSeletivoRepositoryDeTeste.Criar(contexto).AdicionarAsync(processo, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private static ProcessoSeletivo ProcessoComDistincao(CriterioDeDistincaoDasApresentacoes? criterio)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("Processo com distinção", TiposDeProcessoDeTeste.SiSU,
            OrigemCandidatos.ImportacaoExterna, Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = Fase();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        DateOnly? referencia = criterio is CriterioDeDistincaoDasApresentacoes.CompetenciaMensal or CriterioDeDistincaoDasApresentacoes.ExercicioAnual ? new(2026, 2, 1) : null;
        string[]? ocorrencias = criterio == CriterioDeDistincaoDasApresentacoes.Ocorrencia ? ["a", "b"] : null;
        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(Documento(fase.Id), 0, 2, criterio, referencia, ocorrencias).Value!;
        processo.DefinirDocumentosExigidos([regraDeDocumento]).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static FaseCronograma Fase() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, false, false, false, false),
        new DecisoesDeCronogramaDaFase(1, null, null, UnidadesDeTeste.Ceps),
        new PublicacoesDaFase([], false, null), []).Value!;

    private static DocumentoExigido Documento(Guid fase) => DocumentoExigido.Criar(
        new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
        new MomentoDaExigencia(fase, null, null),
        new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
        new IndeferimentoDoDocumento(true, null, []),
        RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
        null).Value!;
}
