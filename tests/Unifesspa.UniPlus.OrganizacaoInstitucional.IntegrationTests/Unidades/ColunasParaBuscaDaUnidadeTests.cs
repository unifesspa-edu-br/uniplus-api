namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// As colunas <c>*_para_busca</c> da Unidade, geradas pelo banco com a função de normalização da plataforma, e a
/// busca que compara contra elas, no Postgres real: cada coluna acompanha o campo que a origina quando ele é
/// gravado e alterado, o Unicode composto e o decomposto dão o mesmo resultado, o caractere nulo do texto
/// pesquisado é descartado e as letras fora da tabela da plataforma passam intactas.
/// </summary>
public sealed class ColunasParaBuscaDaUnidadeTests : IClassFixture<UnidadeDbFixture>
{
    private const string Autor = "admin-busca";

    private readonly UnidadeDbFixture _fixture;

    public ColunasParaBuscaDaUnidadeTests(UnidadeDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Ao gravar, cada coluna para busca guarda o campo sem acento e em minúsculas")]
    public async Task Insert_GeraAFormaNormalizadaDeCadaCampo()
    {
        string sufixo = NovoToken();
        CadastroDeUnidadeDeTeste basico = CadastroDeUnidadeDeTeste.ComNome("Faculdade de Física");
        CadastroDeUnidadeDeTeste cadastro = basico with
        {
            Sigla = $"CIÊNCIA{sufixo}",
            Codigo = $"Código-Ç{sufixo}",
            Alias = "Núcleo Ágil",
        };

        Guid id = await GravarAsync(cadastro);

        (await FormasAsync(id)).Should().Be(
            new FormasParaBusca("faculdade de fisica", $"ciencia{sufixo}", $"codigo-c{sufixo}", cadastro.Slug, "nucleo agil"));
    }

    [Fact(DisplayName = "Alterar o nome faz a coluna para busca do nome acompanhar")]
    public async Task Update_Nome_AcompanhaAColuna()
    {
        CadastroDeUnidadeDeTeste original = CadastroDeUnidadeDeTeste.ComNome("Reitoria");
        Guid id = await GravarAsync(original);

        await AtualizarAsync(id, original with { Nome = "Pró-Reitoria de Extensão" });

        (await FormasAsync(id)).Nome.Should().Be("pro-reitoria de extensao");
    }

    [Fact(DisplayName = "Alterar a sigla faz a coluna para busca da sigla acompanhar")]
    public async Task Update_Sigla_AcompanhaAColuna()
    {
        CadastroDeUnidadeDeTeste original = CadastroDeUnidadeDeTeste.ComNome("Instituto");
        Guid id = await GravarAsync(original);
        string sufixo = NovoToken();

        await AtualizarAsync(id, original with { Sigla = $"ÇÃO{sufixo}" });

        (await FormasAsync(id)).Sigla.Should().Be($"cao{sufixo}");
    }

    [Fact(DisplayName = "Alterar o código faz a coluna para busca do código acompanhar")]
    public async Task Update_Codigo_AcompanhaAColuna()
    {
        CadastroDeUnidadeDeTeste original = CadastroDeUnidadeDeTeste.ComNome("Centro");
        Guid id = await GravarAsync(original);
        string sufixo = NovoToken();

        await AtualizarAsync(id, original with { Codigo = $"Ação-{sufixo}" });

        (await FormasAsync(id)).Codigo.Should().Be($"acao-{sufixo}");
    }

    [Fact(DisplayName = "Alterar o slug faz a coluna para busca do slug acompanhar")]
    public async Task Update_Slug_AcompanhaAColuna()
    {
        CadastroDeUnidadeDeTeste original = CadastroDeUnidadeDeTeste.ComNome("Núcleo");
        Guid id = await GravarAsync(original);
        string novo = $"novo-{NovoToken()}";

        await AtualizarAsync(id, original with { Slug = novo });

        (await FormasAsync(id)).Slug.Should().Be(novo);
    }

    [Fact(DisplayName = "Sem alias a coluna para busca do alias é nula; ao receber alias passa a guardá-lo normalizado")]
    public async Task Update_AliasDeNuloParaValor_AColunaPassaAGuardarOAlias()
    {
        CadastroDeUnidadeDeTeste original = CadastroDeUnidadeDeTeste.ComNome("Diretoria");
        Guid id = await GravarAsync(original);
        (await FormasAsync(id)).Alias.Should().BeNull();

        await AtualizarAsync(id, original with { Alias = "Diretoria Acadêmica" });

        (await FormasAsync(id)).Alias.Should().Be("diretoria academica");
    }

    [Fact(DisplayName = "Ao remover o alias a coluna para busca do alias volta a ser nula")]
    public async Task Update_AliasDeValorParaNulo_AColunaVoltaANula()
    {
        CadastroDeUnidadeDeTeste original = CadastroDeUnidadeDeTeste.ComNome("Coordenação", alias: "Apelido Antigo");
        Guid id = await GravarAsync(original);
        (await FormasAsync(id)).Alias.Should().Be("apelido antigo");

        await AtualizarAsync(id, original with { Alias = null });

        (await FormasAsync(id)).Alias.Should().BeNull();
    }

    [Fact(DisplayName = "O ç composto e o c com cedilha combinante dão a mesma coluna e a mesma busca")]
    public async Task Unicode_CompostoEDecomposto_DaoOMesmoResultado()
    {
        string token = NovoToken();
        Guid composto = await GravarAsync(CadastroDeUnidadeDeTeste.ComNome($"Proço {token}"));
        Guid decomposto = await GravarAsync(CadastroDeUnidadeDeTeste.ComNome($"Proço {token}"));

        (await FormasAsync(composto)).Nome.Should().Be($"proco {token}");
        (await FormasAsync(decomposto)).Nome.Should().Be($"proco {token}");
        (await ListarIdsAsync($"proço {token}")).Should().Contain([composto, decomposto]);
        (await ListarIdsAsync($"proço {token}")).Should().Contain([composto, decomposto]);
    }

    [Theory(DisplayName = "Texto só de caractere nulo, com ou sem espaços, não é busca")]
    [InlineData("\0")]
    [InlineData(" \0 ")]
    [InlineData("\0\0")]
    public async Task Nulo_SoOuComEspacos_NaoEhBusca(string texto)
    {
        Guid id = await GravarAsync(CadastroDeUnidadeDeTeste.ComNome($"Qualquer {NovoToken()}"));

        (await ListarIdsAsync(texto)).Should().Contain(id);
    }

    [Theory(DisplayName = "Caractere nulo misturado ao texto é descartado e a busca segue pelo resto")]
    [InlineData("\0{0}")]
    [InlineData("{0}\0")]
    [InlineData(" \0 {0} \0 ")]
    public async Task Nulo_MisturadoAoTexto_BuscaPeloResto(string molde)
    {
        ArgumentNullException.ThrowIfNull(molde);
        string token = NovoToken();
        Guid comTermo = await GravarAsync(CadastroDeUnidadeDeTeste.ComNome($"Setor {token}"));
        Guid semTermo = await GravarAsync(CadastroDeUnidadeDeTeste.ComNome("Setor sem a marca"));

        IReadOnlyList<Guid> ids = await ListarIdsAsync(molde.Replace("{0}", token, StringComparison.Ordinal));

        ids.Should().Contain(comTermo);
        ids.Should().NotContain(semTermo);
    }

    /// <summary>
    /// A plataforma só troca as letras acentuadas do português; as demais passam intactas nos dois lados, e a
    /// minúscula do termo casa a minúscula do campo.
    /// </summary>
    [Theory(DisplayName = "Letra fora da tabela da plataforma passa intacta: a minúscula casa a minúscula")]
    [InlineData("ø")]
    [InlineData("ß")]
    [InlineData("œ")]
    [InlineData("ł")]
    [InlineData("ş")]
    public async Task LetraForaDaTabela_MinusculaCasaMinuscula(string letra)
    {
        string token = NovoToken();
        Guid id = await GravarAsync(CadastroDeUnidadeDeTeste.ComNome($"{token}{letra}fim"));

        (await ListarIdsAsync($"{token}{letra}fim")).Should().Contain(id);
    }

    /// <summary>
    /// Fora da tabela, a coluna gerada passa a letra como está: o <c>lower</c> do banco, sob a collation
    /// <c>C</c>, só minusculiza ASCII, enquanto o termo vai a minúsculas pela aplicação. Por isso a maiúscula
    /// gravada não casa o termo minúsculo equivalente. A divergência é da plataforma e fica registrada aqui como
    /// comportamento conhecido.
    /// </summary>
    [Theory(DisplayName = "Letra maiúscula fora da tabela da plataforma não casa o termo minúsculo equivalente")]
    [InlineData("Ł", "ł")]
    [InlineData("Ş", "ş")]
    [InlineData("İ", "i")]
    public async Task LetraForaDaTabela_MaiusculaNaoCasaMinuscula(string maiuscula, string minuscula)
    {
        string token = NovoToken();
        Guid id = await GravarAsync(CadastroDeUnidadeDeTeste.ComNome($"{token}{maiuscula}fim"));

        (await ListarIdsAsync($"{token}{minuscula}fim")).Should().NotContain(id);
    }

    private static string NovoToken() => Guid.NewGuid().ToString("N")[..10];

    private async Task<Guid> GravarAsync(CadastroDeUnidadeDeTeste cadastro)
    {
        Unidade unidade = cadastro.Criar();
        await _fixture.GravarAsync(Autor, unidade);
        return unidade.Id;
    }

    private async Task AtualizarAsync(Guid id, CadastroDeUnidadeDeTeste novo)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(Autor);
        Unidade unidade = (await new UnidadeRepository(contexto).ObterPorIdAsync(id, CancellationToken.None))!;

        novo.AplicarEm(unidade).IsSuccess.Should().BeTrue();
        await contexto.SaveChangesAsync();
    }

    private async Task<FormasParaBusca> FormasAsync(Guid id)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        return await contexto.Unidades
            .Where(u => u.Id == id)
            .Select(u => new FormasParaBusca(
                EF.Property<string>(u, UnidadeConfiguration.NomeParaBusca),
                EF.Property<string>(u, UnidadeConfiguration.SiglaParaBusca),
                EF.Property<string>(u, UnidadeConfiguration.CodigoParaBusca),
                EF.Property<string>(u, UnidadeConfiguration.SlugParaBusca),
                EF.Property<string?>(u, UnidadeConfiguration.AliasParaBusca)))
            .SingleAsync();
    }

    private async Task<IReadOnlyList<Guid>> ListarIdsAsync(string texto)
    {
        FiltroListagemUnidades filtro = new(texto, []);
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        (IReadOnlyList<Unidade> itens, _, _) = await new UnidadeRepository(contexto)
            .ListarPaginadoAsync(null, 100, PaginationDirection.Next, filtro, CancellationToken.None);
        return [.. itens.Select(u => u.Id)];
    }

    private sealed record FormasParaBusca(string Nome, string Sigla, string Codigo, string Slug, string? Alias);
}
