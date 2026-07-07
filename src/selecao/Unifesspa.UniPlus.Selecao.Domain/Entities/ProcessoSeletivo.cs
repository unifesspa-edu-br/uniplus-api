namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Agregado-raiz do certame (UNI-REQ-0014/0015): o administrador cria o
/// processo em rascunho e monta a configuração a partir dos cadastros de
/// referência. Todas as entidades de configuração pendem desta raiz e são
/// acessadas e persistidas exclusivamente por ela (repositório único
/// <c>IProcessoSeletivoRepository</c>).
/// </summary>
/// <remarks>
/// <para>
/// F0 entregou a raiz, as etapas pontuadas e a oferta de atendimento
/// especializado; F2 a distribuição de vagas; F3 o bônus regional (RN05) e os
/// critérios de desempate — ambos por referência ao catálogo de regras
/// tipadas (<c>rol_de_regras</c>, Story #772), nunca escalares crus. A
/// classificação (bloco 15º) entra na F4, compondo por referência as
/// dimensões já modeladas.
/// </para>
/// <para>
/// O <c>Edital</c> não é criado aqui: ele é o documento emitido pelo ato de
/// publicação (F5), que congela esta configuração num snapshot imutável
/// (RN08). Enquanto o processo está em rascunho, a configuração é livremente
/// substituível (o comando <c>Definir*</c> troca a coleção inteira). A
/// configuração é CRUD puro via EF Core — a fronteira de Event Sourcing
/// (ADR-0069) começa nos agregados de decisão downstream, nunca aqui.
/// </para>
/// </remarks>
public sealed class ProcessoSeletivo : SoftDeletableEntity
{
    public string Nome { get; private set; } = string.Empty;
    public TipoProcesso Tipo { get; private set; }
    public StatusProcesso Status { get; private set; }

    private readonly List<EtapaProcesso> _etapas = [];
    public IReadOnlyCollection<EtapaProcesso> Etapas => _etapas.AsReadOnly();

    public OfertaAtendimentoEspecializado? OfertaAtendimento { get; private set; }

    private readonly List<ConfiguracaoDistribuicaoVagas> _distribuicaoVagas = [];
    public IReadOnlyCollection<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas => _distribuicaoVagas.AsReadOnly();

    /// <summary>Bônus regional (RN05) — ausência = sem bônus (toggle por presença, INV-B5).</summary>
    public ConfiguracaoBonusRegional? BonusRegional { get; private set; }

    private readonly List<CriterioDesempate> _criteriosDesempate = [];
    public IReadOnlyCollection<CriterioDesempate> CriteriosDesempate => _criteriosDesempate.AsReadOnly();

    /// <summary>Configuração de classificação (15º bloco canônico, Story #775) — compõe por referência a fórmula, precisão, eliminação e ordem de alocação.</summary>
    public ConfiguracaoClassificacao? Classificacao { get; private set; }

    private ProcessoSeletivo() { }

    public static ProcessoSeletivo Criar(string nome, TipoProcesso tipo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (tipo == TipoProcesso.Nenhum)
        {
            throw new ArgumentException("Tipo do processo é obrigatório.", nameof(tipo));
        }

        return new ProcessoSeletivo
        {
            Nome = nome.Trim(),
            Tipo = tipo,
            Status = StatusProcesso.Rascunho,
        };
    }

