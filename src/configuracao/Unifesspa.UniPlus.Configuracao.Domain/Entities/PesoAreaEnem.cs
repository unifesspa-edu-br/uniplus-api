namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Pesos do ENEM por grupo de área (UNI-REQ-0066, módulo Configuração) —
/// materializa o Anexo I da Resolução INEP/ENEM 805/2024: para cada grupo de
/// área (<see cref="GrupoCurso"/>), os pesos das cinco áreas de conhecimento
/// (Redação, Ciências da Natureza, Ciências Humanas, Linguagens e Códigos,
/// Matemática) e a nota mínima de redação (corte) que pode eliminar o candidato.
/// </summary>
/// <remarks>
/// <para>Versionável por <c>Resolucao</c>: cada resolução do INEP gera quatro
/// linhas (uma por grupo). A chave de negócio é o par
/// (<c>Resolucao</c>, <c>GrupoCurso</c>), único entre linhas vivas — validado
/// pelo handler e reforçado por índice único parcial de banco
/// (<c>WHERE is_deleted = false</c>). O par e o <c>Id</c> são imutáveis na
/// atualização (CA-04b).</para>
/// <para>Dado institucional de referência, sem PII (LGPD inaplicável). Nenhuma
/// FK aponta para este cadastro: a ligação <c>curso.grupo_area_enem</c> é por
/// valor sobre o vocabulário de grupos, e o congelamento no bloco de
/// classificação do snapshot (módulo Selecao, ADR-0061) é cópia por valor — por
/// isso a remoção lógica nunca é bloqueada por referência.</para>
/// </remarks>
public sealed class PesoAreaEnem : SoftDeletableEntity, IAuditableEntity
{
    private const int ResolucaoMinLength = 1;
    private const int ResolucaoMaxLength = 40;
    private const int BaseLegalMaxLength = 500;

    /// <summary>Escala persistida dos pesos (<c>numeric(4,2)</c>).</summary>
    private const int EscalaPeso = 2;

    /// <summary>Escala persistida do corte de redação (<c>numeric(6,3)</c>).</summary>
    private const int EscalaCorte = 3;

    /// <summary>Corte de redação padrão (Res. 805/2024, Anexo I) assumido quando omitido.</summary>
    public const decimal CorteRedacaoPadrao = 400m;

    public string Resolucao { get; private set; } = string.Empty;
    public GrupoCurso GrupoCurso { get; private set; } = null!;
    public decimal PesoRedacao { get; private set; }
    public decimal PesoCienciasNatureza { get; private set; }
    public decimal PesoCienciasHumanas { get; private set; }
    public decimal PesoLinguagens { get; private set; }
    public decimal PesoMatematica { get; private set; }
    public decimal CorteRedacao { get; private set; }
    public string BaseLegal { get; private set; } = string.Empty;

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private PesoAreaEnem()
    {
    }

    /// <summary>
    /// Cria uma nova linha de pesos do ENEM. Valida a resolução, o grupo de área
    /// (domínio fechado), a não-negatividade dos cinco pesos e do corte de
    /// redação (que assume <see cref="CorteRedacaoPadrao"/> quando omitido) e a
    /// base legal. A unicidade do par (<paramref name="resolucao"/>,
    /// <paramref name="grupoCurso"/>) entre linhas vivas é responsabilidade do handler.
    /// </summary>
    public static Result<PesoAreaEnem> Criar(
        string resolucao,
        string grupoCurso,
        decimal pesoRedacao,
        decimal pesoCienciasNatureza,
        decimal pesoCienciasHumanas,
        decimal pesoLinguagens,
        decimal pesoMatematica,
        decimal? corteRedacao,
        string baseLegal)
    {
        ArgumentNullException.ThrowIfNull(resolucao);
        ArgumentNullException.ThrowIfNull(grupoCurso);
        ArgumentNullException.ThrowIfNull(baseLegal);

        if (string.IsNullOrWhiteSpace(resolucao))
        {
            return Result<PesoAreaEnem>.Failure(new DomainError(
                PesoAreaEnemErrorCodes.ResolucaoObrigatoria,
                "Resolução é obrigatória."));
        }

        if (resolucao.Trim().Length is < ResolucaoMinLength or > ResolucaoMaxLength)
        {
            return Result<PesoAreaEnem>.Failure(new DomainError(
                PesoAreaEnemErrorCodes.ResolucaoTamanho,
                $"Resolução deve ter entre {ResolucaoMinLength} e {ResolucaoMaxLength} caracteres."));
        }

        Result<GrupoCurso> grupo = GrupoCurso.Criar(grupoCurso);
        if (grupo.IsFailure)
        {
            return Result<PesoAreaEnem>.Failure(new DomainError(
                PesoAreaEnemErrorCodes.GrupoCursoInvalido, grupo.Error!.Message));
        }

        Result<decimal> pesosCorte = ValidarPesosCorteEBaseLegal(
            pesoRedacao, pesoCienciasNatureza, pesoCienciasHumanas, pesoLinguagens, pesoMatematica,
            corteRedacao, baseLegal);
        if (pesosCorte.IsFailure)
        {
            return Result<PesoAreaEnem>.Failure(pesosCorte.Error!);
        }

        var peso = new PesoAreaEnem
        {
            Resolucao = resolucao.Trim(),
            GrupoCurso = grupo.Value!,
        };
        peso.AplicarPesos(
            pesoRedacao, pesoCienciasNatureza, pesoCienciasHumanas, pesoLinguagens, pesoMatematica,
            pesosCorte.Value, baseLegal);

        return Result<PesoAreaEnem>.Success(peso);
    }

