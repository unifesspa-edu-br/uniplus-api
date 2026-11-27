namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class OrigemCanonicaDaFaseTests
{
    [Theory(DisplayName = "A origem canônica da fase recusa nome ausente, vazio ou em branco")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_AusenteOuEmBranco_Recusa(string? nome)
    {
        Action criar = () => _ = new OrigemCanonicaDaFase(
            Guid.NewGuid(), "INSCRICAO", OrigemDataFase.NoEdital, false, false, false, false, false)
        { Nome = nome! };

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A origem canônica da fase guarda o nome informado")]
    public void Nome_Preenchido_FicaGuardado() =>
        OrigensDoTipoDeFaseDeTeste.Da(Guid.NewGuid(), "INSCRICAO", OrigemDataFase.NoEdital, false, false, false, false)
            .ComNome("Inscrição dos candidatos").Nome.Should().Be("Inscrição dos candidatos");
}
