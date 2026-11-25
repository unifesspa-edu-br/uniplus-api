namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class SituacaoDoTermoNaInstalacaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<SituacaoDoTermoNaInstalacao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<SituacaoDoTermoNaInstalacao, string>
            {
                [SituacaoDoTermoNaInstalacao.SemTermoPublicado] = "SEM_TERMO_PUBLICADO",
                [SituacaoDoTermoNaInstalacao.TermoVigenteSemAceite] = "TERMO_VIGENTE_SEM_ACEITE",
            });
}
