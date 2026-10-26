namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>O que, no arquivo ou nas inscrições, não virou nota ao importar um lote.</summary>
public enum TipoDeOcorrenciaDoLote
{
    /// <summary>Inscrição da edição cujo CPF não aparece no arquivo.</summary>
    InscritoSemLinha = 1,

    /// <summary>Linha do arquivo cujo CPF não é de nenhuma inscrição da edição.</summary>
    LinhaSemInscrito = 2,

    /// <summary>Inscrição da edição com mais de uma linha diferente no arquivo.</summary>
    Conflito = 3,
}