    /// <summary>
    /// Substitui integralmente as etapas pontuadas do processo. A ordem, o
    /// caráter e o peso definem o divisor da média
    /// (<see cref="CalcularDivisorMedia"/>).
    /// </summary>
    public Result DefinirEtapas(IReadOnlyList<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        if (etapas.Count == 0)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.EtapasVazias",
                "O processo deve ter ao menos uma etapa pontuada."));
        }

        List<int> ordensInformadas = [.. etapas.Where(e => e.Ordem.HasValue).Select(e => e.Ordem!.Value)];
        if (ordensInformadas.Distinct().Count() != ordensInformadas.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.OrdemEtapaDuplicada",
                "Cada etapa deve ter uma ordem única dentro do processo."));
        }

        // Sem ao menos uma etapa que componha a nota, CalcularDivisorMedia()
        // retorna 0 — um processo só com etapas eliminatórias (ou
        // classificatórias sem peso) prepararia divisão por zero na fórmula
        // da nota final (NOTA FINAL = Soma(Etapa×peso) / divisor).
        if (!etapas.Any(e => e.ComponeNota))
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NenhumaEtapaComponeNota",
                "Ao menos uma etapa deve ter caráter classificatória ou ambas, com peso, para compor a nota final."));
        }

        // INV-B6 sobrevive a DefinirEtapas: um critério de desempate
        // DESEMPATE-MAIOR-NOTA-ETAPA já configurado referencia uma etapa pelo
        // Id (dentro do Args); trocar as etapas sem revalidar deixaria o
        // critério apontando para uma etapa removida — desempate inexecutável
        // (achado Codex). Rejeita a troca de etapas em vez de silenciosamente
        // invalidar o desempate; o admin reconfigura o desempate primeiro.
        List<Guid> novosIdsEtapas = [.. etapas.Select(e => e.Id)];
        IEnumerable<(CriterioDesempate Criterio, ArgsDesempateMaiorNotaEtapa Args)> criteriosPorEtapa =
            _criteriosDesempate
                .Where(c => c.Args is ArgsDesempateMaiorNotaEtapa)
                .Select(c => (Criterio: c, Args: (ArgsDesempateMaiorNotaEtapa)c.Args));
        foreach ((CriterioDesempate criterio, ArgsDesempateMaiorNotaEtapa args) in criteriosPorEtapa)
        {
            if (!novosIdsEtapas.Contains(args.EtapaRef))
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.EtapaReferenciadaPorDesempate",
                    $"A etapa {args.EtapaRef} é referenciada por um critério de desempate (ordem {criterio.Ordem}) e não pode ser removida sem antes reconfigurar o desempate."));
            }
        }

        // INV-B4 sobrevive à reconfiguração de etapas: uma classificação já
        // definida referenciando ELIM-NOTA-MINIMA-ETAPA não pode ficar órfã
        // se a etapa referenciada for removida (mesma proteção do INV-B6
        // para critérios de desempate).
        if (Classificacao is not null)
        {
            IEnumerable<ArgsElimNotaMinimaEtapa> eliminacoesPorEtapa = Classificacao.RegrasEliminacao
                .Where(r => r.Args is ArgsElimNotaMinimaEtapa)
                .Select(r => (ArgsElimNotaMinimaEtapa)r.Args);
            foreach (ArgsElimNotaMinimaEtapa args in eliminacoesPorEtapa)
            {
                if (!novosIdsEtapas.Contains(args.EtapaRef))
                {
                    return Result.Failure(new DomainError(
                        "ProcessoSeletivo.EtapaReferenciadaPorClassificacao",
                        $"A etapa {args.EtapaRef} é referenciada por uma regra de eliminação da classificação e não pode ser removida sem antes reconfigurar a classificação."));
                }
            }
        }

        _etapas.Clear();
        foreach (EtapaProcesso etapa in etapas)
        {
            etapa.VincularProcesso(Id);
            _etapas.Add(etapa);
        }

        return Result.Success();
    }

    /// <summary>
    /// Define (ou substitui) a oferta de atendimento especializado do processo.
    /// A invariante ADR-0067 (tipo de deficiência só sob condição PcD) já foi
    /// garantida na montagem da oferta
    /// (<see cref="OfertaAtendimentoEspecializado.Criar"/>).
    /// </summary>
    public Result DefinirOfertaAtendimento(OfertaAtendimentoEspecializado oferta)
    {
        ArgumentNullException.ThrowIfNull(oferta);

        oferta.VincularProcesso(Id);
        OfertaAtendimento = oferta;
        return Result.Success();
    }

    /// <summary>
    /// Substitui integralmente a distribuição de vagas do processo (Story
    /// #773, modelagem P-A): uma <see cref="ConfiguracaoDistribuicaoVagas"/>
    /// por oferta de curso, sem repetir a mesma oferta duas vezes. As
    /// invariantes de cada configuração (PR, referência demográfica,
    /// modalidades federais) já foram validadas em
    /// <see cref="ConfiguracaoDistribuicaoVagas.Criar"/>.
    /// </summary>
    public Result DefinirDistribuicaoVagas(IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoVagas)
    {
        ArgumentNullException.ThrowIfNull(distribuicaoVagas);

        if (distribuicaoVagas.Count == 0)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.DistribuicaoVagasVazia",
                "O processo deve ter ao menos uma distribuição de vagas configurada."));
        }

        List<Guid> ofertasInformadas = [.. distribuicaoVagas.Select(d => d.OfertaCursoOrigemId)];
        if (ofertasInformadas.Distinct().Count() != ofertasInformadas.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.OfertaCursoDuplicada",
                "Cada oferta de curso só pode ter uma distribuição de vagas no processo."));
        }

        _distribuicaoVagas.Clear();
        foreach (ConfiguracaoDistribuicaoVagas configuracao in distribuicaoVagas)
        {
            configuracao.VincularProcesso(Id);
            _distribuicaoVagas.Add(configuracao);
        }

        return Result.Success();
    }

    /// <summary>
    /// Define (ou remove) o bônus regional do processo (RN05). Passar
    /// <see langword="null"/> remove o bônus — a ausência da entidade já é o
    /// toggle "sem bônus" (INV-B5); não existe um "BONUS-NENHUM".
    /// </summary>
    public Result DefinirBonusRegional(ConfiguracaoBonusRegional? bonus)
    {
        if (bonus is null)
        {
            BonusRegional = null;
            return Result.Success();
        }

        bonus.VincularProcesso(Id);
        BonusRegional = bonus;
        return Result.Success();
    }

    /// <summary>
    /// Substitui integralmente os critérios de desempate do processo (Story
    /// #774, modelagem P-B §2.6). Dimensão opcional (0..*): lista vazia
    /// remove todos os critérios. INV-B6: todo <c>etapa_ref</c> referenciado
    /// por um critério <c>DESEMPATE-MAIOR-NOTA-ETAPA</c> precisa existir entre
    /// as etapas deste processo — senão a config congelaria um desempate
    /// inexecutável.
    /// </summary>
    public Result DefinirCriteriosDesempate(IReadOnlyList<CriterioDesempate> criterios)
    {
        ArgumentNullException.ThrowIfNull(criterios);

        List<int> ordensInformadas = [.. criterios.Select(c => c.Ordem)];
        if (ordensInformadas.Distinct().Count() != ordensInformadas.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.OrdemDesempateDuplicada",
                "Cada critério de desempate deve ter uma ordem única dentro do processo."));
        }

        foreach (CriterioDesempate criterio in criterios)
        {
            if (criterio.Args is not ArgsDesempateMaiorNotaEtapa args)
            {
                continue;
            }

            if (!_etapas.Any(e => e.Id == args.EtapaRef))
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.EtapaRefDesempateInexistente",
                    $"O critério de desempate na ordem {criterio.Ordem} referencia a etapa {args.EtapaRef}, que não existe neste processo (INV-B6)."));
            }
        }

        _criteriosDesempate.Clear();
        foreach (CriterioDesempate criterio in criterios)
        {
            criterio.VincularProcesso(Id);
            _criteriosDesempate.Add(criterio);
        }

        return Result.Success();
    }

    /// <summary>
    /// Define (ou substitui) a configuração de classificação do processo
    /// (Story #775, modelagem P-B §2.1). Valida as invariantes que dependem
    /// de OUTRAS dimensões do agregado: INV-B4 (todo <c>etapa_ref</c> de uma
    /// <c>ELIM-NOTA-MINIMA-ETAPA</c> deve existir entre as etapas do
    /// processo) e a restrição de que <c>ELIM-CORTE-REDACAO</c>/
    /// <c>ELIM-ZERO-EM-AREA</c> só se aplicam a processo baseado em ENEM
    /// (INV-B13 parcial — SiSU/PSVR). As invariantes internas da própria
    /// configuração (INV-B8, limites de <c>NOpcoesAlocacao</c>) já foram
    /// validadas em <see cref="ConfiguracaoClassificacao.Criar"/>.
    /// </summary>
    public Result DefinirClassificacao(ConfiguracaoClassificacao classificacao)
    {
        ArgumentNullException.ThrowIfNull(classificacao);

        bool baseadoEmEnem = Tipo is TipoProcesso.SiSU or TipoProcesso.PSVR;

        foreach (RegraEliminacao regra in classificacao.RegrasEliminacao)
        {
            if (regra.Args is ArgsElimNotaMinimaEtapa notaMinima && !_etapas.Any(e => e.Id == notaMinima.EtapaRef))
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
                    $"A regra de eliminação referencia a etapa {notaMinima.EtapaRef}, que não existe neste processo (INV-B4)."));
            }

            bool somenteEnem = regra.Args is ArgsElimCorteRedacao or ArgsElimZeroEmArea;
            if (somenteEnem && !baseadoEmEnem)
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem",
                    $"A regra {regra.Regra.Codigo} só se aplica a processo baseado em ENEM (SiSU/PSVR)."));
            }
        }

        classificacao.VincularProcesso(Id);
        Classificacao = classificacao;
        return Result.Success();
    }

    /// <summary>
    /// Divisor da média da nota final: soma dos pesos das etapas que compõem
    /// a nota (caráter classificatória ou ambas, com peso declarado). Fórmula:
    /// <c>NOTA FINAL = Soma(Etapa × peso) / fator_de_divisão + bônus_regional</c>.
    /// </summary>
    public decimal CalcularDivisorMedia() =>
        _etapas.Where(e => e.ComponeNota).Sum(e => e.Peso!.Value);

    /// <summary>
    /// Aplicabilidade da concorrência dupla (Lei 14.723/2023, INV-B7):
    /// DERIVADA — <see langword="true"/> sse alguma modalidade selecionada em
    /// <see cref="DistribuicaoVagas"/> tem
    /// <see cref="NaturezaLegalModalidade.CotaReservada"/>. Nunca um toggle
    /// livre nem um campo persistido — computada sob demanda a partir do
    /// estado corrente para nunca dessincronizar se a distribuição de vagas
    /// mudar depois de a classificação ter sido configurada.
    /// </summary>
    public bool ConcorrenciaDuplaAplicavel() =>
        _distribuicaoVagas
            .SelectMany(d => d.Modalidades)
            .Any(m => m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada);
}
