namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;

public sealed class ExibicaoDoFatoDoProcessoTests
{
    [Theory]
    [InlineData(true, ClassificacaoDeProtecao.Identificador, ComponenteDeExibicao.Situacao, ExibicaoDoValorDoItem.Cpf)]
    [InlineData(true, ClassificacaoDeProtecao.Pessoal, ComponenteDeExibicao.Moeda, ExibicaoDoValorDoItem.Cpf)]
    [InlineData(false, ClassificacaoDeProtecao.Identificador, ComponenteDeExibicao.Situacao, ExibicaoDoValorDoItem.Identificador)]
    [InlineData(false, ClassificacaoDeProtecao.Pessoal, ComponenteDeExibicao.Moeda, ExibicaoDoValorDoItem.Moeda)]
    [InlineData(false, ClassificacaoDeProtecao.Pessoal, ComponenteDeExibicao.Situacao, ExibicaoDoValorDoItem.Situacao)]
    [InlineData(false, ClassificacaoDeProtecao.Pessoal, null, ExibicaoDoValorDoItem.Padrao)]
    public void Exibicao_ConformeProtecaoEDeclaracao_ResolvePrioridade(bool cpf, ClassificacaoDeProtecao classe,
        ComponenteDeExibicao? declarada, ExibicaoDoValorDoItem esperada) =>
        Fato(cpf, classe, declarada).Exibicao.Should().Be(esperada);

    [Fact]
    public void Exibicao_DeclaracaoInvalida_LancaExcecao() =>
        FluentActions.Invoking(() => Fato(false, ClassificacaoDeProtecao.Pessoal, (ComponenteDeExibicao)99).Exibicao)
            .Should().Throw<InvalidOperationException>();

    private static FatoDoProcesso Fato(bool cpf, ClassificacaoDeProtecao classe, ComponenteDeExibicao? declarada) =>
        new("FATO", "Fato", "Descrição", FormaDoValorDoItem.Texto, declarada, classe, null, null, 0,
            new Dictionary<string, string>(), cpf);
}
