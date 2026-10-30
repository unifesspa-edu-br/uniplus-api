namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

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

        ClassificacaoDeProtecao sensibilidade = SensibilidadeDoPerfil(perfil, catalogo);

        return new PerfilDto(
            perfil.Id,
            perfil.Nome,
            perfil.Descricao,
            perfil.Ativo,
            perfil.Governanca?.ToCodigo(),
            ValoresCanonicos.De(sensibilidade),
            permissoes);
    }

    /// <summary>
    /// O perfil como o aprovador o avalia agora: as permissões que reúne e a sensibilidade que o catálogo lhes atribui.
    /// Pessoal ou sensível exige também a proteção de dados.
    /// </summary>
    public static PerfilAvaliado Avaliar(Perfil perfil, CatalogoDaAplicacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(catalogo);

        ClassificacaoDeProtecao sensibilidade = SensibilidadeDoPerfil(perfil, catalogo);

        return new PerfilAvaliado(
            perfil.Permissoes.Select(p => p.Codigo),
            ValoresCanonicos.De(sensibilidade),
            sensibilidade >= ClassificacaoDeProtecao.Pessoal,
            perfil.Governanca,
            perfil.Ativo);
    }

    // A sensibilidade do perfil é a mais alta entre as permissões; a escala é crescente. Nunca se grava: deriva do catálogo.
    internal static ClassificacaoDeProtecao SensibilidadeDoPerfil(Perfil perfil, CatalogoDaAplicacao catalogo) =>
        perfil.Permissoes
            .Select(p => SensibilidadeDe(p.Codigo, catalogo))
            .DefaultIfEmpty(ClassificacaoDeProtecao.Publico)
            .Max();

    private static PermissaoDoPerfilDto Descrever(string codigo, CatalogoDaAplicacao catalogo) =>
        catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao)
            ? new PermissaoDoPerfilDto(codigo, definicao!.Nome, ValoresCanonicos.De(definicao.Requisito.Sensibilidade))
            : new PermissaoDoPerfilDto(codigo, codigo, ValoresCanonicos.De(ClassificacaoDeProtecao.Sensivel));

    // Código que o catálogo não conhece (permissão retirada do código) conta como a mais
    // restrita: o perfil não pode parecer menos sensível por causa de uma lacuna.
    internal static ClassificacaoDeProtecao SensibilidadeDe(string codigo, CatalogoDaAplicacao catalogo) =>
        catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao)
            ? definicao!.Requisito.Sensibilidade
            : ClassificacaoDeProtecao.Sensivel;
}
