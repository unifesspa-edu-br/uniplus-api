namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Mapeamento <see cref="Perfil"/> para <see cref="PerfilDto"/>.</summary>
public static class PerfilMapping
{
    public static PerfilDto ToDto(Perfil perfil, CatalogoDaAplicacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(catalogo);

        PermissaoDoPerfilDto[] permissoes =
        [
            .. perfil.Permissoes
                .Select(p => Descrever(p.Codigo, catalogo))
                .OrderBy(static p => p.Codigo, StringComparer.Ordinal),
        ];

        // A sensibilidade do perfil é a mais alta entre as permissões; a escala é crescente.
        Sensibilidade sensibilidade = perfil.Permissoes
            .Select(p => SensibilidadeDe(p.Codigo, catalogo))
            .DefaultIfEmpty(Sensibilidade.Publica)
            .Max();

        return new PerfilDto(
            perfil.Id,
            perfil.Nome,
            perfil.Descricao,
            perfil.Ativo,
            perfil.Governanca?.ToCodigo(),
            ValoresCanonicos.De(sensibilidade),
            permissoes);
    }

    private static PermissaoDoPerfilDto Descrever(string codigo, CatalogoDaAplicacao catalogo) =>
        catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao)
            ? new PermissaoDoPerfilDto(codigo, definicao!.Nome, ValoresCanonicos.De(definicao.Requisito.Sensibilidade))
            : new PermissaoDoPerfilDto(codigo, codigo, ValoresCanonicos.De(Sensibilidade.Sensivel));

    // Código que o catálogo não conhece (permissão retirada do código) conta como a mais
    // restrita: o perfil não pode parecer menos sensível por causa de uma lacuna.
    private static Sensibilidade SensibilidadeDe(string codigo, CatalogoDaAplicacao catalogo) =>
        catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao)
            ? definicao!.Requisito.Sensibilidade
            : Sensibilidade.Sensivel;
}
