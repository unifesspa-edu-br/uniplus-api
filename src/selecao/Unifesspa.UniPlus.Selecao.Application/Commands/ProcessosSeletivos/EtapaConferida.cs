namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Uma etapa do payload e o que a conferência apurou sobre ela, na ordem do payload: a etapa do processo
/// que ela atualiza, o tipo lido do cadastro, a forma de avaliação e a regra de consolidação resolvidas.
/// Cada passo da conferência preenche o que lhe cabe, e a montagem consome o conjunto.
/// </summary>
/// <param name="Declarada">A etapa como veio no payload.</param>
/// <param name="Existente">A etapa do processo com o mesmo <c>Id</c>; <c>null</c> quando é nova.</param>
/// <param name="TipoLido">
/// O tipo lido do cadastro, não nulo quando o vínculo é novo ou muda de tipo, ou quando muda o caráter; do
/// contrário a etapa fica com o snapshot que já tem (ADR-0061).
/// </param>
/// <param name="Forma">A forma de avaliação resolvida; <c>null</c> quando a etapa não a declara.</param>
/// <param name="Consolidacao">A regra de consolidação do catálogo; <c>null</c> quando a etapa não declara.</param>
internal sealed record EtapaConferida(
    EtapaProcessoInput Declarada,
    EtapaProcesso? Existente,
    TipoEtapaView? TipoLido = null,
    FormaDeAvaliacao? Forma = null,
    ReferenciaRegra? Consolidacao = null)
{
    /// <summary>Se a etapa mantém o tipo de etapa ao qual já estava vinculada.</summary>
    public bool VinculoInalterado => Existente?.TipoEtapaOrigemId == Declarada.TipoEtapaOrigemId;

    /// <summary>Se a etapa mantém o vínculo e o caráter, e por isso não é reavaliada contra o cadastro.</summary>
    public bool CaraterInalterado => VinculoInalterado && Existente!.Carater == Declarada.Carater;

    /// <summary>Se a nota da etapa vem do ENEM: o snapshot congelado enquanto o vínculo não muda, o tipo lido quando muda.</summary>
    public bool NotaDeOrigemNoEnem => VinculoInalterado ? Existente!.TipoEtapa.NotaDeOrigemNoEnem : TipoLido!.NotaDeOrigemNoEnem;

    public static EtapaConferida De(EtapaProcessoInput declarada, IReadOnlyDictionary<Guid, EtapaProcesso> existentes) =>
        new(declarada, declarada.Id is { } id && existentes.TryGetValue(id, out EtapaProcesso? existente) ? existente : null);
}
