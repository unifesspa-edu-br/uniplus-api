namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Idempotency;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

public sealed class IdempotencyOptionsTtlEmMicrossegundosTests
{
    private static IdempotencyOptions Configurar(string ttl) => new ConfigurationBuilder()
        .AddInMemoryCollection([new($"{IdempotencyOptions.SectionName}:Ttl", ttl)])
        .Build()
        .GetSection(IdempotencyOptions.SectionName)
        .Get<IdempotencyOptions>()!;

    [Fact(DisplayName = "O TTL configurado com resto de ticks é guardado em microssegundos inteiros, sem arredondar")]
    public void Ttl_ConfiguradoComResto_ChegaEmMicrossegundosInteiros()
    {
        IdempotencyOptions opcoes = Configurar("01:00:00.0000009");

        opcoes.Ttl.Should().Be(TimeSpan.FromHours(1));
        (opcoes.Ttl.Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
    }

    [Fact(DisplayName = "O TTL configurado em microssegundos inteiros permanece como veio")]
    public void Ttl_ConfiguradoEmMicrossegundos_PermaneceIgual() =>
        Configurar("01:00:00.0000010").Ttl.Should().Be(TimeSpan.FromHours(1) + TimeSpan.FromMicroseconds(1));

    [Fact(DisplayName = "O TTL padrão é o teto de 24 horas")]
    public void Ttl_Padrao_E24Horas() => new IdempotencyOptions().Ttl.Should().Be(TimeSpan.FromHours(24));

    [Fact(DisplayName = "O TTL atribuído em código com resto de ticks também é guardado em microssegundos inteiros")]
    public void Ttl_AtribuidoEmCodigo_ChegaEmMicrossegundosInteiros()
    {
        IdempotencyOptions opcoes = new() { Ttl = TimeSpan.FromTicks(18_000_000_001) };

        opcoes.Ttl.Ticks.Should().Be(18_000_000_000);
    }
}
