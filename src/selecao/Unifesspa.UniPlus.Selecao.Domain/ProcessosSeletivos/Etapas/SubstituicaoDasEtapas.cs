namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A decisão de trocar o conjunto de etapas: junta as invariantes do próprio conjunto às dependências
/// que a configuração atual tem das etapas atuais.
/// </summary>
internal sealed class SubstituicaoDasEtapas
{
    private readonly EstadoDaConfiguracao _estado;
    private readonly IReadOnlyList<EtapaProcesso> _novas;
    private readonly EtapasDoProcesso _conjunto;
    private readonly HashSet<Guid> _idsDasNovas;
    private readonly Dictionary<string, FaseCronograma> _fasesPorCodigo;

    internal SubstituicaoDasEtapas(EstadoDaConfiguracao estado, IReadOnlyList<EtapaProcesso> novas)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(novas);

        _estado = estado;
        _novas = novas;
        _conjunto = new EtapasDoProcesso(novas);
        _idsDasNovas = [.. novas.Select(e => e.Id)];
        _fasesPorCodigo = JanelaDaEtapaNaFase.FasesPorCodigo(estado.CronogramaFases);
    }

    /// <summary>
    /// A recusa de trocar o conjunto, na ordem em que orienta quem edita: o conjunto em si, o que a
    /// configuração cita, o cronograma e, por fim, o ciclo recursal, que acumula as violações.
    /// </summary>
    internal Result Conferir()
    {
        DomainError? recusa =
            _conjunto.RecusaDaColecao(_estado.Classificacao)
            ?? EtapaCitadaPeloDesempate()
            ?? EtapaCitadaPelaClassificacao()
            ?? EtapaCitadaPelaExigenciaDocumental()
            ?? EtapaQueMudouDeFaseDaExigencia()
            ?? EtapaSemFaseNoCronograma()
            ?? JanelaDaEtapaNaFase.ViolacaoDaJanelaDaEtapaNaFase(_novas, _fasesPorCodigo);

        return recusa is not null ? Result.Failure(recusa) : ResultadoDoCiclo();
    }

    private Result ResultadoDoCiclo()
    {
        List<FieldError> violacoes = _conjunto.ViolacoesDoCiclo();
        return violacoes.Count > 0 ? Result.ValidationFailure(violacoes) : Result.Success();
    }

    // Trocar as etapas deixaria o critério apontando para uma etapa removida: desempate inexecutável.
    private DomainError? EtapaCitadaPeloDesempate()
    {
        foreach (CriterioDesempate criterio in _estado.CriteriosDesempate)
        {
            if (criterio.Args is ArgsDesempateMaiorNotaEtapa args && !_idsDasNovas.Contains(args.EtapaRef))
            {
                return new DomainError(
                    "ProcessoSeletivo.EtapaReferenciadaPorDesempate",
                    $"A etapa {args.EtapaRef} é referenciada por um critério de desempate (ordem {criterio.Ordem}) e não pode ser removida sem antes reconfigurar o desempate.");
            }
        }

        return null;
    }

    private DomainError? EtapaCitadaPelaClassificacao() =>
        _estado.Classificacao is { } classificacao
            ? EliminacaoQueCitaEtapaRemovida(classificacao) ?? AusenciaNaEtapaQuePassouAEnem(classificacao)
            : null;

    private DomainError? EliminacaoQueCitaEtapaRemovida(ConfiguracaoClassificacao classificacao)
    {
        Guid? etapaOrfa = classificacao.RegrasEliminacao
            .Select(static r => r.Args.EtapaReferenciada())
            .FirstOrDefault(etapaRef => etapaRef is { } id && !_idsDasNovas.Contains(id));
        return etapaOrfa is null
            ? null
            : new DomainError(
                "ProcessoSeletivo.EtapaReferenciadaPorClassificacao",
                $"A etapa {etapaOrfa} é referenciada por uma regra de eliminação da classificação e não pode ser removida sem antes reconfigurar a classificação.");
    }

    // A etapa que continua pode passar a ser a do ENEM, onde a ausência já é a falta em dia de prova.
    private DomainError? AusenciaNaEtapaQuePassouAEnem(ConfiguracaoClassificacao classificacao) =>
        classificacao.RegrasEliminacao
            .Select(r => _conjunto.RecusaDaAusenciaNaEtapaDoEnem(r.Args))
            .FirstOrDefault(static recusa => recusa is not null);

    private DomainError? EtapaCitadaPelaExigenciaDocumental()
    {
        DocumentoExigido? exigenciaOrfa = _estado.DocumentosExigidos
            .FirstOrDefault(d => d.ExigidoNaEtapaId is { } etapaRef && !_idsDasNovas.Contains(etapaRef));
        return exigenciaOrfa is null
            ? null
            : new DomainError(
                "ProcessoSeletivo.EtapaReferenciadaPorExigenciaDocumental",
                $"A etapa {exigenciaOrfa.ExigidoNaEtapaId} coleta o documento {exigenciaOrfa.TipoDocumentoNome} e não pode ser removida sem antes reconfigurar a exigência.");
    }

    private DomainError? EtapaQueMudouDeFaseDaExigencia() =>
        _estado.DocumentosExigidos
            .Select(MudancaDeFaseDaEtapaDe)
            .FirstOrDefault(static recusa => recusa is not null);

    // Manter o id não basta: a exigência declara a fase E a etapa em que o documento é coletado, e só
    // este caminho pode mover a etapa de fase depois de ela já estar referenciada.
    private DomainError? MudancaDeFaseDaEtapaDe(DocumentoExigido exigencia)
    {
        if (exigencia.ExigidoNaEtapaId is not { } etapaRef)
        {
            return null;
        }

        EtapaProcesso? etapaNova = _novas.FirstOrDefault(e => e.Id == etapaRef);
        FaseCronograma? faseDaExigencia = _estado.CronogramaFases.FirstOrDefault(f => f.Id == exigencia.ExigidoNaFaseId);
        return etapaNova is null || faseDaExigencia is null || string.Equals(etapaNova.FaseCodigo, faseDaExigencia.Codigo, StringComparison.Ordinal)
            ? null
            : new DomainError(
                "DocumentoExigido.EtapaNaoPertenceAFase",
                $"A etapa {etapaRef} coleta o documento {exigencia.TipoDocumentoNome} na fase {faseDaExigencia.Codigo} e não pode passar para outra fase sem antes reconfigurar a exigência.");
    }

    // A etapa declara a fase pelo código canônico, e não pelo id, que não sobrevive à reconciliação
    // da restauração. Etapa sem código continua aceita.
    private DomainError? EtapaSemFaseNoCronograma() =>
        _novas
            .Where(e => e.FaseCodigo is { } codigo && !_fasesPorCodigo.ContainsKey(codigo))
            .Select(static e => new DomainError(
                "ProcessoSeletivo.EtapaSemFaseNoCronograma",
                $"A etapa \"{e.Nome}\" declara a fase {e.FaseCodigo}, que não está no cronograma deste processo."))
            .FirstOrDefault();
}
