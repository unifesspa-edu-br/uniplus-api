namespace Unifesspa.UniPlus.Geo.IntegrationTests.Etl;

using System.Runtime.CompilerServices;

using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;

/// <summary>
/// <see cref="IGeoFonteDados"/> em memória para exercitar o importador sem um schema
/// de staging real — determinístico e legível. A <see cref="Versao"/> é a proveniência
/// da carga.
/// </summary>
internal sealed class FonteEmMemoria : IGeoFonteDados
{
    public string Versao { get; init; } = "202601";

    public List<PaisCru> Paises { get; } = [];

    public List<EstadoCru> Estados { get; } = [];

    public List<EstadoIndicadorCru> EstadoIndicadores { get; } = [];

    public List<EstadoFaixaCru> EstadoFaixas { get; } = [];

    public List<CidadeCru> Cidades { get; } = [];

    public List<CidadeTerritorioCru> CidadeTerritorios { get; } = [];

    public List<CidadeIndicadorCru> CidadeIndicadores { get; } = [];

    public List<CidadeFaixaCru> CidadeFaixas { get; } = [];

    public IAsyncEnumerable<PaisCru> LerPaisesAsync(CancellationToken cancellationToken) => ParaAsync(Paises, cancellationToken);

    public IAsyncEnumerable<EstadoCru> LerEstadosAsync(CancellationToken cancellationToken) => ParaAsync(Estados, cancellationToken);

    public IAsyncEnumerable<EstadoIndicadorCru> LerEstadoIndicadoresAsync(CancellationToken cancellationToken) => ParaAsync(EstadoIndicadores, cancellationToken);

    public IAsyncEnumerable<EstadoFaixaCru> LerEstadoFaixasAsync(CancellationToken cancellationToken) => ParaAsync(EstadoFaixas, cancellationToken);

    public IAsyncEnumerable<CidadeCru> LerCidadesAsync(CancellationToken cancellationToken) => ParaAsync(Cidades, cancellationToken);

    public IAsyncEnumerable<CidadeTerritorioCru> LerCidadeTerritoriosAsync(CancellationToken cancellationToken) => ParaAsync(CidadeTerritorios, cancellationToken);

    public IAsyncEnumerable<CidadeIndicadorCru> LerCidadeIndicadoresAsync(CancellationToken cancellationToken) => ParaAsync(CidadeIndicadores, cancellationToken);

    public IAsyncEnumerable<CidadeFaixaCru> LerCidadeFaixasAsync(CancellationToken cancellationToken) => ParaAsync(CidadeFaixas, cancellationToken);

