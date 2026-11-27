namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Mapeamento do <see cref="CatalogoDaAplicacao"/> para as permissões agrupadas por módulo e por resource.</summary>
public static class CatalogoMapping
{
    /// <summary>Os módulos do catálogo, com resources e permissões na ordem do próprio catálogo.</summary>
    /// <param name="catalogo">O catálogo de permissões.</param>
    public static IReadOnlyList<ModuloDoCatalogoResponse> Modulos(CatalogoDaAplicacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        return [.. catalogo.Definicoes.GroupBy(static d => d.Modulo).Select(Modulo)];
    }

    private static ModuloDoCatalogoResponse Modulo(IGrouping<string, DefinicaoDePermissao> modulo) =>
        new(modulo.Key, [.. modulo.GroupBy(static d => d.Resource).Select(Resource)]);

    private static ResourceDoCatalogoResponse Resource(IGrouping<string, DefinicaoDePermissao> resource) =>
        new(resource.Key, [.. resource.Select(Permissao)]);

    private static PermissaoDoCatalogoResponse Permissao(DefinicaoDePermissao definicao) =>
        new(definicao.Codigo, definicao.Nome, definicao.Descricao, CodigoComRotulo<ClassificacaoDeProtecao>.De(definicao.Requisito.Sensibilidade));
}
