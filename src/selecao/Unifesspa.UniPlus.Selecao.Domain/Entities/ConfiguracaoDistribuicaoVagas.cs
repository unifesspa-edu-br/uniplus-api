namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração de distribuição de vagas de uma oferta de curso dentro do
/// <see cref="ProcessoSeletivo"/> (Story #773, modelagem P-A): os inputs que o
/// admin declara — <see cref="VoBase"/>, <see cref="Pr"/>, a referência à
/// regra de distribuição tipada (<c>rol_de_regras</c>) e, quando aplicável, o
/// snapshot da referência demográfica — mais o subconjunto de
/// <see cref="ModalidadeSelecionada"/> que participa desta oferta.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Não modela o <c>QuadroDeVagas</c>.</strong> O quadro (quantidade
/// de vagas por modalidade) é OUTPUT DERIVADO — resultado de aplicar a regra
/// de distribuição sobre estes inputs — e é responsabilidade do motor de
/// cálculo (incremento futuro), não desta configuração. Esta fatia (F2)
/// congela apenas os inputs necessários para o motor futuro recomputar o
/// quadro de forma idempotente.
/// </para>
/// <para>
/// Entidade interna do agregado <see cref="ProcessoSeletivo"/>: criada,
/// substituída e persistida exclusivamente pela raiz, via
/// <see cref="ProcessoSeletivo.DefinirDistribuicaoVagas"/>.
/// </para>
/// </remarks>
public sealed class ConfiguracaoDistribuicaoVagas : EntityBase
{
    private const decimal PrMinimo = 0.5m;
    private const decimal PrMaximo = 1m;

    public Guid ProcessoSeletivoId { get; private set; }
    public Guid OfertaCursoOrigemId { get; private set; }
    public int VoBase { get; private set; }
    public decimal Pr { get; private set; }
    public ReferenciaRegra RegraDistribuicao { get; private set; } = null!;

    /// <summary>
    /// Snapshot da <c>ReferenciaReservaDemografica</c> (Censo + percentuais) —
    /// obrigatório quando <see cref="RegraDistribuicao"/> é
    /// <see cref="RegraDistribuicaoVagasCodigo.Lei12711"/> (INV-5); ausente na
    /// distribuição institucional (quadro fixo, sem cálculo por percentual).
    /// </summary>
    public ReferenciaReservaDemograficaSnapshot? ReferenciaDemografica { get; private set; }

    private readonly List<ModalidadeSelecionada> _modalidades = [];
    public IReadOnlyCollection<ModalidadeSelecionada> Modalidades => _modalidades.AsReadOnly();

    private ConfiguracaoDistribuicaoVagas() { }

