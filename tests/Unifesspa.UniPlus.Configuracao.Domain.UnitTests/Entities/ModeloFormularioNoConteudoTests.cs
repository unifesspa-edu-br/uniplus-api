namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ModeloFormularioNoConteudoTests
{
    private static readonly DomainError Erro = new("Modelo.Recusado", "O conteúdo foi recusado.");

    [Fact(DisplayName = "As recusas do conteúdo: sem campo segue sem campo, a raiz relativa é o conteúdo e o campo é filho dele")]
    public void NoConteudo_PrefixaSoOQueTemCaminho()
    {
        IEnumerable<FieldError> recusas = ModeloFormulario.NoConteudo(
            [new FieldError(null, Erro), new FieldError(string.Empty, Erro), new FieldError("itens[0]", Erro)]);

        recusas.Select(static recusa => recusa.Field).Should().Equal(null, "conteudo", "conteudo.itens[0]");
    }
}
