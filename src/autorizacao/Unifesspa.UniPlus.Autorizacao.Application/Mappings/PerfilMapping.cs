namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Mapeamento <see cref="Perfil"/> para <see cref="PerfilDto"/> e para o retrato que o aprovador avalia.</summary>
public static class PerfilMapping
{
    public static PerfilDto ToDto(Perfil perfil, CatalogoDaAplicacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(catalogo);

        return new PerfilDto(
            perfil.Id,
            perfil.Nome,
            perfil.Descricao,
            perfil.Ativo,
            perfil.Governanca?.ToCodigo(),
            ValoresCanonicos.De(SensibilidadeDoPerfil(perfil, catalogo)),
            ComposicaoDe(perfil, catalogo),
            VersaoMapping.PendenteDe(perfil, catalogo),
            VersaoMapping.UltimaEncerradaSemVigorDe(perfil));
    }

    public static PerfilListadoDto ToListadoDto(Perfil perfil, CatalogoDaAplicacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(catalogo);

        return new PerfilListadoDto(
            perfil.Id,
            perfil.Nome,
            perfil.Descricao,
            perfil.Ativo,
            perfil.Governanca?.ToCodigo(),
            ValoresCanonicos.De(SensibilidadeDoPerfil(perfil, catalogo)),
            perfil.TemComposicaoEmVigor,
            perfil.VersaoPendente is not null);
    }

    /// <summary>
    /// O perfil como o aprovador o avalia agora: as permissões da composição em vigor e a sensibilidade que o catálogo
    /// lhes atribui. Pessoal ou sensível exige também a proteção de dados.
    /// </summary>
    public static PerfilAvaliado Avaliar(Perfil perfil, CatalogoDaAplicacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(catalogo);

        ClassificacaoDeProtecao sensibilidade = MaiorSensibilidade(perfil.ComposicaoEmVigor, catalogo);

        return new PerfilAvaliado(
            perfil.ComposicaoEmVigor,
            ValoresCanonicos.De(sensibilidade),
            sensibilidade >= ClassificacaoDeProtecao.Pessoal,
            perfil.Governanca,
            perfil.Ativo);
    }

    // Código que o catálogo não conhece (permissão retirada do código) conta como a mais restrita: o perfil não pode
    // parecer menos sensível por causa de uma lacuna.
    internal static ClassificacaoDeProtecao SensibilidadeDe(string codigo, CatalogoDaAplicacao catalogo) =>
        catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao)
            ? definicao!.Requisito.Sensibilidade
            : ClassificacaoDeProtecao.Sensivel;

    internal static PermissaoDoPerfilDto Descrever(string codigo, CatalogoDaAplicacao catalogo) =>
        catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao)
            ? new PermissaoDoPerfilDto(codigo, definicao!.Nome, ValoresCanonicos.De(definicao.Requisito.Sensibilidade))
            : new PermissaoDoPerfilDto(codigo, codigo, ValoresCanonicos.De(ClassificacaoDeProtecao.Sensivel));

    // A sensibilidade nunca se grava: deriva do catálogo. É a das permissões em vigor ou, enquanto o perfil não tem
    // vigente, a das permissões da versão pendente.
    private static ClassificacaoDeProtecao SensibilidadeDoPerfil(Perfil perfil, CatalogoDaAplicacao catalogo) =>
        MaiorSensibilidade(CodigosParaExibir(perfil), catalogo);

    private static IReadOnlyList<string> CodigosParaExibir(Perfil perfil) =>
        perfil.TemComposicaoEmVigor ? perfil.ComposicaoEmVigor : perfil.VersaoPendente?.Codigos() ?? [];

    // A sensibilidade do perfil é a mais alta entre as permissões; a escala é crescente.
    private static ClassificacaoDeProtecao MaiorSensibilidade(IEnumerable<string> codigos, CatalogoDaAplicacao catalogo) =>
        codigos
            .Select(c => SensibilidadeDe(c, catalogo))
            .DefaultIfEmpty(ClassificacaoDeProtecao.Publico)
            .Max();

    private static ComposicaoEmVigorDto? ComposicaoDe(Perfil perfil, CatalogoDaAplicacao catalogo) =>
        perfil.Governanca is not null
            ? ComposicaoDaGovernanca(perfil, catalogo)
            : perfil.VersaoVigente is { } vigente ? VersaoMapping.VigenteDe(perfil, vigente, catalogo) : null;

    private static ComposicaoEmVigorDto ComposicaoDaGovernanca(Perfil perfil, CatalogoDaAplicacao catalogo) =>
        new(null, null, VersaoMapping.Descrever(perfil.ComposicaoEmVigor, catalogo), null, null, null);
}
