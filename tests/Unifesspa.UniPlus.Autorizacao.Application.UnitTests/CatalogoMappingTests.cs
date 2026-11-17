namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

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

    [Fact(DisplayName = "A permissão traz código, nome, descrição e a sensibilidade canônica")]
    public void Permissao_TrazOsCamposDoCatalogo() =>
        _modulos[1].Resources[1].Permissoes.Should().ContainSingle().Which.Should().Be(new PermissaoDoCatalogoResponse(
            "teste:laudos:consultar",
            "Consultar laudos",
            "Lista laudos médicos.",
            ValoresCanonicos.De(ClassificacaoDeProtecao.Sensivel)));

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
