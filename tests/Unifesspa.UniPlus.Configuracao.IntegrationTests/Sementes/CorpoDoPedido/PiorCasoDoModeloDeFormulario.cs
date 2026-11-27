namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes.CorpoDoPedido;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes.CorpoDoPedido.TextosNoLimite;

/// <summary>
/// O maior cadastro de modelo de formulário que a validação aceita: todo texto no tamanho máximo, o formulário no teto de
/// itens e cada quantidade sem teto — páginas, termos, grupos, condições, opções — igual à maior que as sementes têm.
/// </summary>
internal static class PiorCasoDoModeloDeFormulario
{
    public static CriarModeloFormularioCommand Cadastro(DimensoesDasSementesDoModelo dimensoes, int documentos) => new(
        Codigo(ModeloFormulario.CodigoMaxLength, 1),
        new DeclaracaoDoModeloInput(true, true),
        new DescritivoDoModeloInput(Texto(ModeloFormulario.NomeMaxLength), Texto(ModeloFormulario.DescricaoMaxLength), Codigo(ModeloFormulario.TipoProcessoCodigoMaxLength, 1)),
        Conteudo(dimensoes, documentos));

    private static ConteudoDoModeloInput Conteudo(DimensoesDasSementesDoModelo dimensoes, int documentos) => new(
        Texto(FormaDoCabecalho.TituloMaxLength),
        [.. Enumerable.Range(0, dimensoes.Paginas).Select(i => Pagina(dimensoes, i))],
        [.. Enumerable.Range(0, ItensForaDeGrupo(dimensoes)).Select(i => Item(dimensoes, i, Codigo(FormaDaPagina.CodigoMaxLength, 0)))],
        [.. Enumerable.Range(0, dimensoes.Termos).Select(i => Termo(dimensoes, i))],
        [.. Enumerable.Range(0, dimensoes.Pressupostos).Select(i => Codigo(FormaDoItem.FatoCodigoMaxLength, i))],
        [.. Enumerable.Range(0, dimensoes.Grupos).Select(i => Grupo(dimensoes, i))],
        DocumentosNoLimite.Conjunto(dimensoes, documentos));

    private static int ItensForaDeGrupo(DimensoesDasSementesDoModelo dimensoes) =>
        FormaDoItem.MaximoDeItens - dimensoes.Grupos * (1 + dimensoes.Subitens);

    private static PaginaDoFormularioInput Pagina(DimensoesDasSementesDoModelo dimensoes, int indice) => new(
        Codigo(FormaDaPagina.CodigoMaxLength, indice), indice, TipoPaginaFormulario.Secao.Codigo(), null,
        new ApresentacaoDaPaginaInput(Texto(FormaDaPagina.TituloMaxLength), Texto(FormaDaPagina.TextoMaxLength), Texto(FormaDaPagina.TextoMaxLength)),
        Predicado(dimensoes, FormaDoItem.FatoCodigoMaxLength));

    private static FatoColetadoInput Item(DimensoesDasSementesDoModelo dimensoes, int indice, string? pagina) => new(
        Codigo(FormaDoItem.FatoCodigoMaxLength, indice), indice,
        new ApresentacaoDoCampoInput(Texto(FormaDoItem.RotuloMaxLength), TipoDeCampo.EscolhaUnica.Codigo(), Texto(FormaDoItem.AjudaMaxLength), true),
        RegrasDoItem(dimensoes), pagina);

    private static RegrasDoCampoInput RegrasDoItem(DimensoesDasSementesDoModelo dimensoes) => new(
        Predicado(dimensoes, FormaDoItem.FatoCodigoMaxLength),
        new ObrigatoriedadeInput(TipoObrigatoriedade.Quando.Codigo(), Predicado(dimensoes, FormaDoItem.FatoCodigoMaxLength)),
        [.. Enumerable.Range(0, dimensoes.Restricoes).Select(_ => Restricao(dimensoes))],
        new ImpedimentoInput(Predicado(dimensoes, FormaDoItem.FatoCodigoMaxLength), Texto(FormaDoItem.MensagemDoImpedimentoMaxLength)));

    private static RestricaoValorInput Restricao(DimensoesDasSementesDoModelo dimensoes) => new(
        TipoRestricaoValor.OpcoesPermitidas.Codigo(),
        Entradas: [.. Enumerable.Range(0, dimensoes.Opcoes).Select(_ => Opcoes(dimensoes))]);

    private static OpcoesCondicionadasInput Opcoes(DimensoesDasSementesDoModelo dimensoes) => new(
        Predicado(dimensoes, FormaDoItem.FatoCodigoMaxLength),
        [.. Enumerable.Range(0, dimensoes.ValoresPorOpcao).Select(i => Codigo(ValorCategorico.CodigoDoValorMaxLength, i))]);

    private static TermoExigidoInput Termo(DimensoesDasSementesDoModelo dimensoes, int indice) => new(
        Codigo(FormaDoTermo.CodigoMaxLength, indice), indice, new VersaoDoTermoInput(Guid.NewGuid(), Guid.NewGuid()), ExibicaoEObrigatoriedade(dimensoes));

    private static GrupoColetadoInput Grupo(DimensoesDasSementesDoModelo dimensoes, int indice) => new(
        Codigo(FormaDoGrupo.CodigoMaxLength, indice), indice, Texto(FormaDoItem.RotuloMaxLength), new OcorrenciasDoGrupoInput(1, true, int.MaxValue),
        ExibicaoEObrigatoriedade(dimensoes),
        [.. Enumerable.Range(0, dimensoes.Subitens).Select(i => Item(dimensoes, i, null))],
        Codigo(FormaDaPagina.CodigoMaxLength, 0));

    private static ExibicaoEObrigatoriedadeInput ExibicaoEObrigatoriedade(DimensoesDasSementesDoModelo dimensoes) => new(
        Predicado(dimensoes, FormaDoItem.FatoCodigoMaxLength),
        new ObrigatoriedadeInput(TipoObrigatoriedade.Quando.Codigo(), Predicado(dimensoes, FormaDoItem.FatoCodigoMaxLength)));
}
