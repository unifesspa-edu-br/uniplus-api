namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using System.Runtime.CompilerServices;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using NetTopologySuite.Geometries;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Parsing;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.GeoEtlUpsert;

/// <summary>
/// Mapas de resolução de FK construídos pela carga de Distrito/Bairro: traduzem os ids
/// int4 da fonte DNE (instáveis entre releases) para os Guid intra-banco (ADR-0054),
/// consumidos pela carga de logradouros. <c>id_distrito</c>/<c>id_bairro</c> são as PKs
/// da fonte (únicos dentro de uma release); homônimos em cidades distintas resolvem
/// para Guids diferentes porque cada par <c>(cidade, nome)</c> é uma entidade própria.
/// </summary>
internal sealed record ResolucaoLocalidades(
    Dictionary<int, Guid> CidadesPorIdDne,
    Dictionary<int, LocalidadeResolvida> DistritosPorIdDne,
    Dictionary<int, LocalidadeResolvida> BairrosPorIdDne);

/// <summary>
/// Distrito/Bairro resolvido: o Guid intra-banco e a <see cref="CidadeId"/> a que
/// pertence. O logradouro só adota a FK de distrito/bairro se a cidade resolvida bater
/// com a sua — garantindo a coerência hierárquica que o domínio delega ao ETL.
/// </summary>
internal readonly record struct LocalidadeResolvida(Guid Id, Guid CidadeId);

/// <summary>
/// Carga das folhas de volume modesto via upsert por chave natural (idempotente, como
/// o importador de topo #672): Distrito e Bairro (+faixas) e o CEP de grande usuário.
/// Roda numa transação única e devolve a <see cref="ResolucaoLocalidades"/> (dicts de
/// FK) para a carga em lote (COPY) dos logradouros. Distrito/Bairro usam upsert (não
/// COPY) porque o volume é baixo e a tradução id4→Guid precisa de iteração em C#.
/// </summary>
internal sealed class GeoImportadorDistritoBairro
{
    private readonly GeoDbContext _contexto;

    public GeoImportadorDistritoBairro(GeoDbContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _contexto = contexto;
    }

