namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>O sentido do arquivo trocado com o INEP.</summary>
public enum TipoDeLeiauteDoInep
{
    /// <summary>O arquivo que o Uni+ gera para o INEP, com os CPFs dos inscritos.</summary>
    Envio = 1,

    /// <summary>O arquivo que o INEP devolve, com as notas e a situação em cada prova.</summary>
    Retorno = 2,
}
