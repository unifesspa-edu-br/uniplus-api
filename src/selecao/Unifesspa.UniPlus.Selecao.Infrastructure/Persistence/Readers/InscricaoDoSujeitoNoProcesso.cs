namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Responde à decisão de autorização se a pessoa tem inscrição enviada e não cancelada no processo. Lê só a existência,
/// pelo par (emissor, sub) que a inscrição guarda do candidato e pelo processo: nenhum dado da inscrição sai daqui.
/// </summary>
public sealed class InscricaoDoSujeitoNoProcesso : IInscricaoDoSujeitoNoProcesso
{
    private readonly SelecaoDbContext _context;

    public InscricaoDoSujeitoNoProcesso(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public Task<bool> TemInscricaoEnviadaAsync(string emissor, string subject, Guid processoId, CancellationToken cancellationToken) =>
        _context.Inscricoes
            .AsNoTracking()
            .AnyAsync(
                i => i.ProcessoSeletivoId == processoId
                    && i.EmissorToken == emissor
                    && i.Sub == subject
                    && i.Situacao != SituacaoInscricao.Rascunho
                    && i.Situacao != SituacaoInscricao.Cancelada,
                cancellationToken);
}
