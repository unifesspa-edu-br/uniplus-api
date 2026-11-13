namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Email;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Email;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Observability;

public sealed class MetricasDaFilaDeEmailTests
{
    private readonly RelogioAvancavel _relogio = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));

    [Fact(DisplayName = "Antes de qualquer envio, os dois instantes existem na coleta e valem zero")]
    public void SemEnvio_OsInstantesExistemEmZero()
    {
        using MetricasDaFilaDeEmail metricas = new(_relogio);
        using ColetorDeMedidas coletor = new(metricas.Medidor);

        coletor.Coletar("uniplus.email.ultimo_envio").Should().ContainSingle().Which.Valor.Should().Be(0);
        coletor.Coletar("uniplus.email.ultima_falha").Should().ContainSingle().Which.Valor.Should().Be(0);
    }

    [Fact(DisplayName = "A amostra publica o retrato da fila e zera a idade da amostra")]
    public void Amostra_PublicaORetratoEZeraAIdade()
    {
        using MetricasDaFilaDeEmail metricas = new(_relogio);
        using ColetorDeMedidas coletor = new(metricas.Medidor);
        _relogio.Avancar(TimeSpan.FromMinutes(3));

        metricas.AtualizarAmostra(new AmostraDaFilaDeEmail(7, TimeSpan.FromSeconds(90), 2, 4));

        coletor.Ultima("uniplus.email.fila.prontas").Should().Be(7);
        coletor.Ultima("uniplus.email.fila.idade_mais_antiga").Should().Be(90);
        coletor.Ultima("uniplus.email.fila.atrasadas").Should().Be(2);
        coletor.Ultima("uniplus.email.mortas").Should().Be(4);
        coletor.Ultima("uniplus.email.amostra.idade").Should().Be(0);
    }

    [Fact(DisplayName = "Sem nova amostra, a idade dela cresce com o relógio desde a subida")]
    public void SemNovaAmostra_AIdadeCresce()
    {
        using MetricasDaFilaDeEmail metricas = new(_relogio);
        using ColetorDeMedidas coletor = new(metricas.Medidor);

        _relogio.Avancar(TimeSpan.FromMinutes(20));

        coletor.Ultima("uniplus.email.amostra.idade").Should().Be(20 * 60);
    }

    [Fact(DisplayName = "Registro que chega fora de ordem não faz o último envio nem a última falha voltarem no tempo")]
    public void RegistroForaDeOrdem_NaoVoltaNoTempo()
    {
        using MetricasDaFilaDeEmail metricas = new(_relogio);
        using ColetorDeMedidas coletor = new(metricas.Medidor);
        DateTimeOffset depois = _relogio.GetUtcNow().AddSeconds(30);

        metricas.RegistrarEnvio(depois, TimeSpan.Zero);
        metricas.RegistrarEnvio(_relogio.GetUtcNow(), TimeSpan.Zero);
        metricas.RegistrarFalha(permanente: false, depois);
        metricas.RegistrarFalha(permanente: false, _relogio.GetUtcNow());

        coletor.Ultima("uniplus.email.ultimo_envio").Should().Be(depois.ToUnixTimeMilliseconds() / 1000d);
        coletor.Ultima("uniplus.email.ultima_falha").Should().Be(depois.ToUnixTimeMilliseconds() / 1000d);
    }

    private sealed class RelogioAvancavel(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan intervalo) => _agora += intervalo;
    }
}
