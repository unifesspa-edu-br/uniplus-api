namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Agregado-raiz do certame (UNI-REQ-0014/0015): o administrador cria o
/// processo em rascunho e monta a configuração a partir dos cadastros de
/// referência. Todas as entidades de configuração pendem desta raiz e são
/// acessadas e persistidas exclusivamente por ela (repositório único
/// <c>IProcessoSeletivoRepository</c>).
/// </summary>
/// <remarks>
/// <para>
/// Esta é a <b>fundação (F0)</b>: entrega a raiz, as etapas pontuadas e a
/// oferta de atendimento especializado. As dimensões que dependem do
/// catálogo de regras tipadas (<c>rol_de_regras</c>) — distribuição de vagas,
/// bônus, critérios de desempate e classificação — entram nas fatias
/// seguintes (F2–F4), referenciando regras versionadas em vez de guardar
/// escalares crus.
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
    /// Divisor da média da nota final: soma dos pesos das etapas que compõem
    /// a nota (caráter classificatória ou ambas, com peso declarado). Fórmula:
    /// <c>NOTA FINAL = Soma(Etapa × peso) / fator_de_divisão + bônus_regional</c>.
    /// </summary>
    public decimal CalcularDivisorMedia() =>
        _etapas.Where(e => e.ComponeNota).Sum(e => e.Peso!.Value);
}
