namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class BlocosDaConfiguracaoCopiasTests
{
    [Fact]
    public void CronogramaDeclarado_ListaOriginalMutada_ConservaFases()
    {
        List<FaseCronograma> fases = [];
        CronogramaDeclarado bloco = new(LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, null, fases);

        ConferirCopia(fases, bloco.CronogramaFases);
    }

    [Fact]
    public void AvaliacaoDeclarada_ListasOriginaisMutadas_ConservaEtapasECriterios()
    {
        List<EtapaProcesso> etapas = [];
        List<CriterioDesempate> criterios = [];
        AvaliacaoDeclarada bloco = new(etapas, criterios, null);

        ConferirCopia(etapas, bloco.Etapas);
        ConferirCopia(criterios, bloco.CriteriosDesempate);
    }

    [Fact]
    public void VagasDeclaradas_ListaOriginalMutada_ConservaDistribuicao()
    {
        List<ConfiguracaoDistribuicaoVagas> ofertas = [];
        VagasDeclaradas bloco = new(ofertas, null, null, null, null, null);

        ConferirCopia(ofertas, bloco.DistribuicaoVagas);
    }

    [Fact]
    public void ExigenciasDeclaradas_ListasOriginaisMutadas_ConservaDocumentosENos()
    {
        List<DocumentoExigido> documentos = [];
        List<NoExigencia> nos = [];
        ExigenciasDeclaradas bloco = new(documentos, nos);

        ConferirCopia(documentos, bloco.DocumentosExigidos);
        ConferirCopia(nos, bloco.NosExigencia);
    }

    [Fact]
    public void ColetaDeclarada_ListasOriginaisMutadas_ConservaEstrutura()
    {
        List<FatoColetado> campos = [];
        List<GrupoColetado> grupos = [];
        List<FormularioProcesso> formularios = [];
        ColetaDeclarada bloco = new(campos, grupos, formularios, [], [], [], null);

        ConferirCopia(campos, bloco.Campos);
        ConferirCopia(grupos, bloco.GruposColetados);
        ConferirCopia(formularios, bloco.Formularios);
    }

    [Fact]
    public void ColetaDeclarada_ListasOriginaisMutadas_ConservaTermosOpcoesEDerivacoes()
    {
        List<TermoExigidoFormulario> termos = [];
        List<OpcaoDeclaradaFato> opcoes = [];
        List<ConfiguracaoDerivacaoFato> derivacoes = [];
        ColetaDeclarada bloco = new([], [], [], termos, opcoes, derivacoes, null);

        ConferirCopia(termos, bloco.TermosExigidos);
        ConferirCopia(opcoes, bloco.OpcoesDeclaradas);
        ConferirCopia(derivacoes, bloco.RegrasDerivacao);
    }

    private static void ConferirCopia<T>(List<T> original, IReadOnlyList<T> copia)
    {
        original.Add(default!);
        copia.Should().BeEmpty();
    }
}
