namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// As inscrições enviadas que escolheram uma edição do ENEM: a edição é a resposta do fato
/// <c>EDICAO_ENEM</c> da própria inscrição, então a consulta não depende de nenhuma tabela nova.
/// </summary>
public sealed class InscritosDaEdicaoDoEnem : IInscritosDaEdicaoDoEnem
{
    private readonly UniPlusDbContext _context;
    private readonly IUniPlusEncryptionService _encryption;

    public InscritosDaEdicaoDoEnem(UniPlusDbContext context, IUniPlusEncryptionService encryption)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(encryption);

        _context = context;
        _encryption = encryption;
    }

    public async Task<IReadOnlyList<InscritoDaEdicao>> ListarAsync(
        Guid processoSeletivoId, string edicaoCodigo, CancellationToken cancellationToken = default)
    {
        var inscritos = await DaEdicao(processoSeletivoId, edicaoCodigo)
            .Select(i => new { i.Id, i.CpfIndice, i.OfertaCursoId, i.OfertaCursoSegundaOpcaoId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. inscritos.Select(static i => new InscritoDaEdicao(
            i.Id,
            IndiceCegoDoCpf.De(i.CpfIndice),
            [.. new[] { i.OfertaCursoId, i.OfertaCursoSegundaOpcaoId }.OfType<Guid>().Distinct()]))];
    }

    public async Task<IReadOnlyList<string>> ListarCpfsAsync(
        Guid processoSeletivoId, string edicaoCodigo, CancellationToken cancellationToken = default)
    {
        Dictionary<Guid, string> cpfs = await CpfDaInscricao
            .LerAsync(
                _context.RespostasDaInscricao.Where(r => DaEdicao(processoSeletivoId, edicaoCodigo).Any(i => i.Id == r.InscricaoId)),
                _encryption,
                cancellationToken)
            .ConfigureAwait(false);

        return [.. cpfs.Values];
    }

    private IQueryable<InscricaoRecord> DaEdicao(Guid processoSeletivoId, string edicaoCodigo) =>
        _context.Inscricoes
            .AsNoTracking()
            .Where(i => i.ProcessoSeletivoId == processoSeletivoId
                && i.Situacao == SituacaoInscricao.Submetida
                && VigenciaDasNotasDoEnem.EdicoesEscolhidas(_context).Any(r => r.InscricaoId == i.Id
                    && r.ValorCodigo == edicaoCodigo));
}
