namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um dataset versionado do calendário de dias não úteis (feriados nacionais,
/// estaduais, municipais e recessos institucionais) usado para contar prazos
/// expressos em dias úteis (UNI-REQ-0080/RN13).
/// </summary>
/// <remarks>
/// <para>Reference data versionada por dataset, não por linha: uma correção publica
/// um dataset novo (<see cref="Criar"/>) com a lista completa de dias não úteis — nunca
/// edita a lista de um dataset existente. Não há método <c>Atualizar</c> por design:
/// o cadastro é ou criado (rascunho, <see cref="Vigente"/> = false), ou marcado vigente
/// (<see cref="MarcarVigente"/>), ou removido por soft-delete (quando ainda não vigente
/// — o handler recusa remover o dataset corrente).</para>
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
    private const int DescricaoMaxLength = 200;

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
    public static Result<CalendarioDiasUteis> Criar(string versaoDataset, IReadOnlyCollection<DiaNaoUtilCriacao> diasNaoUteis)
    {
        ArgumentNullException.ThrowIfNull(versaoDataset);
        ArgumentNullException.ThrowIfNull(diasNaoUteis);

        Result<IReadOnlyList<DiaNaoUtilResolvido>> validacao = ValidarCampos(versaoDataset, diasNaoUteis);
        if (validacao.IsFailure)
        {
            return Result<CalendarioDiasUteis>.Failure(validacao.Error!);
        }

        var calendario = new CalendarioDiasUteis
        {
            VersaoDataset = versaoDataset.Trim(),
            Vigente = false,
        };

        foreach (DiaNaoUtilResolvido dia in validacao.Value!)
        {
            calendario._diasNaoUteis.Add(DiaNaoUtil.Abrir(
                calendario.Id, dia.Abrangencia, dia.MunicipioIbge, dia.Data, dia.Descricao));
        }

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

    private static Result<IReadOnlyList<DiaNaoUtilResolvido>> ValidarCampos(
        string versaoDataset, IReadOnlyCollection<DiaNaoUtilCriacao> diasNaoUteis)
    {
        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Falha(CalendarioDiasUteisErrorCodes.VersaoDatasetObrigatoria, "Versão do dataset é obrigatória.");
        }

        if (versaoDataset.Trim().Length is < VersaoDatasetMinLength or > VersaoDatasetMaxLength)
        {
            return Falha(
                CalendarioDiasUteisErrorCodes.VersaoDatasetTamanho,
                $"Versão do dataset deve ter entre {VersaoDatasetMinLength} e {VersaoDatasetMaxLength} caracteres.");
        }

        if (diasNaoUteis.Count == 0)
        {
            return Falha(
                CalendarioDiasUteisErrorCodes.SemDiaNaoUtil,
                "O dataset precisa de ao menos um dia não útil — um calendário vazio não conta nada.");
        }

        var resolvidos = new List<DiaNaoUtilResolvido>(diasNaoUteis.Count);
        var vistos = new HashSet<(DateOnly Data, Abrangencia Abrangencia, string? Municipio)>();

        foreach (DiaNaoUtilCriacao dia in diasNaoUteis)
        {
            if (!Abrangencias.TryAnalisar(dia.Abrangencia, out Abrangencia abrangencia))
            {
                return Falha(
                    CalendarioDiasUteisErrorCodes.AbrangenciaInvalida,
                    $"Abrangência inválida para a data {dia.Data:yyyy-MM-dd}. Deve ser uma de: "
                    + string.Join(", ", Abrangencias.TokensCanonicos) + ".");
            }

            bool temMunicipio = !string.IsNullOrWhiteSpace(dia.MunicipioIbge);
            if (abrangencia == Abrangencia.Municipal && !temMunicipio)
            {
                return Falha(
                    CalendarioDiasUteisErrorCodes.MunicipioIbgeObrigatorioParaMunicipal,
                    $"Código IBGE do município é obrigatório para a data {dia.Data:yyyy-MM-dd} (abrangência municipal).");
            }

            if (abrangencia != Abrangencia.Municipal && temMunicipio)
            {
                return Falha(
                    CalendarioDiasUteisErrorCodes.MunicipioIbgeApenasParaMunicipal,
                    $"Código IBGE do município só se aplica a abrangência municipal (data {dia.Data:yyyy-MM-dd}).");
            }

            if (string.IsNullOrWhiteSpace(dia.Descricao))
            {
                return Falha(
                    CalendarioDiasUteisErrorCodes.DescricaoObrigatoria,
                    $"Descrição é obrigatória para a data {dia.Data:yyyy-MM-dd}.");
            }

            string descricaoNorm = dia.Descricao.Trim();
            if (descricaoNorm.Length > DescricaoMaxLength)
            {
                return Falha(
                    CalendarioDiasUteisErrorCodes.DescricaoTamanho,
                    $"Descrição deve ter no máximo {DescricaoMaxLength} caracteres (data {dia.Data:yyyy-MM-dd}).");
            }

            if (!vistos.Add((dia.Data, abrangencia, dia.MunicipioIbge)))
            {
                return Falha(
                    CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset,
                    $"Data {dia.Data:yyyy-MM-dd} duplicada no dataset (mesma abrangência e município).");
            }

            resolvidos.Add(new DiaNaoUtilResolvido(abrangencia, dia.MunicipioIbge, dia.Data, descricaoNorm));
        }

        return Result<IReadOnlyList<DiaNaoUtilResolvido>>.Success(resolvidos);
    }

    private static Result<IReadOnlyList<DiaNaoUtilResolvido>> Falha(string codigo, string mensagem) =>
        Result<IReadOnlyList<DiaNaoUtilResolvido>>.Failure(new DomainError(codigo, mensagem));
}

/// <summary>
/// Entrada de criação de um <see cref="DiaNaoUtil"/>, usada só por
/// <see cref="CalendarioDiasUteis.Criar"/>. <c>Abrangencia</c> é o token canônico
/// UPPER_SNAKE (ex.: <c>NACIONAL</c>, <c>MUNICIPAL</c>), analisado internamente.
/// </summary>
public sealed record DiaNaoUtilCriacao(string Abrangencia, string? MunicipioIbge, DateOnly Data, string Descricao);

/// <summary>Dia não útil já validado e com os campos analisados/normalizados, pronto para <see cref="DiaNaoUtil.Abrir"/>.</summary>
internal sealed record DiaNaoUtilResolvido(Abrangencia Abrangencia, string? MunicipioIbge, DateOnly Data, string Descricao);