    private static async IAsyncEnumerable<T> ParaAsync<T>(IEnumerable<T> itens, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (T item in itens)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>
/// Builders de registros crus DNE com defaults sensatos — montam dados de teste
/// legíveis sem repetir os muitos campos posicionais dos records.
/// </summary>
internal static class DadosDne
{
    public static PaisCru Pais(string siglaIso, string nome, string sigla = "XX") =>
        new(siglaIso, sigla, nome, CodigoBcb: null, CodigoRfb: null, CodigoSped: null, CodigoSiscomex: null);

    public static EstadoCru Estado(
        string uf,
        string nome,
        string? regiao = "Norte",
        string? capital = null,
        string? latitude = null,
        string? longitude = null,
        string? faixaIni = null,
        string? faixaFim = null) =>
        new(uf, nome, NomeSemAcento: nome, regiao, capital, faixaIni, faixaFim, latitude, longitude);

    public static EstadoIndicadorCru EstadoIndicador(
        string? uf,
        string? codigoIbge,
        string? idh = null,
        string? populacao = null,
        string? receitas = null) =>
        new(uf, codigoIbge, Gentilico: null, Governador: null, AreaTerritorialKm2: null, populacao,
            DensidadeDemografica: null, MatriculasEnsinoFundamental2023: null, idh, receitas,
            DespesasBrutas: null, RendimentoMensalPerCapita: null, TotalVeiculos2023: null);

    public static EstadoFaixaCru EstadoFaixa(string uf, string faixaIni, string faixaFim, string? descricao = null) =>
        new(uf, descricao, faixaIni, faixaFim);

    public static CidadeCru Cidade(
        string codigoIbge,
        string nome,
        string uf,
        string? ddd = null,
        string? latitude = null,
        string? longitude = null) =>
        new(codigoIbge, nome, NomeSemAcento: nome, uf, ddd, latitude, longitude);

    public static CidadeTerritorioCru CidadeTerritorio(
        string codigoIbge,
        string? mesorregiaoNome = null,
        string? microrregiaoNome = null) =>
        new(codigoIbge, MesorregiaoCodigo: null, mesorregiaoNome, MicrorregiaoCodigo: null, microrregiaoNome,
            RegiaoIntermediariaCodigo: null, RegiaoIntermediariaNome: null, RegiaoImediataCodigo: null, RegiaoImediataNome: null);

    public static CidadeIndicadorCru CidadeIndicador(
        string codigoIbge,
        string? mortalidadeInfantil = null,
        string? populacao = null,
        string? aniversario = null) =>
        new(codigoIbge, Gentilico: null, Prefeito: null, AreaTerritorialKm2: null, populacao,
            DensidadeDemografica: null, Escolarizacao6a14: null, Idh: null, mortalidadeInfantil,
            Receitas: null, Despesas: null, PibPerCapita: null, aniversario);

    public static CidadeFaixaCru CidadeFaixa(string codigoIbge, string faixaIni, string faixaFim) =>
        new(codigoIbge, faixaIni, faixaFim);
}

/// <summary>
/// Decora uma <see cref="FonteEmMemoria"/> fazendo a leitura de cidades lançar — o
/// País/Estado já foram persistidos (dentro da transação) quando a falha ocorre, o
/// que prova o rollback total da carga (nenhum dado publicado em release parcial).
/// </summary>
internal sealed class FonteComFalhaNasCidades : IGeoFonteDados
{
    private readonly FonteEmMemoria _interna;

    public FonteComFalhaNasCidades(FonteEmMemoria interna)
    {
        _interna = interna;
    }

    public string Versao => _interna.Versao;

    public IAsyncEnumerable<PaisCru> LerPaisesAsync(CancellationToken cancellationToken) => _interna.LerPaisesAsync(cancellationToken);

    public IAsyncEnumerable<EstadoCru> LerEstadosAsync(CancellationToken cancellationToken) => _interna.LerEstadosAsync(cancellationToken);

    public IAsyncEnumerable<EstadoIndicadorCru> LerEstadoIndicadoresAsync(CancellationToken cancellationToken) => _interna.LerEstadoIndicadoresAsync(cancellationToken);

    public IAsyncEnumerable<EstadoFaixaCru> LerEstadoFaixasAsync(CancellationToken cancellationToken) => _interna.LerEstadoFaixasAsync(cancellationToken);

    public IAsyncEnumerable<CidadeCru> LerCidadesAsync(CancellationToken cancellationToken) => Falhar();

    public IAsyncEnumerable<CidadeTerritorioCru> LerCidadeTerritoriosAsync(CancellationToken cancellationToken) => _interna.LerCidadeTerritoriosAsync(cancellationToken);

    public IAsyncEnumerable<CidadeIndicadorCru> LerCidadeIndicadoresAsync(CancellationToken cancellationToken) => _interna.LerCidadeIndicadoresAsync(cancellationToken);

    public IAsyncEnumerable<CidadeFaixaCru> LerCidadeFaixasAsync(CancellationToken cancellationToken) => _interna.LerCidadeFaixasAsync(cancellationToken);

    private static async IAsyncEnumerable<CidadeCru> Falhar()
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new InvalidOperationException("Falha simulada ao ler cidades do dataset.");
#pragma warning disable CS0162 // Unreachable: necessário para o método ser um iterador async.
        yield break;
#pragma warning restore CS0162
    }
}
