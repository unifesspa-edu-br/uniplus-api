namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaCoerenciaDaConsequencia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosFormulariosComConsequencia;

/// <summary>
/// A ordem das causas das consequências: cada família reúne os formulários, as bancas, as exigências e os grupos, nessa ordem,
/// e as famílias seguem a ordem do assunto: facultativo, inscrição, acréscimo, modalidade em questão, ação da vaga e ampla.
/// </summary>
public sealed class OrdemDasCausasDasConsequenciasTests
{
    [Fact]
    public void Causas_FormularioFacultativoEBancaComAcrescimoNaoOferecido_RecusaOFormularioPrimeiro()
    {
        ProcessoSeletivo processo = ProcessoComBancaNaFase(ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        DefinirNoFormulario(processo, PapelDoFormularioDeTeste.IsencaoTaxa, ConsequenciaDoDescumprimento.EliminaDoProcesso);

        Campos(processo).Should().HaveCount(2);
        Campos(processo)[0].Should().StartWith("formularios[");
        Campos(processo)[1].Should().StartWith("fases[0].bancasRequeridas[");
    }

    [Fact]
    public void Causas_ExigenciaComAcrescimoNaoOferecidoEFormularioSemModalidadeEmQuestao_RecusaAExigenciaPrimeiro()
    {
        ProcessoSeletivo processo = ProcessoComBancaNaFase(null);
        DefinirNoFormulario(processo, PapelDoFormularioDeTeste.Habilitacao, ConsequenciaDoDescumprimento.RetiraAModalidade);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaQueRetiraOAcrescimo(processo), 0).Value!]).IsSuccess.Should().BeTrue();

        Campos(processo).Should().HaveCount(2);
        Campos(processo)[0].Should().StartWith("documentosExigidos.exigencias[");
        Campos(processo)[1].Should().StartWith("formularios[");
    }

    [Fact]
    public void Causas_FormularioBancaExigenciaEGrupoComAcrescimoNaoOferecido_SegueAOrdemDosLugares()
    {
        ProcessoSeletivo processo = ProcessoComBancaNaFase(ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        DefinirNoFormulario(processo, PapelDoFormularioDeTeste.Habilitacao, ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaQueRetiraOAcrescimo(processo), 0).Value!, GrupoQueRetiraOAcrescimo(processo)])
            .IsSuccess.Should().BeTrue();

        string[] campos = Campos(processo);

        campos.Should().HaveCount(4);
        campos[0].Should().StartWith("formularios[");
        campos[1].Should().StartWith("fases[0].bancasRequeridas[");
        campos[2].Should().StartWith("documentosExigidos.exigencias[");
        campos[3].Should().StartWith("documentosExigidos.regrasDeComprovacao[");
    }

    [Fact]
    public void Causas_BancaSemModalidadeEmQuestaoEFormularioSemAmpla_RecusaABancaPrimeiroEAmplaDasModalidadesPorUltimo()
    {
        ProcessoSeletivo processo = ProcessoComBancaNaFase(ConsequenciaDoDescumprimento.RetiraAModalidade, acaoDaModalidade: "RETIRA_A_MODALIDADE");
        ConsequenciasDaRetiradaDaModalidadeTests.RetirarAmplaDaOferta(processo);
        DefinirNoFormulario(processo, PapelDoFormularioDeTeste.Habilitacao, ConsequenciaDoDescumprimento.RetiraAModalidade);
        ExigirNoFormulario(processo, PapelDoFormularioDeTeste.Habilitacao, "LB_PPI");

        string[] campos = Campos(processo);

        campos.Should().HaveCount(3);
        campos[0].Should().StartWith("fases[0].bancasRequeridas[");
        campos[1].Should().StartWith("formularios[");
        campos[2].Should().StartWith("distribuicaoVagas[");
    }

    private static string[] Campos(ProcessoSeletivo processo) => [.. Causas(processo.Declaracao(), processo).Select(static causa => causa.Field!)];

    private static void DefinirNoFormulario(ProcessoSeletivo processo, PapelDoFormularioDeTeste papel, ConsequenciaDoDescumprimento consequencia) =>
        processo.DefinirFormulario(papel, null, null, FormularioDeTeste.Paginas(), null, consequencia).IsSuccess.Should().BeTrue();

    private static DocumentoExigido ExigenciaQueRetiraOAcrescimo(ProcessoSeletivo processo) =>
        DocumentoGeral(processo.CronogramaFases.First().Id, obrigatorio: false, consequencia: "RETIRA_O_ACRESCIMO", papel: PapelDoFormularioDeTeste.Inscricao);

    private static RegraDeComprovacao GrupoQueRetiraOAcrescimo(ProcessoSeletivo processo)
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoGeral(processo.CronogramaFases.First().Id, obrigatorio: false, consequencia: null, papel: PapelDoFormularioDeTeste.Inscricao), 0).Value!;
        return RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "RETIRA_O_ACRESCIMO"), 1, [BaseLegalDoGrupo(StatusBaseLegal.Resolvido)], [item]).Value!;
    }

    /// <summary>
    /// A oferta cuja modalidade LB_PPI tem a ação dada para o indeferido, sem fator oferecido, e a fase da inscrição com a
    /// banca de homologação e uma banca que avalia o mérito com a consequência da ausência dada.
    /// </summary>
    private static ProcessoSeletivo ProcessoComBancaNaFase(ConsequenciaDoDescumprimento? consequencia, string? acaoDaModalidade = null)
    {
        ModalidadeSelecionada modalidade = NovaModalidadeComAcao("LB_PPI", NaturezaLegalModalidade.CotaReservada,
            ComposicaoVagasModalidade.DentroDaReservaLegal, acaoDaModalidade);
        ProcessoSeletivo processo = NovoProcessoComModalidade(modalidade);
        BancaRequerida banca = BancaRequerida.Criar(ChaveLocal.Criar("banca_merito").Value, TiposDeBancaCopiadosDeTeste.Avaliadora(), [], BancasDeTeste.Regra(), consequenciaDaAusencia: consequencia);
        processo.RemoverFormulario(PapelDoFormularioDeTeste.Inscricao).IsSuccess.Should().BeTrue();
        Result definido = processo.DefinirCronogramaFases([ProcessoConformeFactory.FaseConforme(bancas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase(), banca])], []);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }
}
