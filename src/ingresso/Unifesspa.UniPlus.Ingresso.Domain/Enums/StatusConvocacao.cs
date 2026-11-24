namespace Unifesspa.UniPlus.Ingresso.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação de uma convocação. O código de cada membro é o valor gravado na coluna <c>status</c>.</summary>
[DescricaoDoContrato("A situação de uma convocação: pendente até o candidato se manifestar, aceita, recusada, não comparecida ou expirada.")]
public enum StatusConvocacao
{
    [CodigoDeContrato("PENDENTE", "Pendente", Descricao = "O candidato foi convocado e ainda não se manifestou.")]
    Pendente = 1,
    [CodigoDeContrato("ACEITA", "Aceita", Descricao = "O candidato aceitou a convocação.")]
    Aceita = 2,
    [CodigoDeContrato("RECUSADA", "Recusada", Descricao = "O candidato recusou a convocação.")]
    Recusada = 3,
    [CodigoDeContrato("NAO_COMPARECEU", "Não compareceu", Descricao = "O candidato não compareceu no prazo da convocação.")]
    NaoCompareceu = 4,
    [CodigoDeContrato("EXPIRADA", "Expirada", Descricao = "O prazo da convocação terminou sem manifestação.")]
    Expirada = 5
}