    /// <summary>
    /// Atualiza os cinco pesos, o corte de redação e a base legal. Nunca altera o
    /// <c>Id</c>, a <c>Resolucao</c> nem o <c>GrupoCurso</c> (chave de negócio
    /// imutável — CA-04b). Revalida a não-negatividade dos pesos e do corte e a
    /// presença da base legal.
    /// </summary>
    public Result Atualizar(
        decimal pesoRedacao,
        decimal pesoCienciasNatureza,
        decimal pesoCienciasHumanas,
        decimal pesoLinguagens,
        decimal pesoMatematica,
        decimal? corteRedacao,
        string baseLegal)
    {
        ArgumentNullException.ThrowIfNull(baseLegal);

        Result<decimal> pesosCorte = ValidarPesosCorteEBaseLegal(
            pesoRedacao, pesoCienciasNatureza, pesoCienciasHumanas, pesoLinguagens, pesoMatematica,
            corteRedacao, baseLegal);
        if (pesosCorte.IsFailure)
        {
            return Result.Failure(pesosCorte.Error!);
        }

        AplicarPesos(
            pesoRedacao, pesoCienciasNatureza, pesoCienciasHumanas, pesoLinguagens, pesoMatematica,
            pesosCorte.Value, baseLegal);

        return Result.Success();
    }

    private void AplicarPesos(
        decimal pesoRedacao,
        decimal pesoCienciasNatureza,
        decimal pesoCienciasHumanas,
        decimal pesoLinguagens,
        decimal pesoMatematica,
        decimal corteRedacao,
        string baseLegal)
    {
        PesoRedacao = Arredondar(pesoRedacao, EscalaPeso);
        PesoCienciasNatureza = Arredondar(pesoCienciasNatureza, EscalaPeso);
        PesoCienciasHumanas = Arredondar(pesoCienciasHumanas, EscalaPeso);
        PesoLinguagens = Arredondar(pesoLinguagens, EscalaPeso);
        PesoMatematica = Arredondar(pesoMatematica, EscalaPeso);
        CorteRedacao = Arredondar(corteRedacao, EscalaCorte);
        BaseLegal = baseLegal.Trim();
    }

    // Valida os cinco pesos (não-negativos), resolve e valida o corte de redação
    // (padrão 400 quando omitido; não-negativo) e a base legal. Devolve o corte resolvido.
    private static Result<decimal> ValidarPesosCorteEBaseLegal(
        decimal pesoRedacao,
        decimal pesoCienciasNatureza,
        decimal pesoCienciasHumanas,
        decimal pesoLinguagens,
        decimal pesoMatematica,
        decimal? corteRedacao,
        string baseLegal)
    {
        DomainError? pesoInvalido =
            ValidarPeso(pesoRedacao, "redação")
            ?? ValidarPeso(pesoCienciasNatureza, "ciências da natureza")
            ?? ValidarPeso(pesoCienciasHumanas, "ciências humanas")
            ?? ValidarPeso(pesoLinguagens, "linguagens e códigos")
            ?? ValidarPeso(pesoMatematica, "matemática");
        if (pesoInvalido is not null)
        {
            return Result<decimal>.Failure(pesoInvalido);
        }

        decimal corte = corteRedacao ?? CorteRedacaoPadrao;
        if (corte < 0)
        {
            return Result<decimal>.Failure(new DomainError(
                PesoAreaEnemErrorCodes.CorteRedacaoNegativo,
                "Corte de redação não pode ser negativo."));
        }

        if (string.IsNullOrWhiteSpace(baseLegal))
        {
            return Result<decimal>.Failure(new DomainError(
                PesoAreaEnemErrorCodes.BaseLegalObrigatoria,
                "Base legal é obrigatória."));
        }

        if (baseLegal.Trim().Length > BaseLegalMaxLength)
        {
            return Result<decimal>.Failure(new DomainError(
                PesoAreaEnemErrorCodes.BaseLegalTamanho,
                $"Base legal deve ter no máximo {BaseLegalMaxLength} caracteres."));
        }

        return Result<decimal>.Success(corte);
    }

    private static DomainError? ValidarPeso(decimal valor, string area) =>
        valor < 0
            ? new DomainError(
                PesoAreaEnemErrorCodes.PesoNegativo,
                $"O peso de {area} não pode ser negativo.")
            : null;

    private static decimal Arredondar(decimal valor, int escala) =>
        Math.Round(valor, escala, MidpointRounding.ToEven);
}
