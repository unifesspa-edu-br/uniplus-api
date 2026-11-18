namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

public sealed class EnvelopeLidoDivulgacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture) : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact]
    public async Task Reidratar_PadraoMinimizadoSemLinhaPersistida_ReproduzOsBytes()
    {
        (ProcessoSeletivo processo, _) = ApoioDoEnvelopeCodec.ProcessoDeInscricao("PS divulgação padrão");
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes);
        EnvelopeReidratado lido = ApoioDoEnvelopeCodec.Reidratado(versao);
        lido.Configuracao.DivulgacaoETaxa.ConfiguracaoDivulgacao.Should().BeNull();
        Guid linha = await PersistirPadraoExplicitoAsync(processo);
        await RemoverLinhaAsync(processo.Id, lido.Configuracao.DivulgacaoETaxa.ConfiguracaoDivulgacao);
        await ConferirAusenciaEBytesAsync(processo.Id, linha, versao, lido, congelado);
    }

    private async Task<Guid> PersistirPadraoExplicitoAsync(ProcessoSeletivo processo)
    {
        processo.DefinirConfiguracaoDivulgacao(ConfiguracaoDivulgacao.Criar(["numero_inscricao"], null).Value!).IsSuccess.Should().BeTrue();
        await using UniPlusDbContext db = fixture.CreateDbContext();
        db.Add(processo);
        await db.SaveChangesAsync();
        return processo.ConfiguracaoDivulgacao!.Id;
    }

    private async Task RemoverLinhaAsync(Guid processoId, ConfiguracaoDivulgacao? configuracao)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = (await ProcessoSeletivoRepositoryDeTeste.Criar(db).ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        processo.ConfiguracaoDivulgacao.Should().NotBeNull();
        processo.DefinirConfiguracaoDivulgacao(configuracao).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private async Task ConferirAusenciaEBytesAsync(Guid processoId, Guid linha, VersaoConfiguracao versao, EnvelopeReidratado envelope, SnapshotCanonico congelado)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        (await db.Set<ConfiguracaoDivulgacao>().AnyAsync(c => c.Id == linha)).Should().BeFalse();
        ProcessoSeletivo processo = (await ProcessoSeletivoRepositoryDeTeste.Criar(db).ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        processo.ConfiguracaoDivulgacao.Should().BeNull();
        FidelidadeDoEnvelopeDeTeste.Recodificar(processo, versao, envelope).Bytes.Should().Equal(congelado.Bytes);
    }
}
