namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A escolha que sai do processo junto com a fase ou a etapa que ele deixa de ter: a pendente vai com o lugar, e a que
/// já consta do edital publicado recusa a saída (UNI-REQ-0124).
/// </summary>
public sealed class QueSaemComOsLugaresTests
{
    private const string Objetiva = "Prova objetiva";
    private const string Discursiva = "Prova discursiva";

    private static MotivoDeDecisao Motivo(string codigo = "NOTA_ZERADA") =>
        ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.Classificacao, codigo, SentidoDoMotivo.Desfavoravel);

    private static MotivoDoProcesso Na(ProcessoSeletivo processo, string etapa, int entrada, string codigo = "NOTA_ZERADA") =>
        ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaEtapa(processo, etapa), Motivo(codigo), entrada);

    private static void SaiAEtapa(ProcessoSeletivo processo, string nome) =>
        processo.DefinirEtapas([.. processo.Etapas.Where(e => e.Nome != nome)], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

    private static Result<IReadOnlyList<MotivoDoProcesso>> QueSaem(LugaresDaDecisao antes, ProcessoSeletivo processo, params MotivoDoProcesso[] escolha) =>
        EscolhaDosMotivos.QueSaemComOsLugares(antes, processo, escolha);

    [Fact(DisplayName = "No rascunho, a etapa que sai leva só os motivos escolhidos nela")]
    public void Rascunho_EtapaQueSai_LevaOsMotivosDela()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDoProcesso daQueSai = Na(processo, Discursiva, entrada: 1);
        MotivoDoProcesso daQueFica = Na(processo, Objetiva, entrada: 1);
        LugaresDaDecisao antes = LugaresDaDecisao.Do(processo);
        SaiAEtapa(processo, Discursiva);

        QueSaem(antes, processo, daQueSai, daQueFica).Value.Should().Equal(daQueSai);
    }

    [Fact(DisplayName = "A etapa que continua, com a mesma identidade, não leva motivo algum")]
    public void Rascunho_EtapaQueContinua_NaoLevaNada()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDoProcesso escolhido = Na(processo, Objetiva, entrada: 1);
        LugaresDaDecisao antes = LugaresDaDecisao.Do(processo);
        SaiAEtapa(processo, Discursiva);

        QueSaem(antes, processo, escolhido).Value.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase que sai leva os motivos dela e os das etapas que saem com ela")]
    public void Rascunho_FaseQueSai_LevaOsMotivosDaFaseEDasEtapas()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugarDaDecisao daFase = ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO");
        MotivoDoProcesso naFase = ProcessoParaMotivos.Escolha(processo, daFase, Motivo(), 1);
        MotivoDoProcesso naEtapa = Na(processo, Objetiva, entrada: 1);
        LugaresDaDecisao antes = LugaresDaDecisao.Do(processo);
        processo.DefinirCronogramaFases([processo.CronogramaFases.Last()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        SaiAEtapa(processo, Objetiva);

        QueSaem(antes, processo, naFase, naEtapa).Value.Should().BeEquivalentTo([naFase, naEtapa]);
    }

    [Fact(DisplayName = "Na sessão de retificação, o motivo pendente da etapa que sai vai com ela")]
    public void Retificacao_MotivoPendente_Sai()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        MotivoDoProcesso pendente = Na(processo, Discursiva, entrada: VersaoConfiguracao.NumeroDaSucessora(VersaoConfiguracao.NumeroDaAbertura));
        LugaresDaDecisao antes = LugaresDaDecisao.Do(processo);
        SaiAEtapa(processo, Discursiva);

        QueSaem(antes, processo, pendente).Value.Should().Equal(pendente);
    }

    [Fact(DisplayName = "Na sessão de retificação, o motivo já publicado recusa a saída da etapa, com o campo e os códigos")]
    public void Retificacao_MotivoPublicado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        MotivoDoProcesso publicado = Na(processo, Discursiva, entrada: VersaoConfiguracao.NumeroDaAbertura);
        LugaresDaDecisao antes = LugaresDaDecisao.Do(processo);
        SaiAEtapa(processo, Discursiva);

        FieldError recusa = QueSaem(antes, processo, publicado).Errors.Should().ContainSingle().Subject;

        recusa.Field.Should().Be("cronograma.etapas[Prova discursiva]");
        recusa.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaComMotivoPublicado);
        recusa.Error.Message.Should().Be(
            "A etapa 'Prova discursiva' não pode ser removida: os motivos NOTA_ZERADA já constam do edital publicado nela.");
    }

    [Fact(DisplayName = "Motivo pendente e publicado no mesmo lugar recusam a saída e nada é devolvido para remover")]
    public void Retificacao_PendenteEPublicadoJuntos_RecusaSemDevolver()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        MotivoDoProcesso publicado = Na(processo, Discursiva, entrada: VersaoConfiguracao.NumeroDaAbertura, codigo: "SEM_NOTA");
        MotivoDoProcesso pendente = Na(processo, Discursiva, entrada: VersaoConfiguracao.NumeroDaSucessora(VersaoConfiguracao.NumeroDaAbertura));
        LugaresDaDecisao antes = LugaresDaDecisao.Do(processo);
        SaiAEtapa(processo, Discursiva);

        Result<IReadOnlyList<MotivoDoProcesso>> resultado = QueSaem(antes, processo, publicado, pendente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Single().Error.Message.Should().Contain("SEM_NOTA").And.NotContain("NOTA_ZERADA");
    }

    [Fact(DisplayName = "Dois lugares com motivo publicado geram duas recusas, a fase antes da etapa e os códigos em ordem")]
    public void Retificacao_DoisLugaresPublicados_DuasRecusasNaOrdem()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        MotivoDoProcesso naFase = ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), Motivo("B_CODIGO"), 1);
        MotivoDoProcesso naFaseTambem = ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), Motivo("A_CODIGO"), 1);
        MotivoDoProcesso naEtapa = Na(processo, Objetiva, entrada: 1);
        LugaresDaDecisao antes = LugaresDaDecisao.Do(processo);
        processo.DefinirCronogramaFases([processo.CronogramaFases.Last()], [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        SaiAEtapa(processo, Objetiva);

        FieldError[] recusas = [.. QueSaem(antes, processo, naEtapa, naFase, naFaseTambem).Errors];

        recusas.Select(r => r.Field).Should().Equal("cronograma.fases[INSCRICAO]", "cronograma.etapas[Prova objetiva]");
        recusas[0].Error.Message.Should().Contain("A_CODIGO, B_CODIGO");
    }
}
