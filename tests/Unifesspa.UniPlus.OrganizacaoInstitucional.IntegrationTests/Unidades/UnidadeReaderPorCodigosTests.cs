namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Readers;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// A leitura das Unidades vivas por código, numa só consulta direto do banco (sem o cache da lista): só o código que uma Unidade
/// viva tem hoje volta, o código repetido volta uma vez, a Unidade excluída não volta e o texto com caractere nulo, que o banco
/// recusaria, não tem Unidade e não derruba a leitura.
/// </summary>
public sealed class UnidadeReaderPorCodigosTests : IClassFixture<UnidadeDbFixture>
{
    private const string Usuario = "admin-leitura-por-codigos";

    private readonly UnidadeDbFixture _fixture;

    public UnidadeReaderPorCodigosTests(UnidadeDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static UnidadeReader Leitor(UniPlusDbContext contexto) =>
        new(Substitute.For<ICacheService>(), contexto, NullLogger<UnidadeReader>.Instance);

    [Fact(DisplayName = "ListarPorCodigosAsync devolve só as Unidades vivas dos códigos pedidos, e o código sem Unidade não volta")]
    public async Task ListarPorCodigosAsync_DevolveSoAsUnidadesQueExistem()
    {
        CadastroDeUnidadeDeTeste primeira = CadastroDeUnidadeDeTeste.ComNome("Unidade de leitura A");
        CadastroDeUnidadeDeTeste segunda = CadastroDeUnidadeDeTeste.ComNome("Unidade de leitura B");
        await _fixture.GravarAsync(Usuario, primeira.Criar(), segunda.Criar());
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<UnidadeView> unidades = await Leitor(contexto).ListarPorCodigosAsync([primeira.Codigo, "CODIGO_SEM_UNIDADE"], CancellationToken.None);

        unidades.Should().ContainSingle().Which.Codigo.Should().Be(primeira.Codigo);
    }

    [Fact(DisplayName = "ListarPorCodigosAsync devolve o código repetido no lote uma só vez")]
    public async Task ListarPorCodigosAsync_CodigoRepetido_VoltaUmaVez()
    {
        CadastroDeUnidadeDeTeste cadastro = CadastroDeUnidadeDeTeste.ComNome("Unidade de leitura repetida");
        await _fixture.GravarAsync(Usuario, cadastro.Criar());
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<UnidadeView> unidades = await Leitor(contexto).ListarPorCodigosAsync([cadastro.Codigo, cadastro.Codigo], CancellationToken.None);

        unidades.Should().ContainSingle();
    }

    [Fact(DisplayName = "ListarPorCodigosAsync não devolve a Unidade excluída")]
    public async Task ListarPorCodigosAsync_UnidadeExcluida_NaoVolta()
    {
        CadastroDeUnidadeDeTeste cadastro = CadastroDeUnidadeDeTeste.ComNome("Unidade de leitura excluída");
        Unidade unidade = cadastro.Criar();
        await _fixture.GravarAsync(Usuario, unidade);
        await _fixture.RemoverAsync<Unidade>(Usuario, unidade.Id);
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<UnidadeView> unidades = await Leitor(contexto).ListarPorCodigosAsync([cadastro.Codigo], CancellationToken.None);

        unidades.Should().BeEmpty();
    }

    [Fact(DisplayName = "ListarPorCodigosAsync com o caractere nulo no código não encontra Unidade e não falha")]
    public async Task ListarPorCodigosAsync_CaractereNulo_NaoTemUnidade()
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<UnidadeView> unidades = await Leitor(contexto).ListarPorCodigosAsync(["CODIGO\0NULO"], CancellationToken.None);

        unidades.Should().BeEmpty();
    }
}
