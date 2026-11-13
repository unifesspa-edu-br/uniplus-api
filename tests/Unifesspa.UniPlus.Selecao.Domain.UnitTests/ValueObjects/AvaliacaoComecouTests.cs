namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O início da avaliação por lugar: a fase só conta pelo que é dela, a etapa pela etapa iniciada.</summary>
public sealed class AvaliacaoComecouTests
{
    private static readonly Guid Fase = Guid.CreateVersion7();
    private static readonly Guid Etapa = Guid.CreateVersion7();

    [Fact(DisplayName = "A fase produtora tem a avaliação começada")]
    public void FaseProdutora_Comecou()
    {
        FasesIniciadas iniciadas = new([KeyValuePair.Create<Guid, Guid?>(Fase, null)], fasesProdutoras: [Fase]);

        iniciadas.AvaliacaoComecou(LugarDaDecisao.DaFase(Fase)).Should().BeTrue();
    }

    [Fact(DisplayName = "A fase iniciada só por algo que não é dela não tem a avaliação começada")]
    public void FaseIniciadaPorOutraCoisa_NaoComecou()
    {
        FasesIniciadas iniciadas = new([KeyValuePair.Create<Guid, Guid?>(Fase, null)]);

        iniciadas.Contem(Fase).Should().BeTrue();
        iniciadas.AvaliacaoComecou(LugarDaDecisao.DaFase(Fase)).Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa iniciada tem a avaliação começada, e a irmã não")]
    public void EtapaIniciada_Comecou()
    {
        FasesIniciadas iniciadas = new([], etapas: [new EtapaIniciada(Etapa, "Prova", NaVersaoVigente: true)]);

        iniciadas.AvaliacaoComecou(LugarDaDecisao.DaEtapa(Etapa)).Should().BeTrue();
        iniciadas.AvaliacaoComecou(LugarDaDecisao.DaEtapa(Guid.CreateVersion7())).Should().BeFalse();
    }

    [Fact(DisplayName = "A fase produtora e a etapa com o mesmo identificador não se confundem")]
    public void FaseEEtapa_ComMesmoIdentificador_NaoSeConfundem()
    {
        FasesIniciadas iniciadas = new([], etapas: [new EtapaIniciada(Fase, "Prova", NaVersaoVigente: true)]);

        iniciadas.AvaliacaoComecou(LugarDaDecisao.DaFase(Fase)).Should().BeFalse();
        iniciadas.AvaliacaoComecou(LugarDaDecisao.DaEtapa(Fase)).Should().BeTrue();
    }

    [Fact(DisplayName = "Sem nenhuma fase iniciada, nenhum lugar tem a avaliação começada")]
    public void Nenhuma_NaoComecou()
    {
        FasesIniciadas.Nenhuma.AvaliacaoComecou(LugarDaDecisao.DaFase(Fase)).Should().BeFalse();
        FasesIniciadas.Nenhuma.AvaliacaoComecou(LugarDaDecisao.DaEtapa(Etapa)).Should().BeFalse();
    }
}
