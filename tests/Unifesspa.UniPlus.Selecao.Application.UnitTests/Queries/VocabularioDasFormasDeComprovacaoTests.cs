namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class VocabularioDasFormasDeComprovacaoTests
{
    [Fact(DisplayName = "As formas oferecidas apresentam os códigos e rótulos de cada regra")]
    public void Listar_DevolveCodigosERotulos()
    {
        IReadOnlyList<CodigoComRotulo<FormaDeComprovacao>> formas = ListarFormasDeComprovacaoQueryHandler.Handle(new());

        formas.Select(static forma => forma.Codigo).Should().Equal("DOCUMENTO", "TODOS_OS_ITENS", "QUANTIDADE_MINIMA_DE_ITENS");
        formas.Should().OnlyContain(static forma => !string.IsNullOrWhiteSpace(forma.Rotulo));
    }

    [Fact(DisplayName = "A consulta das formas exige uma pergunta")]
    public void Listar_SemConsulta_Recusa()
    {
        FluentActions.Invoking(() => ListarFormasDeComprovacaoQueryHandler.Handle(null!)).Should().Throw<ArgumentNullException>();
    }
}
