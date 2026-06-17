namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;

/// <summary>
/// Fonte do dataset DNE para o ETL (ADR-0092). Abstrai a origem dos registros crus
/// por tabela — em produção/dev a implementação <see cref="DneStagingFonte"/> lê via
/// <c>SELECT</c> streamado de um schema de staging; em teste, uma fonte em memória.
/// A <see cref="Versao"/> (AAAAMM) é a fonte única de verdade da release: o importador
/// a usa para gravar a proveniência, evitando carimbar dados de uma versão com outra.
/// </summary>
/// <remarks>
/// Esta interface cobre o topo da hierarquia (País/Estado/Cidade, Story #672). As
/// folhas (Distrito/Bairro/Logradouro) entram por extensão aditiva na Story irmã.
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
}
