namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O sentido do arquivo trocado com o INEP.</summary>
public enum TipoDeLeiauteDoInep
{
    /// <summary>O arquivo que o Uni+ gera para o INEP, com os CPFs dos inscritos.</summary>
    [CodigoDeContrato("ENVIO", "Envio", Descricao = "Arquivo enviado ao INEP.")]
    Envio = 1,

    /// <summary>O arquivo que o INEP devolve, com as notas e a situação em cada prova.</summary>
    [CodigoDeContrato("RETORNO", "Retorno", Descricao = "Arquivo recebido do INEP.")]
    Retorno = 2,
}
