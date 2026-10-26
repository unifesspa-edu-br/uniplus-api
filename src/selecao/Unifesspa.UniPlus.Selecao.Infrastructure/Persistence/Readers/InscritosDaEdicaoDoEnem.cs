namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// As inscrições enviadas que escolheram uma edição do ENEM: a edição é a resposta do fato
/// <c>EDICAO_ENEM</c> da própria inscrição, então a consulta não depende de nenhuma tabela nova.
/// </summary>
public sealed class InscritosDaEdicaoDoEnem : IInscritosDaEdicaoDoEnem
{
    private const int GrauDeParalelismo = 16;

    private readonly SelecaoDbContext _context;
    private readonly IUniPlusEncryptionService _encryption;

    public InscritosDaEdicaoDoEnem(SelecaoDbContext context, IUniPlusEncryptionService encryption)
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
        List<byte[]> cifrados = await DaEdicao(processoSeletivoId, edicaoCodigo)
            .Select(i => i.CpfCiphertext)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string[] cpfs = new string[cifrados.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, cifrados.Count),
            new ParallelOptions { MaxDegreeOfParallelism = GrauDeParalelismo, CancellationToken = cancellationToken },
            async (indice, ct) =>
            {
                byte[] plano = await _encryption
                    .DecryptAsync(SelecaoEncryptionKeys.IdentificadoresAesGcm, cifrados[indice], ct)
                    .ConfigureAwait(false);
                cpfs[indice] = System.Text.Encoding.UTF8.GetString(plano);
            }).ConfigureAwait(false);

        return cpfs;
    }

    private IQueryable<InscricaoRecord> DaEdicao(Guid processoSeletivoId, string edicaoCodigo) =>
        _context.Inscricoes
            .AsNoTracking()
            .Where(i => i.ProcessoSeletivoId == processoSeletivoId
                && i.Situacao == SituacaoInscricao.Submetida
                && VigenciaDasNotasDoEnem.EdicoesEscolhidas(_context).Any(r => r.InscricaoId == i.Id
                    && r.ValorCodigo == edicaoCodigo));
}
