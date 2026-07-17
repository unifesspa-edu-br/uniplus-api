namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Política, no nível do <b>processo</b>, que ancora a apuração de <c>FAIXA_ETARIA</c> na
/// publicação (Story #554, PR-b — B-03 do plano). Uma única política ancora TODOS os
/// gatilhos por idade do processo. O congelamento da <c>DateOnly</c> concreta
/// (<c>dataReferenciaFatos</c> no envelope) é da PR-e; este VO e a validação estrutural de
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

    public static Result<ReferenciaTemporalFatos> Criar(ReferenciaTipo tipo, DateOnly? data, Guid? faseId)
    {
        if (tipo == ReferenciaTipo.Nenhuma)
        {
            return Result<ReferenciaTemporalFatos>.Failure(new DomainError(
                "ReferenciaTemporalFatos.TipoObrigatorio",
                "O tipo de referência temporal é obrigatório (FIM_INSCRICAO, INICIO_FASE, FIM_FASE ou DATA_ESPECIFICA)."));
        }

        bool exigeData = tipo == ReferenciaTipo.DataEspecifica;
        if (exigeData != data.HasValue)
        {
            return Result<ReferenciaTemporalFatos>.Failure(new DomainError(
                "ReferenciaTemporalFatos.DataIncoerenteComTipo",
                exigeData
                    ? "DATA_ESPECIFICA exige a data."
                    : "A data só é aceita quando o tipo é DATA_ESPECIFICA."));
        }

        bool exigeFase = tipo is ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase;
        if (exigeFase != faseId.HasValue)
        {
            return Result<ReferenciaTemporalFatos>.Failure(new DomainError(
                "ReferenciaTemporalFatos.FaseIncoerenteComTipo",
                exigeFase
                    ? "INICIO_FASE/FIM_FASE exigem a fase âncora."
                    : "A fase âncora só é aceita quando o tipo é INICIO_FASE ou FIM_FASE."));
        }

        return Result<ReferenciaTemporalFatos>.Success(new ReferenciaTemporalFatos(tipo, data, faseId));
    }
}
