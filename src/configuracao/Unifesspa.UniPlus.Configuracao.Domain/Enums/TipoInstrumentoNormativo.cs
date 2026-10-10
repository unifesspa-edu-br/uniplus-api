namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Tipo de instrumento normativo aceito na Base Legal de Bônus Regional.
/// Vocabulário fechado dos documentos formais que instituem ou modificam o bônus em
/// processo seletivo de IFES — os atos normativos e o próprio edital do processo.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Nenhum"/> é sentinela de ausência: <see cref="TipoInstrumentoNormativoCodigo.FromCodigo"/>
/// o devolve para código desconhecido, e ele não aparece no vocabulário publicado
/// — anunciá-lo daria a entender que "nenhum" é um tipo de instrumento que uma
/// Base Legal poderia referenciar.
/// </para>
/// <para>
/// A ordem de declaração define a ordem do vocabulário anunciado pelo endpoint
/// <c>GET /api/configuracao/vocabularios/tipos-instrumento-normativo</c>: lei primeiro,
/// parecer e edital por último — hierarquia de força normativa.
/// </para>
/// </remarks>
public enum TipoInstrumentoNormativo
{
    Nenhum = 0,
    Lei = 1,
    Decreto = 2,
    Portaria = 3,
    Resolucao = 4,
    InstrucaoNormativa = 5,
    Parecer = 6,
    Edital = 7,
}
