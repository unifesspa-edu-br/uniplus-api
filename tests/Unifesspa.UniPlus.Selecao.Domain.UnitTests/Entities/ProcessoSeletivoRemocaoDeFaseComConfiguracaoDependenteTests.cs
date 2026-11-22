namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// Cobertura da remoção de uma fase do cronograma: leva junto as etapas, os documentos e os grupos
/// que eram só dela, e continua recusada quando o que depende dela é de outra fase.
/// </summary>
public sealed class ProcessoSeletivoRemocaoDeFaseComConfiguracaoDependenteTests
{
    private static CriterioDesempate CriterioDesempateComEtapa(Guid etapaId) =>
        CriterioDesempate.Criar(
            1,
            ReferenciaRegra.De(CriterioDesempateCodigo.MaiorNotaEtapa, "v1", new string('a', 64)),
            new ParametrosDesempateMaiorNotaEtapa(etapaId)).Value!;

    private static RegraDeComprovacao GrupoDaFaseComItens(FaseCronograma fase)
    {
        RegraDeComprovacao grupoInterno = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, null, null, [], [RegraDeDocumentoDaFase(fase.Id, 0), RegraDeDocumentoDaFase(fase.Id, 1)]).Value!;
        return RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 1, null, null, [], [grupoInterno, RegraDeDocumentoDaFase(fase.Id, 2)]).Value!;
    }

    /// <summary>
    /// O operador removia a fase e o passo travava: as etapas dela ficavam apontando para uma
    /// fase que não existe mais, e a gravação seguinte era recusada sem dizer o que fazer.
    /// </summary>
    [Fact(DisplayName = "Remover a fase leva junto as etapas que aconteciam nela")]
    public void DefinirCronogramaFases_RemoveFase_LevaAsEtapasDela()
    {
        FaseCronograma avaliacao = Fase(2, "AVALIACAO");
        ProcessoSeletivo processo = ProcessoComFases(Fase(1, "HABILITACAO"), avaliacao);
        AdicionarEtapas(processo, Etapa("Análise documental", "HABILITACAO", 1), Etapa("Prova objetiva", "AVALIACAO", 2));

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "AVALIACAO", avaliacao.FaseCanonicaOrigemId)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().ContainSingle()
            .Which.FaseCodigo.Should().Be("AVALIACAO", "só a etapa da fase removida sai");
    }

    /// <summary>
    /// O defeito que existia: remover a fase que o cadastro marca como agrupadora limpava
    /// TODAS as etapas do processo, inclusive as das outras fases.
    /// </summary>
    [Fact(DisplayName = "Remover uma fase não toca nas etapas das outras")]
    public void DefinirCronogramaFases_RemoveFase_PreservaEtapasDasOutras()
    {
        FaseCronograma habilitacao = Fase(2, "HABILITACAO");
        ProcessoSeletivo processo = ProcessoComFases(Fase(1, "SOLICITACAO_ISENCAO"), habilitacao);
        AdicionarEtapas(
            processo,
            Etapa("Conferência do pedido", "SOLICITACAO_ISENCAO", 1),
            Etapa("Envio dos documentos pessoais", "HABILITACAO", 2),
            Etapa("Envio dos comprovantes de renda", "HABILITACAO", 3));

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "HABILITACAO", habilitacao.FaseCanonicaOrigemId)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().HaveCount(2);
        processo.Etapas.Should().OnlyContain(e => e.FaseCodigo == "HABILITACAO");
    }

    /// <summary>
    /// A exigência declarada na fase é configuração dela. Antes, ela travava a remoção e o
    /// operador tinha de esvaziar a lista de documentos à mão para conseguir tirar a fase.
    /// </summary>
    [Fact(DisplayName = "Remover a fase leva junto os documentos exigidos nela")]
    public void DefinirCronogramaFases_RemoveFase_LevaOsDocumentosDela()
    {
        FaseCronograma isencao = Fase(1, "SOLICITACAO_ISENCAO");
        FaseCronograma habilitacao = Fase(2, "HABILITACAO");
        ProcessoSeletivo processo = ProcessoComFases(isencao, habilitacao);
        ExigirDocumentos(processo, Exigencia(isencao.Id), Exigencia(habilitacao.Id, finalidade: FinalidadeFormulario.Habilitacao));

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "HABILITACAO", habilitacao.FaseCanonicaOrigemId)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle()
            .Which.ExigidoNaFaseId.Should().Be(habilitacao.Id);
        processo.RegrasDeComprovacao.Should().ContainSingle();
    }

    /// <summary>
    /// O que não é da fase continua barrando: apagar um critério de desempate por tabela
    /// deixaria a classificação inexecutável sem que ninguém tivesse pedido.
    /// </summary>
    [Fact(DisplayName = "Remover a fase é recusado quando a etapa dela desempata a classificação")]
    public void DefinirCronogramaFases_EtapaDaFaseDesempata_Recusa()
    {
        FaseCronograma habilitacao = Fase(2, "HABILITACAO");
        ProcessoSeletivo processo = ProcessoComFases(Fase(1, "AVALIACAO"), habilitacao);
        EtapaProcesso prova = Etapa("Prova objetiva", "AVALIACAO", 1);
        AdicionarEtapas(processo, prova);
        processo.DefinirCriteriosDesempate(
            [CriterioDesempateComEtapa(prova.Id)]).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "HABILITACAO", habilitacao.FaseCanonicaOrigemId)], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.EtapaDaFaseReferenciada");
    }

    [Fact(DisplayName = "Remover uma fase retira suas regras e preserva as regras das demais fases")]
    public void DefinirCronogramaFases_RemoveFase_PodaGruposEmCascataEPreservaOutraFase()
    {
        FaseCronograma faseQueFica = Fase(1, "INSCRICAO");
        FaseCronograma faseQueSai = Fase(2, "RECURSO");
        ProcessoSeletivo processo = ProcessoComFases(faseQueFica, faseQueSai);
        RegraDeComprovacao regraDeDocumentoQueFica = RegraDeDocumentoDaFase(faseQueFica.Id, 0);
        processo.DefinirDocumentosExigidos([regraDeDocumentoQueFica, GrupoDaFaseComItens(faseQueSai)])
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "INSCRICAO", faseQueFica.FaseCanonicaOrigemId)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle().Which.ExigidoNaFaseId.Should().Be(faseQueFica.Id);
        processo.RegrasDeComprovacao.Should().ContainSingle().Which.Should().BeSameAs(regraDeDocumentoQueFica);
    }
}
