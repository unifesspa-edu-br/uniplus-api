namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class NiveisDeAtendimentoTests
{
    [Fact(DisplayName = "A recusa do nível repetido aponta o campo e não repete o rótulo enviado")]
    public void Validar_NivelRepetido_MensagemSemORotuloEnviado()
    {
        List<FieldError> erros = NiveisDeAtendimento.Validar(
            [new("Nível fictício de teste", 1m), new("Nível fictício de teste", 0.5m)]);

        FieldError erro = erros.Should().ContainSingle().Subject;
        (erro.Field, erro.Error.Code).Should().Be(("escalaDeAtendimento[1].rotulo", NiveisDeAtendimento.NivelDuplicado));
        erro.Error.Message.Should().Be("Há nível de atendimento repetido na escala: cada rótulo aparece uma só vez.");
        erro.Error.Message.Should().NotContainEquivalentOf("fictício");
    }
}
