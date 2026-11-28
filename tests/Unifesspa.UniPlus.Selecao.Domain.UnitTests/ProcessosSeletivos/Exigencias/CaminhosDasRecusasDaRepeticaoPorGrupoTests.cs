namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Exigencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaRepeticaoPorGrupo;

/// <summary>
/// Na gravação, a recusa da repetição por grupo aponta o grupo da regra no caminho dela no pedido. As repetições de
/// grupo inexistente saem antes das dos campos do grupo, cada conjunto na ordem do pedido (ADR-0023, ADR-0125).
/// </summary>
public sealed class CaminhosDasRecusasDaRepeticaoPorGrupoTests
{
    private const string Campo = ".grupoDoFormularioParaRepeticao";
    private const string GrupoQueOProcessoNaoTem = "PESSOAS_JURIDICAS";
    private const string GrupoInexistente = "RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado";

    private static RegraDeComprovacao RegraDeDocumento(Guid fase, int ordem, string? repetePeloGrupo = null) =>
        RegraDeComprovacao.CriarRegraDeDocumento(
            ApoioDaRepeticaoPorGrupo.Documento(fase, PapelDoFormularioDeTeste.Inscricao), ordem, grupoDoFormularioParaRepeticao: repetePeloGrupo).Value!;

    private static RegraDeComprovacao Grupo(int ordem, string? repetePeloGrupo, params RegraDeComprovacao[] itens) =>
        RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.TodosOsItens, null, null), ordem, [], itens, repetePeloGrupo).Value!;

    private static List<(string? Campo, string Codigo)> Recusas(Result resultado) =>
        [.. resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code))];

    [Fact(DisplayName = "As repetições de grupo inexistente saem antes das dos campos de grupo conhecido, cada uma na posição da regra")]
    public void DefinirDocumentosExigidos_GrupoInexistenteECampoDeFasePosterior_GrupoInexistentePrimeiro()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = Processo();

        Result resultado = processo.DefinirDocumentosExigidos(
        [
            RegraDeDocumento(inscricao.Id, 0, Composicao),
            RegraDeDocumento(inscricao.Id, 1),
            RegraDeDocumento(inscricao.Id, 2, GrupoQueOProcessoNaoTem),
        ]);

        Recusas(resultado).Should().Equal(
            ("regrasDeComprovacao[2]" + Campo, GrupoInexistente),
            ("regrasDeComprovacao[0]" + Campo, DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior));
    }

    [Fact(DisplayName = "A regra de grupo inexistente não esconde a recusa dos campos de outra regra")]
    public void DefinirDocumentosExigidos_GrupoInexistenteNumaRegra_NaoCortaAsDemais()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = Processo();

        Result resultado = processo.DefinirDocumentosExigidos([RegraDeDocumento(inscricao.Id, 0, GrupoQueOProcessoNaoTem), RegraDeDocumento(inscricao.Id, 1, Composicao)]);

        Recusas(resultado).Select(static recusa => recusa.Campo).Should().Equal("regrasDeComprovacao[0]" + Campo, "regrasDeComprovacao[1]" + Campo);
    }

    [Fact(DisplayName = "A repetição de um item de grupo aponta a posição do item dentro do grupo")]
    public void DefinirDocumentosExigidos_ItemDeGrupoComGrupoInexistente_ApontaOItem()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = Processo();

        Result resultado = processo.DefinirDocumentosExigidos(
            [Grupo(0, null, RegraDeDocumento(inscricao.Id, 0), RegraDeDocumento(inscricao.Id, 1, GrupoQueOProcessoNaoTem))]);

        Recusas(resultado).Should().Equal(("regrasDeComprovacao[0].itens[1]" + Campo, GrupoInexistente));
    }

    [Fact(DisplayName = "A repetição do próprio grupo de regras aponta a posição do grupo no pedido")]
    public void DefinirDocumentosExigidos_GrupoDeRegrasQueRepeteEmFasePosterior_ApontaOGrupo()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = Processo();

        Result resultado = processo.DefinirDocumentosExigidos([RegraDeDocumento(inscricao.Id, 0), Grupo(1, Composicao, RegraDeDocumento(inscricao.Id, 0))]);

        Recusas(resultado).Should().Equal(("regrasDeComprovacao[1]" + Campo, DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior));
    }

    [Fact(DisplayName = "Na publicação, a repetição pelo grupo retirado do formulário depois mantém o código de recusa")]
    public void PendenciaDaRepeticaoPorGrupo_GrupoRetiradoDepois_MantemOCodigo()
    {
        (ProcessoSeletivo processo, _, FaseCronograma habilitacao) = Processo();
        RegraDeComprovacao regra = RegraDeComprovacao.CriarRegraDeDocumento(
            ApoioDaRepeticaoPorGrupo.Documento(habilitacao.Id, PapelDoFormularioDeTeste.Habilitacao), 0, grupoDoFormularioParaRepeticao: Composicao).Value!;
        processo.DefinirDocumentosExigidos([regra]).IsSuccess.Should().BeTrue();
        PendenciasDasExigencias.PendenciaDaRepeticaoPorGrupo(processo.Declaracao()).Should().BeNull();

        processo.DefinirFatosColetados(PapelDoFormularioDeTeste.Habilitacao, [], []).IsSuccess.Should().BeTrue();

        PendenciasDasExigencias.PendenciaDaRepeticaoPorGrupo(processo.Declaracao())!.Code.Should().Be(GrupoInexistente);
    }
}
