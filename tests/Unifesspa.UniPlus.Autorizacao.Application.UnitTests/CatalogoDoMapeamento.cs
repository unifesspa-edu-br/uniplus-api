namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Catálogo de dois módulos, com permissões de várias sensibilidades, para os testes dos mapeamentos.</summary>
internal static class CatalogoDoMapeamento
{
    public static readonly CatalogoDaAplicacao Catalogo = new([new Fonte<PermissaoDeTeste>(), new Fonte<PermissaoDeRelatorio>()]);

    private sealed class Fonte<TEnum> : IFonteDeCatalogo
        where TEnum : struct, Enum
    {
        public Type TipoDoEnum => typeof(TEnum);

        public IReadOnlyList<DefinicaoDePermissao> Definicoes() => CatalogoDePermissoes<TEnum>.Todas();
    }

    [ModuloDePermissoes("teste")]
    private enum PermissaoDeTeste
    {
        [Permissao("teste:itens:ler", "Consultar itens", "Lista itens.", ClassificacaoDeProtecao.Interno)]
        Ler = 1,

        [Permissao("teste:itens:manter", "Manter itens", "Cria e edita itens.", ClassificacaoDeProtecao.Pessoal,
            BaseLegal = "LGPD art. 7º, III")]
        Manter = 2,

        [Permissao("teste:laudos:consultar", "Consultar laudos", "Lista laudos médicos.", ClassificacaoDeProtecao.Sensivel,
            BaseLegal = "LGPD art. 11, II, a")]
        ConsultarLaudos = 3,
    }

    [ModuloDePermissoes("relatorio")]
    private enum PermissaoDeRelatorio
    {
        [Permissao("relatorio:resumos:emitir", "Emitir resumos", "Gera resumos.", ClassificacaoDeProtecao.Publico)]
        Emitir = 1,
    }
}
