namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// O limite da espera pela trava precisa ser positivo: zero desligaria o <c>lock_timeout</c> do banco e a espera voltaria a
/// ser sem fim. O prazo da nova tentativa viaja em segundos inteiros no cabeçalho <c>Retry-After</c>.
/// </summary>
public sealed class TravaDoProcessoOptionsTests
{
    [Fact(DisplayName = "Os valores padrão da espera pela trava são válidos")]
    public void Padrao_EhValido() =>
        TravaDoProcessoOptions.EhValida(new TravaDoProcessoOptions()).Should().BeTrue();

    [Theory(DisplayName = "A espera máxima nula ou negativa é recusada")]
    [InlineData(0)]
    [InlineData(-1)]
    public void EsperaMaximaNaoPositiva_Recusa(int milissegundos) =>
        TravaDoProcessoOptions.EhValida(new TravaDoProcessoOptions { EsperaMaxima = TimeSpan.FromMilliseconds(milissegundos) }).Should().BeFalse();

    [Fact(DisplayName = "A espera máxima maior que o limite de 32 bits em milissegundos do banco é recusada")]
    public void EsperaMaximaAlemDoLimiteDoBanco_Recusa() =>
        TravaDoProcessoOptions.EhValida(new TravaDoProcessoOptions { EsperaMaxima = TimeSpan.FromMilliseconds((double)int.MaxValue + 1) }).Should().BeFalse();

    [Fact(DisplayName = "A espera máxima fracionária arredonda para cima, em milissegundos inteiros")]
    public void EsperaMaximaFracionaria_ArredondaParaCima() =>
        new TravaDoProcessoOptions { EsperaMaxima = TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond + 1) }.EsperaMaximaEmMilissegundos.Should().Be(2);
}
