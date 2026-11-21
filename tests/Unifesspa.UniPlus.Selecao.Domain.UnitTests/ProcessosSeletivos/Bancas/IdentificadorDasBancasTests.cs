namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

public sealed class IdentificadorDasBancasTests
{
    private static readonly Guid Primeiro = new("00000000-0000-4000-8000-000000000001");
    private static readonly Guid Segundo = new("00000000-0000-4000-8000-000000000002");

    [Fact(DisplayName = "Identificadores diferentes, ou bancas sem identificador, não são recusados")]
    public void Repetidos_SemRepeticao_NaoRecusa()
    {
        IdentificadorDasBancas.Repetidos([Primeiro, Segundo, null, null], "bancas", IdentificadorDasBancas.DaEtapa).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca que repete o identificador de uma anterior é recusada no campo dela, da segunda ocorrência em diante")]
    public void Repetidos_ComRepeticao_RecusaDaSegundaOcorrenciaEmDiante()
    {
        IReadOnlyList<FieldError> recusas = IdentificadorDasBancas.Repetidos(
            [Primeiro, Segundo, Primeiro, Primeiro], "bancasRequeridas", IdentificadorDasBancas.DaFase);

        recusas.Select(static r => r.Field).Should().Equal("bancasRequeridas[2].id", "bancasRequeridas[3].id");
        recusas.Should().OnlyContain(r => r.Error.Code == BancaErrorCodes.IdDuplicado);
    }

    [Theory(DisplayName = "A mensagem diz se a repetição é na fase ou na etapa e não repete o valor recebido")]
    [InlineData(IdentificadorDasBancas.DaFase)]
    [InlineData(IdentificadorDasBancas.DaEtapa)]
    public void Repetidos_Mensagem_NomeiaOLugarSemEcoDoValor(string lugar)
    {
        FieldError recusa = IdentificadorDasBancas.Repetidos([Primeiro, Primeiro], "bancas", lugar).Single();

        recusa.Error.Message.Should().Be(
            $"O mesmo identificador de banca foi informado em mais de uma banca da {lugar}; cada banca tem identificador próprio.");
        recusa.Error.Message.Should().NotContain(Primeiro.ToString());
    }
}
