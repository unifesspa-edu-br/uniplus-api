namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Liga ao formulário os itens, os grupos e os termos que o teste montou pelos construtores, que só conhecem o papel do
/// formulário: a coleta publicada e a viva guardam o formulário de cada item, e as projeções leem por ele.
/// </summary>
internal static class ColetaNoFormularioDeTeste
{
    public static FatoColetado NoFormulario(this FatoColetado item, FormularioProcesso formulario) => FatoColetado.Criar(
        item.FatoCodigo,
        item.Ordem,
        item.PaginaCodigo,
        item.Apresentacao,
        item.Regras,
        new CopiaDoItemNoCatalogo(
            item.OrigemValores, item.Formato, item.ClassificacaoProtecao, item.HipoteseLegal, item.FinalidadeTratamento, item.Descricao, item.ComponenteExibicao),
        formulario).Value!;

    public static GrupoColetado NoFormulario(this GrupoColetado grupo, FormularioProcesso formulario) =>
        GrupoColetadoDeTeste.Como(grupo).ComCampos([.. grupo.Subitens.Select(subitem => subitem.NoFormulario(formulario))]).Construir();

    public static TermoExigidoFormulario NoFormulario(this TermoExigidoFormulario termo, FormularioProcesso formulario) =>
        TermoExigidoFormulario.Criar(
            termo.Codigo,
            termo.Ordem,
            new VersaoTermoEscolhida(termo.TermoId, termo.VersaoId, termo.TermoCodigo, termo.VersaoNumero, termo.Nome, termo.Texto, termo.BaseLegal, termo.FormaAceite, termo.HashVersao),
            termo.Exibicao,
            termo.Obrigatoriedade,
            formulario).Value!;
}
