namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

internal static class ModeloFormularioMapping
{
    public static ModeloFormularioDto ToDto(this ModeloFormulario modelo)
    {
        ConteudoDoModelo conteudo = modelo.Conteudo;
        return new ModeloFormularioDto(
            modelo.Id,
            modelo.Codigo,
            modelo.Nome,
            modelo.Descricao,
            EstruturaFormulario.ParaToken(modelo.Finalidade),
            modelo.TipoProcessoCodigo,
            modelo.Ativo,
            new ConteudoDoModeloInput(
                conteudo.Titulo,
                [.. conteudo.Etapas.Select(static e => new EtapaFormularioInput(
                    e.Codigo, e.Ordem, EstruturaFormulario.ParaToken(e.Tipo), EstruturaFormulario.ParaToken(e.Bloco),
                    e.Titulo, e.Descricao, e.Aviso, EntradaDeRegras.ParaEntrada(e.Exibicao)))],
                [.. conteudo.Itens.Select(static i => new FatoColetadoInput(
                    i.FatoCodigo, i.Ordem, i.Rotulo, i.TipoRenderizacao.ToCodigo(), EntradaDeRegras.ParaEntrada(i.Obrigatoriedade),
                    EntradaDeRegras.ParaEntrada(i.Exibicao), i.EtapaCodigo, EntradaDeRegras.ParaEntrada(i.Obrigatoriedade.Predicado),
                    i.Ajuda, i.PedirConfirmacao, [.. i.Restricoes.Select(EntradaDeRegras.ParaEntrada)]))],
                [.. conteudo.Termos.Select(static t => new TermoExigidoInput(
                    t.Codigo, t.Ordem, t.TermoId, t.VersaoId, EntradaDeRegras.ParaEntrada(t.Exibicao),
                    EntradaDeRegras.ParaEntrada(t.Obrigatoriedade), EntradaDeRegras.ParaEntrada(t.Obrigatoriedade.Predicado)))],
                conteudo.Pressupostos));
    }
}
