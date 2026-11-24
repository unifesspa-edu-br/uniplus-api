namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// De onde vêm os candidatos do <see cref="Entities.ProcessoSeletivo"/> — o eixo que deriva o piso mínimo do cronograma de fases, <b>nunca</b> o
/// <see cref="TipoProcesso"/>. Atributo declarado, obrigatório na criação do processo; a origem ainda
/// não declarada é <see langword="null"/>.
/// </summary>
/// <remarks>
/// Prova de indistinguibilidade: um processo do tipo SiSU com
/// <see cref="ImportacaoExterna"/> publica exatamente igual a um processo do tipo PSIQ
/// com a mesma origem — o veredicto do gate depende só deste atributo, nunca do rótulo
/// do tipo.
/// </remarks>
public enum OrigemCandidatos
{
    [CodigoDeContrato(
        "INSCRICAO_PROPRIA",
        "Inscrição própria",
        Descricao = "O processo coleta a própria inscrição: exige ao menos uma fase de coleta de inscrição.")]
    InscricaoPropria = 1,

    [CodigoDeContrato(
        "IMPORTACAO_EXTERNA",
        "Importação externa",
        Descricao = "Os candidatos vêm de importação externa, como a do SiSU: nenhuma fase de coleta é exigida.")]
    ImportacaoExterna = 2,
}
