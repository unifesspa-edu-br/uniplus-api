namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Política, no nível do <b>processo</b>, que ancora a apuração de <c>FAIXA_ETARIA</c> na
/// publicação (Story #554, PR #896 — B-03 do plano). Uma única política ancora TODOS os
/// gatilhos por idade do processo. O congelamento da <c>DateOnly</c> concreta
/// (<c>dataReferenciaFatos</c> no envelope) é da PR #903; este VO e a validação estrutural de
/// publicação (<see cref="Entities.ProcessoSeletivo"/>) nascem aqui.
/// </summary>
/// <remarks>
/// Coerência tudo-ou-nada por variante (N-I01): <see cref="Enums.ReferenciaTipo.DataEspecifica"/>
/// exige <see cref="Data"/> e proíbe <see cref="FaseId"/>; <see cref="Enums.ReferenciaTipo.InicioFase"/>/
/// <see cref="Enums.ReferenciaTipo.FimFase"/> exigem <see cref="FaseId"/> e proíbem
/// <see cref="Data"/>; <see cref="Enums.ReferenciaTipo.FimInscricao"/> não usa nenhum dos dois
/// campos opcionais (resolve contra a fase que coleta inscrição, ADR-0111 — sem parâmetro
/// aqui, resolvida na publicação).
/// </remarks>
public sealed record ReferenciaTemporalFatos
{
    private ReferenciaTemporalFatos(ReferenciaTipo tipo, DateOnly? data, Guid? faseId)
    {
        Tipo = tipo;
        Data = data;
        FaseId = faseId;
    }

    public ReferenciaTipo Tipo { get; }

    /// <summary>Só presente quando <see cref="Tipo"/> é <see cref="Enums.ReferenciaTipo.DataEspecifica"/>.</summary>
    public DateOnly? Data { get; }

    /// <summary>Só presente quando <see cref="Tipo"/> é <see cref="Enums.ReferenciaTipo.InicioFase"/> ou <see cref="Enums.ReferenciaTipo.FimFase"/>.</summary>
    public Guid? FaseId { get; }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — o array
    /// <c>errors[]</c> do contrato público (ADR-0023) precisa de todas as regras violadas no
    /// mesmo lote. Recebe o enum já resolvido — usada pelos chamadores internos que já têm um
    /// <see cref="ReferenciaTipo"/> confiável (ex.: reconstrução do envelope canônico
    /// congelado); o caminho de wire usa a sobrecarga que recebe o token cru, que distingue
    /// "ausente" de "presente mas não reconhecido" antes de chegar aqui.
    /// </summary>
    public static Result<ReferenciaTemporalFatos> Criar(ReferenciaTipo? tipo, DateOnly? data, Guid? faseId)
    {
        if (tipo is null || !Enum.IsDefined(tipo.Value))
        {
            return tipo is null
                ? Result<ReferenciaTemporalFatos>.ValidationFailure(
                    [new("tipo", new DomainError("ReferenciaTemporalFatos.TipoObrigatorio", "O tipo de referência temporal é obrigatório."))])
                : TipoDesconhecido();
        }

        List<FieldError> erros = [.. new[] { RecusaDaData(tipo.Value, data), RecusaDaFase(tipo.Value, faseId) }.OfType<FieldError>()];
        return erros.Count > 0
            ? Result<ReferenciaTemporalFatos>.ValidationFailure(erros)
            : Result<ReferenciaTemporalFatos>.Success(new ReferenciaTemporalFatos(tipo.Value, data, faseId));
    }

    private static FieldError? RecusaDaData(ReferenciaTipo tipo, DateOnly? data)
    {
        bool exigeData = tipo == ReferenciaTipo.DataEspecifica;
        return exigeData == data.HasValue ? null : new("data", new DomainError(
            "ReferenciaTemporalFatos.DataIncoerenteComTipo",
            exigeData ? "DATA_ESPECIFICA exige a data." : "A data só é aceita quando o tipo é DATA_ESPECIFICA."));
    }

    private static FieldError? RecusaDaFase(ReferenciaTipo tipo, Guid? faseId)
    {
        bool exigeFase = tipo is ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase;
        return exigeFase == faseId.HasValue ? null : new("faseId", new DomainError(
            "ReferenciaTemporalFatos.FaseIncoerenteComTipo",
            exigeFase ? "INICIO_FASE/FIM_FASE exigem a fase de referência." : "A fase só é aceita quando o tipo é INICIO_FASE ou FIM_FASE."));
    }

    public static Result<ReferenciaTemporalFatos> Criar(string? tipoCodigo, DateOnly? data, Guid? faseId)
    {
        bool conhecido = CatalogoDe<ReferenciaTipo>.TentarObter(tipoCodigo, out ReferenciaTipo tipo);
        if (!conhecido && !string.IsNullOrWhiteSpace(tipoCodigo))
        {
            return TipoDesconhecido();
        }

        return Criar(conhecido ? tipo : null, data, faseId);
    }

    private static Result<ReferenciaTemporalFatos> TipoDesconhecido() =>
        Result<ReferenciaTemporalFatos>.ValidationFailure(
        [
            new("tipo", new DomainError(
                "ReferenciaTemporalFatos.TipoDesconhecido",
                "Tipo de referência temporal fora do catálogo fechado (FIM_INSCRICAO, INICIO_FASE, FIM_FASE, DATA_ESPECIFICA).")),
        ]);
}
