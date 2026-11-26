namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosFormulariosComConsequencia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// A fase do formulário fica sem julgar só quando a leitura recusou uma fase inteira e o formulário cita uma fase que não
/// está entre as adotadas: só ele deixa de ser conferido, e só nas regras que dependem da fase.
/// </summary>
public sealed class FaseSemJulgarNosFormulariosTests
{
    private const string CampoDaHabilitacao = "formularios[1].consequenciaDaFaltaDeEnvio";

    [Fact]
    public void Causas_ParteRecusadaDentroDeFaseAdotada_ConfereOFormulario()
    {
        (ProcessoSeletivo processo, _) = HabilitacaoNaPropriaFase(ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        ConfiguracaoDeclarada configuracao = Lida([.. processo.CronogramaFases], [.. processo.Etapas], ComPartesRecusadas("fases[0].unidadeResponsavel"), processo.Declaracao());

        Causas(configuracao, processo).Select(static c => c.Field).Should().Equal(CampoDaHabilitacao);
    }

    [Fact]
    public void Causas_FaseInteiraRecusadaQueEraADoFormulario_DeixaOFormularioSemJulgar()
    {
        (ProcessoSeletivo processo, Guid habilitacao) = HabilitacaoNaPropriaFase(ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        ConfiguracaoDeclarada configuracao = SemAFase(processo, habilitacao);

        Causas(configuracao, processo).Should().BeEmpty();
    }

    [Fact]
    public void Causas_FaseInteiraRecusadaQueNaoEraADoFormulario_ConfereOFormulario()
    {
        (ProcessoSeletivo processo, _) = HabilitacaoNaPropriaFase(ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        ConfiguracaoDeclarada configuracao = Lida([.. processo.CronogramaFases], [.. processo.Etapas], ComPartesRecusadas("fases"), processo.Declaracao());

        Causas(configuracao, processo).Select(static c => c.Field).Should().Equal(CampoDaHabilitacao);
    }

    [Fact]
    public void Causas_FaseRecusadaComFormularioDeInscricaoDeclarando_RecusaAInscricao()
    {
        (ProcessoSeletivo processo, Guid habilitacao) = HabilitacaoNaPropriaFase(ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        DeclararNaInscricao(processo);
        ConfiguracaoDeclarada configuracao = SemAFase(processo, habilitacao);

        Causas(configuracao, processo).Select(static c => (c.Field, c.Error.Code)).Should().Equal(
            ("formularios[0].consequenciaDaFaltaDeEnvio", FormularioProcessoErrorCodes.ConsequenciaEmFormularioDaInscricao));
    }

    [Fact]
    public void Causas_FaseRecusadaComPedidoDeIsencaoDeclarando_RecusaOFacultativo()
    {
        (ProcessoSeletivo processo, Guid habilitacao) = HabilitacaoNaPropriaFase(ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        processo.DefinirFormulario(FinalidadeFormulario.IsencaoTaxa, null, null, FormularioDeTeste.Paginas(), null, ConsequenciaDoDescumprimento.EliminaDoProcesso)
            .IsSuccess.Should().BeTrue();
        ConfiguracaoDeclarada configuracao = SemAFase(processo, habilitacao);

        Causas(configuracao, processo).Select(static c => (c.Field, c.Error.Code)).Should().Equal(
            ("formularios[1].consequenciaDaFaltaDeEnvio", FormularioProcessoErrorCodes.ConsequenciaEmFormularioFacultativo));
    }

    [Fact]
    public void Painel_FaseDoFormularioSemJulgar_PendeOsItensQueDependemDaFase()
    {
        (ProcessoSeletivo processo, Guid habilitacao) = HabilitacaoNaPropriaFase(ConsequenciaDoDescumprimento.EliminaDoProcesso);
        ConfiguracaoDeclarada configuracao = SemAFase(processo, habilitacao);

        ItensDoFormulario(configuracao, processo).Where(static item => !item.Ok).Select(static item => item.Codigo).Should().BeEquivalentTo(
            "formulario_consequencia_em_formulario_da_inscricao", "formulario_acrescimo_nao_oferecido", "formulario_sem_modalidade_em_questao", "formulario_retira_a_modalidade_sem_ampla_concorrencia");
    }

    [Theory]
    [InlineData("fases[0].unidadeResponsavel")]
    [InlineData("fases")]
    public void Painel_FaseDoFormularioJulgada_AprovaOsItens(string recusada)
    {
        (ProcessoSeletivo processo, _) = HabilitacaoNaPropriaFase(ConsequenciaDoDescumprimento.EliminaDoProcesso);
        ConfiguracaoDeclarada configuracao = Lida([.. processo.CronogramaFases], [.. processo.Etapas], ComPartesRecusadas(recusada), processo.Declaracao());

        ItensDoFormulario(configuracao, processo).Should().OnlyContain(static item => item.Ok);
    }

    [Fact]
    public void Painel_FaseRecusadaSemFormularioDeclarando_AprovaOsItens()
    {
        (ProcessoSeletivo processo, Guid habilitacao) = HabilitacaoNaPropriaFase(null);
        ConfiguracaoDeclarada configuracao = SemAFase(processo, habilitacao);

        ItensDoFormulario(configuracao, processo).Should().OnlyContain(static item => item.Ok);
    }

    /// <summary>A leitura aceitou todas as fases menos a da habilitação, que foi recusada inteira.</summary>
    private static ConfiguracaoDeclarada SemAFase(ProcessoSeletivo processo, Guid fase) =>
        Lida([.. processo.CronogramaFases.Where(f => f.Id != fase)], [.. processo.Etapas], ComPartesRecusadas("fases"), processo.Declaracao());

    private static void DeclararNaInscricao(ProcessoSeletivo processo)
    {
        FormularioProcesso inscricao = processo.FormularioDe(FinalidadeFormulario.Inscricao)!;
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, inscricao.FaseId, inscricao.Titulo, FormularioDeTeste.Paginas(), null, ConsequenciaDoDescumprimento.EliminaDoProcesso)
            .IsSuccess.Should().BeTrue();
    }

    private static (ProcessoSeletivo Processo, Guid Fase) HabilitacaoNaPropriaFase(ConsequenciaDoDescumprimento? consequencia)
    {
        ProcessoSeletivo processo = ProcessoCom(FinalidadeFormulario.Habilitacao, null);
        Guid fase = AcrescentarFaseDaHabilitacao(processo);
        Result definido = processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, fase, null, FormularioDeTeste.Paginas(), null, consequencia);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
        return (processo, fase);
    }
}
