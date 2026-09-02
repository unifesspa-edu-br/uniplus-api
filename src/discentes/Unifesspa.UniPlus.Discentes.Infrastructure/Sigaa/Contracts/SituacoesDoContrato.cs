namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

using System.Collections.Frozen;
using System.Collections.Generic;

/// <summary>
/// As situações acadêmicas que a origem promete emitir.
/// </summary>
/// <remarks>
/// O contrato declara o campo como conjunto fechado, e os identificadores ausentes daqui
/// estão de fora por decisão da origem — são estados que não correspondem a vínculo. Aceitar
/// qualquer inteiro positivo guardaria como discente alguém que a origem não classifica
/// assim, e o registro entraria na réplica com o CPF cifrado junto, contado como sucesso.
///
/// Como o conjunto é do contrato, e não do domínio da réplica, ele mora na camada que
/// traduz a origem: o módulo espelha a situação que recebe, sem opinar sobre quais existem.
/// </remarks>
internal static class SituacoesDoContrato
{
    private static readonly FrozenSet<int> Declaradas =
        new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 14, 100 }.ToFrozenSet();

    public static bool Declarada(int id) => Declaradas.Contains(id);
}
