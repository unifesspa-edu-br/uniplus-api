namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.EtapasLidasDeTeste;

/// <summary>A fase que a etapa cita pela chave vira o código da fase que a leitura adotou; a recusada pela leitura não gera causa nova, e a chave que nenhuma fase tem é recusa da etapa.</summary>
public sealed class LeituraDaFaseDaEtapaTests
{
    private static FasesLidas Fases(params FaseNoDocumento[] fases) => LeituraDasFases.Ler(fases, Cadastros());

    [Fact(DisplayName = "A chave da fase adotada vira o código da fase, sem recusa")]
    public void Chave_DeFaseAdotada_ViraOCodigo()
    {
        ValorLido<string?> fase = LeituraDaFaseDaEtapa.Ler(Etapa(0, "etapa_a", fase: "avaliacao"), Fases(Fase(0, "avaliacao", "INSCRICAO")));

        fase.Valor.Should().Be("INSCRICAO");
        fase.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa que não cita fase fica sem fase e sem recusa")]
    public void Etapa_SemChaveDeFase_FicaSemFaseESemRecusa()
    {
        ValorLido<string?> fase = LeituraDaFaseDaEtapa.Ler(Etapa(0, "etapa_a"), Fases(Fase(0, "avaliacao", "INSCRICAO")));

        fase.Valor.Should().BeNull();
        fase.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase que a leitura recusou deixa a etapa sem fase e sem causa nova, porque a causa é a da fase")]
    public void Chave_DeFaseRecusadaPelaLeitura_FicaSemFaseESemRecusa()
    {
        ValorLido<string?> fase = LeituraDaFaseDaEtapa.Ler(Etapa(0, "etapa_a", fase: "avaliacao"), Fases(Fase(0, "avaliacao", "FASE-DIGITADA")));

        fase.Valor.Should().BeNull();
        fase.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A chave que nenhuma fase do documento tem recusa o campo da fase da etapa, sem repetir a chave escrita")]
    public void Chave_Inexistente_RecusaOCampoDaFase()
    {
        ValorLido<string?> fase = LeituraDaFaseDaEtapa.Ler(Etapa(2, "etapa_c", fase: "chave_digitada"), Fases(Fase(0, "avaliacao", "INSCRICAO")));

        fase.Valor.Should().BeNull();
        RecusaDaParte recusa = fase.Recusas.Should().ContainSingle().Subject;
        recusa.Parte.Should().Be("faseCodigo");
        recusa.Campo.Should().Be("faseCodigo");
        recusa.PosicaoNoDocumento.Should().Be(2);
        recusa.Chave.Should().Be("etapa_c");
        recusa.Erro.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseNoCronograma");
        recusa.Erro.Message.Should().NotContain("chave_digitada").And.NotContain("Prova objetiva de exemplo");
    }
}
