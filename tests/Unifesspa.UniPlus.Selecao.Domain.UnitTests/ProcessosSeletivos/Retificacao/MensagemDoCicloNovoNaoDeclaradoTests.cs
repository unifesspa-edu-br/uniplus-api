namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao.FaseDaRetificacaoDeTeste;

/// <summary>
/// A recusa do ciclo novo não declarado cita o resultado preliminar pelo nome que a versão publicada guardou, nunca
/// pelo código do tipo de ato nem pelo nome que o catálogo tenha passado a ter.
/// </summary>
public sealed class MensagemDoCicloNovoNaoDeclaradoTests
{
    private const string Codigo = "RESULTADO_PRELIMINAR";

    [Fact(DisplayName = "Cita o nome guardado na versão publicada e não o código do tipo")]
    public void NomeDaCopia_SubstituiOCodigo()
    {
        string mensagem = MensagemDaRecusa(Nomes(("RESULTADO_PRELIMINAR", "Resultado preliminar")));

        mensagem.Should().Contain("do resultado preliminar 'Resultado preliminar'");
        mensagem.Should().NotContain(Codigo);
    }

    [Fact(DisplayName = "Não cita o nome que o catálogo passou a ter depois da publicação")]
    public void NomeNovoDoCatalogo_NaoApareceNaMensagem()
    {
        string mensagem = MensagemDaRecusa(Nomes((Codigo, "Resultado preliminar")));

        mensagem.Should().NotContain("Preliminar da classificação");
    }

    [Fact(DisplayName = "Sem nome guardado para o tipo, diz só que é um resultado preliminar, sem o código")]
    public void SemNomeGuardado_NaoCitaOCodigo()
    {
        string mensagem = MensagemDaRecusa(Nomes());

        mensagem.Should().Contain("de entrada de um resultado preliminar, que já foi publicado");
        mensagem.Should().NotContain(Codigo);
    }

    private static NomesDosTiposDeAto Nomes(params (string Codigo, string Nome)[] nomes) =>
        new(nomes.ToDictionary(static n => n.Codigo, static n => n.Nome, StringComparer.Ordinal));

    private static string MensagemDaRecusa(NomesDosTiposDeAto nomes)
    {
        ProdutoDaFase preliminar = Preliminar(Codigo);
        FaseCronograma inscricao = Fase(1, "INSCRICAO");
        FaseCronograma prova = Fase(2, "PROVA", produtos: [preliminar]);
        FaseCronograma provaComPrazoNovo = ComJanela(prova, Instante.AddDays(1), Instante.AddDays(9));

        ResultadoDoCicloNovo resultado = ConferenciaDoCicloNovoDaRetificacao.Conferir(
            [inscricao, prova], [], [inscricao, provaComPrazoNovo], [], new HashSet<IdentidadeDoItemDoEdital>(),
            Fatos(Iniciadas(), Item(prova, preliminar)), nomes);

        return resultado.Recusas.Should().ContainSingle().Subject.Error.Message;
    }
}
