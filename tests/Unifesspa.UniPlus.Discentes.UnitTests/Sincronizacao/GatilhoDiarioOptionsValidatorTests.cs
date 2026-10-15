namespace Unifesspa.UniPlus.Discentes.UnitTests.Sincronizacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

public sealed class GatilhoDiarioOptionsValidatorTests
{
    [Fact]
    public void Aceita_horario_e_fuso_instalados()
    {
        GatilhoDiarioOptions opcoes = new()
        {
            Horario = "03:15",
            FusoHorarioId = "America/Belem",
            HorarioLimiteAlerta = "04:00",
            FusoHorarioAlertaId = "America/Belem",
            CanalAlerta = "discentes-sync",
            RetryAposSegundos = [30, 120],
            PrazoReatribuicaoSegundos = 300,
        };

        new GatilhoDiarioOptionsValidator().Validate(null, opcoes).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("3:15")]
    [InlineData("25:00")]
    public void Recusa_horario_fora_do_formato_exigido(string horario)
    {
        GatilhoDiarioOptions opcoes = new()
        {
            Horario = horario,
            FusoHorarioId = "America/Belem",
            HorarioLimiteAlerta = "04:00",
            FusoHorarioAlertaId = "America/Belem",
            CanalAlerta = "discentes-sync",
            RetryAposSegundos = [30, 120],
            PrazoReatribuicaoSegundos = 300,
        };

        new GatilhoDiarioOptionsValidator()
            .Validate(null, opcoes)
            .FailureMessage.Should().Contain(nameof(GatilhoDiarioOptions.Horario));
    }

    [Fact]
    public void Recusa_fuso_nao_instalado()
    {
        GatilhoDiarioOptions opcoes = new()
        {
            Horario = "03:15",
            FusoHorarioId = "Fuso/Inexistente",
            HorarioLimiteAlerta = "04:00",
            FusoHorarioAlertaId = "America/Belem",
            CanalAlerta = "discentes-sync",
            RetryAposSegundos = [30, 120],
            PrazoReatribuicaoSegundos = 300,
        };

        new GatilhoDiarioOptionsValidator()
            .Validate(null, opcoes)
            .FailureMessage.Should().Contain(nameof(GatilhoDiarioOptions.FusoHorarioId));
    }

    [Fact]
    public void Recusa_configuracao_operacional_incompleta()
    {
        GatilhoDiarioOptions opcoes = new()
        {
            Horario = "03:15",
            FusoHorarioId = "America/Belem",
        };

        string falhas = new GatilhoDiarioOptionsValidator()
            .Validate(null, opcoes)
            .FailureMessage ?? string.Empty;

        falhas.Should().Contain(nameof(GatilhoDiarioOptions.HorarioLimiteAlerta));
        falhas.Should().Contain(nameof(GatilhoDiarioOptions.FusoHorarioAlertaId));
        falhas.Should().Contain(nameof(GatilhoDiarioOptions.CanalAlerta));
        falhas.Should().Contain(nameof(GatilhoDiarioOptions.RetryAposSegundos));
        falhas.Should().Contain(nameof(GatilhoDiarioOptions.PrazoReatribuicaoSegundos));
    }
}
