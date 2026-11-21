namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>O modelo na forma de leitura, a mesma da manutenção e do leitor cross-módulo.</summary>
public static class ModeloFormularioMapping
{
    public static ModeloFormularioView ToView(this ModeloFormulario modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        return new ModeloFormularioView(
            modelo.Id,
            modelo.Codigo,
            modelo.Nome,
            modelo.Descricao,
            EstruturaFormulario.ParaToken(modelo.Finalidade),
            modelo.TipoProcessoCodigo,
            modelo.Ativo,
            modelo.Conteudo.ParaEntrada());
    }

    /// <summary>O conteúdo do modelo na forma de entrada da escrita.</summary>
    public static ConteudoDoModeloInput ParaEntrada(this ConteudoDoModelo conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        return new ConteudoDoModeloInput(
            conteudo.Titulo,
            [.. conteudo.Etapas.Select(EtapaParaEntrada)],
            [.. conteudo.Itens.Select(ItemParaEntrada)],
            [.. conteudo.Termos.Select(TermoParaEntrada)],
            conteudo.Pressupostos,
            [.. conteudo.Grupos.Select(GrupoParaEntrada)],
            [.. conteudo.Documentos.Select(DocumentoParaEntrada)]);
    }

    private static EtapaFormularioInput EtapaParaEntrada(EtapaDoModelo etapa) => new(
        etapa.Codigo, etapa.Ordem, EstruturaFormulario.ParaToken(etapa.Tipo), EstruturaFormulario.ParaToken(etapa.Bloco),
        etapa.Titulo, etapa.Descricao, etapa.Aviso, EntradaDeRegras.ParaEntrada(etapa.Exibicao));

    private static FatoColetadoInput ItemParaEntrada(ItemDoModelo campo) => new(
        campo.FatoCodigo, campo.Ordem, campo.Apresentacao.Rotulo, campo.Apresentacao.TipoRenderizacao.Codigo(),
        EntradaDeRegras.ParaEntrada(campo.Regras.Obrigatoriedade), EntradaDeRegras.ParaEntrada(campo.Regras.Exibicao), campo.EtapaCodigo,
        EntradaDeRegras.ParaEntrada(campo.Regras.Obrigatoriedade.Predicado), campo.Apresentacao.Ajuda, campo.Apresentacao.PedirConfirmacao,
        [.. campo.Regras.Restricoes.Select(EntradaDeRegras.ParaEntrada)], EntradaDeRegras.ParaEntrada(campo.Regras.Impedimento));

    private static TermoExigidoInput TermoParaEntrada(TermoDoModelo termo) => new(
        termo.Codigo, termo.Ordem, termo.TermoId, termo.VersaoId, EntradaDeRegras.ParaEntrada(termo.Exibicao),
        EntradaDeRegras.ParaEntrada(termo.Obrigatoriedade), EntradaDeRegras.ParaEntrada(termo.Obrigatoriedade.Predicado));

    private static GrupoColetadoInput GrupoParaEntrada(GrupoDoModelo grupo) => new(
        grupo.Codigo, grupo.Ordem, grupo.Rotulo, grupo.EtapaCodigo, grupo.Minimo, grupo.Maximo, EntradaDeRegras.ParaEntrada(grupo.Exibicao),
        EntradaDeRegras.ParaEntrada(grupo.Obrigatoriedade), EntradaDeRegras.ParaEntrada(grupo.Obrigatoriedade.Predicado),
        [.. grupo.Subitens.Select(ItemParaEntrada)], grupo.IncluiCandidato);

    private static DocumentoExigidoDoModeloInput DocumentoParaEntrada(DocumentoExigidoDoModelo documento) => new(
        documento.FaseCodigo, documento.TipoDocumentoCodigo, documento.Obrigatorio, EntradaDeRegras.ParaEntrada(documento.Quando),
        documento.ConsequenciaIndeferimento, [.. documento.BasesLegais.Select(BaseLegalParaEntrada)], PorOpcaoParaEntrada(documento.PorOpcao));

    private static BaseLegalDoModeloInput BaseLegalParaEntrada(BaseLegalDoDocumentoDoModelo baseLegal) =>
        new(baseLegal.Referencia, baseLegal.Abrangencia, baseLegal.Status, baseLegal.Observacao);

    private static ExigenciaPorOpcaoInput? PorOpcaoParaEntrada(ExigenciaPorOpcao? porOpcao) =>
        porOpcao is null ? null : new ExigenciaPorOpcaoInput(porOpcao.FatoCodigo, [.. porOpcao.Exceto]);
}
