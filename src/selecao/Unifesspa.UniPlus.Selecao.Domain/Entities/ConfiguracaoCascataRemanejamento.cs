namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A sequência legal de remanejamento das cotas reservadas de um
/// <see cref="ProcessoSeletivo"/> (Story #575, RN-CASCATA-1..5) — a outra
/// metade da flag <see cref="Enums.RegraRemanejamentoModalidade.SegueCascata"/>
/// de <see cref="ModalidadeSelecionada"/>, que hoje aponta para o nada.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Uma cascata por processo</strong> (não por oferta de curso): a ordem
/// legal não muda de curso para curso, e modelá-la por oferta permitiria duas
/// ofertas do mesmo certame com ordens legais diferentes — estado que a lei não
/// admite. A cobertura (RN-CASCATA-1/2/2b), essa sim, é validada por oferta, em
/// <see cref="ProcessoSeletivo.PendenciaDaCascata"/>.
/// </para>
/// <para>
/// A forma (RN-CASCATA-4) é validada aqui, na borda — ordens únicas/contíguas
/// por origem, sem destino repetido, limites de tamanho. O <em>conteúdo</em>
/// (RN-CASCATA-5 — o payload bate com o <c>esquema_args</c> da regra
/// referenciada) é responsabilidade do handler da Application, que resolve a
/// regra no catálogo antes de montar esta entidade.
/// </para>
/// </remarks>
public sealed partial class ConfiguracaoCascataRemanejamento : EntityBase
{
    /// <summary>Alinhado a <c>ConfiguracaoCascataRemanejamentoConfiguration</c> (varchar(60)).</summary>
    public const int FallbackMaxLength = 60;

    private const int MaxOrigens = 8;
    private const int MaxDestinos = 56;

    [GeneratedRegex("^[A-Z0-9_]+$")]
    private static partial Regex CodigoValido();

    public Guid ProcessoSeletivoId { get; private set; }
    public ReferenciaRegra Regra { get; private set; } = null!;
    public string FallbackCodigo { get; private set; } = string.Empty;

    private readonly List<DestinoRemanejamento> _destinos = [];
    public IReadOnlyCollection<DestinoRemanejamento> Destinos => _destinos.AsReadOnly();

    private ConfiguracaoCascataRemanejamento() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — as
    /// checagens de forma (fallback, limites de origens/destinos) e as de coerência por
    /// origem (ordem duplicada/não contígua, destino repetido) não dependem umas das
    /// outras; um payload que viola várias ao mesmo tempo reporta todas no mesmo lote. As
    /// mensagens não ecoam o código da origem (ADR-0023) — o chamador já sabe qual origem
    /// enviou, porque o <c>field</c> do erro é <c>"destinos"</c> em todos os casos aqui:
    /// nenhuma dessas violações é de um único item da lista (são cruzadas entre itens), então
    /// não há um índice único para apontar.
    /// </summary>
    public static Result<ConfiguracaoCascataRemanejamento> Criar(
        ReferenciaRegra regra, string fallbackCodigo, IReadOnlyList<DestinoRemanejamento> destinos)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(destinos);

        List<FieldError> erros = [];
        ValidarFallback(fallbackCodigo, erros);

        HashSet<string> origens = new(StringComparer.Ordinal);
        foreach (DestinoRemanejamento destino in destinos)
        {
            origens.Add(destino.ModalidadeOrigemCodigo);
        }

        ValidarLimites(destinos.Count, origens.Count, erros);

