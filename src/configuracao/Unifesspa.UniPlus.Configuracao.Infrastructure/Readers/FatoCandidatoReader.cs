namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IFatoCandidatoReader"/> (ADR-0056, ADR-0111):
/// leitura direta do catálogo <c>rol_de_fatos_candidato</c> (<c>AsNoTracking</c>). Sem
/// cache — o catálogo é de baixo volume (nove fatos), imutável (seed-governado), e
/// o congelamento por valor no consumidor (ADR-0061) dispensa releitura quente
/// (mesmo padrão do <c>TipoDocumentoReader</c>).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class FatoCandidatoReader : IFatoCandidatoReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public FatoCandidatoReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FatoCandidatoView>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        List<FatoCandidato> fatos = await _dbContext.FatosCandidato
            .AsNoTracking()
            .Include(f => f.ValoresDominioDeclarados)
            .OrderBy(f => f.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. fatos.Select(ParaView)];
    }

    public async Task<FatoCandidatoView?> ObterPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default)
    {
        FatoCandidato? fato = await _dbContext.FatosCandidato
            .AsNoTracking()
            .Include(f => f.ValoresDominioDeclarados)
            .FirstOrDefaultAsync(f => f.Codigo == codigo, cancellationToken)
            .ConfigureAwait(false);

        return fato is null ? null : ParaView(fato);
    }

    private static FatoCandidatoView ParaView(FatoCandidato f)
    {
        IReadOnlyList<FatoValorDominioViewItem>? valoresDominioDeclarados =
            ParaValoresDominioDeclarados(f.ValoresDominioDeclarados);

        return new(
            f.Id,
            f.Codigo,
            f.Nome,
            f.Descricao,
            DominiosFato.ParaTokenCanonico(f.Dominio),
            OrigensFato.ParaTokenCanonico(f.Origem),
            CardinalidadesFato.ParaTokenCanonico(f.Cardinalidade),
            ParaValoresDominio(f.ValoresDominio, valoresDominioDeclarados),
            f.PontoResolucao,
            f.Binding,
            valoresDominioDeclarados);
    }

    private static IReadOnlyList<FatoValorDominioViewItem>? ParaValoresDominioDeclarados(
        IReadOnlyCollection<FatoValorDominio> valores) =>
        valores.Count == 0
            ? null
            : [.. valores
                .OrderBy(v => v.Ordem)
                .ThenBy(v => v.Codigo, StringComparer.Ordinal)
                .Select(v => new FatoValorDominioViewItem(v.Codigo, v.Descricao, v.Ordem, v.Ativo))];

    /// <summary>
    /// Um categórico estático migrado para <c>FatoValorDominio</c> (ex.: <c>COR_RACA</c>,
    /// <c>SEXO</c>, <c>NACIONALIDADE</c>) tem o <c>jsonb</c> legado nulo — projeta os
    /// códigos declarados de volta para <see cref="FatoCandidatoView.ValoresDominio"/>
    /// para que o consumidor cross-módulo continue classificando o fato como categórico
    /// <b>estático</b> (não escopo-processo/dinâmico), preservando o contrato de leitura.
    /// </summary>
    private static IReadOnlyList<string>? ParaValoresDominio(
        IReadOnlyList<string>? valoresDominio, IReadOnlyList<FatoValorDominioViewItem>? valoresDominioDeclarados) =>
        valoresDominio ?? valoresDominioDeclarados?.Select(static v => v.Codigo).ToList();
}
