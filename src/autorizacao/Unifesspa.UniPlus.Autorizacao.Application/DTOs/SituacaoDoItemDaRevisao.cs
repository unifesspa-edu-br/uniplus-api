namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Em que ponto está o item da revisão periódica dos acessos: a decisão gravada ou o motivo de não haver decisão.</summary>
public enum SituacaoDoItemDaRevisao
{
    /// <summary>Sem decisão, sobre concessão ainda ativa.</summary>
    [CodigoDeContrato("PENDENTE", "Pendente")]
    Pendente = 1,

    /// <summary>A concessão foi confirmada pela revisão.</summary>
    [CodigoDeContrato("CONFIRMADA", "Confirmada")]
    Confirmada = 2,

    /// <summary>A concessão foi revogada pela revisão.</summary>
    [CodigoDeContrato("REVOGADA", "Revogada")]
    Revogada = 3,

    /// <summary>Sem decisão, porque a concessão terminou fora da revisão.</summary>
    [CodigoDeContrato("CONCESSAO_ENCERRADA", "Concessão encerrada")]
    ConcessaoEncerrada = 4,
}
