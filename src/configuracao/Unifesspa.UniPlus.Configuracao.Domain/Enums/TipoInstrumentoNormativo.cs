namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Tipo de instrumento normativo aceito na Base Legal de Bônus Regional.
/// Vocabulário fechado dos documentos formais que instituem ou modificam o bônus em
/// processo seletivo de IFES — os atos normativos e o próprio edital do processo.
/// </summary>
/// <remarks>
/// A identidade gravada e trafegada do tipo é o código declarado (ADR-0176). A ordem de declaração define
/// a ordem do vocabulário anunciado pelo endpoint
/// <c>GET /api/configuracao/vocabularios/tipos-instrumento-normativo</c>: lei primeiro,
/// parecer e edital por último — hierarquia de força normativa.
/// </remarks>
[DescricaoDoContrato("Tipo do ato normativo que institui ou modifica o bônus regional de um processo seletivo.")]
public enum TipoInstrumentoNormativo
{
    [CodigoDeContrato(
        "LEI",
        "Lei",
        Descricao = "Ato normativo de maior hierarquia no ordenamento infraconstitucional, emanado do Poder Legislativo.")]
    Lei = 1,

    [CodigoDeContrato(
        "DECRETO",
        "Decreto",
        Descricao = "Ato normativo do Poder Executivo que regulamenta leis ou dispõe sobre matéria de sua competência.")]
    Decreto = 2,

    [CodigoDeContrato(
        "PORTARIA",
        "Portaria",
        Descricao = "Ato administrativo de autoridade pública que disciplina procedimentos internos ou matéria de competência específica.")]
    Portaria = 3,

    [CodigoDeContrato(
        "RESOLUCAO",
        "Resolução",
        Descricao = "Ato normativo de órgão colegiado (conselho, câmara, comitê) com efeitos internos ou externos conforme a norma instituidora.")]
    Resolucao = 4,

    [CodigoDeContrato(
        "INSTRUCAO_NORMATIVA",
        "Instrução Normativa",
        Descricao = "Ato administrativo de caráter técnico-operacional que detalha a execução de normas superiores.")]
    InstrucaoNormativa = 5,

    [CodigoDeContrato(
        "PARECER",
        "Parecer",
        Descricao = "Manifestação técnica ou jurídica de órgão competente que orienta a aplicação de normas — vinculante quando aprovado pela autoridade competente.")]
    Parecer = 6,

    [CodigoDeContrato(
        "EDITAL",
        "Edital",
        Descricao = "Ato de convocação do processo seletivo que fixa as regras do certame, inclusive o bônus que ele institui.")]
    Edital = 7,
}
