namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

/// <summary>
/// Todo acesso de topo ao envelope passa pela classificação de exposição: o bloco público devolve a própria
/// chave, e o interno ou não classificado falha, para que a projeção nunca o leia por engano.
/// </summary>
public sealed class BlocoPublicoDaProjecaoDoCertameTests
{
    public static TheoryData<string> Publicos() => [.. ClassificacaoDosBlocosDoCertame.Publicados];

    public static TheoryData<string> Internos() => [.. ClassificacaoDosBlocosDoCertame.Internos];

    [Theory(DisplayName = "O bloco público devolve a própria chave")]
    [MemberData(nameof(Publicos))]
    public void BlocoPublico_BlocoPublico_DevolveAChave(string chave) =>
        ProjecaoDoCertamePublicado.BlocoPublico(chave).Should().Be(chave);

    [Theory(DisplayName = "O bloco interno falha, nomeando a chave")]
    [MemberData(nameof(Internos))]
    public void BlocoPublico_BlocoInterno_Falha(string chave) =>
        FluentActions.Invoking(() => ProjecaoDoCertamePublicado.BlocoPublico(chave))
            .Should().Throw<InvalidOperationException>().WithMessage($"*'{chave}'*não está classificado como público*");

    [Fact(DisplayName = "O bloco que a classificação não conhece falha")]
    public void BlocoPublico_BlocoNaoClassificado_Falha() =>
        FluentActions.Invoking(static () => ProjecaoDoCertamePublicado.BlocoPublico("blocoNovo"))
            .Should().Throw<InvalidOperationException>().WithMessage("*'blocoNovo'*");
}
