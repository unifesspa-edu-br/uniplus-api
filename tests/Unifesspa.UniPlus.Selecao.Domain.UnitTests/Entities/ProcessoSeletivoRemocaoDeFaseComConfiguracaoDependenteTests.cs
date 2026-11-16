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
            ReferenciaRegra.Criar(CriterioDesempateCodigo.MaiorNotaEtapa, "v1", new string('a', 64)).Value!,
            new ArgsDesempateMaiorNotaEtapa(etapaId)).Value!;

    private static NoExigencia GrupoAninhadoDaFase(FaseCronograma fase)
    {
        NoExigencia grupoInterno = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, null, null, [], [FolhaDaFase(fase.Id, 0), FolhaDaFase(fase.Id, 1)]).Value!;
        return NoExigencia.CriarGrupo(TipoNo.GrupoE, 1, null, null, [], [grupoInterno, FolhaDaFase(fase.Id, 2)]).Value!;
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
            [Fase(1, "AVALIACAO", avaliacao.FaseCanonicaOrigemId)], [], PrecondicaoIfMatch.Ausente);

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
            [Fase(1, "HABILITACAO", habilitacao.FaseCanonicaOrigemId)], [], PrecondicaoIfMatch.Ausente);

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
        ExigirFolhas(processo, Exigencia(isencao.Id), Exigencia(habilitacao.Id, finalidade: FinalidadeFormulario.Habilitacao));

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "HABILITACAO", habilitacao.FaseCanonicaOrigemId)], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle()
            .Which.ExigidoNaFaseId.Should().Be(habilitacao.Id);
        processo.NosExigencia.Should().ContainSingle();
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
            [CriterioDesempateComEtapa(prova.Id)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "HABILITACAO", habilitacao.FaseCanonicaOrigemId)], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.EtapaDaFaseReferenciada");
    }

    [Fact(DisplayName = "Remover a fase poda em cascata os grupos da fase e preserva a árvore da fase que fica")]
    public void DefinirCronogramaFases_RemoveFase_PodaGruposEmCascataEPreservaOutraFase()
    {
        FaseCronograma faseQueFica = Fase(1, "INSCRICAO");
        FaseCronograma faseQueSai = Fase(2, "RECURSO");
        ProcessoSeletivo processo = ProcessoComFases(faseQueFica, faseQueSai);
        NoExigencia folhaQueFica = FolhaDaFase(faseQueFica.Id, 0);
        processo.DefinirDocumentosExigidos([folhaQueFica, GrupoAninhadoDaFase(faseQueSai)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "INSCRICAO", faseQueFica.FaseCanonicaOrigemId)], [], PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle().Which.ExigidoNaFaseId.Should().Be(faseQueFica.Id);
        processo.NosExigencia.Should().ContainSingle().Which.Should().BeSameAs(folhaQueFica);
    }
}
