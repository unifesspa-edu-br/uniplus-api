namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// A escolha que sai do processo junto com a fase ou a etapa que ele deixa de ter.
/// </summary>
public sealed class QueSaemComOsLugaresTests
{
    private const string Objetiva = "Prova objetiva";
    private const string Discursiva = "Prova discursiva";

    private static MotivoDeDecisao Motivo(string codigo = "NOTA_ZERADA") =>
        ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.Classificacao, codigo, SentidoDoMotivo.Desfavoravel);

    private static MotivoDoProcesso Na(ProcessoSeletivo processo, string etapa) =>
        ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaEtapa(processo, etapa), Motivo(), versaoDeEntrada: 1);

    private static void SaiAEtapa(ProcessoSeletivo processo, string nome) =>
        processo.DefinirEtapas([.. processo.Etapas.Where(e => e.Nome != nome)]).IsSuccess.Should().BeTrue();

    private static IReadOnlyList<MotivoDoProcesso> QueSaem(LugaresDaDecisao antes, ProcessoSeletivo processo, params MotivoDoProcesso[] escolha) =>
        EscolhaDosMotivos.QueSaemComOsLugares(antes, processo, escolha);

    [Fact(DisplayName = "No rascunho, a etapa que sai leva só os motivos escolhidos nela")]
    public void Rascunho_EtapaQueSai_LevaOsMotivosDela()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDoProcesso daQueSai = Na(processo, Discursiva);
        MotivoDoProcesso daQueFica = Na(processo, Objetiva);
        LugaresDaDecisao antes = ProcessoParaMotivos.Lugares(processo);
        SaiAEtapa(processo, Discursiva);

        QueSaem(antes, processo, daQueSai, daQueFica).Should().Equal(daQueSai);
    }

    [Fact(DisplayName = "A etapa que continua, com a mesma identidade, não leva motivo algum")]
    public void Rascunho_EtapaQueContinua_NaoLevaNada()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDoProcesso escolhido = Na(processo, Objetiva);
        LugaresDaDecisao antes = ProcessoParaMotivos.Lugares(processo);
        SaiAEtapa(processo, Discursiva);

        QueSaem(antes, processo, escolhido).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase que sai leva os motivos dela e os das etapas que saem com ela")]
    public void Rascunho_FaseQueSai_LevaOsMotivosDaFaseEDasEtapas()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugarDaDecisao daFase = ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO");
        MotivoDoProcesso naFase = ProcessoParaMotivos.Escolha(processo, daFase, Motivo(), 1);
        MotivoDoProcesso naEtapa = Na(processo, Objetiva);
        LugaresDaDecisao antes = ProcessoParaMotivos.Lugares(processo);
        processo.DefinirCronogramaFases([processo.CronogramaFases.Last()], []).IsSuccess.Should().BeTrue();
        SaiAEtapa(processo, Objetiva);

        QueSaem(antes, processo, naFase, naEtapa).Should().BeEquivalentTo([naFase, naEtapa]);
    }
}
