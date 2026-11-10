namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Observability;

public sealed class MetricasDaGovernancaDePerfisTests
{
    private readonly RelogioAvancavel _relogio = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));

    [Fact(DisplayName = "A amostragem lê o estado da governança, entrega às métricas e zera a idade da amostra")]
    public async Task Amostragem_PublicaOEstado()
    {
        using MetricasDaGovernancaDePerfis metricas = new(_relogio);
        using ColetorDeMedidas coletor = new(metricas.Medidor);
        AmostragemDaGovernanca amostragem = new(new LeituraFixa(new AmostraDaGovernanca(3, 2, 5, 1)), metricas, _relogio);
        _relogio.Avancar(TimeSpan.FromMinutes(7));

        await amostragem.ExecutarAsync(CancellationToken.None);

        coletor.Ultima("uniplus.autorizacao.versoes_pendentes.vencendo").Should().Be(3);
        coletor.Ultima("uniplus.autorizacao.janelas_abertas").Should().Be(2);
        coletor.Ultima("uniplus.autorizacao.reclassificacoes_pendentes").Should().Be(5);
        coletor.Ultima("uniplus.autorizacao.divergencias_da_governanca").Should().Be(1);
        coletor.Ultima("uniplus.autorizacao.amostra.idade").Should().Be(0);
    }

    [Fact(DisplayName = "Sem nova amostra, a idade dela cresce com o relógio desde a subida")]
    public void SemNovaAmostra_AIdadeCresce()
    {
        using MetricasDaGovernancaDePerfis metricas = new(_relogio);
        using ColetorDeMedidas coletor = new(metricas.Medidor);

        _relogio.Avancar(TimeSpan.FromMinutes(20));

        coletor.Ultima("uniplus.autorizacao.amostra.idade").Should().Be(20 * 60);
    }

    [Fact(DisplayName = "O aviso sem destinatário soma no contador com a etiqueta do tipo")]
    public void AvisoSemDestinatario_SomaPorTipo()
    {
        using MetricasDaGovernancaDePerfis metricas = new(_relogio);
        using ColetorDeMedidas coletor = new(metricas.Medidor);

        metricas.AvisoSemDestinatario("versao_encerrada");
        metricas.AvisoSemDestinatario("versao_encerrada");
        metricas.AvisoSemDestinatario("lote_enviado_com_recusas");

        coletor.Soma("uniplus.autorizacao.avisos_sem_destinatario", "tipo", "versao_encerrada").Should().Be(2);
    }

    private sealed class LeituraFixa(AmostraDaGovernanca amostra) : ILeituraDoMonitoramento
    {
        public Task<AmostraDaGovernanca> LerAsync(DateTimeOffset agora, CancellationToken cancellationToken = default) => Task.FromResult(amostra);
    }

    private sealed class RelogioAvancavel(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan intervalo) => _agora += intervalo;
    }
}
