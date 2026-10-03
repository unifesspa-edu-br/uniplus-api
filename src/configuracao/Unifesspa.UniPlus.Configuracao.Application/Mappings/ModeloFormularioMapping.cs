namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>O modelo na forma de leitura, a mesma da manutenção e do leitor cross-módulo.</summary>
public static class ModeloFormularioMapping
{
    public static ModeloFormularioView ToView(this ModeloFormulario modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        ConteudoDoModelo conteudo = modelo.Conteudo;
        return new ModeloFormularioView(
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
                [.. conteudo.Itens.Select(ParaEntrada)],
                [.. conteudo.Termos.Select(static t => new TermoExigidoInput(
                    t.Codigo, t.Ordem, t.TermoId, t.VersaoId, EntradaDeRegras.ParaEntrada(t.Exibicao),
                    EntradaDeRegras.ParaEntrada(t.Obrigatoriedade), EntradaDeRegras.ParaEntrada(t.Obrigatoriedade.Predicado)))],
                conteudo.Pressupostos,
                [.. conteudo.Grupos.Select(static g => new GrupoColetadoInput(
                    g.Codigo, g.Ordem, g.Rotulo, g.EtapaCodigo, g.Minimo, g.Maximo, EntradaDeRegras.ParaEntrada(g.Exibicao),
                    EntradaDeRegras.ParaEntrada(g.Obrigatoriedade), EntradaDeRegras.ParaEntrada(g.Obrigatoriedade.Predicado),
                    [.. g.Subitens.Select(ParaEntrada)], g.IncluiCandidato))]));
    }

    private static FatoColetadoInput ParaEntrada(ItemDoModelo campo) => new(
        campo.FatoCodigo, campo.Ordem, campo.Rotulo, campo.TipoRenderizacao.ToCodigo(), EntradaDeRegras.ParaEntrada(campo.Obrigatoriedade),
        EntradaDeRegras.ParaEntrada(campo.Exibicao), campo.EtapaCodigo, EntradaDeRegras.ParaEntrada(campo.Obrigatoriedade.Predicado),
        campo.Ajuda, campo.PedirConfirmacao, [.. campo.Restricoes.Select(EntradaDeRegras.ParaEntrada)]);
}
