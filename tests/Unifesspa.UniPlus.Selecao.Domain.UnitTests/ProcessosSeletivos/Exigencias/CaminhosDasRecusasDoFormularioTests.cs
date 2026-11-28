namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Exigencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// Na gravação, a recusa do formulário da exigência aponta o caminho do documento no pedido, com a posição de cada item
/// em cada lista que o caminho atravessa (ADR-0023), e as recusas de itens diferentes saem juntas (ADR-0125).
/// </summary>
public sealed class CaminhosDasRecusasDoFormularioTests
{
    private const string Campo = ".documento.momento.formularioCodigo";

    private static RegraDeComprovacao Aceita(Guid fase, int ordem) =>
        RegraDeComprovacao.CriarRegraDeDocumento(Exigencia(fase, papel: PapelDoFormularioDeTeste.Inscricao), ordem).Value!;

    private static RegraDeComprovacao Recusada(Guid fase, int ordem) =>
        RegraDeComprovacao.CriarRegraDeDocumento(Exigencia(fase, papel: PapelDoFormularioDeTeste.IsencaoTaxa), ordem).Value!;

    private static RegraDeComprovacao Grupo(int ordem, params RegraDeComprovacao[] itens) =>
        RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.TodosOsItens, null, null), ordem, [], itens).Value!;

    private static List<string?> Campos(Result resultado) => [.. resultado.Errors.Select(static erro => erro.Field)];

    [Fact(DisplayName = "A recusa do documento do meio da lista aponta a posição dele no pedido")]
    public void DefinirDocumentosExigidos_DocumentoDoMeio_ApontaAPosicaoNoPedido()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = ProcessoComOsDoisFormularios();

        Result resultado = processo.DefinirDocumentosExigidos([Aceita(inscricao.Id, 0), Recusada(inscricao.Id, 1), Aceita(inscricao.Id, 2)]);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(erro =>
            erro.Field == "regrasDeComprovacao[1]" + Campo && erro.Error.Code == FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase);
    }

    [Fact(DisplayName = "Os itens recusados de um grupo saem cada um na posição dele dentro do grupo")]
    public void DefinirDocumentosExigidos_ItensDoGrupo_ApontamAPosicaoNoGrupo()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = ProcessoComOsDoisFormularios();
        RegraDeComprovacao grupo = Grupo(0, Recusada(inscricao.Id, 0), Recusada(inscricao.Id, 1), Recusada(inscricao.Id, 2));

        Result resultado = processo.DefinirDocumentosExigidos([grupo]);

        Campos(resultado).Should().Equal("regrasDeComprovacao[0].itens[0]" + Campo, "regrasDeComprovacao[0].itens[1]" + Campo, "regrasDeComprovacao[0].itens[2]" + Campo);
    }

    [Fact(DisplayName = "Itens de um grupo anterior e documento do meio saem juntos, cada um no caminho do seu lugar")]
    public void DefinirDocumentosExigidos_GrupoAnteriorEDocumentoDoMeio_SaemJuntos()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = ProcessoComOsDoisFormularios();

        Result resultado = processo.DefinirDocumentosExigidos(
        [
            Grupo(0, Recusada(inscricao.Id, 0), Recusada(inscricao.Id, 1)),
            Aceita(inscricao.Id, 1),
            Recusada(inscricao.Id, 2),
            Aceita(inscricao.Id, 3),
        ]);

        Campos(resultado).Should().Equal(
            "regrasDeComprovacao[0].itens[0]" + Campo, "regrasDeComprovacao[0].itens[1]" + Campo, "regrasDeComprovacao[2]" + Campo);
    }

    [Fact(DisplayName = "Mais de uma recusa da mesma família saem juntas, na ordem do pedido")]
    public void DefinirDocumentosExigidos_VariasRecusasDoFormulario_AcumulaNaOrdemDoPedido()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = ProcessoComOsDoisFormularios();

        Result resultado = processo.DefinirDocumentosExigidos(
            [Recusada(inscricao.Id, 0), Aceita(inscricao.Id, 1), Grupo(2, Recusada(inscricao.Id, 0), Recusada(inscricao.Id, 1))]);

        Campos(resultado).Should().Equal("regrasDeComprovacao[0]" + Campo, "regrasDeComprovacao[2].itens[0]" + Campo, "regrasDeComprovacao[2].itens[1]" + Campo);
    }

    [Fact(DisplayName = "Grupo dentro de grupo acrescenta um itens[] por nível ao caminho")]
    public void DefinirDocumentosExigidos_GrupoDentroDeGrupo_UmItensPorNivel()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = ProcessoComOsDoisFormularios();
        RegraDeComprovacao interno = Grupo(0, Recusada(inscricao.Id, 0), Recusada(inscricao.Id, 1));

        Result resultado = processo.DefinirDocumentosExigidos([Grupo(0, interno, Recusada(inscricao.Id, 1))]);

        Campos(resultado).Should().Equal(
            "regrasDeComprovacao[0].itens[0].itens[0]" + Campo, "regrasDeComprovacao[0].itens[0].itens[1]" + Campo, "regrasDeComprovacao[0].itens[1]" + Campo);
    }

    [Theory(DisplayName = "O número de documentos de um grupo anterior não muda o caminho da recusa de um documento posterior")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void DefinirDocumentosExigidos_GrupoAnteriorMaior_NaoMudaOCaminhoDoDocumentoSeguinte(int documentosDoGrupo)
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = ProcessoComOsDoisFormularios();
        RegraDeComprovacao[] itens = [.. Enumerable.Range(0, documentosDoGrupo).Select(posicao => Aceita(inscricao.Id, posicao))];

        Result resultado = processo.DefinirDocumentosExigidos([Grupo(0, itens), Recusada(inscricao.Id, 1)]);

        Campos(resultado).Should().Equal("regrasDeComprovacao[1]" + Campo);
    }

    [Fact(DisplayName = "Na publicação, a recusa do formulário continua com o código de hoje e o caminho da lista única")]
    public void PendenciaDoFormularioDasExigencias_FormularioMudouDeFase_MantemOCodigo()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, FaseCronograma isencao) = ProcessoComOsDoisFormularios();
        processo.DefinirDocumentosExigidos([Aceita(inscricao.Id, 0), RegraDeComprovacao.CriarRegraDeDocumento(Exigencia(isencao.Id, papel: PapelDoFormularioDeTeste.IsencaoTaxa), 1).Value!])
            .IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(
                FormularioDeTeste.DeclaradoNoPapel(PapelDoFormularioDeTeste.IsencaoTaxa, inscricao.Id), null, FormularioDeTeste.Paginas(PapelDoFormularioDeTeste.IsencaoTaxa))
            .IsSuccess.Should().BeTrue();

        IReadOnlyList<FieldError> recusas = ConferenciaDasExigenciasDoFormulario.Conferir(
            processo.Declaracao().Coleta.FormulariosDeclarados,
            [.. processo.DocumentosExigidos.Select(ExigenciaDeDocumentoDoFormulario.De)],
            [.. processo.DocumentosExigidos.Select(static (_, posicao) => $"exigencias[{posicao}]")]);

        recusas.Select(static recusa => recusa.Field).Should().OnlyContain(campo => campo!.StartsWith("exigencias[", StringComparison.Ordinal) && campo.EndsWith(".momento.formularioCodigo", StringComparison.Ordinal));
        recusas.Should().ContainSingle().Which.Error.Code.Should().Be(FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase);
    }
}
