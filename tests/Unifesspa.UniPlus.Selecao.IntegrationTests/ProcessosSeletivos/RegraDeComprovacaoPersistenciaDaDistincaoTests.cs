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
    [InlineData(ChaveDistincao.CompetenciaMensal)]
    [InlineData(ChaveDistincao.ExercicioAnual)]
    [InlineData(ChaveDistincao.Ocorrencia)]
    public async Task ObterParaMutacao_DistincaoDaFolha_ConservaValoresEmOutroContexto(ChaveDistincao? chave)
    {
        ProcessoSeletivo processo = ProcessoComDistincao(chave);
        await PersistirAsync(processo);
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        ProcessoSeletivo recarregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(contexto)
            .ObterParaMutacaoAsync(processo.Id, CancellationToken.None))!;

        RegraDeComprovacao original = processo.RegrasDeComprovacao.Single();
        RegraDeComprovacao folha = recarregado.RegrasDeComprovacao.Should().ContainSingle().Which;
        folha.Id.Should().Be(original.Id);
        folha.Distincao.Should().BeEquivalentTo(original.Distincao);
        folha.QuantidadeMinima.Should().Be(2);
        folha.DocumentoExigido!.Id.Should().Be(original.DocumentoExigidoId!.Value);
    }

    private async Task PersistirAsync(ProcessoSeletivo processo)
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        await ProcessoSeletivoRepositoryDeTeste.Criar(contexto).AdicionarAsync(processo, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private static ProcessoSeletivo ProcessoComDistincao(ChaveDistincao? chave)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("Processo com distinção", TiposDeProcessoDeTeste.SiSU,
            OrigemCandidatos.ImportacaoExterna, Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = Fase();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        DateOnly? referencia = chave is ChaveDistincao.CompetenciaMensal or ChaveDistincao.ExercicioAnual ? new(2026, 2, 1) : null;
        string[]? ocorrencias = chave == ChaveDistincao.Ocorrencia ? ["a", "b"] : null;
        RegraDeComprovacao folha = RegraDeComprovacao.CriarRegraDeDocumento(Documento(fase.Id), 0, 2, chave, referencia, ocorrencias).Value!;
        processo.DefinirDocumentosExigidos([folha]).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static FaseCronograma Fase() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, false, false, false, false),
        new DecisoesDeCronogramaDaFase(1, null, null, UnidadesDeTeste.Ceps),
        new PublicacoesDaFase([], false, null), []).Value!;

    private static DocumentoExigido Documento(Guid fase) => DocumentoExigido.Criar(fase, Guid.CreateVersion7(),
        "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Geral,
        true, null, [], [], null, FormatosPermitidos.Criar(true, null).Value!, null).Value!;
}
