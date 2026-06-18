namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Resolve um CEP pela cascata determinística (ADR-0090) sobre o
/// <see cref="GeoDbContext"/>, só com reference data vigente (ADR-0092):
/// <list type="number">
/// <item><c>Logradouro</c> pelo CEP — primário (desempate estável) + alternativos;</item>
/// <item><c>CepGrandeUsuario</c> — nome do órgão + cidade/UF da faixa CEP;</item>
/// <item>faixa de cidade (e bairro/distrito mais específicos) que contém o CEP;</item>
/// <item>nada casa → <see langword="null"/> (404 no controller).</item>
/// </list>
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em GeoInfrastructureRegistration.")]
internal sealed class CepReader : ICepReader
{
    private readonly GeoDbContext _dbContext;

    public CepReader(GeoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<CepResolvidoDto?> ResolverAsync(string cep, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cep);

        return await ResolverPorLogradouroAsync(cep, cancellationToken).ConfigureAwait(false)
            ?? await ResolverPorGrandeUsuarioAsync(cep, cancellationToken).ConfigureAwait(false)
            ?? await ResolverPorFaixaAsync(cep, cancellationToken).ConfigureAwait(false);
    }

    // (1) Logradouro pelo CEP. CEP não é único: retorna TODOS, ordenados pela chave de
    // desempate estável (nome_normalizado, distrito_id, bairro_id, Id) — o Id (Guid v7)
    // é o tie-breaker final, garantindo o mesmo primário/ordem entre execuções.
    private async Task<CepResolvidoDto?> ResolverPorLogradouroAsync(string cep, CancellationToken cancellationToken)
    {
        List<CepLogradouroLinha> linhas = await (
            from l in _dbContext.Logradouros.AsNoTracking()
            where l.Vigente && l.Cep == cep
            join c in _dbContext.Cidades.AsNoTracking().Where(c => c.Vigente) on l.CidadeId equals c.Id
            orderby l.NomeNormalizado, l.DistritoId, l.BairroId, l.Id
            select new CepLogradouroLinha(
                l.Tipo,
                l.Nome,
                l.Latitude,
                l.Longitude,
                c.CodigoIbge,
                c.Nome,
                c.Uf,
                _dbContext.Distritos
                    .Where(d => d.Vigente && d.Id == l.DistritoId)
                    .Select(d => (string?)d.Nome)
                    .FirstOrDefault(),
                _dbContext.Bairros
                    .Where(b => b.Vigente && b.Id == l.BairroId)
                    .Select(b => (string?)b.Nome)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (linhas.Count == 0)
        {
            return null;
        }

        CepLogradouroLinha primario = linhas[0];

        // Complemento (lado par/ímpar, faixa) é atributo do CEP, sem FK ao logradouro e
        // potencialmente múltiplo. Só preenche quando há exatamente UM — escolha
        // determinística e auditável; ambíguo (0 ou >1) fica null.
        List<string> complementos = await _dbContext.LogradouroComplementos
            .AsNoTracking()
            .Where(lc => lc.Vigente && lc.Cep == cep)
            .Select(lc => lc.Complemento)
            .Take(2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        string? complemento = complementos.Count == 1 ? complementos[0] : null;

        IReadOnlyList<CandidatoLogradouroDto> alternativos = [.. linhas
            .Skip(1)
            .Select(l => new CandidatoLogradouroDto(l.Tipo, l.Nome, l.BairroNome, l.DistritoNome, l.Latitude, l.Longitude))];

        return new CepResolvidoDto(
            Cep: cep,
            Tipo: primario.Tipo,
            Logradouro: primario.Nome,
            Complemento: complemento,
            Bairro: primario.BairroNome,
            Distrito: primario.DistritoNome,
            Cidade: primario.CidadeNome,
            CodigoIbge: primario.CodigoIbge,
            Uf: primario.Uf,
            Latitude: primario.Latitude,
            Longitude: primario.Longitude,
            NivelResolucao: CepResolucao.NivelLogradouro,
            Origem: CepResolucao.OrigemLogradouro)
        {
            Alternativos = alternativos,
        };
    }

    // (2) CepGrandeUsuario (CEP exclusivo de órgão). O grande usuário só guarda
    // cep+nome — cidade/UF vêm da faixa de cidade que contém o CEP. O nome enriquece
    // o campo Logradouro (o DTO não tem campo dedicado). Sem faixa, não há como
    // territorializar → null (não resolve).
    private async Task<CepResolvidoDto?> ResolverPorGrandeUsuarioAsync(string cep, CancellationToken cancellationToken)
    {
        string? nome = await _dbContext.CepGrandesUsuarios
            .AsNoTracking()
            .Where(g => g.Vigente && g.Cep == cep)
            .Select(g => g.Nome)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (nome is null)
        {
            return null;
        }

        CepTerritorioCidade? cidade = await ResolverCidadePorFaixaAsync(cep, cancellationToken).ConfigureAwait(false);
        if (cidade is null)
        {
            return null;
        }

        return new CepResolvidoDto(
            Cep: cep,
            Tipo: null,
            Logradouro: nome,
            Complemento: null,
            Bairro: null,
            Distrito: null,
            Cidade: cidade.Nome,
            CodigoIbge: cidade.CodigoIbge,
            Uf: cidade.Uf,
            Latitude: cidade.Latitude,
            Longitude: cidade.Longitude,
            NivelResolucao: CepResolucao.NivelCidade,
            Origem: CepResolucao.OrigemGrandeUsuario);
    }

    // (3) Faixa: cidade que contém o CEP; enriquece com bairro/distrito quando houver
    // faixa mais específica. Nível/origem refletem o mais específico (bairro > distrito
    // > cidade). Logradouro ausente é esperado (CEP geral), não erro.
    private async Task<CepResolvidoDto?> ResolverPorFaixaAsync(string cep, CancellationToken cancellationToken)
    {
        CepTerritorioCidade? cidade = await ResolverCidadePorFaixaAsync(cep, cancellationToken).ConfigureAwait(false);
        if (cidade is null)
        {
            return null;
        }

        string? bairroNome = await ResolverNomePorFaixaBairroAsync(cep, cidade.Id, cancellationToken).ConfigureAwait(false);
        string? distritoNome = await ResolverNomePorFaixaDistritoAsync(cep, cidade.Id, cancellationToken).ConfigureAwait(false);

        (string nivel, string origem) = (bairroNome, distritoNome) switch
        {
            (not null, _) => (CepResolucao.NivelBairro, CepResolucao.OrigemFaixaBairro),
            (null, not null) => (CepResolucao.NivelDistrito, CepResolucao.OrigemFaixaDistrito),
            _ => (CepResolucao.NivelCidade, CepResolucao.OrigemFaixaCidade),
        };

        return new CepResolvidoDto(
            Cep: cep,
            Tipo: null,
            Logradouro: null,
            Complemento: null,
            Bairro: bairroNome,
            Distrito: distritoNome,
            Cidade: cidade.Nome,
            CodigoIbge: cidade.CodigoIbge,
            Uf: cidade.Uf,
            Latitude: cidade.Latitude,
            Longitude: cidade.Longitude,
            NivelResolucao: nivel,
            Origem: origem);
    }

    // Faixa de CEP da cidade que contém o CEP. O predicado de range
    // (cep_inicial <= cep <= cep_final) vai em SQL parametrizado (FromSqlInterpolated):
    // comparação de string nativa do PG sobre 8 dígitos zero-padded — sargável e sem
    // depender da tradução de string.Compare. OrderBy por Id torna a escolha
    // determinística mesmo se faixas se sobrepuserem (dado inconsistente da fonte).
    private Task<CepTerritorioCidade?> ResolverCidadePorFaixaAsync(string cep, CancellationToken cancellationToken)
    {
        FormattableString faixaSql =
            $"SELECT * FROM cidade_faixa_cep WHERE vigente AND cep_inicial <= {cep} AND cep_final >= {cep}";

        return (
            from f in _dbContext.CidadeFaixasCep.FromSqlInterpolated(faixaSql).AsNoTracking()
            join c in _dbContext.Cidades.AsNoTracking().Where(c => c.Vigente) on f.CidadeId equals c.Id
            orderby c.Id
            select new CepTerritorioCidade(c.Id, c.CodigoIbge, c.Nome, c.Uf, c.Latitude, c.Longitude))
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Faixa de bairro restrita à cidade já resolvida (a faixa indexa BairroId, mas o
    // bairro carrega CidadeId) + desempate estável — range sobreposto não devolve
    // bairro de outra cidade nem varia entre execuções.
    private Task<string?> ResolverNomePorFaixaBairroAsync(string cep, Guid cidadeId, CancellationToken cancellationToken)
    {
        FormattableString faixaSql =
            $"SELECT * FROM bairro_faixa_cep WHERE vigente AND cep_inicial <= {cep} AND cep_final >= {cep}";

        return (
            from f in _dbContext.BairroFaixasCep.FromSqlInterpolated(faixaSql).AsNoTracking()
            join b in _dbContext.Bairros.AsNoTracking().Where(b => b.Vigente && b.CidadeId == cidadeId) on f.BairroId equals b.Id
            orderby b.NomeNormalizado, b.Id
            select (string?)b.Nome)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<string?> ResolverNomePorFaixaDistritoAsync(string cep, Guid cidadeId, CancellationToken cancellationToken)
    {
        FormattableString faixaSql =
            $"SELECT * FROM distrito_faixa_cep WHERE vigente AND cep_inicial <= {cep} AND cep_final >= {cep}";

        return (
            from f in _dbContext.DistritoFaixasCep.FromSqlInterpolated(faixaSql).AsNoTracking()
            join d in _dbContext.Distritos.AsNoTracking().Where(d => d.Vigente && d.CidadeId == cidadeId) on f.DistritoId equals d.Id
            orderby d.NomeNormalizado, d.Id
            select (string?)d.Nome)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Projeções read-only (evitam materializar a entidade inteira).
    private sealed record CepLogradouroLinha(
        string? Tipo,
        string Nome,
        decimal? Latitude,
        decimal? Longitude,
        string CodigoIbge,
        string CidadeNome,
        string Uf,
        string? DistritoNome,
        string? BairroNome);

    private sealed record CepTerritorioCidade(
        Guid Id,
        string CodigoIbge,
        string Nome,
        string Uf,
        decimal? Latitude,
        decimal? Longitude);
}
