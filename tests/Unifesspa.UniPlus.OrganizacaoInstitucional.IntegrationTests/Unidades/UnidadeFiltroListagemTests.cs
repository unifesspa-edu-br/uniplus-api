namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

public sealed class UnidadeFiltroListagemTests : IClassFixture<UnidadeDbFixture>
{
    private readonly ApoioDaListagemDeUnidades _apoio;

    public UnidadeFiltroListagemTests(UnidadeDbFixture fixture)
    {
        _apoio = new ApoioDaListagemDeUnidades(fixture);
    }

    [Fact(DisplayName = "Busca casa contra a sigla")]
    public async Task Busca_CasaContraASigla()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        Guid emSigla = await _apoio.SemearAsync("Centro Alpha", sigla: $"{iso}SG");

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(ApoioDaListagemDeUnidades.FiltroDeBusca(iso));

        ids.Should().Contain(emSigla);
    }

    [Fact(DisplayName = "Busca casa contra o nome")]
    public async Task Busca_CasaContraONome()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        Guid emNome = await _apoio.SemearAsync($"Faculdade {iso}");

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(ApoioDaListagemDeUnidades.FiltroDeBusca(iso));

        ids.Should().Contain(emNome);
    }

    [Fact(DisplayName = "Busca casa contra o código")]
    public async Task Busca_CasaContraOCodigo()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        Guid emCodigo = await _apoio.SemearAsync("Instituto Beta", codigo: $"{iso}CD");

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(ApoioDaListagemDeUnidades.FiltroDeBusca(iso));

        ids.Should().Contain(emCodigo);
    }

    [Fact(DisplayName = "Busca casa contra o slug")]
    public async Task Busca_CasaContraOSlug()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        Guid emSlug = await _apoio.SemearAsync("Diretoria Gama", slug: $"s{iso}-sl");

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(ApoioDaListagemDeUnidades.FiltroDeBusca(iso));

        ids.Should().Contain(emSlug);
    }

    [Fact(DisplayName = "Busca casa contra o alias")]
    public async Task Busca_CasaContraOAlias()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        Guid emAlias = await _apoio.SemearAsync("Núcleo Delta", alias: $"{iso} regional");

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(ApoioDaListagemDeUnidades.FiltroDeBusca(iso));

        ids.Should().Contain(emAlias);
    }

    [Fact(DisplayName = "Busca não devolve a unidade que não contém o termo")]
    public async Task Busca_NaoDevolveUnidadeSemOTermo()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        Guid comTermo = await _apoio.SemearAsync($"Faculdade {iso}");
        Guid semTermo = await _apoio.SemearAsync("Coordenação Sem Marca");

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(ApoioDaListagemDeUnidades.FiltroDeBusca(iso));

        ids.Should().Contain(comTermo);
        ids.Should().NotContain(semTermo);
    }

    [Fact(DisplayName = "Busca é case-insensitive: termo em minúsculas casa o campo em maiúsculas")]
    public async Task Busca_EhCaseInsensitive()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        Guid id = await _apoio.SemearAsync($"Faculdade {iso.ToUpperInvariant()}");

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(ApoioDaListagemDeUnidades.FiltroDeBusca(iso));

        ids.Should().Contain(id);
    }

    [Fact(DisplayName = "Busca é insensível a acento (termo acentuado e sem acento casam o mesmo registro)")]
    public async Task Busca_EhAcentoInsensivel()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        Guid id = await _apoio.SemearAsync($"Promoção{iso}");

        IReadOnlyList<Unidade> semAcento = await _apoio.ListarAsync(ApoioDaListagemDeUnidades.FiltroDeBusca($"promocao{iso}"));
        IReadOnlyList<Unidade> comAcento = await _apoio.ListarAsync(ApoioDaListagemDeUnidades.FiltroDeBusca($"promoção{iso}"));

        semAcento.Select(u => u.Id).Should().Contain(id);
        comAcento.Select(u => u.Id).Should().Contain(id);
    }

    // Pares com o MESMO comprimento: sem escape, o curinga no termo casaria
    // o caractere/sequência divergente do par, devolvendo o registro errado.
    [Fact(DisplayName = "O curinga _ do LIKE no termo é tratado como literal, não como um caractere qualquer")]
    public async Task Busca_EscapaOUnderscoreDoLike()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        Guid comUnderscore = await _apoio.SemearAsync($"Alfa_{iso}");
        Guid soLetra = await _apoio.SemearAsync($"AlfaX{iso}");

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(ApoioDaListagemDeUnidades.FiltroDeBusca($"Alfa_{iso}"));

        ids.Should().Contain(comUnderscore);
        ids.Should().NotContain(soLetra, "'_' deve ser literal, não curinga de 1 caractere");
    }

    [Fact(DisplayName = "O curinga % do LIKE no termo é tratado como literal, não como uma sequência qualquer")]
    public async Task Busca_EscapaOPercentDoLike()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        Guid comPercent = await _apoio.SemearAsync($"Beta%{iso}");
        Guid soSequencia = await _apoio.SemearAsync($"BetaZZ{iso}");

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(ApoioDaListagemDeUnidades.FiltroDeBusca($"Beta%{iso}"));

        ids.Should().Contain(comPercent);
        ids.Should().NotContain(soSequencia, "'%' deve ser literal, não curinga de sequência");
    }

}
