namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Globalization;

using Domain.Entities;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class ProcessoSeletivoRepository : IProcessoSeletivoRepository
{
    private readonly SelecaoDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ProcessoSeletivoRepository(SelecaoDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
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
        // Carregamento de LEITURA: sem lock e sem a sessão editorial. O FOR UPDATE que
        // vivia aqui existia para serializar os handlers de mutação — e eles agora têm
        // carregamento próprio (ObterParaMutacaoAsync, ADR-0110 D4). Mantê-lo aqui faria
        // duas consultas GET concorrentes ao mesmo processo se serializarem uma na outra,
        // sem que nenhuma delas escreva coisa alguma.
        return await ComConfiguracao(_context.ProcessosSeletivos)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProcessoSeletivo?> ObterParaMutacaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Lock pessimista da linha raiz do agregado (revisão do PR #791, Story #759 T4):
        // serializa handlers concorrentes (os seis Definir*, a abertura/fechamento da
        // sessão editorial, Publicar e Retificar) que carregam o MESMO processo — sem
        // isso, um Definir* que leu Status=Rascunho antes de uma publicação concorrente
        // pode persistir mutação DEPOIS de a versão já ter sido congelada, furando a RN08
        // sem que o guard em memória tenha visibilidade da publicação alheia. O
        // SELECT ... FOR UPDATE roda na MESMA transação ambiente do Wolverine
        // (EnrollDbContextInTransaction) — a segunda transação concorrente bloqueia aqui
        // até a primeira committar ou reverter.
        await _context.Database
            .ExecuteSqlInterpolatedAsync($"SELECT 1 FROM selecao.processos_seletivos WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        // O Rascunho é o que distingue este carregamento do de leitura: é dele que a
        // allowlist da D4 depende, e um null "por não ter sido carregado" recusaria uma
        // edição legítima.
        return await ComConfiguracao(_context.ProcessosSeletivos)
            .Include(p => p.Rascunho)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static IQueryable<ProcessoSeletivo> ComConfiguracao(IQueryable<ProcessoSeletivo> query) =>
        query
            // Produtos da etapa: MESMO raciocínio do produto da fase abaixo. Sem o
            // ThenInclude, a coleção nasce vazia em todo carregamento novo — o read-back
            // diria que a etapa não publica nada, e a redefinição faria Clear() num backing
            // list já vazio, deixando as linhas antigas no banco.
            .Include(p => p.Etapas).ThenInclude(e => e.Produtos)
            .Include(p => p.Etapas).ThenInclude(e => e.Bancas)
            .Include(p => p.Etapas).ThenInclude(e => e.Recursos)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Condicoes)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Recursos)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.TiposDeficiencia)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.VagasOfertadas)
            // Bônus regional referencia Base Legal tipada (Story #1466) — MESMO raciocínio das
            // demais coleções filhas acima: sem o ThenInclude, o snapshot de municípios nasce
            // sempre vazio em todo carregamento novo do agregado — o GET do processo (CA-04)
            // sempre mentiria "nenhum município", mesmo com linhas persistidas.
            .Include(p => p.BonusRegional!).ThenInclude(b => b.Municipios)
            // Divulgação pública (UNI-REQ-0050, issue #563) — MESMO raciocínio de BonusRegional
            // acima: sem o Include, a navegação 0..1 nasce null em todo carregamento novo do
            // agregado, e o read-back administrativo (ProcessoSeletivoDto) sempre veria ausência.
            .Include(p => p.ConfiguracaoDivulgacao)
            // Taxa de inscrição e isenção (issue #1112) — MESMO raciocínio acima: sem o Include,
            // uma configuração persistida reaparece null na próxima leitura, a publicação
            // recusaria como "não declarada" mesmo já declarada, e o GET administrativo mentiria.
            .Include(p => p.ConfiguracaoTaxaInscricao)
            .Include(p => p.CriteriosDesempate)
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            // Cronograma de fases (Story #851) — 3 coleções novas (produtos publicados,
            // bancas requeridas, regra de recurso 1:1) somadas às já existentes. Sem o
            // Include dos produtos, ProduzResultado — que deriva deles — nasce falso em todo
            // carregamento novo do agregado: o gate de publicação recusaria "nenhuma fase
            // produz resultado" num cronograma que declara vários, e a redefinição faria
            // Clear() num backing list já vazio, deixando as linhas antigas no banco.
            .Include(p => p.CronogramaFases).ThenInclude(f => f.RegraRecurso)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.RecorteDeCompetencia)
            // Documentos exigidos (Story #554, issue #547, PR #895) — sem o Include, a
            // coleção tracked nasce vazia em todo carregamento novo do agregado:
            // DefinirDocumentosExigidos faria Clear() num backing list já vazio (linhas
            // antigas sobrevivem no banco, PUT acumula) e a guarda B-01/CA-01 sempre
            // veria zero exigências. Gatilho DNF (issue #892, PR #896) — mesmo raciocínio
            // para Condicoes: sem o ThenInclude, PendenciaDasExigenciasDocumentais veria
            // sempre zero condições, e CA-01 (GERAL x condição) falharia aberta. Base
            // legal (issue #549, PR #898) — mesmo raciocínio para BasesLegais: sem o
            // ThenInclude, ValidadorBaseLegalExigencias veria sempre zero bases e o 5º
            // item de AvaliarConformidade reprovaria toda exigência que determina
            // resultado, mesmo com bases RESOLVIDO persistidas.
            .Include(p => p.DocumentosExigidos).ThenInclude(d => d.Condicoes)
            .Include(p => p.DocumentosExigidos).ThenInclude(d => d.BasesLegais)
            // Árvore de satisfação (Story #920) — MESMO raciocínio acima: sem o Include, a
            // coleção tracked nasce vazia em todo carregamento novo do agregado.
            // DefinirDocumentosExigidos faria Clear() num backing list já vazio (linhas
            // antigas sobrevivem no banco) e GruposComConsequenciaTemBaseLegalResolvida/os
            // gates de grupo REMOVE_VANTAGEM e PENDENCIA_REENVIO reverso sempre veriam zero
            // nós. NoExigencia.DocumentoExigido é fixed-up automaticamente pelo change
            // tracker a partir da MESMA instância já trazida por DocumentosExigidos acima —
            // sem novo ThenInclude de Condicoes/BasesLegais do documento.
            .Include(p => p.NosExigencia).ThenInclude(n => n.DocumentoExigido)
            .Include(p => p.NosExigencia).ThenInclude(n => n.BasesLegais)
            // Grafo de coleta de fatos (Story #926) — MESMO raciocínio: sem o Include, a coleção
            // tracked nasce vazia e DefinirFatosColetados faria Clear() num backing list já vazio,
            // deixando as linhas antigas no banco. Sem o ThenInclude das pré-condições, o grafo
            // seria reidratado sem aresta nenhuma: todo fato pareceria coletado
            // incondicionalmente, e campos que deveriam ser suprimidos passariam a ser exigidos.
            .Include(p => p.FatosColetados).ThenInclude(f => f.Precondicoes)
            // Regras de derivação (Story #927) — MESMO raciocínio, nos três níveis: sem os Include, a
            // configuração reidrataria sem regra nenhuma (ou sem as condições/o predicado de cada
            // regra), e a substituição por inteiro faria Clear() num backing list vazio, deixando as
            // linhas antigas no banco. O motor produziria conjunto errado ou indeterminado.
            .Include(p => p.RegrasDerivacao).ThenInclude(c => c.Regras).ThenInclude(r => r.Condicoes)
            // Cascata de remanejamento (Story #575) — MESMO raciocínio: sem o Include, a
            // navegação 0..1 nasce null em todo carregamento novo do agregado, e o canonicalizer
            // congelaria uma cascata vazia sem nenhum erro (o defeito que esta story fecha).
            .Include(p => p.Cascata!).ThenInclude(c => c.Destinos)
            // AsSplitQuery obrigatório a partir desta entrega: o produto cartesiano de
            // TODAS as coleções (etapas × condições × recursos × tipos × vagas ×
            // modalidades × desempate × eliminação × fases × bancas) num único JOIN
            // explode o tamanho do resultado — split query traz cada coleção em uma
            // consulta própria.
            .AsSplitQuery();

    public async Task AdicionarVersaoConfiguracaoAsync(VersaoConfiguracao versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(versao);
        await _context.VersoesConfiguracao.AddAsync(versao, cancellationToken).ConfigureAwait(false);
    }

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
        // Npgsql exige DateTimeOffset em UTC (offset zero) ao comparar contra
        // colunas timestamptz — um instante com offset não-UTC (ex.: -03:00
        // vindo de um Accept RFC 3339) falharia na execução da query. Normaliza
        // aqui, no boundary que dona a interação com o Npgsql, preservando o
        // mesmo instante; qualquer chamador fica DB-safe.
        DateTimeOffset instanteUtc = instante.ToUniversalTime();

        // Configuração vigente = VERSÃO de maior vigente_a_partir_de ≤ instante,
        // desempatada pelo número (ADR-0075/0076/0104). Nenhum atributo do ato entra
        // na query: o que ordena é o relógio do sistema, e não a data que o documento
        // declara — que a retificação republica inalterada, e que um acervo migrado
        // pode trazer regredida. O seletor é, por isso, imune a tipos de ato;
        // ix_versoes_configuracao_processo_vigencia o cobre.
        //
        // O empate de instante é permitido por desenho (não há unicidade sobre
        // vigente_a_partir_de): quando o relógio regride, VersaoConfiguracao.Suceder
        // ancora a sucessora no instante da anterior, e é o número decrescente que
        // elege a mais nova.
        //
        // O EXISTS através de ProcessosSeletivos herda o filtro global de
        // soft-delete (ProcessoSeletivo é SoftDeletableEntity; VersaoConfiguracao
        // é forense, sem exclusão lógica própria): um processo excluído
        // logicamente não vaza sua configuração congelada — cai no mesmo caminho
        // 404 que o resto da API, coerente com ExisteAsync.
        return await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoSeletivoId
                && v.VigenteAPartirDe <= instanteUtc
                && _context.ProcessosSeletivos.Any(p => p.Id == processoSeletivoId))
            .OrderByDescending(v => v.VigenteAPartirDe)
            .ThenByDescending(v => v.NumeroVersao)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LinhagemDeVersao>> ObterLinhagemVigenteAsync(
        Guid processoSeletivoId,
        DateTimeOffset instante,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset instanteUtc = instante.ToUniversalTime();

        // Mesma ordenação e mesmo filtro de exclusão lógica de ObterVersaoVigenteAsync — o que muda
        // é que aqui a lista inteira desce, em vez de só o topo: a versão mais nova pode não ter
        // ato registrado, e quem lê precisa do degrau seguinte para não tirar do ar um certame que
        // já é público.
        return await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoSeletivoId
                && v.VigenteAPartirDe <= instanteUtc
                && _context.ProcessosSeletivos.Any(p => p.Id == processoSeletivoId))
            .OrderByDescending(v => v.VigenteAPartirDe)
            .ThenByDescending(v => v.NumeroVersao)
            .Select(v => new LinhagemDeVersao(v.NumeroVersao, v.AtoCriadorId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VersaoConfiguracao?> ObterVersaoPorNumeroAsync(
        Guid processoSeletivoId,
        int numeroVersao,
        CancellationToken cancellationToken = default)
    {
        return await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoSeletivoId
                && v.NumeroVersao == numeroVersao
                && _context.ProcessosSeletivos.Any(p => p.Id == processoSeletivoId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<CandidatoDaVitrine> Itens, DateTimeOffset InstanteEfetivo, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        ListarVitrineAsync(
            DateTimeOffset instanteSeForAPrimeiraPagina,
            SituacaoDoCertame situacao,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            CancellationToken cancellationToken = default)
    {
        DateTimeOffset instanteUtc = (InstanteDaAncora(afterSortKey) ?? instanteSeForAPrimeiraPagina).ToUniversalTime();

        IQueryable<ProcessoSeletivo> query = _context.ProcessosSeletivos
            .AsNoTracking()
            // Janela nula = nunca publicado. É também o que mantém a chave de ordenação não-nula,
            // que o motor de seek exige: com NULL o WHERE do seek não casa e a página vem vazia.
            .Where(p => p.PeriodoInscricaoFimVigente != null);

        query = situacao switch
        {
            SituacaoDoCertame.InscricoesAbertas => query.Where(p => p.PeriodoInscricaoFimVigente >= instanteUtc),
            SituacaoDoCertame.Encerradas => query.Where(p => p.PeriodoInscricaoFimVigente < instanteUtc),
            _ => query,
        };

        // A ordem por urgência é uma ROTAÇÃO da ordem de prazo no ponto `instante`: o booleano põe
        // quem já encerrou depois, e dentro de cada segmento o prazo crescente é a mesma ordem.
        // O identificador fecha a ordem total — sem ele, dois certames que encerram no mesmo
        // instante trocariam de lugar entre páginas.
        //
        // O índice de prazo NÃO serve esta ordenação: a primeira coluna é expressão sobre um
        // parâmetro, e o planner não a casa com uma B-tree sobre a coluna. Ele serve os predicados
        // de recorte por situação, que são faixas sobre a mesma coluna. A ordenação paga uma
        // ordenação em memória sobre o conjunto publicado — aceitável no volume de certames de uma
        // instituição, e o lugar por onde começar se deixar de ser.
        OrderedKeysetPage<ProcessoSeletivo> page = await OrderedKeysetCursor
            .ApplyAsync(
                query,
                b => b
                    .Ascending(p => p.PeriodoInscricaoFimVigente!.Value < instanteUtc)
                    .Ascending(p => p.PeriodoInscricaoFimVigente!.Value)
                    .Ascending(p => p.Id),
                p => SortKeyDaVitrine(p, instanteUtc, situacao),
                (sortKey, id) => AncoraDaVitrine(sortKey, id, situacao),
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        CandidatoDaVitrine[] itens = [.. page.Items.Select(static p => new CandidatoDaVitrine(p.Id, p.Nome))];

        return (itens, instanteUtc, page.Previous, page.Next);
    }

    /// <summary>Instante congelado, recorte e prazo — ver <see cref="SortKeyDaVitrine"/>.</summary>
    private const int PartesDaAncora = 3;

    /// <summary>
    /// Chave de ordenação da âncora: o instante congelado, o recorte e o prazo, nessa ordem. O
    /// prazo vai em forma canônica UTC — ordem lexicográfica e ordem cronológica coincidem nesse
    /// formato, e ele é estável entre culturas.
    /// </summary>
    /// <remarks>
    /// O segmento (aberto/encerrado) <b>não</b> viaja: ele é a comparação do prazo contra o
    /// instante, e os dois já estão aqui. Serializá-lo criaria uma segunda cópia de um fato
    /// derivado, capaz de contradizer a que o seek de fato usa.
    /// </remarks>
    private static string SortKeyDaVitrine(
        ProcessoSeletivo processo,
        DateTimeOffset instanteUtc,
        SituacaoDoCertame situacao)
    {
        DateTimeOffset prazo = processo.PeriodoInscricaoFimVigente!.Value;

        // O instante entra na chave para sobreviver ao percurso. A segmentação entre abertos e
        // encerrados depende dele, e recomputá-lo a cada página moveria de segmento o certame cujo
        // prazo vence no meio da navegação — ele apareceria duas vezes ou sumiria.
        //
        // O recorte entra pela mesma razão que a assinatura de KeysetSort o carrega: a âncora é uma
        // posição DENTRO de um conjunto, e continuar com outro filtro de situação é retomar de uma
        // posição que não existe naquele conjunto — o seek passaria adiante de linhas que deveria
        // devolver, ou não casaria com nenhuma e responderia fim de coleção com itens de sobra.
        return CompositeSortKey.Serialize(
            Instante(instanteUtc),
            RecorteDaVitrine(situacao),
            Instante(prazo));
    }

    /// <summary>Recorte da consulta, tal como viaja na âncora. Nome do membro, não o número.</summary>
    private static string RecorteDaVitrine(SituacaoDoCertame situacao) =>
        situacao.ToString();

    /// <summary>Instante em forma canônica UTC — ordem lexicográfica e cronológica coincidem.</summary>
    private static string Instante(DateTimeOffset valor) =>
        valor.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    /// <summary>
    /// Instante congelado na primeira página, lido de volta da âncora. <see langword="null"/> quando
    /// não há âncora — é a primeira página, e o relógio decide.
    /// </summary>
    private static DateTimeOffset? InstanteDaAncora(string? sortKey)
    {
        if (!CompositeSortKey.TryDeserialize(sortKey, PartesDaAncora, out IReadOnlyList<string> partes)
            || !DateTimeOffset.TryParse(
                partes[0], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTimeOffset instante))
        {
            return null;
        }

        return instante;
    }

    /// <summary>
    /// Remonta a âncora que o motor de seek compara contra as colunas do keyset.
    /// </summary>
    /// <remarks>
    /// <b>Os nomes dos membros não são livres</b>: o motor resolve cada coluna do keyset no objeto
    /// de referência pela MESMA cadeia de propriedades que a consulta usa
    /// (<c>PeriodoInscricaoFimVigente.Value</c> e <c>Id</c>). Um membro com outro nome não é
    /// encontrado e a continuação estoura — por isso a âncora expõe a janela crua, e o segmento
    /// (aberto/encerrado) se deriva dela contra o instante congelado, como na consulta.
    /// </remarks>
    private static object AncoraDaVitrine(string sortKey, Guid id, SituacaoDoCertame situacao)
    {
        if (!CompositeSortKey.TryDeserialize(sortKey, PartesDaAncora, out IReadOnlyList<string> partes)
            || !string.Equals(partes[1], RecorteDaVitrine(situacao), StringComparison.Ordinal)
            || !DateTimeOffset.TryParse(
                partes[2], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTimeOffset prazo))
        {
            throw new CursorAnchorMismatchException("Âncora da vitrine fora da forma esperada.");
        }

        return new { PeriodoInscricaoFimVigente = (DateTimeOffset?)prazo, Id = id };
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<LinhagemDeVersao>>> ObterLinhagensVigentesAsync(
        IReadOnlyCollection<Guid> processoSeletivoIds,
        DateTimeOffset instante,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processoSeletivoIds);

        if (processoSeletivoIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<LinhagemDeVersao>>();
        }

        DateTimeOffset instanteUtc = instante.ToUniversalTime();
        Guid[] ids = [.. processoSeletivoIds];

        var linhas = await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => ids.Contains(v.ProcessoSeletivoId)
                && v.VigenteAPartirDe <= instanteUtc
                && _context.ProcessosSeletivos.Any(p => p.Id == v.ProcessoSeletivoId))
            .OrderByDescending(v => v.VigenteAPartirDe)
            .ThenByDescending(v => v.NumeroVersao)
            .Select(v => new { v.ProcessoSeletivoId, Degrau = new LinhagemDeVersao(v.NumeroVersao, v.AtoCriadorId) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return linhas
            .GroupBy(static l => l.ProcessoSeletivoId)
            .ToDictionary(
                static g => g.Key,
                static g => (IReadOnlyList<LinhagemDeVersao>)[.. g.Select(static l => l.Degrau)]);
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
                // Mesmo filtro de exclusão lógica dos irmãos desta família: processo excluído
                // logicamente não vaza a sua configuração congelada por nenhuma das portas.
                && _context.ProcessosSeletivos.Any(p => p.Id == v.ProcessoSeletivoId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ContadoresDaVitrine> ContarVitrinePorSituacaoAsync(
        DateTimeOffset instante,
        TimeSpan limiarDosUltimosDias,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset instanteUtc = instante.ToUniversalTime();
        DateTimeOffset limiar = instanteUtc + limiarDosUltimosDias;

        // Agrupamento constante para o provider emitir UMA consulta com as três contagens
        // condicionais, em vez de três viagens que poderiam discordar entre si.
        var contagem = await _context.ProcessosSeletivos
            .AsNoTracking()
            .Where(p => p.PeriodoInscricaoFimVigente != null)
            .GroupBy(static _ => 1)
            .Select(g => new
            {
                Abertas = g.Count(p => p.PeriodoInscricaoFimVigente >= limiar),
                UltimosDias = g.Count(p =>
                    p.PeriodoInscricaoFimVigente >= instanteUtc && p.PeriodoInscricaoFimVigente < limiar),
                Encerrados = g.Count(p => p.PeriodoInscricaoFimVigente < instanteUtc),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return contagem is null
            ? new ContadoresDaVitrine(0, 0, 0)
            : new ContadoresDaVitrine(contagem.Abertas, contagem.UltimosDias, contagem.Encerrados);
    }

    public async Task<bool> ExisteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessosSeletivos
            .AsNoTracking()
            .AnyAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<ProcessoSeletivo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        // Keyset bidirecional (ADR-0089): ordenação, âncora, probe n+1, reversão
        // e flags ficam no helper. Com Guid v7 (ADR-0032) a ordem por Id é cronológica.
        IQueryable<ProcessoSeletivo> query = _context.ProcessosSeletivos.AsNoTracking();

        CursorKeysetPage<ProcessoSeletivo> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }
}
