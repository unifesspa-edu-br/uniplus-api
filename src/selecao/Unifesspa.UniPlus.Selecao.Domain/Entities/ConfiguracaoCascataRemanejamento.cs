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
    private const int FallbackMaxLength = 60;
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

    public static Result<ConfiguracaoCascataRemanejamento> Criar(
        ReferenciaRegra regra, string fallbackCodigo, IReadOnlyList<DestinoRemanejamento> destinos)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(destinos);

        if (string.IsNullOrWhiteSpace(fallbackCodigo) || fallbackCodigo.Length > FallbackMaxLength || !CodigoValido().IsMatch(fallbackCodigo))
        {
            return Result<ConfiguracaoCascataRemanejamento>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.FallbackObrigatorio",
                $"O código de fallback é obrigatório, precisa casar com ^[A-Z0-9_]+$ e ter no máximo {FallbackMaxLength} caracteres."));
        }

        if (destinos.Count == 0)
        {
            return Result<ConfiguracaoCascataRemanejamento>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.SemDestinos",
                "A cascata precisa de ao menos um destino."));
        }

        HashSet<string> origens = new(StringComparer.Ordinal);
        foreach (DestinoRemanejamento destino in destinos)
        {
            origens.Add(destino.ModalidadeOrigemCodigo);
        }

        if (origens.Count > MaxOrigens)
        {
            return Result<ConfiguracaoCascataRemanejamento>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.ExcedeLimiteDeOrigens",
                $"A cascata declara {origens.Count} origens — o máximo é {MaxOrigens}."));
        }

        if (destinos.Count > MaxDestinos)
        {
            return Result<ConfiguracaoCascataRemanejamento>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.ExcedeLimiteDeDestinos",
                $"A cascata declara {destinos.Count} destinos — o máximo é {MaxDestinos}."));
        }

        foreach (string origem in origens)
        {
            List<DestinoRemanejamento> destinosDaOrigem = [.. destinos
                .Where(d => string.Equals(d.ModalidadeOrigemCodigo, origem, StringComparison.Ordinal))
                .OrderBy(d => d.Ordem)];

            List<int> ordens = [.. destinosDaOrigem.Select(d => d.Ordem)];
            if (ordens.Distinct().Count() != ordens.Count)
            {
                return Result<ConfiguracaoCascataRemanejamento>.Failure(new DomainError(
                    "ConfiguracaoCascataRemanejamento.OrdemDuplicadaNaOrigem",
                    $"A origem \"{origem}\" tem mais de um destino na mesma posição de ordem."));
            }

            for (int indice = 0; indice < ordens.Count; indice++)
            {
                if (ordens[indice] != indice + 1)
                {
                    return Result<ConfiguracaoCascataRemanejamento>.Failure(new DomainError(
                        "ConfiguracaoCascataRemanejamento.OrdemNaoContigua",
                        $"A origem \"{origem}\" tem uma sequência de ordens não contígua a partir de 1."));
                }
            }

            List<string> destinosCodigos = [.. destinosDaOrigem.Select(d => d.ModalidadeDestinoCodigo)];
            if (destinosCodigos.Distinct(StringComparer.Ordinal).Count() != destinosCodigos.Count)
            {
                return Result<ConfiguracaoCascataRemanejamento>.Failure(new DomainError(
                    "ConfiguracaoCascataRemanejamento.DestinoDuplicado",
                    $"A origem \"{origem}\" repete o mesmo destino mais de uma vez."));
            }
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

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
