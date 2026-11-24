namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que, no arquivo ou nas inscrições, não virou nota ao importar um lote.</summary>
public enum TipoDeOcorrenciaDoLote
{
    /// <summary>Inscrição da edição cujo CPF não aparece no arquivo.</summary>
    [CodigoDeContrato("INSCRITO_SEM_LINHA", "Inscrito sem linha", Descricao = "A inscrição não aparece no arquivo.")]
    InscritoSemLinha = 1,

    /// <summary>Linha do arquivo cujo CPF não é de nenhuma inscrição da edição.</summary>
    [CodigoDeContrato("LINHA_SEM_INSCRITO", "Linha sem inscrito", Descricao = "A linha não corresponde a uma inscrição.")]
    LinhaSemInscrito = 2,

    /// <summary>Inscrição da edição com mais de uma linha diferente no arquivo.</summary>
    [CodigoDeContrato("CONFLITO", "Conflito", Descricao = "A inscrição tem linhas diferentes no arquivo.")]
    Conflito = 3,
}
