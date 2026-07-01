namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IModalidadeReader"/> (ADR-0056): leitura direta do
/// banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete por
/// convenção). Sem cache — o cadastro é de baixo volume e o congelamento por valor
/// no consumidor (ADR-0061) dispensa releitura quente (mesmo padrão do
/// <c>TipoDocumentoReader</c>). Ordena por <c>Codigo</c> ascendente.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class ModalidadeReader : IModalidadeReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public ModalidadeReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ModalidadeView>> ListarVivosAsync(
        CancellationToken cancellationToken = default)
    {
        List<Modalidade> entidades = await _dbContext.Modalidades
            .AsNoTracking()
            .OrderBy(m => m.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<ModalidadeView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        Modalidade? entidade = await _dbContext.Modalidades
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static ModalidadeView ParaView(Modalidade m) =>
        new(
            m.Id,
            m.Codigo.Valor,
            m.Descricao,
            NaturezasLegais.ParaTokenCanonico(m.NaturezaLegal),
            ComposicoesVagas.ParaTokenCanonico(m.ComposicaoVagas),
            m.ComposicaoOrigem,
            m.RegraRemanejamento is { } regra ? RegrasRemanejamento.ParaTokenCanonico(regra) : null,
            m.RemanejamentoArgs.Destino,
            m.RemanejamentoArgs.Par,
            m.RemanejamentoArgs.Fallback,
            m.CriteriosCumulativos,
            m.AcaoQuandoIndeferido is { } acao ? AcoesQuandoIndeferido.ParaTokenCanonico(acao) : null,
            m.BaseLegal);
}
