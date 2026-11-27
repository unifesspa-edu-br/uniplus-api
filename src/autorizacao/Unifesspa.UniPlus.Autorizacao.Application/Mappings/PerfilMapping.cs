namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Mapeamento <see cref="Perfil"/> para <see cref="PerfilDto"/> e para o perfil como o aprovador o avalia.</summary>
public static class PerfilMapping
{
    /// <summary>O perfil como a API o expõe.</summary>
    /// <param name="perfil">O perfil.</param>
    /// <param name="catalogo">O catálogo de permissões.</param>
    /// <param name="leitor">Quem lê, para dizer se a versão pendente é dele; nulo quando a resposta não é de uma leitura.</param>
    internal static PerfilDto ToDto(Perfil perfil, CatalogoDaAplicacao catalogo, Identidade? leitor = null) =>
        new(
            perfil.Id,
            perfil.Nome,
            perfil.Descricao,
            perfil.Ativo,
            GovernancaDe(perfil),
            CodigoComRotulo<ClassificacaoDeProtecao>.De(SensibilidadeDoPerfil(perfil, catalogo)),
            CodigoComRotulo<SituacaoDoAcessoDoPerfil>.De(perfil.SituacaoDoAcesso),
            ComposicaoDe(perfil, catalogo),
            VersaoMapping.PendenteDe(perfil, catalogo, leitor),
            VersaoMapping.UltimaEncerradaSemVigorDe(perfil),
            SuspensaoDe(perfil, catalogo),
            perfil.DesativacaoAberta?.DesativadaEm);

    internal static PerfilListadoDto ToListadoDto(Perfil perfil, CatalogoDaAplicacao catalogo) =>
        new(
            perfil.Id,
            perfil.Nome,
            perfil.Descricao,
            perfil.Ativo,
            GovernancaDe(perfil),
            CodigoComRotulo<ClassificacaoDeProtecao>.De(SensibilidadeDoPerfil(perfil, catalogo)),
            CodigoComRotulo<SituacaoDoAcessoDoPerfil>.De(perfil.SituacaoDoAcesso),
            perfil.VersaoPendente is not null,
            SuspensaoDe(perfil, catalogo),
            perfil.DesativacaoAberta?.DesativadaEm);

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

    // As etapas exigidas se calculam pelo catálogo atual a cada leitura; o motivo e as pessoas ficam na trilha.
    private static SuspensaoDoPerfilDto? SuspensaoDe(Perfil perfil, CatalogoDaAplicacao catalogo) =>
        perfil.SuspensaoAberta is { } suspensao
            ? new SuspensaoDoPerfilDto(
                suspensao.SuspensaEm,
                CodigoComRotulo<EtapaDaVersao>.De(suspensao.Papel),
                [.. SuspensaoDoPerfil.EtapasExigidas(Avaliar(perfil, catalogo).ExigeAprovacaoDeProtecaoDeDados).Select(CodigoComRotulo<EtapaDaVersao>.De)],
                [.. suspensao.Decisoes.Select(static d => CodigoComRotulo<EtapaDaVersao>.De(d.Etapa))])
            : null;

    // Código que o catálogo não conhece (permissão retirada do código) conta como a mais restrita: o perfil não pode
    // parecer menos sensível por causa de uma lacuna.
    internal static ClassificacaoDeProtecao SensibilidadeDe(string codigo, CatalogoDaAplicacao catalogo) =>
        catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao)
            ? definicao!.Requisito.Sensibilidade
            : ClassificacaoDeProtecao.Sensivel;

    internal static PermissaoDoPerfilDto Descrever(string codigo, CatalogoDaAplicacao catalogo) =>
        catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao)
            ? new PermissaoDoPerfilDto(codigo, definicao!.Nome, CodigoComRotulo<ClassificacaoDeProtecao>.De(definicao.Requisito.Sensibilidade))
            : new PermissaoDoPerfilDto(codigo, codigo, CodigoComRotulo<ClassificacaoDeProtecao>.De(ClassificacaoDeProtecao.Sensivel));

    internal static CodigoComRotulo<TipoDeGovernanca>? GovernancaDe(Perfil perfil) =>
        perfil.Governanca is { } governanca ? CodigoComRotulo<TipoDeGovernanca>.De(governanca) : null;

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
