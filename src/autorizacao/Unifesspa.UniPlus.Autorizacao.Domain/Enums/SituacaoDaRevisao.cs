namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Situação da revisão periódica dos acessos, derivada dos itens a cada leitura: em andamento enquanto há item sem decisão
/// sobre concessão ainda ativa, e concluída quando não há.
/// </summary>
public enum SituacaoDaRevisao
{
    [CodigoDeContrato("EM_ANDAMENTO", "Em andamento")]
    EmAndamento = 1,
    [CodigoDeContrato("CONCLUIDA", "Concluída")]
    Concluida = 2,
}
