namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que a trilha do bloqueio do perfil registra: os eventos da suspensão e da desativação e a decisão de uma etapa da retomada.</summary>
public enum TipoDoRegistroDoBloqueio
{
    [CodigoDeContrato("SUSPENSAO", "Suspensão")]
    Suspensao = 1,

    [CodigoDeContrato("DECISAO_DA_RETOMADA", "Decisão de etapa da retomada")]
    DecisaoDaRetomada = 2,

    [CodigoDeContrato("RETOMADA", "Retomada")]
    Retomada = 3,

    [CodigoDeContrato("DESATIVACAO", "Desativação")]
    Desativacao = 4,

    [CodigoDeContrato("REATIVACAO", "Reativação")]
    Reativacao = 5,
}
