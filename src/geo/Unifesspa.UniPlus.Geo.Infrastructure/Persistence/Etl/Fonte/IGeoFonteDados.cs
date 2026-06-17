namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;

/// <summary>
/// Fonte do dataset DNE para o ETL (ADR-0092). Abstrai a origem dos registros crus
/// por tabela — em produção/dev a implementação <see cref="DneStagingFonte"/> lê via
/// <c>SELECT</c> streamado de um schema de staging; em teste, uma fonte em memória.
/// A <see cref="Versao"/> (AAAAMM) é a fonte única de verdade da release: o importador
/// a usa para gravar a proveniência, evitando carimbar dados de uma versão com outra.
/// </summary>
/// <remarks>
/// Cobre toda a hierarquia DNE: o topo (País/Estado/Cidade, Story #672) e as folhas
/// (Distrito/Bairro/Logradouro e satélites, Story #673). As folhas trazem os ids int4
/// da fonte para resolução de FK.
/// </remarks>
internal interface IGeoFonteDados
{
    /// <summary>Release DNE (AAAAMM) que esta fonte representa.</summary>
    string Versao { get; }

    IAsyncEnumerable<PaisCru> LerPaisesAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<EstadoCru> LerEstadosAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<EstadoIndicadorCru> LerEstadoIndicadoresAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<EstadoFaixaCru> LerEstadoFaixasAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<CidadeCru> LerCidadesAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<CidadeTerritorioCru> LerCidadeTerritoriosAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<CidadeIndicadorCru> LerCidadeIndicadoresAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<CidadeFaixaCru> LerCidadeFaixasAsync(CancellationToken cancellationToken);

    // --- Folhas (Story #673) ---

    /// <summary>Mapa <c>id_cidade</c> → <c>cidade_ibge</c> para resolver a FK <c>cidade_id</c> (int4) em Guid.</summary>
    IAsyncEnumerable<CidadeIdCru> LerCidadeIdsAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<DistritoCru> LerDistritosAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<FaixaLocalidadeCru> LerDistritoFaixasAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<BairroCru> LerBairrosAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<FaixaLocalidadeCru> LerBairroFaixasAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<CepGrandeUsuarioCru> LerCepGrandesUsuariosAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<LogradouroComplementoCru> LerLogradouroComplementosAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<LogradouroCru> LerLogradourosAsync(CancellationToken cancellationToken);
}
