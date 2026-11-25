namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class ProcessoSeletivoRepository : IProcessoSeletivoRepository
{
    private readonly UniPlusDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly BloqueioDeLinhaComEsperaLimitada _bloqueio;

    public ProcessoSeletivoRepository(UniPlusDbContext context, TimeProvider timeProvider, IOptions<TravaDoProcessoOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(opcoes);

        _context = context;
        _timeProvider = timeProvider;
        _bloqueio = new BloqueioDeLinhaComEsperaLimitada(context, opcoes.Value);
    }

    public async Task<ProcessoSeletivo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessosSeletivos
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessoSeletivo>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProcessosSeletivos
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(ProcessoSeletivo entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.ProcessosSeletivos.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(ProcessoSeletivo entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.ProcessosSeletivos.Update(entity);
    }

    public void Remover(ProcessoSeletivo entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.MarkAsDeleted("system", _timeProvider.GetUtcNow());
    }

    public async Task<ProcessoSeletivo?> ObterComConfiguracaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Carregamento de LEITURA, sem lock: o FOR UPDATE é de ObterParaMutacaoAsync. Mantê-lo aqui
        // faria duas consultas GET concorrentes ao mesmo processo se serializarem uma na outra, sem que
        // nenhuma delas escreva coisa alguma.
        return await ComConfiguracao(_context.ProcessosSeletivos)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProcessoSeletivo?> ObterParaMutacaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Lock pessimista da linha raiz do agregado: serializa handlers concorrentes
        // (os Definir*, Publicar e Retificar) que carregam o MESMO processo — sem
        // isso, um Definir* que leu Situacao=Rascunho antes de uma publicação concorrente
        // pode persistir mutação DEPOIS de a versão já ter sido congelada, furando o congelamento dos parâmetros na publicação (UNI-REQ-0019)
        // sem que o guard em memória tenha visibilidade da publicação alheia. O
        // SELECT ... FOR UPDATE roda na MESMA transação ambiente do Wolverine
        // (EnrollDbContextInTransaction) — a segunda transação concorrente bloqueia aqui
        // até a primeira committar ou reverter.
        await TravarAsync(id, cancellationToken).ConfigureAwait(false);

        return await ComConfiguracao(_context.ProcessosSeletivos)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task TravarAsync(Guid id, CancellationToken cancellationToken = default) =>
        _bloqueio.AdquirirAsync(InstrucaoDeBloqueio.ProcessoExclusivo, id, cancellationToken);

    // Todo Include abaixo existe porque, sem ele, a coleção (ou a navegação 0..1) nasce vazia (ou nula) em
    // cada carregamento novo do agregado: a leitura diria que nada foi declarado, as recusas de publicação
    // enxergariam zero itens, e a redefinição faria Clear() numa lista já vazia, deixando as linhas antigas
    // no banco. O AsSplitQuery é obrigatório: o produto cartesiano de todas as coleções num único JOIN
    // explode o tamanho do resultado, e a consulta dividida traz cada coleção em consulta própria.
    private static IQueryable<ProcessoSeletivo> ComConfiguracao(IQueryable<ProcessoSeletivo> query)
    {
        IQueryable<ProcessoSeletivo> carregada = ComEtapas(query);
        carregada = ComAtendimentoEspecializado(carregada);
        carregada = ComVagasEBonusRegional(carregada);
        carregada = ComDivulgacaoETaxaDeInscricao(carregada);
        carregada = ComDesempateEClassificacao(carregada);
        carregada = ComCronogramaDeFases(carregada);
        carregada = ComDocumentosExigidos(carregada);
        carregada = ComColetaDeFatos(carregada);
        carregada = ComTermosEFormularios(carregada);
        carregada = ComRegrasDeDerivacao(carregada);
        carregada = ComCascata(carregada);
        return carregada.AsSplitQuery();
    }

    private static IQueryable<ProcessoSeletivo> ComEtapas(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.Etapas).ThenInclude(e => e.Produtos)
            .Include(p => p.Etapas).ThenInclude(e => e.Bancas)
            .Include(p => p.Etapas).ThenInclude(e => e.Recursos)
            .Include(p => p.Etapas).ThenInclude(e => e.ItensDeAvaliacao)
            .Include(p => p.Etapas).ThenInclude(e => e.EscalaDeAtendimento);

    private static IQueryable<ProcessoSeletivo> ComAtendimentoEspecializado(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.AtendimentoEspecializado!).ThenInclude(o => o.Condicoes)
            .Include(p => p.AtendimentoEspecializado!).ThenInclude(o => o.Recursos)
            .Include(p => p.AtendimentoEspecializado!).ThenInclude(o => o.TiposDeficiencia);

    private static IQueryable<ProcessoSeletivo> ComVagasEBonusRegional(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.VagasOfertadas)
            .Include(p => p.BonusRegional!).ThenInclude(b => b.Municipios);

    private static IQueryable<ProcessoSeletivo> ComDivulgacaoETaxaDeInscricao(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.ConfiguracaoDivulgacao)
            .Include(p => p.ConfiguracaoTaxaInscricao);

    private static IQueryable<ProcessoSeletivo> ComDesempateEClassificacao(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.CriteriosDesempate)
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .Include(p => p.Classificacao!).ThenInclude(c => c.QuadroPesoAreaEnem).ThenInclude(g => g.Areas);

    private static IQueryable<ProcessoSeletivo> ComCronogramaDeFases(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.CronogramaFases).ThenInclude(f => f.RegraRecurso)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.CandidatosAtendidos);

    // RegrasDeComprovacao.DocumentoExigido é preenchido pelo rastreador de alterações a partir da mesma instância
    // trazida por DocumentosExigidos, e por isso não tem Include próprio.
    private static IQueryable<ProcessoSeletivo> ComDocumentosExigidos(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.DocumentosExigidos).ThenInclude(d => d.Condicoes)
            .Include(p => p.DocumentosExigidos).ThenInclude(d => d.BasesLegais)
            .Include(p => p.RegrasDeComprovacao).ThenInclude(n => n.DocumentoExigido)
            .Include(p => p.RegrasDeComprovacao).ThenInclude(n => n.BasesLegais);

    private static IQueryable<ProcessoSeletivo> ComColetaDeFatos(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.Campos).ThenInclude(f => f.Precondicoes)
            .Include(p => p.GruposColetados).ThenInclude(g => g.Subitens)
            .Include(p => p.OpcoesDeclaradas);

    private static IQueryable<ProcessoSeletivo> ComTermosEFormularios(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.TermosExigidos)
            .Include(p => p.Formularios).ThenInclude(f => f.Paginas);

    private static IQueryable<ProcessoSeletivo> ComRegrasDeDerivacao(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.RegrasDerivacao).ThenInclude(c => c.Regras).ThenInclude(r => r.Condicoes);

    private static IQueryable<ProcessoSeletivo> ComCascata(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.Cascata!).ThenInclude(c => c.Destinos);

    public async Task AdicionarVersaoConfiguracaoAsync(VersaoConfiguracao versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(versao);
        await _context.VersoesConfiguracao.AddAsync(versao, cancellationToken).ConfigureAwait(false);
    }

    public async Task<VersaoConfiguracao?> ObterVersaoPorNumeroAsync(Guid processoId, int numero, CancellationToken cancellationToken = default) =>
        await _context.VersoesConfiguracao.AsNoTracking()
            .FirstOrDefaultAsync(versao => versao.ProcessoSeletivoId == processoId && versao.NumeroVersao == numero, cancellationToken)
            .ConfigureAwait(false);

    public async Task<VersaoConfiguracao?> ObterVersaoAtualAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default)
    {
        // A versão corrente é a de maior NÚMERO — não a de maior vigência nem a
        // mais recente por id. A numeração é contígua e monotônica por processo
        // (ux_versoes_configuracao_processo_numero + trigger de sucessão), então
        // o topo da cadeia é inequívoco mesmo se duas versões compartilharem o
        // mesmo instante de vigência (permitido por desenho — ADR-0104).
        return await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoSeletivoId)
            .OrderByDescending(v => v.NumeroVersao)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VersaoConfiguracao>> ObterVersoesAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoSeletivoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ObterAtosCriadoresAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoSeletivoId)
            .OrderBy(v => v.NumeroVersao)
            .Select(v => v.AtoCriadorId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VersaoConfiguracao?> ObterVersaoVigenteAsync(
        Guid processoSeletivoId,
        DateTimeOffset instante,
        CancellationToken cancellationToken = default)
    {
        // O Npgsql só compara timestamptz com DateTimeOffset em UTC; offset diferente falharia na consulta.
        DateTimeOffset instanteUtc = instante.ToUniversalTime();

        // Vigente é a versão de maior vigente_a_partir_de ≤ instante, desempatada pelo número (ADR-0075/0076/0104).
        // Ordena o relógio do sistema, não a data declarada no ato; o empate de instante é permitido, porque
        // Suceder ancora a sucessora no instante da anterior quando o relógio regride, e o número elege a mais nova.
        return await _context.Vigentes(processoSeletivoId, instanteUtc)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VersaoConfiguracao>> ObterVersoesPorAtoCriadorAsync(
        IReadOnlyCollection<Guid> atoCriadorIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(atoCriadorIds);
        if (atoCriadorIds.Count == 0)
        {
            return [];
        }
        Guid[] atos = [.. atoCriadorIds];

        return await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => atos.Contains(v.AtoCriadorId)
                // Processo excluído logicamente não vaza a configuração congelada por nenhuma das portas.
                && _context.Set<ProcessoSeletivo>().Any(p => p.Id == v.ProcessoSeletivoId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<string?> ObterNomeAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default) =>
        _context.ProcessosSeletivos
            .AsNoTracking()
            .Where(p => p.Id == processoSeletivoId)
            .Select(p => p.Nome)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> ObterSiglaDaUnidadeAdministradoraAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default) =>
        _context.ProcessosSeletivos
            .AsNoTracking()
            .Where(p => p.Id == processoSeletivoId)
            .Select(p => p.UnidadeAdministradora.Sigla)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> ObterNomeDaUnidadeAdministradoraAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default) =>
        _context.ProcessosSeletivos
            .AsNoTracking()
            .Where(p => p.Id == processoSeletivoId)
            .Select(p => p.UnidadeAdministradora.Nome)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<UnidadeENomeDoProcesso?> ObterUnidadeENomeAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default) =>
        _context.ProcessosSeletivos
            .AsNoTracking()
            .Where(p => p.Id == processoSeletivoId)
            .Select(p => new UnidadeENomeDoProcesso(p.UnidadeAdministradoraOrigemId, p.Nome))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<EstadoDoProcesso?> ObterEstadoAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default) =>
        _context.ProcessosSeletivos
            .AsNoTracking()
            .Where(p => p.Id == processoSeletivoId)
            .Select(p => new EstadoDoProcesso(
                p.UnidadeAdministradoraOrigemId,
                p.Situacao,
                _context.Set<VersaoConfiguracao>().Any(v => v.ProcessoSeletivoId == p.Id)))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> ExisteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessosSeletivos
            .AsNoTracking()
            .AnyAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> IdentificadorLegivelEmUsoAsync(
        IdentificadorLegivel identificador,
        Guid? excluirId,
        CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters: a unicidade inclui os excluídos logicamente, como o índice único.
        return await _context.ProcessosSeletivos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => excluirId == null || p.Id != excluirId)
            .AnyAsync(p => p.IdentificadorLegivel == identificador, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<ProcessoSeletivo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        IReadOnlyCollection<Guid> unidadesDoRascunho,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unidadesDoRascunho);

        // Keyset bidirecional (ADR-0089); com Guid v7 (ADR-0032) a ordem por Id é cronológica.
        IQueryable<ProcessoSeletivo> query = _context.ProcessosSeletivos.AsNoTracking()
            .Where(p => EstadoDoProcesso.SituacoesDeLeituraAberta.Contains(p.Situacao)
                || (p.Situacao == SituacaoDoProcessoSeletivo.Cancelado
                    && _context.Set<VersaoConfiguracao>().Any(v => v.ProcessoSeletivoId == p.Id))
                || unidadesDoRascunho.Contains(p.UnidadeAdministradoraOrigemId));

        CursorKeysetPage<ProcessoSeletivo> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }
}