        foreach (string origem in origens)
        {
            List<DestinoRemanejamento> destinosDaOrigem = [.. destinos
                .Where(d => string.Equals(d.ModalidadeOrigemCodigo, origem, StringComparison.Ordinal))
                .OrderBy(d => d.Ordem)];

            List<int> ordens = [.. destinosDaOrigem.Select(d => d.Ordem)];
            if (ordens.Distinct().Count() != ordens.Count)
            {
                erros.Add(new("destinos", new DomainError(
                    "ConfiguracaoCascataRemanejamento.OrdemDuplicadaNaOrigem",
                    "Uma origem tem mais de um destino na mesma posição de ordem.")));
            }

            for (int indice = 0; indice < ordens.Count; indice++)
            {
                if (ordens[indice] != indice + 1)
                {
                    erros.Add(new("destinos", new DomainError(
                        "ConfiguracaoCascataRemanejamento.OrdemNaoContigua",
                        "Uma origem tem uma sequência de ordens não contígua a partir de 1.")));
                    break;
                }
            }

            List<string> destinosCodigos = [.. destinosDaOrigem.Select(d => d.ModalidadeDestinoCodigo)];
            if (destinosCodigos.Distinct(StringComparer.Ordinal).Count() != destinosCodigos.Count)
            {
                erros.Add(new("destinos", new DomainError(
                    "ConfiguracaoCascataRemanejamento.DestinoDuplicado",
                    "Uma origem repete o mesmo destino mais de uma vez.")));
            }
        }

        if (erros.Count > 0)
        {
            return Result<ConfiguracaoCascataRemanejamento>.ValidationFailure(erros);
        }

        ConfiguracaoCascataRemanejamento cascata = new()
        {
            Regra = regra,
            FallbackCodigo = fallbackCodigo,
        };
        foreach (DestinoRemanejamento destino in destinos)
        {
            destino.VincularCascata(cascata.Id);
            cascata._destinos.Add(destino);
        }

        return Result<ConfiguracaoCascataRemanejamento>.Success(cascata);
    }

    /// <summary>
    /// Fallback e limites de contagem não dependem de os itens terem passado por
    /// <see cref="DestinoRemanejamento.Criar"/> — só do código de fallback cru e da
    /// contagem/origens dos itens do payload, malformados ou não. Quando algum item falha
    /// individualmente, o chamador (o handler) não tem uma <see cref="IReadOnlyList{T}"/> de
    /// <see cref="DestinoRemanejamento"/> válida para passar a <see cref="Criar"/> — mas essas
    /// violações continuam detectáveis e não podem desaparecer do lote só porque outro item
    /// também falhou (achado de revisão).
    /// </summary>
    public static List<FieldError> ValidarFallbackELimitesIndependentesDeItens(
        string? fallbackCodigo, int totalDestinos, IEnumerable<string?> origensCodigosBrutos)
    {
        ArgumentNullException.ThrowIfNull(origensCodigosBrutos);

        List<FieldError> erros = [];
        ValidarFallback(fallbackCodigo, erros);

        int totalOrigens = origensCodigosBrutos
            .Where(codigo => !string.IsNullOrWhiteSpace(codigo))
            .Distinct(StringComparer.Ordinal)
            .Count();
        ValidarLimites(totalDestinos, totalOrigens, erros);

        return erros;
    }

    private static void ValidarFallback(string? fallbackCodigo, List<FieldError> erros)
    {
        if (string.IsNullOrWhiteSpace(fallbackCodigo) || fallbackCodigo.Length > FallbackMaxLength || !CodigoValido().IsMatch(fallbackCodigo))
        {
            erros.Add(new("fallbackCodigo", new DomainError(
                "ConfiguracaoCascataRemanejamento.FallbackObrigatorio",
                $"O código de fallback é obrigatório, precisa casar com ^[A-Z0-9_]+$ e ter no máximo {FallbackMaxLength} caracteres.")));
        }
    }

    private static void ValidarLimites(int totalDestinos, int totalOrigens, List<FieldError> erros)
    {
        if (totalDestinos == 0)
        {
            erros.Add(new("destinos", new DomainError(
                "ConfiguracaoCascataRemanejamento.SemDestinos",
                "A cascata precisa de ao menos um destino.")));
        }

        if (totalOrigens > MaxOrigens)
        {
            erros.Add(new("destinos", new DomainError(
                "ConfiguracaoCascataRemanejamento.ExcedeLimiteDeOrigens",
                $"A cascata não pode declarar mais de {MaxOrigens} origens.")));
        }

        if (totalDestinos > MaxDestinos)
        {
            erros.Add(new("destinos", new DomainError(
                "ConfiguracaoCascataRemanejamento.ExcedeLimiteDeDestinos",
                $"A cascata não pode declarar mais de {MaxDestinos} destinos.")));
        }
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
