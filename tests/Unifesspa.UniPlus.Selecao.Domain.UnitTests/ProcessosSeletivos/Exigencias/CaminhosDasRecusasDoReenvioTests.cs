namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Exigencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// A consequência de reenvio exige uma fase que permita complementação. Na gravação, cada regra que a pede em fase que não
/// permite é recusada no caminho dela no pedido, e as recusas saem juntas (ADR-0023, ADR-0125).
/// </summary>
public sealed class CaminhosDasRecusasDoReenvioTests
{
    private const string Codigo = "DocumentoExigido.PendenciaReenvioExigeComplementacao";

    private static RegraDeComprovacao Documento(Guid fase, int ordem, bool abrePrazo) =>
        RegraDeComprovacao.CriarRegraDeDocumento(Exigencia(fase, abrePrazoParaReenvio: abrePrazo), ordem).Value!;

    private static RegraDeComprovacao GrupoQueAbrePrazo(int ordem, params RegraDeComprovacao[] itens) =>
        RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null, true), ordem, [], itens).Value!;

    private static (ProcessoSeletivo Processo, Guid FaseSemComplementacao) ProcessoSemComplementacao()
    {
        FaseCronograma fase = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: false);
        return (ProcessoComFases(fase), fase.Id);
    }

    [Fact(DisplayName = "A regra de documento do meio que abre prazo de reenvio sem complementação aponta o indeferimento do documento")]
    public void DefinirDocumentosExigidos_DocumentoDoMeio_ApontaOIndeferimentoDoDocumento()
    {
        (ProcessoSeletivo processo, Guid fase) = ProcessoSemComplementacao();

        Result resultado = processo.DefinirDocumentosExigidosNosFormulariosMinimos(
            [Documento(fase, 0, abrePrazo: false), Documento(fase, 1, abrePrazo: true), Documento(fase, 2, abrePrazo: false)]);

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("regrasDeComprovacao[1].documento.indeferimento.abrePrazoParaReenvio", Codigo));
    }

    [Fact(DisplayName = "O grupo que abre prazo de reenvio sem complementação aponta o indeferimento do grupo")]
    public void DefinirDocumentosExigidos_Grupo_ApontaOIndeferimentoDoGrupo()
    {
        (ProcessoSeletivo processo, Guid fase) = ProcessoSemComplementacao();

        Result resultado = processo.DefinirDocumentosExigidosNosFormulariosMinimos(
            [Documento(fase, 0, abrePrazo: false), GrupoQueAbrePrazo(1, Documento(fase, 0, abrePrazo: false))]);

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("regrasDeComprovacao[1].indeferimento.abrePrazoParaReenvio", Codigo));
    }

    [Fact(DisplayName = "As recusas de reenvio de grupo, de documento isolado e de documento dentro de grupo saem juntas, cada uma no seu lugar")]
    public void DefinirDocumentosExigidos_GrupoEDocumentos_AcumulaNaOrdemDoPedido()
    {
        (ProcessoSeletivo processo, Guid fase) = ProcessoSemComplementacao();

        Result resultado = processo.DefinirDocumentosExigidosNosFormulariosMinimos(
        [
            GrupoQueAbrePrazo(0, Documento(fase, 0, abrePrazo: false), Documento(fase, 1, abrePrazo: true)),
            Documento(fase, 1, abrePrazo: true),
        ]);

        resultado.Errors.Select(static erro => erro.Field).Should().Equal(
            "regrasDeComprovacao[0].indeferimento.abrePrazoParaReenvio",
            "regrasDeComprovacao[0].itens[1].documento.indeferimento.abrePrazoParaReenvio",
            "regrasDeComprovacao[1].documento.indeferimento.abrePrazoParaReenvio");
    }
}
