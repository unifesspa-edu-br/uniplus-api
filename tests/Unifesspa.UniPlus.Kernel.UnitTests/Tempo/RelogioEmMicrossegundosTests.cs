namespace Unifesspa.UniPlus.Kernel.UnitTests.Tempo;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Tempo;

public sealed class RelogioEmMicrossegundosTests
{
    private static readonly DateTimeOffset InstanteComSetimaCasa = new(2026, 11, 13, 9, 30, 15, TimeSpan.Zero);

    [Fact(DisplayName = "GetUtcNow descarta a 7ª casa decimal da fonte")]
    public void GetUtcNow_DescartaSetimaCasa()
    {
        FonteDeRelogioDeTeste fonte = new() { Agora = InstanteComSetimaCasa.AddTicks(1_234_567) };
        RelogioEmMicrossegundos relogio = new(fonte);

        relogio.GetUtcNow().Should().Be(InstanteComSetimaCasa.AddTicks(1_234_560));
    }

    [Fact(DisplayName = "GetUtcNow consultado 1000 vezes devolve sempre múltiplo de 10 ticks")]
    public void GetUtcNow_SempreMultiploDeMicrossegundo()
    {
        FonteDeRelogioDeTeste fonte = new() { Agora = InstanteComSetimaCasa };
        RelogioEmMicrossegundos relogio = new(fonte);

        for (int i = 0; i < 1000; i++)
        {
            fonte.Agora = fonte.Agora.AddTicks(1);
            (relogio.GetUtcNow().Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
        }
    }

    [Fact(DisplayName = "GetTimestamp e TimestampFrequency repassam os valores da fonte")]
    public void Timestamp_RepassaAFonte()
    {
        RelogioEmMicrossegundos relogio = new(new FonteDeRelogioDeTeste());

        relogio.GetTimestamp().Should().Be(FonteDeRelogioDeTeste.Timestamp);
        relogio.TimestampFrequency.Should().Be(FonteDeRelogioDeTeste.FrequenciaDeTimestamp);
    }

    [Fact(DisplayName = "LocalTimeZone repassa o fuso da fonte")]
    public void LocalTimeZone_RepassaAFonte()
    {
        TimeZoneInfo fuso = TimeZoneInfo.CreateCustomTimeZone("teste", TimeSpan.FromHours(-3), "teste", "teste");
        RelogioEmMicrossegundos relogio = new(new FonteDeRelogioDeTeste { FusoLocal = fuso });

        relogio.LocalTimeZone.Should().BeSameAs(fuso);
    }

    [Fact(DisplayName = "CreateTimer cria o temporizador na fonte com os mesmos parâmetros")]
    public void CreateTimer_RepassaAFonte()
    {
        FonteDeRelogioDeTeste fonte = new();
        RelogioEmMicrossegundos relogio = new(fonte);
        TimeSpan espera = TimeSpan.FromSeconds(5);
        TimeSpan periodo = TimeSpan.FromMinutes(1);

        using ITimer temporizador = relogio.CreateTimer(_ => { }, null, espera, periodo);

        temporizador.Should().BeSameAs(fonte.TemporizadorCriado);
        fonte.ParametrosDoTemporizador.Should().Be((espera, periodo));
    }

    [Fact(DisplayName = "DoSistema devolve sempre a mesma instância")]
    public void DoSistema_EhSingleton()
    {
        RelogioEmMicrossegundos.DoSistema.Should().BeSameAs(RelogioEmMicrossegundos.DoSistema);
    }

    [Fact(DisplayName = "DoSistema devolve o instante atual truncado em microssegundos")]
    public void DoSistema_DevolveInstanteTruncado()
    {
        DateTimeOffset antes = DateTimeOffset.UtcNow.TruncadoEmMicrossegundos();

        DateTimeOffset instante = RelogioEmMicrossegundos.DoSistema.GetUtcNow();

        (instante.Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
        instante.Should().BeOnOrAfter(antes);
    }

    [Fact(DisplayName = "Construtor recusa fonte nula")]
    public void Construtor_RecusaFonteNula()
    {
        Action criar = () => _ = new RelogioEmMicrossegundos(null!);

        criar.Should().Throw<ArgumentNullException>();
    }
}
