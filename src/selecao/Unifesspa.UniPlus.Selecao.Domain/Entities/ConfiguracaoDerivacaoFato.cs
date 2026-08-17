namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A configuração da derivação de um fato num processo (Story #927): o código do fato derivado e a
/// lista de regras que o resolvem. É a forma persistida da regra que o binding
/// <c>REGRA_DERIVACAO:{codigoDoFato}</c> referencia.
/// </summary>
/// <remarks>
/// <para>
/// Só é editável antes da primeira publicação (processo em rascunho), como a coleta de fatos: a
/// regra ainda não entra no envelope congelado nem na restauração, e permitir a edição sob
/// retificação criaria estado mutável fora da configuração congelada. A edição sob retificação,
/// junto do congelamento da regra no envelope, é entregue na story do congelamento conjunto.
/// </para>
/// <para>
/// A validação aqui é <b>estrutural</b> — forma das condições, unicidade da ordem das regras. A
/// validação <b>semântica</b> (o fato alvo é derivado com binding de regra; os fatos citados e o
/// código contribuído pertencem ao vocabulário e ao domínio) depende de dados cross-módulo e é da
/// Application, no comando que define a regra.
/// </para>
/// </remarks>
public sealed class ConfiguracaoDerivacaoFato : EntityBase
{
    private readonly List<RegraDerivacaoConfigurada> _regras = [];

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Código do fato derivado que estas regras resolvem.</summary>
    public string CodigoFato { get; private set; } = string.Empty;

    public IReadOnlyCollection<RegraDerivacaoConfigurada> Regras => _regras.AsReadOnly();

    /// <summary>
    /// Códigos dos fatos citados nos predicados <c>quando</c> de todas as regras, sem repetição — a
    /// lista de dependências da derivação. Recomputada dos citados, nunca persistida (§927), e usada
    /// como aresta de <see cref="Enums.TipoArestaGrafo.Derivacao"/> no grafo conjunto (§928, §6).
    /// </summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. _regras
            .SelectMany(static r => r.Condicoes)
            .Select(static c => c.Fato)
            .Distinct(StringComparer.Ordinal)];

    private ConfiguracaoDerivacaoFato() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — código do
    /// fato, presença de regras e unicidade de ordem não dependem uns dos outros. A mensagem de
    /// ordem duplicada não ecoa mais a ordem nem o código do fato (ADR-0023).
    /// </summary>
    public static Result<ConfiguracaoDerivacaoFato> Criar(
        string codigoFato,
        IReadOnlyList<RegraDerivacaoConfigurada>? regras)
    {
        IReadOnlyList<RegraDerivacaoConfigurada> lista = regras ?? [];
        List<FieldError> erros = ValidarFormaBasica(codigoFato, lista.Count, lista.Select(static r => r.Ordem));
        if (erros.Count > 0)
        {
            return Result<ConfiguracaoDerivacaoFato>.ValidationFailure(erros);
        }

        ConfiguracaoDerivacaoFato config = new() { CodigoFato = codigoFato.Trim() };
        foreach (RegraDerivacaoConfigurada regra in lista)
        {
            regra.VincularConfiguracao(config.Id);
            config._regras.Add(regra);
        }

        return Result<ConfiguracaoDerivacaoFato>.Success(config);
    }

    /// <summary>
    /// Código do fato, presença de regras e unicidade de ordem não dependem do vocabulário
    /// cross-módulo nem das regras já resolvidas — só do código cru e da contagem/ordens brutas
    /// do payload. Existe separada para o handler poder confirmar a forma de TODAS as
    /// configurações numa primeira passada, antes de resolver o catálogo (mesmo padrão de
    /// <c>FatoColetado.ValidarFormaBasica</c>, PR #1214).
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(string? codigoFato, int totalRegras, IEnumerable<int> ordensBrutas)
    {
        ArgumentNullException.ThrowIfNull(ordensBrutas);

        List<FieldError> erros = [];

        if (string.IsNullOrWhiteSpace(codigoFato))
        {
            erros.Add(new("codigoFato", new DomainError(
                ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoObrigatorio,
                "O código do fato derivado é obrigatório.")));
        }

        if (totalRegras == 0)
        {
            erros.Add(new("regras", new DomainError(
                ConfiguracaoDerivacaoFatoErrorCodes.SemRegras,
                "A derivação de um fato precisa de ao menos uma regra.")));
        }

        List<int> ordens = [.. ordensBrutas];
        if (ordens.Distinct().Count() != ordens.Count)
        {
            erros.Add(new("regras", new DomainError(
                ConfiguracaoDerivacaoFatoErrorCodes.OrdemRegraDuplicada,
                "A ordem de uma regra é usada por mais de uma regra na mesma derivação.")));
        }

        return erros;
    }

    internal void VincularProcessoSeletivo(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Reconstrói o VO <see cref="RegrasDerivacaoFato"/> que o motor consome, contra o domínio de
    /// valores do fato — que, para um fato de escopo-processo como <c>MODALIDADE</c>, é o conjunto
    /// de modalidades ofertadas pelo processo, e não uma lista global do catálogo. O domínio é dado
    /// pelo chamador; a reconstrução valida a coerência (dependências, auto-referência, código
    /// contribuído no domínio) e devolve <see cref="Result{T}"/> — nunca lança.
    /// </summary>
    public Result<RegrasDerivacaoFato> ParaRegrasDerivacao(IReadOnlyCollection<string> dominioContribui)
    {
        ArgumentNullException.ThrowIfNull(dominioContribui);

        List<RegraDerivacao> regras = [];
        foreach (RegraDerivacaoConfigurada regra in _regras.OrderBy(static r => r.Ordem))
        {
            Result<RegraDerivacao> regraResult = regra.ParaRegraDerivacao();
            if (regraResult.IsFailure)
            {
                return Result<RegrasDerivacaoFato>.Failure(regraResult.Error!);
            }

            regras.Add(regraResult.Value!);
        }

        // As dependências são recomputadas da união dos fatos citados — nunca persistidas, para não
        // divergir das regras. A factory do VO valida a coerência que sobra.
        IReadOnlyCollection<string> dependencias =
            [.. regras.SelectMany(static r => r.FatosCitados).Distinct(StringComparer.Ordinal)];

        return RegrasDerivacaoFato.Criar(CodigoFato, regras, dependencias, dominioContribui);
    }
}

/// <summary>Códigos de erro de <see cref="ConfiguracaoDerivacaoFato"/>.</summary>
public static class ConfiguracaoDerivacaoFatoErrorCodes
{
    public const string CodigoFatoObrigatorio = "ConfiguracaoDerivacaoFato.CodigoFatoObrigatorio";
    public const string SemRegras = "ConfiguracaoDerivacaoFato.SemRegras";
    public const string OrdemRegraDuplicada = "ConfiguracaoDerivacaoFato.OrdemRegraDuplicada";
    public const string CodigoFatoDuplicado = "ConfiguracaoDerivacaoFato.CodigoFatoDuplicado";
}