    public async Task<ResolucaoLocalidades> ImportarAsync(
        IGeoFonteDados fonte,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction transacao =
            await _contexto.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transacao.ConfigureAwait(false))
        {
            Dictionary<int, Guid> cidadesPorIdDne =
                await ResolverCidadesPorIdDneAsync(fonte, cancellationToken).ConfigureAwait(false);

            Dictionary<int, LocalidadeResolvida> distritosPorIdDne = await ImportarLocalidadesAsync(
                "distrito",
                ComoLocalidades(fonte.LerDistritosAsync(cancellationToken),
                    static d => new LocalidadeCru(d.IdDistrito, d.Nome, d.NomeSemAcento, d.CidadeIdDne, d.Uf, d.Latitude, d.Longitude),
                    cancellationToken),
                cidadesPorIdDne,
                _contexto.Distritos,
                e => $"{e.CidadeId:N}|{e.NomeNormalizado}",
                (cidadeId, uf, nome, nomeNorm, lat, lon, coord, idDne, versao) =>
                    Distrito.Importar(cidadeId, uf, nome, nomeNorm, lat, lon, coord, idDne, versao),
                (e, cidadeId, uf, nome, nomeNorm, lat, lon, coord, idDne, versao) =>
                    e.Atualizar(cidadeId, uf, nome, nomeNorm, lat, lon, coord, idDne, versao),
                fonte.Versao,
                relatorio,
                cancellationToken).ConfigureAwait(false);

            await ImportarFaixasAsync(
                "distrito_faixa",
                fonte.LerDistritoFaixasAsync(cancellationToken),
                distritosPorIdDne,
                _contexto.DistritoFaixasCep,
                f => ChaveFaixa(f.DistritoId, f.CepInicial, f.CepFinal),
                (paiId, ini, fim, versao) => DistritoFaixaCep.Importar(paiId, ini, fim, versao),
                (e, versao) => e.Atualizar(versao),
                fonte.Versao,
                relatorio,
                cancellationToken).ConfigureAwait(false);

            Dictionary<int, LocalidadeResolvida> bairrosPorIdDne = await ImportarLocalidadesAsync(
                "bairro",
                ComoLocalidades(fonte.LerBairrosAsync(cancellationToken),
                    static b => new LocalidadeCru(b.IdBairro, b.Nome, b.NomeSemAcento, b.CidadeIdDne, b.Uf, b.Latitude, b.Longitude),
                    cancellationToken),
                cidadesPorIdDne,
                _contexto.Bairros,
                e => $"{e.CidadeId:N}|{e.NomeNormalizado}",
                (cidadeId, uf, nome, nomeNorm, lat, lon, coord, idDne, versao) =>
                    Bairro.Importar(cidadeId, uf, nome, nomeNorm, lat, lon, coord, idDne, versao),
                (e, cidadeId, uf, nome, nomeNorm, lat, lon, coord, idDne, versao) =>
                    e.Atualizar(cidadeId, uf, nome, nomeNorm, lat, lon, coord, idDne, versao),
                fonte.Versao,
                relatorio,
                cancellationToken).ConfigureAwait(false);

            await ImportarFaixasAsync(
                "bairro_faixa",
                fonte.LerBairroFaixasAsync(cancellationToken),
                bairrosPorIdDne,
                _contexto.BairroFaixasCep,
                f => ChaveFaixa(f.BairroId, f.CepInicial, f.CepFinal),
                (paiId, ini, fim, versao) => BairroFaixaCep.Importar(paiId, ini, fim, versao),
                (e, versao) => e.Atualizar(versao),
                fonte.Versao,
                relatorio,
                cancellationToken).ConfigureAwait(false);

            await ImportarGrandesUsuariosAsync(fonte, relatorio, cancellationToken).ConfigureAwait(false);

            await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ResolucaoLocalidades(cidadesPorIdDne, distritosPorIdDne, bairrosPorIdDne);
        }
    }

    // {id_cidade(int4) → Cidade.Id} via JOIN staging (id_cidade→cidade_ibge) × domínio
    // (codigo_ibge→Id). A Cidade não guarda o id4 da DNE (chave natural = codigo_ibge),
    // então a ponte é o código IBGE. Cidades não carregadas no domínio são ignoradas.
    private async Task<Dictionary<int, Guid>> ResolverCidadesPorIdDneAsync(
        IGeoFonteDados fonte,
        CancellationToken cancellationToken)
    {
        Dictionary<string, Guid> ibgeParaGuid =
            await _contexto.Cidades.ToDictionaryAsync(c => c.CodigoIbge, c => c.Id, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);

        Dictionary<int, Guid> mapa = [];
        await foreach (CidadeIdCru cru in fonte.LerCidadeIdsAsync(cancellationToken).ConfigureAwait(false))
        {
            string? codigoIbge = ChaveCodigo(cru.CodigoIbge);
            if (cru.IdCidade is int idCidade && codigoIbge is not null
                && ibgeParaGuid.TryGetValue(codigoIbge, out Guid cidadeGuid))
            {
                mapa[idCidade] = cidadeGuid; // last-wins (id_cidade é PK na fonte)
            }
        }

        return mapa;
    }

    private async Task<Dictionary<int, LocalidadeResolvida>> ImportarLocalidadesAsync<T>(
        string tabela,
        IAsyncEnumerable<LocalidadeCru> crus,
        Dictionary<int, Guid> cidadesPorIdDne,
        DbSet<T> dbSet,
        Func<T, string> chaveExistente,
        CriarLocalidade<T> criar,
        AtualizarLocalidade<T> atualizar,
        string versao,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
        where T : class
    {
        ContadorTabela contador = relatorio.Tabela(tabela);
        Dictionary<string, T> existentes =
            (await dbSet.ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(chaveExistente, StringComparer.Ordinal);
        Dictionary<string, T> jaVistos = new(StringComparer.Ordinal);
        Dictionary<int, LocalidadeResolvida> idDneParaGuid = [];

        await foreach (LocalidadeCru cru in crus.ConfigureAwait(false))
        {
            contador.ContarLido();

            string? nomeNorm = NormalizarObrigatorio(cru.NomeSemAcento ?? cru.Nome);
            if (nomeNorm is null)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            if (cru.CidadeIdDne is not int idCidade || !cidadesPorIdDne.TryGetValue(idCidade, out Guid cidadeGuid))
            {
                contador.ContarOrfao(cru.IdDne?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(sem id)");
                continue;
            }

            // Deriva lat/lon do Point já validado (PontoFactory limita a ±90/±180): evita
            // persistir coordenada fora do domínio ou estourar o numeric(9,6) por dado
            // sujo (ex.: 91 / 1234.5) — mesma estratégia do COPY de logradouro.
            Point? coordenada = PontoFactory.Criar(cru.Lat, cru.Lon);
            decimal? latitude = coordenada is null ? null : (decimal)coordenada.Y;
            decimal? longitude = coordenada is null ? null : (decimal)coordenada.X;
            string nome = cru.Nome ?? string.Empty;
            string uf = cru.Uf ?? string.Empty;
            string? idOrigemDne = cru.IdDne?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string chave = $"{cidadeGuid:N}|{nomeNorm}";

            T? entidade = Upsert(
                chave,
                jaVistos,
                existentes,
                () => criar(cidadeGuid, uf, nome, nomeNorm, latitude, longitude, coordenada, idOrigemDne, versao),
                e => atualizar(e, cidadeGuid, uf, nome, nomeNorm, latitude, longitude, coordenada, idOrigemDne, versao),
                dbSet,
                contador);

            if (entidade is not null && cru.IdDne is int idDne)
            {
                idDneParaGuid[idDne] = new LocalidadeResolvida(IdDe(entidade), cidadeGuid);
            }
        }

        await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return idDneParaGuid;
    }

    private async Task ImportarFaixasAsync<T>(
        string tabela,
        IAsyncEnumerable<FaixaLocalidadeCru> crus,
        Dictionary<int, LocalidadeResolvida> localidadesPorIdDne,
        DbSet<T> dbSet,
        Func<T, string> chaveExistente,
        Func<Guid, string, string, string, Result<T>> criar,
        Func<T, string, Result> atualizar,
        string versao,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
        where T : class
    {
        ContadorTabela contador = relatorio.Tabela(tabela);
        Dictionary<string, T> existentes =
            (await dbSet.ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(chaveExistente, StringComparer.Ordinal);
        Dictionary<string, T> jaVistas = new(StringComparer.Ordinal);

        await foreach (FaixaLocalidadeCru cru in crus.ConfigureAwait(false))
        {
            contador.ContarLido();

            if (cru.IdPaiDne is not int idPai || !localidadesPorIdDne.TryGetValue(idPai, out LocalidadeResolvida pai))
            {
                contador.ContarOrfao(cru.IdPaiDne?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(sem id)");
                continue;
            }

            string? cepInicial = ChaveCodigo(cru.FaixaIni);
            string? cepFinal = ChaveCodigo(cru.FaixaFim);
            if (cepInicial is null || cepFinal is null)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            string chave = ChaveFaixa(pai.Id, cepInicial, cepFinal);
            Upsert(
                chave,
                jaVistas,
                existentes,
                () => criar(pai.Id, cepInicial, cepFinal, versao),
                e => atualizar(e, versao),
                dbSet,
                contador);
        }

        await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ImportarGrandesUsuariosAsync(
        IGeoFonteDados fonte,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
    {
        ContadorTabela contador = relatorio.Tabela("cep_grande_usuario");
        Dictionary<string, CepGrandeUsuario> existentes =
            await _contexto.CepGrandesUsuarios.ToDictionaryAsync(g => g.Cep, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
        Dictionary<string, CepGrandeUsuario> jaVistos = new(StringComparer.Ordinal);

        await foreach (CepGrandeUsuarioCru cru in fonte.LerCepGrandesUsuariosAsync(cancellationToken).ConfigureAwait(false))
        {
            contador.ContarLido();
            string? cep = ChaveCodigo(cru.Cep);
            if (cep is null)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            string nome = cru.Nome ?? string.Empty;
            string? nomeNorm = cru.NomeSemAcento;
            Upsert(
                cep,
                jaVistos,
                existentes,
                () => CepGrandeUsuario.Importar(cep, nome, nomeNorm, fonte.Versao),
                e => e.Atualizar(nome, nomeNorm, fonte.Versao),
                _contexto.CepGrandesUsuarios,
                contador);
        }

        await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // Id é client-generated (Guid v7 no EntityBase), disponível antes do SaveChanges —
    // por isso os dicts de FK já são válidos durante a iteração.
    private static Guid IdDe<T>(T entidade)
        where T : class =>
        ((Unifesspa.UniPlus.Kernel.Domain.Entities.EntityBase)(object)entidade).Id;

    private static string? NormalizarObrigatorio(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : GeoTexto.NormalizarTexto(valor);

    private static async IAsyncEnumerable<LocalidadeCru> ComoLocalidades<TFonte>(
        IAsyncEnumerable<TFonte> fonte,
        Func<TFonte, LocalidadeCru> projetar,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (TFonte item in fonte.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return projetar(item);
        }
    }

    private readonly record struct LocalidadeCru(
        int? IdDne,
        string? Nome,
        string? NomeSemAcento,
        int? CidadeIdDne,
        string? Uf,
        string? Lat,
        string? Lon);

    private delegate Result<T> CriarLocalidade<T>(
        Guid cidadeId, string uf, string nome, string nomeNorm,
        decimal? lat, decimal? lon, Point? coord, string? idOrigemDne, string versao);

    private delegate Result AtualizarLocalidade<T>(
        T entidade, Guid cidadeId, string uf, string nome, string nomeNorm,
        decimal? lat, decimal? lon, Point? coord, string? idOrigemDne, string versao);
}
