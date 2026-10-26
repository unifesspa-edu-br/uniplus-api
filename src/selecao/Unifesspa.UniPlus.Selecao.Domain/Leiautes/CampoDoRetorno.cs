namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>O campo do Uni+ que uma coluna do arquivo de retorno alimenta.</summary>
public enum CampoDoRetorno
{
    NumeroDeInscricaoNoEnem = 1,
    Cpf = 2,

    /// <summary>A nota de uma área; a área vem em <see cref="ColunaDoRetorno.Area"/>.</summary>
    Nota = 3,

    /// <summary>A situação da prova de uma área; a área vem em <see cref="ColunaDoRetorno.Area"/>.</summary>
    Situacao = 4,

    /// <summary>Coluna que o Uni+ não usa.</summary>
    Ignorado = 5,
}
