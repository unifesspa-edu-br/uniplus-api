namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;

public sealed class CatalogoMappingTests
{
    private readonly IReadOnlyList<ModuloDoCatalogoResponse> _modulos = CatalogoMapping.Modulos(CatalogoDoMapeamento.Catalogo);

    [Fact(DisplayName = "Os módulos saem na ordem do catálogo, que não é a das fontes")]
    public void Modulos_SaemNaOrdemDoCatalogo() =>
        _modulos.Select(static m => m.Modulo).Should().Equal("relatorio", "teste");

    [Fact(DisplayName = "Os resources de cada módulo saem na ordem do catálogo")]
    public void Resources_SaemNaOrdemDoCatalogo() =>
        _modulos[1].Resources.Select(static r => r.Resource).Should().Equal("itens", "laudos");

    [Fact(DisplayName = "As permissões de cada resource saem na ordem do catálogo")]
    public void Permissoes_SaemNaOrdemDoCatalogo() =>
        _modulos[1].Resources[0].Permissoes.Select(static p => p.Codigo).Should().Equal("teste:itens:ler", "teste:itens:manter");

    [Fact(DisplayName = "A permissão traz código, nome e descrição")]
    public void Permissao_TrazOsCamposDoCatalogo()
    {
        PermissaoDoCatalogoResponse permissao = _modulos[1].Resources[1].Permissoes.Should().ContainSingle().Which;

        (permissao.Codigo, permissao.Nome, permissao.Descricao).Should().Be(("teste:laudos:consultar", "Consultar laudos", "Lista laudos médicos."));
    }

    [Fact(DisplayName = "A sensibilidade da permissão sai com o código e o rótulo")]
    public void Permissao_TrazASensibilidadeComCodigoERotulo()
    {
        PermissaoDoCatalogoResponse permissao = _modulos[1].Resources[1].Permissoes.Should().ContainSingle().Which;

        (permissao.Sensibilidade.Codigo, permissao.Sensibilidade.Rotulo).Should().Be(("SENSIVEL", "Sensível"));
    }

    [Fact(DisplayName = "O outro módulo traz o seu próprio resource e permissão")]
    public void SegundoModulo_TrazOProprioResource() =>
        _modulos[0].Resources.Should().ContainSingle().Which.Permissoes.Select(static p => p.Codigo)
            .Should().Equal("relatorio:resumos:emitir");

    [Fact(DisplayName = "Catálogo nulo é recusado")]
    public void Modulos_CatalogoNulo_Recusa()
    {
        Action chamada = () => CatalogoMapping.Modulos(null!);

        chamada.Should().Throw<ArgumentNullException>();
    }
}
