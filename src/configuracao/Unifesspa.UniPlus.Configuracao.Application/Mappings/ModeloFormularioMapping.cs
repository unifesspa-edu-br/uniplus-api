namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// O modelo nas duas formas de leitura: a da manutenção (<see cref="ModeloFormularioDto"/>) e a do leitor entre
/// módulos (<see cref="ModeloFormularioView"/>), que traz o conteúdo na forma de entrada da escrita.
/// </summary>
public static class ModeloFormularioMapping
{
    /// <summary>O modelo na forma de leitura da manutenção, que volta à escrita sem transformação.</summary>
    public static ModeloFormularioDto ToDto(this ModeloFormulario modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        return new ModeloFormularioDto(
            modelo.Id,
            modelo.Codigo,
            CodigoComRotulo<FinalidadeFormulario>.De(modelo.Finalidade),
            modelo.Ativo,
            new DescritivoDoModeloDto(modelo.Nome, modelo.Descricao, modelo.TipoProcessoCodigo),
            modelo.Conteudo.ToDto());
    }

    /// <summary>O conteúdo do modelo na forma de leitura da manutenção.</summary>
    public static ConteudoDoModeloDto ToDto(this ConteudoDoModelo conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        return new ConteudoDoModeloDto(
            conteudo.Titulo,
            [.. conteudo.Paginas.Select(PaginaParaLeitura)],
            [.. conteudo.Itens.Select(ItemParaLeitura)],
            [.. conteudo.Termos.Select(TermoParaLeitura)],
            conteudo.Pressupostos,
            [.. conteudo.Grupos.Select(GrupoParaLeitura)],
            conteudo.Documentos.ParaLeitura());
    }

    private static PaginaDoFormularioDto PaginaParaLeitura(PaginaDoModelo pagina) => new(
        pagina.Codigo, pagina.Ordem, pagina.Tipo.Codigo(), pagina.Bloco?.Codigo(),
        new ApresentacaoDaPaginaDto(pagina.Titulo, pagina.Descricao, pagina.Aviso), pagina.Exibicao is { } exibicao ? LeituraDeRegras.ToDto(exibicao) : null);

    private static ItemDoModeloDto ItemParaLeitura(ItemDoModelo item) => new(
        item.FatoCodigo, item.Ordem, item.PaginaCodigo, item.Apresentacao.ToDto(), item.Regras.ToDto());

    private static TermoDoModeloDto TermoParaLeitura(TermoDoModelo termo) => new(
        termo.Codigo, termo.Ordem, new VersaoDoTermoDoModeloDto(termo.TermoId, termo.VersaoId), termo.Regras.ToDto());

    private static GrupoDoModeloDto GrupoParaLeitura(GrupoDoModelo grupo) => new(
        grupo.Codigo, grupo.Ordem, grupo.PaginaCodigo, grupo.Rotulo, grupo.Ocorrencias.ToDto(), grupo.Regras.ToDto(),
        [.. grupo.Subitens.Select(ItemParaLeitura)]);

    /// <summary>O modelo na forma do leitor entre módulos, com o conteúdo na forma de entrada da escrita.</summary>
    public static ModeloFormularioView ToView(this ModeloFormulario modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        return new ModeloFormularioView(
            modelo.Id,
            modelo.Codigo,
            modelo.Nome,
            modelo.Descricao,
            modelo.Finalidade.Codigo(),
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
            [.. conteudo.Paginas.Select(PaginaParaEntrada)],
            [.. conteudo.Itens.Select(ItemParaEntrada)],
            [.. conteudo.Termos.Select(TermoParaEntrada)],
            conteudo.Pressupostos,
            [.. conteudo.Grupos.Select(GrupoParaEntrada)],
            conteudo.Documentos.ParaEntrada());
    }

    private static PaginaDoFormularioInput PaginaParaEntrada(PaginaDoModelo pagina) => new(
        pagina.Codigo, pagina.Ordem, pagina.Tipo.Codigo(), pagina.Bloco?.Codigo(),
        new ApresentacaoDaPaginaInput(pagina.Titulo, pagina.Descricao, pagina.Aviso), EntradaDeRegras.ParaEntrada(pagina.Exibicao));

    private static FatoColetadoInput ItemParaEntrada(ItemDoModelo campo) => new(
        campo.FatoCodigo, campo.Ordem, EntradaDeRegras.ParaEntrada(campo.Apresentacao), EntradaDeRegras.ParaEntrada(campo.Regras), campo.PaginaCodigo);

    private static TermoExigidoInput TermoParaEntrada(TermoDoModelo termo) => new(
        termo.Codigo, termo.Ordem, new VersaoDoTermoInput(termo.TermoId, termo.VersaoId), EntradaDeRegras.ParaEntrada(termo.Regras));

    private static GrupoColetadoInput GrupoParaEntrada(GrupoDoModelo grupo) => new(
        grupo.Codigo, grupo.Ordem, grupo.Rotulo, EntradaDeRegras.ParaEntrada(grupo.Ocorrencias), EntradaDeRegras.ParaEntrada(grupo.Regras),
        [.. grupo.Subitens.Select(ItemParaEntrada)], grupo.PaginaCodigo);
}
