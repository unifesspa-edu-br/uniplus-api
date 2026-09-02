namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

public sealed class VarreduraPaginadaTests
{
    [Fact]
    public async Task Percorre_todas_as_paginas_previstas_pelo_total()
    {
        ApiPaginada api = new(totalDeItens: 25, paginasExistentes: 3);

        IReadOnlyList<PaginaDeVinculos> paginas = await Percorrer(api, itensPorPagina: 10);

        paginas.Should().HaveCount(3);
        api.PaginasPedidas.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task Segue_alem_do_total_quando_a_origem_indica_continuacao()
    {
        // A origem cresce enquanto a varredura acontece: o total lido na primeira página
        // ficou desatualizado. Parar nele deixaria vínculos de fora sem erro nenhum, com a
        // execução registrada como completa.
        ApiPaginada api = new(totalDeItens: 20, paginasExistentes: 4);

        IReadOnlyList<PaginaDeVinculos> paginas = await Percorrer(api, itensPorPagina: 10);

        paginas.Should().HaveCount(4, "quem decide o fim é a origem, não a conta inicial");
        api.PaginasPedidas.Should().BeEquivalentTo([1, 2, 3, 4]);
    }

    [Fact]
    public async Task Sem_total_percorre_em_sequencia_ate_a_origem_encerrar()
    {
        ApiPaginada api = new(totalDeItens: null, paginasExistentes: 3);

        IReadOnlyList<PaginaDeVinculos> paginas = await Percorrer(api, itensPorPagina: 10);

        paginas.Should().HaveCount(3);
        api.PaginasPedidas.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task Coleção_vazia_encerra_na_primeira_pagina()
    {
        ApiPaginada api = new(totalDeItens: 0, paginasExistentes: 1);

        IReadOnlyList<PaginaDeVinculos> paginas = await Percorrer(api, itensPorPagina: 10);

        paginas.Should().ContainSingle();
        api.PaginasPedidas.Should().BeEquivalentTo([1]);
    }

    [Fact]
    public async Task Envelope_sem_a_propriedade_de_itens_e_recusado()
    {
        // Sem a propriedade, a resposta parece uma página vazia. Aceitá-la encerraria a
        // varredura sem violação de contrato e registraria sucesso, deixando a réplica
        // desatualizada sem que nada apontasse a divergência.
        ApiSemMembro api = new();

        await Assert.ThrowsAsync<EnvelopeDaOrigemInvalidoException>(
            async () => await Percorrer(api, itensPorPagina: 10));
    }

    [Fact]
    public async Task Colecao_legitimamente_vazia_continua_sendo_aceita()
    {
        // O contraponto do teste acima: lista vazia é resposta válida, e recusá-la faria
        // um filtro sem correspondência virar erro.
        ApiPaginada api = new(totalDeItens: 0, paginasExistentes: 0);

        IReadOnlyList<PaginaDeVinculos> paginas = await Percorrer(api, itensPorPagina: 10);

        paginas.Should().ContainSingle().Which.Itens.Should().BeEmpty();
    }

    private static async Task<IReadOnlyList<PaginaDeVinculos>> Percorrer(
        ISigaaVinculoDiscenteApi api,
        int itensPorPagina)
    {
        SigaaVinculoDiscenteClient cliente = new(
            api,
            Options.Create(new SigaaOptions
            {
                BaseUrl = "https://sigaa.exemplo.test",
                Usuario = "servico",
                Senha = "segredo",
                ItensPorPagina = itensPorPagina,
                GrauDeParalelismo = 2,
            }));

        List<PaginaDeVinculos> paginas = [];
        await foreach (PaginaDeVinculos pagina in cliente.PercorrerAsync(new FiltroDeVinculos("G")))
        {
            paginas.Add(pagina);
        }

        return paginas;
    }
}

/// <summary>
/// Origem paginada em que o total declarado e o número real de páginas podem divergir —
/// que é o que acontece quando a base cresce durante a varredura.
/// </summary>
internal sealed class ApiPaginada : ISigaaVinculoDiscenteApi
{
    private readonly int? _totalDeItens;
    private readonly int _paginasExistentes;

    public ApiPaginada(int? totalDeItens, int paginasExistentes)
    {
        _totalDeItens = totalDeItens;
        _paginasExistentes = paginasExistentes;
    }

    public List<int> PaginasPedidas { get; } = [];

    public Task<ColecaoHydra<VinculoDiscentePayload>> ObterVinculosAsync(
        string nivel,
        int? anoIngressoMinimo,
        IEnumerable<int>? situacoes,
        int itensPorPagina,
        int pagina,
        CancellationToken cancellationToken = default)
    {
        PaginasPedidas.Add(pagina);

        bool haProxima = pagina < _paginasExistentes;
        int nestaPagina = pagina <= _paginasExistentes ? itensPorPagina : 0;

        return Task.FromResult(new ColecaoHydra<VinculoDiscentePayload>
        {
            Itens = [.. Enumerable.Range(0, nestaPagina).Select(_ => new VinculoDiscentePayload())],
            TotalDeItens = _totalDeItens,
            Visao = new VisaoHydra { Proxima = haProxima ? $"/api/vinculo_discentes?page={pagina + 1}" : null },
        });
    }
}

/// <summary>Origem que responde sem a propriedade de itens do envelope Hydra.</summary>
internal sealed class ApiSemMembro : ISigaaVinculoDiscenteApi
{
    public Task<ColecaoHydra<VinculoDiscentePayload>> ObterVinculosAsync(
        string nivel,
        int? anoIngressoMinimo,
        IEnumerable<int>? situacoes,
        int itensPorPagina,
        int pagina,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ColecaoHydra<VinculoDiscentePayload> { TotalDeItens = 42 });
}
