namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Implementação de <see cref="IFatoCandidatoReader"/> (ADR-0056, ADR-0111) e de
/// <see cref="IFatoCandidatoDoCatalogoReader"/>: leitura direta do catálogo <c>dados_do_candidato</c>
/// (<c>AsNoTracking</c>), a mesma para as duas portas. Sem cache — o catálogo é de baixo volume, e o
/// congelamento por valor no consumidor (ADR-0061) dispensa releitura quente
/// (mesmo padrão do <c>TipoDocumentoReader</c>).
/// </summary>
internal sealed class FatoCandidatoReader : IFatoCandidatoReader, IFatoCandidatoDoCatalogoReader
{
    private readonly UniPlusDbContext _dbContext;

    public FatoCandidatoReader(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FatoCandidatoView>> ListarAsync(
        CancellationToken cancellationToken = default) =>
        ParaViews(await LerTodosAsync(cancellationToken).ConfigureAwait(false));

    async Task<IReadOnlyList<FatoDoCatalogoDto>> IFatoCandidatoDoCatalogoReader.ListarAsync(
        CancellationToken cancellationToken) =>
        ParaFatosDoCatalogo(await LerTodosAsync(cancellationToken).ConfigureAwait(false));

    public async Task<FatoCandidatoView?> ObterPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default)
    {
        FatosLidos? lidos = await LerPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false);
        return lidos is null ? null : ParaViews(lidos).Single();
    }

    async Task<FatoDoCatalogoDto?> IFatoCandidatoDoCatalogoReader.ObterPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken)
    {
        FatosLidos? lidos = await LerPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false);
        return lidos is null ? null : ParaFatosDoCatalogo(lidos).Single();
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

    private async Task<FatosLidos> LerTodosAsync(CancellationToken cancellationToken)
    {
        List<FatoCandidato> fatos = await _dbContext.FatosCandidato
            .AsNoTracking()
            .Include(f => f.ValoresDominioDeclarados)
            .OrderBy(f => f.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, FatoCandidato> porCodigo = fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);
        return new(fatos, porCodigo, await TiposDeficienciaSeAlgumUsaAsync(fatos, cancellationToken).ConfigureAwait(false));
    }

    private async Task<FatosLidos?> LerPorCodigoAsync(string codigo, CancellationToken cancellationToken)
    {
        if (await FatoComValoresAsync(codigo, cancellationToken).ConfigureAwait(false) is not { } fato)
        {
            return null;
        }

        // O agregado expõe os valores do fato de membro, que vem junto.
        Dictionary<string, FatoCandidato> porCodigo = new(StringComparer.Ordinal) { [fato.Codigo] = fato };
        if (fato.FatoDeMembroAgregado is { } membro
            && await FatoComValoresAsync(membro, cancellationToken).ConfigureAwait(false) is { } doMembro)
        {
            porCodigo[doMembro.Codigo] = doMembro;
        }

        return new([fato], porCodigo, await TiposDeficienciaSeAlgumUsaAsync([fato], cancellationToken).ConfigureAwait(false));
    }

    private Task<FatoCandidato?> FatoComValoresAsync(string codigo, CancellationToken cancellationToken) =>
        _dbContext.FatosCandidato
            .AsNoTracking()
            .Include(f => f.ValoresDominioDeclarados)
            .FirstOrDefaultAsync(f => f.Codigo == codigo, cancellationToken);

    private static List<FatoCandidatoView> ParaViews(FatosLidos lidos) =>
        [.. lidos.Fatos.Select(f => ParaView(f, lidos))];

    private static List<FatoDoCatalogoDto> ParaFatosDoCatalogo(FatosLidos lidos) =>
        [.. lidos.Fatos.Select(f => new FatoDoCatalogoDto(ParaView(f, lidos), OperadoresAdmitidosDoFato.De(f)))];

    /// <summary>
    /// Os valores do fato de fonte <see cref="FonteValoresFato.TiposDeficiencia"/>: os tipos do cadastro,
    /// pelo código, com o nome como rótulo, em ordem de código. O tipo removido entra inativo, como o
    /// valor desativado de um fato de valores próprios, para que a condição que já o cita continue
    /// resolvendo; o código recriado vale pelo tipo vivo. <see langword="null"/> quando nenhum fato
    /// lido usa a fonte, ou quando o cadastro está vazio.
    /// </summary>
    private async Task<IReadOnlyList<FatoValorDominioViewItem>?> TiposDeficienciaSeAlgumUsaAsync(
        IEnumerable<FatoCandidato> fatos, CancellationToken cancellationToken)
    {
        if (!fatos.Any(static f => f.FonteValores == FonteValoresFato.TiposDeficiencia))
        {
            return null;
        }

        FatoValorDominioViewItem[] tipos = ComoValoresDoFato(await LerCadastroDeTiposAsync(cancellationToken).ConfigureAwait(false));
        return tipos.Length == 0 ? null : tipos;
    }

    private Task<List<TipoDoCadastro>> LerCadastroDeTiposAsync(CancellationToken cancellationToken) =>
        _dbContext.TiposDeficiencia
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Select(static t => new TipoDoCadastro(t.Codigo, t.Nome, t.IsDeleted, t.DeletedAt))
            .ToListAsync(cancellationToken);

    private static FatoValorDominioViewItem[] ComoValoresDoFato(IEnumerable<TipoDoCadastro> cadastro) =>
        [.. cadastro
            .GroupBy(static t => t.Codigo.Valor, StringComparer.Ordinal)
            .Select(static g => g.FirstOrDefault(static t => !t.Removido) ?? g.MaxBy(static t => t.RemovidoEm)!)
            .OrderBy(static t => t.Codigo.Valor, StringComparer.Ordinal)
            .Select(static (t, ordem) => new FatoValorDominioViewItem(t.Codigo.Valor, t.Nome, ordem, Ativo: !t.Removido))];

    private static FatoCandidatoView ParaView(FatoCandidato f, FatosLidos lidos)
    {
        IReadOnlyList<FatoValorDominioViewItem>? valores = ValoresDoFato(f, lidos);
        return new(
            f.Id, f.Codigo, f.Nome, f.Descricao,
            DominiosFato.ParaTokenCanonico(f.Dominio), OrigensFato.ParaTokenCanonico(f.Origem), CardinalidadesFato.ParaTokenCanonico(f.Cardinalidade),
            valores?.Select(static v => v.Codigo).ToList(),
            f.PontoResolucao, f.Binding, valores, f.FonteValores is { } fonte ? FontesValoresFato.ParaTokenCanonico(fonte) : null,
            f.Ativo, ClassificacoesDeProtecao.ParaTokenCanonico(f.ClassificacaoProtecao), HipotesesLegaisTratamento.ParaTokenCanonico(f.HipoteseLegal), f.FinalidadeTratamento,
            f.Aprovacao.EstaAprovada, f.Formato is { } formato and not FormatoTexto.Nenhum ? FormatosTexto.ParaTokenCanonico(formato) : null,
            EscoposFato.ParaTokenCanonico(f.Escopo), f.ComponenteExibicao is { } componente ? ComponentesDeExibicao.ParaTokenCanonico(componente) : null);
    }

    private static IReadOnlyList<FatoValorDominioViewItem>? ValoresDoFato(FatoCandidato f, FatosLidos lidos) =>
        f.FonteValores == FonteValoresFato.TiposDeficiencia
            ? lidos.TiposDeficiencia
            : ParaValoresDominioDeclarados(VocabularioDoCatalogo.ValoresDe(f, lidos.PorCodigo));

    private static IReadOnlyList<FatoValorDominioViewItem>? ParaValoresDominioDeclarados(
        IReadOnlyCollection<FatoValorDominio> valores) =>
        valores.Count == 0
            ? null
            : [.. valores
                .OrderBy(v => v.Ordem)
                .ThenBy(v => v.Codigo, StringComparer.Ordinal)
                .Select(v => new FatoValorDominioViewItem(v.Codigo, v.Descricao, v.Ordem, v.Ativo, v.Orientacao))];

    private sealed record FatosLidos(
        IReadOnlyList<FatoCandidato> Fatos,
        IReadOnlyDictionary<string, FatoCandidato> PorCodigo,
        IReadOnlyList<FatoValorDominioViewItem>? TiposDeficiencia);

    private sealed record TipoDoCadastro(CodigoTipoDeficiencia Codigo, string Nome, bool Removido, DateTimeOffset? RemovidoEm);
}
