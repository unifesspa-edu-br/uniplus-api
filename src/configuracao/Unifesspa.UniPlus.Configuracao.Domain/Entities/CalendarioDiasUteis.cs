namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um dataset versionado do calendário de dias não úteis (feriados nacionais,
/// estaduais, municipais e recessos institucionais) usado para contar prazos
/// expressos em dias úteis (recurso administrativo e efeito suspensivo, UNI-REQ-0080). Cada entrada marca o dia civil inteiro, nunca meio
/// expediente (UNI-REQ-0114); ter um dataset vigente é condição para publicar certame
/// cuja contagem distinga dia útil de não útil (UNI-REQ-0116).
/// </summary>
/// <remarks>
/// <para>O dataset nasce completo em <see cref="Criar"/> e admite inclusão pontual de
/// data via <see cref="IncluirDiaNaoUtil"/> — correção ou complemento antes de o
/// calendário ser consolidado em um Processo Seletivo publicado (decisão de PO,
/// api#1458). A operação preserva <see cref="Id"/> e <see cref="VersaoDataset"/> e
/// nunca remove/edita data já cadastrada. Processos Seletivos já publicados não são
/// afetados: eles carregam cópia por valor do calendário congelada na publicação
/// (padrão snapshot-copy, ADR-0061), sem referência viva a este agregado.</para>
/// <para>No máximo um dataset é vigente por vez — a invariante cross-agregado é
/// aplicada pelo handler (que desmarca o vigente anterior na mesma transação) e
/// reforçada por índice único parcial de banco, nunca só por guarda em memória.</para>
/// <para>Motor de contagem de dias úteis (como um instante-âncora em dia não útil se
/// resolve) é escopo futuro do módulo de Recursos — esta entidade só entrega o
/// cadastro e a pergunta "há dataset vigente?".</para>
/// </remarks>
public sealed class CalendarioDiasUteis : SoftDeletableEntity, IAuditableEntity
{
    private const int VersaoDatasetMinLength = 1;
    private const int VersaoDatasetMaxLength = 60;

    private static readonly DomainError DataDuplicada = new(
        CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset,
        "Data duplicada no dataset (mesma abrangência, município e UF).");

    private static readonly DomainError VersaoObrigatoria = new(
        CalendarioDiasUteisErrorCodes.VersaoDatasetObrigatoria, "Versão do dataset é obrigatória.");

    private static readonly DomainError VersaoComTamanhoInvalido = new(
        CalendarioDiasUteisErrorCodes.VersaoDatasetTamanho,
        $"Versão do dataset deve ter entre {VersaoDatasetMinLength} e {VersaoDatasetMaxLength} caracteres.");

    private static readonly DomainError SemDiaNaoUtil = new(
        CalendarioDiasUteisErrorCodes.SemDiaNaoUtil,
        "O dataset precisa de ao menos um dia não útil — um calendário vazio não conta nada.");

    private readonly List<DiaNaoUtil> _diasNaoUteis = [];

    public string VersaoDataset { get; private set; } = string.Empty;
    public bool Vigente { get; private set; }
    public IReadOnlyList<DiaNaoUtil> DiasNaoUteis => _diasNaoUteis.AsReadOnly();

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private CalendarioDiasUteis()
    {
    }

    /// <summary>
    /// Cria um dataset novo, com a lista completa de dias não úteis. Nasce sempre
    /// <b>não vigente</b> — tornar-se o dataset corrente é uma operação explícita
    /// (<see cref="MarcarVigente"/>), nunca implícita na criação. <c>Abrangencia</c>
    /// chega como token canônico UPPER_SNAKE (ex.: <c>NACIONAL</c>) — a análise e a
    /// validação de campo ocorrem aqui, não no chamador.
    /// </summary>
    public static Result<CalendarioDiasUteis> Criar(string? versaoDataset, IReadOnlyList<DiaNaoUtilCriacao?>? diasNaoUteis)
    {
        var calendario = new CalendarioDiasUteis { Vigente = false };
        Result<string> versao = ConferirVersaoDoDataset(versaoDataset);
        FieldError[] recusas = [.. versao.Errors, .. calendario.IncluirDiasDoDataset(diasNaoUteis)];
        if (versao.Value is not { } versaoConferida || recusas.Length > 0)
        {
            return Result<CalendarioDiasUteis>.ValidationFailure(recusas);
        }

        calendario.VersaoDataset = versaoConferida;

        return Result<CalendarioDiasUteis>.Success(calendario);
    }

    /// <summary>
    /// Marca este dataset como o vigente. Falha se já estiver vigente — chamar de
    /// novo sobre o mesmo dataset é erro do caller, não um no-op silencioso. Não
    /// desmarca nenhum outro dataset: essa é responsabilidade do handler, que
    /// precisa localizar o vigente anterior (se houver) na mesma transação.
    /// </summary>
    public Result MarcarVigente()
    {
        if (Vigente)
        {
            return Result.Failure(new DomainError(
                CalendarioDiasUteisErrorCodes.JaVigente,
                "Este dataset já é o vigente."));
        }

        Vigente = true;
        return Result.Success();
    }

    /// <summary>Desmarca este dataset como vigente. Chamado pelo handler sobre o vigente anterior.</summary>
    public void MarcarNaoVigente() => Vigente = false;

    /// <summary>
    /// Inclui uma data não útil neste dataset já existente, preservando
    /// <see cref="Id"/>, <see cref="VersaoDataset"/> e todas as datas já
    /// cadastradas (api#1458). Aplica as mesmas validações de campo usadas em
    /// <see cref="Criar"/> e recusa duplicata contra as datas <b>já persistidas</b>
    /// no agregado, a mesma checagem que <see cref="Criar"/> aplica a cada item
    /// contra os itens já aceitos da lista. A defesa final contra duplicata
    /// sob concorrência real é o índice único de banco (sem token de concorrência
    /// no pai: inserir um filho não muta o pai, então nada aqui depende de
    /// <c>xmin</c>).
    /// </summary>
    public Result<DiaNaoUtil> IncluirDiaNaoUtil(DiaNaoUtilCriacao? item)
    {
        Result<DiaNaoUtilResolvido> conferido = ConferenciaDoDiaNaoUtil.Conferir(item, "item");
        if (conferido.Value is not { } resolvido)
        {
            return Result<DiaNaoUtil>.ValidationFailure(conferido.Errors);
        }

        if (JaCadastrado(resolvido))
        {
            return Result<DiaNaoUtil>.Failure(DataDuplicada);
        }

        return Result<DiaNaoUtil>.Success(Acrescentar(resolvido));
    }

    private bool JaCadastrado(DiaNaoUtilResolvido dia) => _diasNaoUteis.Any(existente =>
        existente.Data == dia.Data
        && existente.Abrangencia == dia.Abrangencia
        && existente.MunicipioIbge == dia.MunicipioIbge
        && existente.Uf == dia.Uf);

    private DiaNaoUtil Acrescentar(DiaNaoUtilResolvido resolvido)
    {
        DiaNaoUtil dia = DiaNaoUtil.Abrir(Id, resolvido);
        _diasNaoUteis.Add(dia);

        return dia;
    }

    /// <summary>
    /// Inclui os dias da lista de criação, acumulando toda recusa em vez de parar na primeira; cada item é rotulado por
    /// posição (<c>diasNaoUteis[i]</c> e seus subcampos). Item recusado não entra no dataset, então não provoca recusa
    /// de repetição derivada.
    /// </summary>
    private List<FieldError> IncluirDiasDoDataset(IReadOnlyList<DiaNaoUtilCriacao?>? dias)
    {
        if (dias is null || dias.Count == 0)
        {
            return [new FieldError("diasNaoUteis", SemDiaNaoUtil)];
        }

        List<FieldError> recusas = [];
        for (int indice = 0; indice < dias.Count; indice++)
        {
            recusas.AddRange(IncluirDiaDoDataset(dias[indice], $"diasNaoUteis[{indice}]").Errors);
        }

        return recusas;
    }

    private Result IncluirDiaDoDataset(DiaNaoUtilCriacao? item, string campo)
    {
        Result<DiaNaoUtilResolvido> conferido = ConferenciaDoDiaNaoUtil.Conferir(item, campo);
        if (conferido.Value is not { } resolvido)
        {
            return Result.ValidationFailure(conferido.Errors);
        }

        if (JaCadastrado(resolvido))
        {
            return Result.ValidationFailure([new FieldError(campo, DataDuplicada)]);
        }

        Acrescentar(resolvido);

        return Result.Success();
    }

    /// <summary>
    /// Confere a versão do dataset: obrigatória e dentro do tamanho, devolvendo-a sem espaços nas pontas.
    /// </summary>
    private static Result<string> ConferirVersaoDoDataset(string? versaoDataset)
    {
        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<string>.ValidationFailure([new FieldError("versaoDataset", VersaoObrigatoria)]);
        }

        string versao = versaoDataset.Trim();
        return versao.Length is < VersaoDatasetMinLength or > VersaoDatasetMaxLength
            ? Result<string>.ValidationFailure([new FieldError("versaoDataset", VersaoComTamanhoInvalido)])
            : Result<string>.Success(versao);
    }
}
