namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Implementação de <see cref="IFatoCandidatoReader"/> (ADR-0056, ADR-0111):
/// leitura direta do catálogo <c>rol_de_fatos_candidato</c> (<c>AsNoTracking</c>). Sem
/// cache — o catálogo é de baixo volume, e o congelamento por valor no consumidor (ADR-0061)
/// dispensa releitura quente
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

        Dictionary<string, FatoCandidato> porCodigo = fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);
        return [.. fatos.Select(f => ParaView(f, porCodigo))];
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

        if (fato is null)
        {
            return null;
        }

        // O agregado expõe os valores do fato de membro, que vem junto.
        Dictionary<string, FatoCandidato> porCodigo = new(StringComparer.Ordinal) { [fato.Codigo] = fato };
        if (fato.FatoDeMembroAgregado is { } membro
            && await _dbContext.FatosCandidato.AsNoTracking().Include(f => f.ValoresDominioDeclarados)
                .FirstOrDefaultAsync(f => f.Codigo == membro, cancellationToken).ConfigureAwait(false) is { } doMembro)
        {
            porCodigo[doMembro.Codigo] = doMembro;
        }

        return ParaView(fato, porCodigo);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<RegraDerivacao>>> ListarRegrasPadraoAsync(
        CancellationToken cancellationToken = default)
    {
        List<FatoCandidato> fatos = await _dbContext.FatosCandidato
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return fatos
            .Where(static f => f.RegrasPadrao.Count > 0 && VinculoDeFato.Usa(f.Binding, VinculoDeFato.RegraDeDerivacao))
            .ToDictionary(static f => f.Codigo, static f => f.RegrasPadrao, StringComparer.Ordinal);
    }

    private static FatoCandidatoView ParaView(FatoCandidato f, IReadOnlyDictionary<string, FatoCandidato> porCodigo)
    {
        IReadOnlyList<FatoValorDominioViewItem>? valoresDominioDeclarados =
            ParaValoresDominioDeclarados(VocabularioDoCatalogo.ValoresDe(f, porCodigo));

        return new(
            f.Id,
            f.Codigo,
            f.Nome,
            f.Descricao,
            DominiosFato.ParaTokenCanonico(f.Dominio),
            OrigensFato.ParaTokenCanonico(f.Origem),
            CardinalidadesFato.ParaTokenCanonico(f.Cardinalidade),
            valoresDominioDeclarados?.Select(static v => v.Codigo).ToList(),
            f.PontoResolucao,
            f.Binding,
            valoresDominioDeclarados,
            f.FonteValores is { } fonte ? FontesValoresFato.ParaTokenCanonico(fonte) : null,
            f.Ativo,
            f.Formato is { } formato and not FormatoTexto.Nenhum ? FormatosTexto.ParaTokenCanonico(formato) : null,
            EscoposFato.ParaTokenCanonico(f.Escopo));
    }

    private static IReadOnlyList<FatoValorDominioViewItem>? ParaValoresDominioDeclarados(
        IReadOnlyCollection<FatoValorDominio> valores) =>
        valores.Count == 0
            ? null
            : [.. valores
                .OrderBy(v => v.Ordem)
                .ThenBy(v => v.Codigo, StringComparer.Ordinal)
                .Select(v => new FatoValorDominioViewItem(v.Codigo, v.Descricao, v.Ordem, v.Ativo, v.Orientacao))];
}
