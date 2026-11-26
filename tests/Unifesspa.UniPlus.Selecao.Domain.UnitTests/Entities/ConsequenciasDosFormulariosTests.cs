namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaCoerenciaDaConsequencia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosFormulariosComConsequencia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// A consequência da falta de envio declarada no formulário: o pedido de isenção e o formulário que coleta a inscrição
/// não a declaram, e nos demais ela é conferida como a das exigências, formulário por formulário.
/// </summary>
public sealed class ConsequenciasDosFormulariosTests
{
    [Fact]
    public void Facultativos_PedidoDeIsencaoComConsequencia_RecusaNoCampoDoFormulario()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.IsencaoTaxa, ConsequenciaDoDescumprimento.EliminaDoProcesso);

        FieldError recusa = ConsequenciasDosFormularios.Facultativos(processo.Declaracao()).Should().ContainSingle().Subject;

        recusa.Error.Code.Should().Be(FormularioProcessoErrorCodes.ConsequenciaEmFormularioFacultativo);
        recusa.Field.Should().Be("formularios[1].consequenciaDaFaltaDeEnvio");
        recusa.Error.Message.Should().Contain("'ISENCAO_TAXA'");
    }

    [Fact]
    public void DaInscricao_FormularioDeInscricaoComConsequencia_RecusaNoCampoDoFormulario()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Inscricao, ConsequenciaDoDescumprimento.EliminaDoProcesso);

        FieldError recusa = ConsequenciasDosFormularios.DaInscricao(processo.Declaracao(), Cronograma(processo.Declaracao())).Should().ContainSingle().Subject;

        recusa.Error.Code.Should().Be(FormularioProcessoErrorCodes.ConsequenciaEmFormularioDaInscricao);
        recusa.Field.Should().Be("formularios[0].consequenciaDaFaltaDeEnvio");
        recusa.Error.Message.Should().Contain("'INSCRICAO'");
    }

    [Fact]
    public void DaInscricao_FormularioDeHabilitacaoNaFaseQueColetaInscricao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, null);
        Guid fase = AcrescentarFaseDaHabilitacao(processo, coletaInscricao: true);
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, fase, null, FormularioDeTeste.Paginas(), null, ConsequenciaDoDescumprimento.EliminaDoProcesso)
            .IsSuccess.Should().BeTrue();

        FieldError recusa = ConsequenciasDosFormularios.DaInscricao(processo.Declaracao(), Cronograma(processo.Declaracao())).Should().ContainSingle().Subject;

        recusa.Error.Code.Should().Be(FormularioProcessoErrorCodes.ConsequenciaEmFormularioDaInscricao);
        recusa.Field.Should().Be("formularios[1].consequenciaDaFaltaDeEnvio");
    }

    [Fact]
    public void Causas_FormularioDeHabilitacaoQueEliminaDoProcesso_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.EliminaDoProcesso);

        Causas(processo.Declaracao(), processo).Should().BeEmpty();
    }

    [Fact]
    public void Causas_FormularioSemConsequencia_NaoRecusaNemNoPedidoDeIsencao()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.IsencaoTaxa, null);

        Causas(processo.Declaracao(), processo).Should().BeEmpty();
    }

    [Fact]
    public void Facultativos_PedidoDeIsencaoRecusadoNaoGeraAsCausasDeCoerencia()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.IsencaoTaxa, ConsequenciaDoDescumprimento.RetiraAModalidade);

        Causas(processo.Declaracao(), processo).Select(static c => c.Error.Code)
            .Should().Equal(FormularioProcessoErrorCodes.ConsequenciaEmFormularioFacultativo);
    }

    [Fact]
    public void Acrescimo_FormularioObrigatorioSemFatorOferecido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.RetiraOAcrescimo);

        ConsequenciasDosFormularios.Acrescimo(processo.Declaracao(), Cronograma(processo.Declaracao())).Should().ContainSingle()
            .Which.Field.Should().Be("formularios[1].consequenciaDaFaltaDeEnvio");
    }

    [Fact]
    public void Acrescimo_FormularioObrigatorioComFatorOferecido_Aceita()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        ConfiguracaoBonusRegional bonus = ConfiguracaoBonusRegional.Criar(
            ReferenciaRegra.De(RegraDeBonus.Multiplicativo.Codigo(), "v1", HashFixo), 1.2m, null,
            FormularioDeTeste.FatoBooleanoDaInscricao, new BaseLegalCongelada(Guid.CreateVersion7(), "PORTARIA", "PORTARIA", "Portaria", "Inclusão regional", [("1504208", "Marabá", "PA")])).Value!;
        processo.DefinirBonusRegional(true, bonus).IsSuccess.Should().BeTrue();

        ConsequenciasDosFormularios.Acrescimo(processo.Declaracao(), Cronograma(processo.Declaracao())).Should().BeEmpty();
    }

    [Fact]
    public void SemModalidade_RetiraAModalidadeSemExigenciaNoFormulario_Recusa()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.RetiraAModalidade);

        ConsequenciasDosFormularios.SemModalidade(processo.Declaracao(), Fatos(processo), Cronograma(processo.Declaracao())).Should().ContainSingle()
            .Which.Field.Should().Be("formularios[1].consequenciaDaFaltaDeEnvio");
    }

    [Theory]
    [InlineData("LB_PPI", false)]
    [InlineData("INEXISTENTE", true)]
    public void SemModalidade_ExigenciaDoFormulario_ConfereAModalidadeAlcancada(string codigo, bool recusa)
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.RetiraAModalidade);
        ExigirNoFormulario(processo, FinalidadeFormulario.Habilitacao, codigo);

        ConsequenciasDosFormularios.SemModalidade(processo.Declaracao(), Fatos(processo), Cronograma(processo.Declaracao())).Any().Should().Be(recusa);
    }

    [Fact]
    public void SemModalidade_ExigenciaGeralNoFormulario_AlcancaTodasAsModalidadesERecusa()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.RetiraAModalidade);
        Guid fase = AcrescentarFaseDaHabilitacao(processo);
        DocumentoExigido geral = DocumentoGeral(fase, obrigatorio: false, consequencia: null, finalidade: FinalidadeFormulario.Habilitacao);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(geral, 0).Value!]).IsSuccess.Should().BeTrue();

        ConsequenciasDosFormularios.SemModalidade(processo.Declaracao(), Fatos(processo), Cronograma(processo.Declaracao())).Should().ContainSingle()
            .Which.Field.Should().Be("formularios[1].consequenciaDaFaltaDeEnvio");
    }

    [Fact]
    public void SemModalidade_ExigenciaDeOutroFormulario_NaoConta()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.RetiraAModalidade);
        ExigirNoFormulario(processo, FinalidadeFormulario.Inscricao, "LB_PPI");

        ConsequenciasDosFormularios.SemModalidade(processo.Declaracao(), Fatos(processo), Cronograma(processo.Declaracao())).Should().ContainSingle();
    }

    [Theory]
    [InlineData("LB_PPI", true)]
    [InlineData("INEXISTENTE", false)]
    public void SemAmpla_OfertaSemAmpla_RecusaQuandoAExigenciaDoFormularioAlcancaAOferta(string codigo, bool recusa)
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.RetiraAModalidade);
        ConsequenciasDaRetiradaDaModalidadeTests.RetirarAmplaDaOferta(processo);
        ExigirNoFormulario(processo, FinalidadeFormulario.Habilitacao, codigo);

        ConsequenciasDosFormularios.SemAmpla(processo.Declaracao(), Fatos(processo), Cronograma(processo.Declaracao())).Any().Should().Be(recusa);
    }

    [Fact]
    public void SemAmpla_OfertaComAmpla_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.RetiraAModalidade);
        ExigirNoFormulario(processo, FinalidadeFormulario.Habilitacao, "LB_PPI");

        ConsequenciasDosFormularios.SemAmpla(processo.Declaracao(), Fatos(processo), Cronograma(processo.Declaracao())).Should().BeEmpty();
    }

    [Fact]
    public void Causas_FormulariosDaMesmaFase_TemConsequenciasIndependentes()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.EliminaDoProcesso);
        processo.DefinirFormulario(FinalidadeFormulario.IsencaoTaxa, null, null, FormularioDeTeste.Paginas()).IsSuccess.Should().BeTrue();

        Causas(processo.Declaracao(), processo).Should().BeEmpty();
    }

    [Fact]
    public void Causas_UmFormularioIncoerenteNaoRecusaOOutro()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.RetiraAModalidade);
        processo.DefinirFormulario(FinalidadeFormulario.IsencaoTaxa, null, null, FormularioDeTeste.Paginas(), null, ConsequenciaDoDescumprimento.EliminaDoProcesso)
            .IsSuccess.Should().BeTrue();

        Causas(processo.Declaracao(), processo).Select(static c => (c.Field, c.Error.Code)).Should().Equal(
            ("formularios[1].consequenciaDaFaltaDeEnvio", FormularioProcessoErrorCodes.ConsequenciaEmFormularioFacultativo),
            ("formularios[2].consequenciaDaFaltaDeEnvio", "ConsequenciasDoDescumprimento.SemModalidadeEmQuestao"));
    }

    [Fact]
    public void Causas_VariasRecusas_SeguemAOrdemFacultativoInscricaoEDepoisCoerencia()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        processo.DefinirFormulario(FinalidadeFormulario.IsencaoTaxa, null, null, FormularioDeTeste.Paginas(), null, ConsequenciaDoDescumprimento.EliminaDoProcesso)
            .IsSuccess.Should().BeTrue();
        FormularioProcesso inscricao = processo.FormularioDe(FinalidadeFormulario.Inscricao)!;
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, inscricao.FaseId, inscricao.Titulo, FormularioDeTeste.Paginas(), null, ConsequenciaDoDescumprimento.EliminaDoProcesso)
            .IsSuccess.Should().BeTrue();

        Causas(processo.Declaracao(), processo).Select(static c => c.Field).Should().Equal(
            "formularios[1].consequenciaDaFaltaDeEnvio",
            "formularios[0].consequenciaDaFaltaDeEnvio",
            "formularios[2].consequenciaDaFaltaDeEnvio");
    }

    [Fact]
    public void Painel_FormularioConforme_AprovaOsCincoItensDoFormulario()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.EliminaDoProcesso);

        ItensDoFormulario(processo.Declaracao(), processo).Should().HaveCount(5).And.OnlyContain(static item => item.Ok);
    }

    [Theory]
    [InlineData(FinalidadeFormulario.IsencaoTaxa, "formulario_consequencia_em_formulario_facultativo")]
    [InlineData(FinalidadeFormulario.Inscricao, "formulario_consequencia_em_formulario_da_inscricao")]
    public void Painel_FormularioQueNaoDeclara_PendeOItemDele(FinalidadeFormulario finalidade, string item)
    {
        ProcessoSeletivo processo = ProcessoCom(finalidade, ConsequenciaDoDescumprimento.EliminaDoProcesso);

        ItensDoFormulario(processo.Declaracao(), processo).Single(i => i.Codigo == item).Ok.Should().BeFalse();
        ItensDoFormulario(processo.Declaracao(), processo).Count(static i => !i.Ok).Should().Be(1);
    }

    [Theory]
    [InlineData(ConsequenciaDoDescumprimento.RetiraOAcrescimo, "formulario_acrescimo_nao_oferecido")]
    [InlineData(ConsequenciaDoDescumprimento.RetiraAModalidade, "formulario_sem_modalidade_em_questao")]
    public void Painel_FormularioComConsequenciaIncoerente_PendeOItemDele(ConsequenciaDoDescumprimento consequencia, string item)
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, consequencia);

        ItensDoFormulario(processo.Declaracao(), processo).Single(i => i.Codigo == item).Ok.Should().BeFalse();
    }

    [Fact]
    public void Painel_FormulariosRecusados_MantemTodosOsItensPendentes()
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, ConsequenciaDoDescumprimento.EliminaDoProcesso);
        ConfiguracaoDeclarada configuracao = Lida([.. processo.CronogramaFases], [.. processo.Etapas], ComPartesRecusadas("formularios"), processo.Declaracao());

        ItensDoFormulario(configuracao, processo).Should().OnlyContain(static item => !item.Ok);
    }
}
