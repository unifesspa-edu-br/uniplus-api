namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O que as bancas de um tipo fazem no processo seletivo (UNI-REQ-0139). Vocabulário fechado: cada tipo de banca
/// cadastrado tem exatamente um papel, e as regras da seleção decidem pelo papel, nunca pelo código do tipo.
/// </summary>
[DescricaoDoContrato("O que as bancas de um tipo fazem no processo seletivo. Cada tipo de banca cadastrado tem exatamente um papel; as regras da seleção decidem pelo papel, e não pelo código do tipo.")]
public enum PapelDaBanca
{
    [CodigoDeContrato("AVALIA_O_MERITO", "Avalia o mérito")]
    AvaliaOMerito = 1,

    [CodigoDeContrato("JULGA_A_PRIMEIRA_INSTANCIA", "Julga o recurso em 1ª instância")]
    JulgaAPrimeiraInstancia = 2,

    [CodigoDeContrato("IMPORTA_AS_NOTAS_DO_INEP", "Importa as notas do INEP")]
    ImportaAsNotasDoInep = 3,

    [CodigoDeContrato("IMPORTA_CANDIDATOS", "Importa candidatos")]
    ImportaCandidatos = 4,
}
