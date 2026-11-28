namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Exigencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// Na gravação, a recusa da fase de referência do prazo de emissão aponta o requisito do documento no caminho dele no pedido,
/// item por item e na ordem do pedido (ADR-0023, ADR-0125). Os itens de um grupo podem divergir nesta recusa, ao contrário do
/// formulário, que o grupo compartilha.
/// </summary>
public sealed class CaminhosDasRecusasDasDatasDeReferenciaTests
{
    private const string Campo = ".documento.requisitos.prazoDeEmissao.referencia";

    private static RegraDeComprovacao Aceita(Guid fase, int ordem) =>
        RegraDeComprovacao.CriarRegraDeDocumento(Exigencia(fase), ordem).Value!;

    private static RegraDeComprovacao ComFaseDeReferenciaForaDoCronograma(Guid fase, int ordem)
    {
        DataDeReferenciaDoDocumento referencia = new DataDeReferenciaBuilder().NoInicioDaFase(Guid.CreateVersion7()).Construir();
        DocumentoExigido documento = new DocumentoExigidoBuilder().NaFase(fase)
            .ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().Referenciado(referencia).Construir())
            .ComValidade(new ValidadeDoDocumentoBuilder().NaoExige().Construir())
            .Construir();
        return RegraDeComprovacao.CriarRegraDeDocumento(documento, ordem).Value!;
    }

    private static RegraDeComprovacao Grupo(int ordem, params RegraDeComprovacao[] itens) =>
        RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.TodosOsItens, null, null), ordem, [], itens).Value!;

    [Fact(DisplayName = "A recusa do item do meio de um grupo de três aponta a posição dele no grupo")]
    public void DefinirDocumentosExigidos_ItemDoMeioDoGrupo_ApontaAPosicaoNoGrupo()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = ProcessoComOsDoisFormularios();
        RegraDeComprovacao grupo = Grupo(0, Aceita(inscricao.Id, 0), ComFaseDeReferenciaForaDoCronograma(inscricao.Id, 1), Aceita(inscricao.Id, 2));

        Result resultado = processo.DefinirDocumentosExigidos([grupo]);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(erro =>
            erro.Field == "regrasDeComprovacao[0].itens[1]" + Campo && erro.Error.Code == DataDeReferenciaDoDocumentoErrorCodes.FaseNaoPertenceAoProcesso);
    }

    [Fact(DisplayName = "A recusa de um item de grupo anterior e a do documento do meio saem juntas, cada uma no seu caminho")]
    public void DefinirDocumentosExigidos_ItemDeGrupoAnteriorEDocumentoDoMeio_SaemJuntos()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = ProcessoComOsDoisFormularios();

        Result resultado = processo.DefinirDocumentosExigidos(
        [
            Grupo(0, Aceita(inscricao.Id, 0), ComFaseDeReferenciaForaDoCronograma(inscricao.Id, 1), Aceita(inscricao.Id, 2)),
            Aceita(inscricao.Id, 1),
            ComFaseDeReferenciaForaDoCronograma(inscricao.Id, 2),
            Aceita(inscricao.Id, 3),
        ]);

        resultado.Errors.Select(static erro => erro.Field).Should().Equal("regrasDeComprovacao[0].itens[1]" + Campo, "regrasDeComprovacao[2]" + Campo);
    }
}
