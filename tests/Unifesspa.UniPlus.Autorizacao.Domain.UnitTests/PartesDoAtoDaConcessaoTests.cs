namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class PartesDoAtoDaConcessaoTests
{
    [Fact(DisplayName = "O ato sem quem age é recusado ao ser montado, antes de chegar à trilha")]
    public void AtoDoAgente_SemAgente_Lanca()
    {
        Action montar = () => _ = new AtoDoAgente(null!, DateTimeOffset.UnixEpoch);

        montar.Should().Throw<ArgumentNullException>().WithParameterName("Agente");
    }

    [Fact(DisplayName = "O titular sem a pessoa é recusado ao ser montado")]
    public void TitularDaConcessao_SemPessoa_Lanca()
    {
        Action montar = () => _ = new TitularDaConcessao(null!, "leandro.viana@uniplus-test.local");

        montar.Should().Throw<ArgumentNullException>().WithParameterName("Pessoa");
    }
}