    /// <summary>
    /// Cria a configuração de distribuição de vagas de uma oferta, validando
    /// INV-1 (limites do PR), INV-5 (referência demográfica completa quando
    /// Lei 12.711) e INV-6 (8 modalidades federais + AC obrigatórias quando
    /// Lei 12.711). As invariantes próprias de cada modalidade (INV-2, INV-12,
    /// coerência de composição/remanejamento) já foram validadas em
    /// <see cref="ModalidadeSelecionada.Criar"/>.
    /// </summary>
    public static Result<ConfiguracaoDistribuicaoVagas> Criar(
        Guid ofertaCursoOrigemId,
        int voBase,
        decimal pr,
        ReferenciaRegra regraDistribuicao,
        ReferenciaReservaDemograficaSnapshot? referenciaDemografica,
        IReadOnlyList<ModalidadeSelecionada> modalidades)
    {
        ArgumentNullException.ThrowIfNull(regraDistribuicao);
        ArgumentNullException.ThrowIfNull(modalidades);

        if (voBase <= 0)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.VoBaseInvalido",
                "VO_base deve ser maior que zero."));
        }

        // INV-1: 0,5 ≤ PR ≤ 1 (art. 10, II — piso legal 50%, teto 100%).
        if (pr < PrMinimo || pr > PrMaximo)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.PrForaDoLimite",
                $"PR deve estar entre {PrMinimo:0.0} e {PrMaximo:0.0} (art. 10, II)."));
        }

        if (modalidades.Count == 0)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.ModalidadesVazias",
                "A oferta deve ter ao menos uma modalidade selecionada."));
        }

        List<string> codigosInformados = [.. modalidades.Select(m => m.Codigo)];
        if (codigosInformados.Distinct(StringComparer.Ordinal).Count() != codigosInformados.Count)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada",
                "Cada modalidade só pode ser selecionada uma vez por oferta."));
        }

        // Coerência referencial: a Configuração só prova que os códigos de
        // origem/destino/par/fallback existem globalmente no cadastro — não
        // que participam desta oferta. Uma modalidade cujo cruzamento aponta
        // para um código fora do conjunto selecionado deixaria o motor de
        // vagas/remanejamento futuro apontando para uma modalidade ausente.
        DomainError? erroReferenciaCruzada = ValidarReferenciasCruzadas(modalidades, codigosInformados);
        if (erroReferenciaCruzada is not null)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(erroReferenciaCruzada);
        }

        bool ehLei12711 = regraDistribuicao.Codigo == RegraDistribuicaoVagasCodigo.Lei12711;

        if (ehLei12711)
        {
            // INV-5: referência demográfica completa quando a regra é a Lei 12.711.
            if (referenciaDemografica is null)
            {
                return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                    "ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaObrigatoria",
                    "A distribuição pela Lei 12.711 exige a referência de reserva demográfica (INV-5)."));
            }

            // INV-6: as 8 modalidades federais + AC são obrigatórias.
            IReadOnlyList<string> faltantes = [.. ModalidadesFederaisLei12711.CodigosComAc
                .Where(codigo => !codigosInformados.Contains(codigo, StringComparer.Ordinal))];
            if (faltantes.Count > 0)
            {
                return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                    "ConfiguracaoDistribuicaoVagas.ModalidadesFederaisIncompletas",
                    $"A distribuição pela Lei 12.711 exige as 8 modalidades federais e AC; faltam: {string.Join(", ", faltantes)} (INV-6)."));
            }
        }
        else if (referenciaDemografica is not null)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaIndevida",
                "A referência de reserva demográfica só se aplica à distribuição pela Lei 12.711."));
        }

        ConfiguracaoDistribuicaoVagas configuracao = new()
        {
            OfertaCursoOrigemId = ofertaCursoOrigemId,
            VoBase = voBase,
            Pr = pr,
            RegraDistribuicao = regraDistribuicao,
            ReferenciaDemografica = referenciaDemografica,
        };

        foreach (ModalidadeSelecionada modalidade in modalidades)
        {
            modalidade.VincularConfiguracao(configuracao.Id);
            configuracao._modalidades.Add(modalidade);
        }

        return Result<ConfiguracaoDistribuicaoVagas>.Success(configuracao);
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Valida que todo código de cruzamento (origem de RETIRA_DE, destino de
    /// DESTINO_UNICO, par/fallback de CRUZADO) referencia uma modalidade
    /// presente neste MESMO conjunto selecionado — não apenas uma modalidade
    /// existente no cadastro global. Achado do Codex: sem esta checagem, uma
    /// modalidade poderia apontar para um código nunca selecionado nesta
    /// oferta, deixando o motor de vagas/remanejamento futuro sem a
    /// modalidade referenciada.
    /// </summary>
    private static DomainError? ValidarReferenciasCruzadas(
        IReadOnlyList<ModalidadeSelecionada> modalidades, IReadOnlyList<string> codigosInformados)
    {
        foreach (ModalidadeSelecionada modalidade in modalidades)
        {
            if (modalidade.ComposicaoOrigemCodigo is { } origem && !codigosInformados.Contains(origem, StringComparer.Ordinal))
            {
                return new DomainError(
                    "ConfiguracaoDistribuicaoVagas.ComposicaoOrigemNaoSelecionada",
                    $"Modalidade {modalidade.Codigo} referencia a origem {origem}, que não está selecionada nesta oferta.");
            }

            if (modalidade.RemanejamentoDestino is { } destino && !codigosInformados.Contains(destino, StringComparer.Ordinal))
            {
                return new DomainError(
                    "ConfiguracaoDistribuicaoVagas.RemanejamentoDestinoNaoSelecionado",
                    $"Modalidade {modalidade.Codigo} referencia o destino de remanejamento {destino}, que não está selecionado nesta oferta.");
            }

            if (modalidade.RemanejamentoPar is { } par && !codigosInformados.Contains(par, StringComparer.Ordinal))
            {
                return new DomainError(
                    "ConfiguracaoDistribuicaoVagas.RemanejamentoParNaoSelecionado",
                    $"Modalidade {modalidade.Codigo} referencia o par de remanejamento {par}, que não está selecionado nesta oferta.");
            }

            if (modalidade.RemanejamentoFallback is { } fallback && !codigosInformados.Contains(fallback, StringComparer.Ordinal))
            {
                return new DomainError(
                    "ConfiguracaoDistribuicaoVagas.RemanejamentoFallbackNaoSelecionado",
                    $"Modalidade {modalidade.Codigo} referencia o fallback de remanejamento {fallback}, que não está selecionado nesta oferta.");
            }
        }

        return null;
    }
}
