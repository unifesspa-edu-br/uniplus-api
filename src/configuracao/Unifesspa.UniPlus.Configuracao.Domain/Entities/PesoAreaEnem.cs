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
/// atualização — mudar resolução ou grupo caracterizaria outra linha, não uma edição.</para>
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

    /// <summary>Escala persistida do corte de redação (<c>numeric(7,3)</c>).</summary>
    private const int EscalaCorte = 3;

    /// <summary>Corte de redação padrão (Res. 805/2024, Anexo I) assumido quando omitido.</summary>
    public const decimal CorteRedacaoPadrao = 400m;

    /// <summary>
    /// Teto de cada peso de área — limite da precisão persistida (<c>numeric(4,2)</c>).
    /// Um valor acima disso estouraria a coluna; o guard transforma o overflow num
    /// erro de domínio (422) em vez de 500.
    /// </summary>
    public const decimal PesoMaximo = 99.99m;

    /// <summary>
    /// Nota máxima da redação do ENEM (escala 0–1000) — teto do corte de redação.
    /// Acima disso o corte não tem sentido e estouraria a coluna persistida.
    /// </summary>
    public const decimal CorteRedacaoMaximo = 1000m;

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
    /// Cria uma nova linha de pesos do ENEM, acumulando toda violação
    /// independente em vez de parar na primeira. Valida a resolução, o grupo de
    /// área (domínio fechado), a não-negatividade dos cinco pesos e do corte de
    /// redação (que assume <see cref="CorteRedacaoPadrao"/> quando omitido) e a
    /// base legal. A unicidade do par (<paramref name="resolucao"/>,
    /// <paramref name="grupoCurso"/>) entre linhas vivas é responsabilidade do handler.
    /// </summary>
    public static Result<PesoAreaEnem> Criar(
        string? resolucao,
        string? grupoCurso,
        decimal pesoRedacao,
        decimal pesoCienciasNatureza,
        decimal pesoCienciasHumanas,
        decimal pesoLinguagens,
        decimal pesoMatematica,
        decimal? corteRedacao,
        string? baseLegal)
    {
        List<FieldError> erros = [];

        string? resolucaoNorm = null;
        if (string.IsNullOrWhiteSpace(resolucao))
        {
            erros.Add(new("resolucao", new DomainError(
                PesoAreaEnemErrorCodes.ResolucaoObrigatoria, "Resolução é obrigatória.")));
        }
        else
        {
            resolucaoNorm = resolucao.Trim();
            if (resolucaoNorm.Length is < ResolucaoMinLength or > ResolucaoMaxLength)
            {
                erros.Add(new("resolucao", new DomainError(
                    PesoAreaEnemErrorCodes.ResolucaoTamanho,
                    $"Resolução deve ter entre {ResolucaoMinLength} e {ResolucaoMaxLength} caracteres.")));
                resolucaoNorm = null;
            }
        }

        Result<GrupoCurso> grupo = GrupoCurso.Criar(grupoCurso);
        if (grupo.IsFailure)
        {
            erros.Add(new("grupoCurso", new DomainError(
                PesoAreaEnemErrorCodes.GrupoCursoInvalido, grupo.Error!.Message)));
        }

        Result<decimal> pesosCorte = ValidarPesosCorteEBaseLegal(
            pesoRedacao, pesoCienciasNatureza, pesoCienciasHumanas, pesoLinguagens, pesoMatematica,
            corteRedacao, baseLegal);
        if (pesosCorte.IsFailure)
        {
            erros.AddRange(pesosCorte.Errors);
        }

        if (erros.Count > 0)
        {
            return Result<PesoAreaEnem>.ValidationFailure(erros);
        }

        var peso = new PesoAreaEnem
        {
            Resolucao = resolucaoNorm!,
            GrupoCurso = grupo.Value!,
        };
        peso.AplicarPesos(
            pesoRedacao, pesoCienciasNatureza, pesoCienciasHumanas, pesoLinguagens, pesoMatematica,
            pesosCorte.Value, baseLegal!);

        return Result<PesoAreaEnem>.Success(peso);
    }

    /// <summary>
    /// Atualiza os cinco pesos, o corte de redação e a base legal, acumulando
    /// toda violação independente. Nunca altera o <c>Id</c>, a <c>Resolucao</c>
    /// nem o <c>GrupoCurso</c> (chave de negócio imutável — mudá-la
    /// caracterizaria outra linha, não uma edição). Revalida a não-negatividade
    /// dos pesos e do corte e a presença da base legal.
    /// </summary>
    public Result Atualizar(
        decimal pesoRedacao,
        decimal pesoCienciasNatureza,
        decimal pesoCienciasHumanas,
        decimal pesoLinguagens,
        decimal pesoMatematica,
        decimal corteRedacao,
        string? baseLegal)
    {
        Result<decimal> pesosCorte = ValidarPesosCorteEBaseLegal(
            pesoRedacao, pesoCienciasNatureza, pesoCienciasHumanas, pesoLinguagens, pesoMatematica,
            corteRedacao, baseLegal);
        if (pesosCorte.IsFailure)
        {
            return Result.ValidationFailure(pesosCorte.Errors);
        }

        AplicarPesos(
            pesoRedacao, pesoCienciasNatureza, pesoCienciasHumanas, pesoLinguagens, pesoMatematica,
            pesosCorte.Value, baseLegal!);

        return Result.Success();
    }

    /// <summary>
    /// Valida os cinco pesos, o corte de redação e a base legal — os únicos
    /// campos editáveis na atualização — sem I/O e sem mutar nada. Para o handler
    /// de atualização falhar rápido antes de buscar a linha por Id (validação
    /// sempre vence 404).
    /// </summary>
    public static Result ValidarCamposDoPayload(
        decimal pesoRedacao,
        decimal pesoCienciasNatureza,
        decimal pesoCienciasHumanas,
        decimal pesoLinguagens,
        decimal pesoMatematica,
        decimal corteRedacao,
        string? baseLegal)
    {
        Result<decimal> resultado = ValidarPesosCorteEBaseLegal(
            pesoRedacao, pesoCienciasNatureza, pesoCienciasHumanas, pesoLinguagens, pesoMatematica,
            corteRedacao, baseLegal);

        return resultado.IsFailure ? Result.ValidationFailure(resultado.Errors) : Result.Success();
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
    // (padrão 400 quando omitido; não-negativo) e a base legal, acumulando toda
    // violação independente. Devolve o corte resolvido.
    private static Result<decimal> ValidarPesosCorteEBaseLegal(
        decimal pesoRedacao,
        decimal pesoCienciasNatureza,
        decimal pesoCienciasHumanas,
        decimal pesoLinguagens,
        decimal pesoMatematica,
        decimal? corteRedacao,
        string? baseLegal)
    {
        List<FieldError> erros = [];

        AdicionarSeInvalido(erros, "pesoRedacao", pesoRedacao, "redação");
        AdicionarSeInvalido(erros, "pesoCienciasNatureza", pesoCienciasNatureza, "ciências da natureza");
        AdicionarSeInvalido(erros, "pesoCienciasHumanas", pesoCienciasHumanas, "ciências humanas");
        AdicionarSeInvalido(erros, "pesoLinguagens", pesoLinguagens, "linguagens e códigos");
        AdicionarSeInvalido(erros, "pesoMatematica", pesoMatematica, "matemática");

        decimal corte = corteRedacao ?? CorteRedacaoPadrao;
        if (corte < 0)
        {
            erros.Add(new("corteRedacao", new DomainError(
                PesoAreaEnemErrorCodes.CorteRedacaoNegativo, "Corte de redação não pode ser negativo.")));
        }
        else if (corte > CorteRedacaoMaximo)
        {
            erros.Add(new("corteRedacao", new DomainError(
                PesoAreaEnemErrorCodes.CorteRedacaoExcedeMaximo,
                $"Corte de redação não pode exceder {CorteRedacaoMaximo} (nota máxima da redação do ENEM).")));
        }

        if (string.IsNullOrWhiteSpace(baseLegal))
        {
            erros.Add(new("baseLegal", new DomainError(
                PesoAreaEnemErrorCodes.BaseLegalObrigatoria, "Base legal é obrigatória.")));
        }
        else if (baseLegal.Trim().Length > BaseLegalMaxLength)
        {
            erros.Add(new("baseLegal", new DomainError(
                PesoAreaEnemErrorCodes.BaseLegalTamanho,
                $"Base legal deve ter no máximo {BaseLegalMaxLength} caracteres.")));
        }

        return erros.Count == 0 ? Result<decimal>.Success(corte) : Result<decimal>.ValidationFailure(erros);
    }

    private static void AdicionarSeInvalido(List<FieldError> erros, string campo, decimal valor, string area)
    {
        DomainError? erro = ValidarPeso(valor, area);
        if (erro is not null)
        {
            erros.Add(new(campo, erro));
        }
    }

    private static DomainError? ValidarPeso(decimal valor, string area) =>
        valor switch
        {
            < 0 => new DomainError(
                PesoAreaEnemErrorCodes.PesoNegativo,
                $"O peso de {area} não pode ser negativo."),
            _ when valor > PesoMaximo => new DomainError(
                PesoAreaEnemErrorCodes.PesoExcedeMaximo,
                $"O peso de {area} não pode exceder {PesoMaximo}."),
            _ => null,
        };

    private static decimal Arredondar(decimal valor, int escala) =>
        Math.Round(valor, escala, MidpointRounding.ToEven);
}
