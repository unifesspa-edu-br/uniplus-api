namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma.VersoesPublicadasDeTeste;

/// <summary>
/// A pergunta única "a fase pode começar?": com as condições que existem hoje, nada impede o início, e a resposta traz o
/// instante do primeiro fato.
/// </summary>
public sealed class CondicoesDoInicioDaFaseTests
{
    [Fact(DisplayName = "O primeiro fato de uma fase pode iniciá-la, no instante em que ele passou a contar")]
    public void PodeComecar_PrimeiroFato_RespondeSimComOInstanteDoFato()
    {
        FatoQuePodeIniciarAFase primeiroFato = new(Guid.CreateVersion7(), Versao(UmaFase()), Abertura);

        Result<DateTimeOffset> resposta = CondicoesDoInicioDaFase.PodeComecar(primeiroFato);

        resposta.IsSuccess.Should().BeTrue();
        resposta.Value.Should().Be(Abertura);
    }

    [Fact(DisplayName = "O fato que não se amarra a versão nenhuma também pode iniciar a fase")]
    public void PodeComecar_FatoSemVersaoAplicavel_RespondeSim()
    {
        FatoQuePodeIniciarAFase primeiroFato = new(Guid.CreateVersion7(), VersaoAplicavel: null, Abertura);

        CondicoesDoInicioDaFase.PodeComecar(primeiroFato).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Sem o fato a pergunta não se faz")]
    public void PodeComecar_FatoNulo_Lanca()
    {
        Func<Result<DateTimeOffset>> perguntar = () => CondicoesDoInicioDaFase.PodeComecar(null!);

        perguntar.Should().Throw<ArgumentNullException>();
    }
}
