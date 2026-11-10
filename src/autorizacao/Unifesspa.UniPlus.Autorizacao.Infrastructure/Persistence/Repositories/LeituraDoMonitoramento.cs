namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>A leitura do estado da governança de perfis para as métricas: consultas sem rastreamento e sem trava.</summary>
internal sealed class LeituraDoMonitoramento : ILeituraDoMonitoramento
{
    private static readonly TimeSpan HorizonteDoVencimento = TimeSpan.FromHours(48);

    private readonly AutorizacaoDbContext _context;

    public LeituraDoMonitoramento(AutorizacaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<AmostraDaGovernanca> LerAsync(DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        new(
            await VersoesVencendoAsync(agora, cancellationToken).ConfigureAwait(false),
            await _context.EfetivamenteAbertas(agora).LongCountAsync(cancellationToken).ConfigureAwait(false),
            await ReclassificacoesPendentesAsync(agora, cancellationToken).ConfigureAwait(false),
            await DivergenciasDaImplantacaoAtualAsync(cancellationToken).ConfigureAwait(false));

    private async Task<long> VersoesVencendoAsync(DateTimeOffset agora, CancellationToken cancellationToken)
    {
        DateTimeOffset limite = agora + HorizonteDoVencimento;

        return await _context.VersoesDosPerfis.AsNoTracking()
            .LongCountAsync(v => v.Situacao == SituacaoDaVersao.Pendente && v.ExpiraEm > agora && v.ExpiraEm <= limite, cancellationToken)
            .ConfigureAwait(false);
    }

    // A mais recente de cada perfil e permissão que ainda suspende o código em alguma concessão não revogada.
    private async Task<long> ReclassificacoesPendentesAsync(DateTimeOffset agora, CancellationToken cancellationToken)
    {
        IQueryable<CodigoDaConcessao> suspensos = _context.CodigosSuspensosPorReclassificacao(agora);

        return await _context.Reclassificacoes.AsNoTracking()
            .Where(r => !_context.Reclassificacoes.Any(o => o.PerfilId == r.PerfilId && o.Codigo == r.Codigo && o.OrdemNaTrilha > r.OrdemNaTrilha))
            .LongCountAsync(
                r => suspensos.Any(k => k.Codigo == r.Codigo
                    && _context.Concessoes.Any(c => c.Id == k.ConcessaoId && c.PerfilId == r.PerfilId)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<long> DivergenciasDaImplantacaoAtualAsync(CancellationToken cancellationToken) =>
        await _context.DivergenciasDaGovernanca.AsNoTracking()
            .LongCountAsync(
                d => d.ImplantacaoId == _context.ImplantacoesDasPermissoes.OrderByDescending(c => c.Sequencia).Select(c => c.Id).First(),
                cancellationToken)
            .ConfigureAwait(false);
}
