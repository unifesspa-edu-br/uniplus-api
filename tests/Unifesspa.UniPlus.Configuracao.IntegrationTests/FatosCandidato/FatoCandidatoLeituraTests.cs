namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// A leitura do catálogo de fatos: o leitor cross-módulo ordenado por código, a resolução por chave
/// natural e a hipótese legal e a finalidade que a Seleção congela.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class FatoCandidatoLeituraTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public FatoCandidatoLeituraTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A Seleção lê do catálogo a hipótese legal e a finalidade de cada fato, para congelá-las junto da classificação")]
    public async Task Leitor_TrazAHipoteseLegalEAFinalidadeDoFato()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<FatoCandidatoView> fatos = await new FatoCandidatoReader(ctx).ListarAsync(CancellationToken.None);

        FatoCandidatoView modalidade = fatos.Single(f => f.Codigo == "MODALIDADE");
        modalidade.ClassificacaoProtecao.Should().Be("INTERNO");
        modalidade.HipoteseLegal.Should().Be("CUMPRIMENTO_OBRIGACAO_LEGAL");
        modalidade.FinalidadeTratamento.Should().Be(FatoCandidatoSeed.Itens.Single(i => i.Codigo == "MODALIDADE").FinalidadeTratamento);
    }

    [Fact(DisplayName = "Reader (resolvível de fora do assembly) lista ordenado por código e projeta a view com PontoResolucao/Binding")]
    public async Task Reader_Lista_OrdenadoPorCodigo()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new FatoCandidatoReader(ctx);

        // Os fatos de sistema entre os listados: o catálogo também tem fatos do administrador semeados.
        IReadOnlyList<FatoCandidatoView> views =
            [.. (await reader.ListarAsync()).Where(static v => FatoCandidatoSeed.Itens.Any(i => i.Codigo == v.Codigo))];

        views.Should().HaveCount(58);
        views.Select(v => v.Codigo).Should().BeInAscendingOrder(StringComparer.Ordinal);

        FatoCandidatoView corRaca = views.Single(v => v.Codigo == "COR_RACA");
        corRaca.Dominio.Should().Be("CATEGORICO");
        corRaca.Origem.Should().Be("DECLARADO");
        corRaca.Cardinalidade.Should().Be("ESCALAR");
        corRaca.PontoResolucao.Should().Be("INSCRICAO");
        corRaca.Binding.Should().Be("CAMPO_FORMULARIO:COR_RACA");
        // A view projeta os códigos dos valores declarados em ValoresDominio, que o consumidor
        // cross-módulo (PredicadoDnfValidador) usa como domínio do categórico estático.
        corRaca.ValoresDominio.Should().Equal("BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO");
        corRaca.ValoresDominioDeclarados.Should().NotBeNull().And.HaveCount(6);
        corRaca.ValoresDominioDeclarados!.Select(v => v.Codigo).Should().Contain("PRETA");

        FatoCandidatoView faixaEtaria = views.Single(v => v.Codigo == "FAIXA_ETARIA");
        faixaEtaria.Nome.Should().Be("Idade", "o comprovante mostra a idade em anos, com o rótulo do catálogo");
        faixaEtaria.Origem.Should().Be("DERIVADO");
        faixaEtaria.Binding.Should().Be("ATRIBUTO_CANDIDATO:FAIXA_ETARIA");
        faixaEtaria.ValoresDominioDeclarados.Should().BeNull();

        FatoCandidatoView modalidade = views.Single(v => v.Codigo == "MODALIDADE");
        modalidade.ValoresDominioDeclarados.Should().BeNull("MODALIDADE é escopo-processo, sem FatoValorDominio filhos");
    }

    [Fact(DisplayName = "Reader resolve por chave natural e devolve null para código inexistente")]
    public async Task Reader_ObterPorCodigo_ResolveOuNull()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new FatoCandidatoReader(ctx);

        FatoCandidatoView? sexo = await reader.ObterPorCodigoAsync("SEXO");
        sexo.Should().NotBeNull();
        sexo!.ValoresDominioDeclarados.Should().NotBeNull()
            .And.HaveCount(3)
            .And.ContainSingle(v => v.Codigo == "MASCULINO");

        FatoCandidatoView? inexistente = await reader.ObterPorCodigoAsync("NAO_EXISTE");
        inexistente.Should().BeNull();
    }
}
