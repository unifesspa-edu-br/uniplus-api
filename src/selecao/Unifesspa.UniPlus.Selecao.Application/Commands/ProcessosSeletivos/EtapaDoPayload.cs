namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>Leitura de uma etapa do payload de <see cref="DefinirEtapasCommand"/> na forma que o domínio recebe.</summary>
internal static class EtapaDoPayload
{
    /// <summary>Prefixa o campo de cada erro com a posição da etapa no payload.</summary>
    public static IReadOnlyList<FieldError> ComIndice(IEnumerable<FieldError> erros, int indice) =>
        [.. erros.Select(erro => erro.Field is null ? erro : erro with { Field = $"etapas[{indice}].{erro.Field}" })];

    public static List<ItemDeAvaliacaoDeclarado> ItensDeclarados(EtapaProcessoInput input) =>
        [.. (input.ItensDeAvaliacao ?? []).Select(static i => new ItemDeAvaliacaoDeclarado(i.Id, i.Rotulo, i.PontuacaoMaxima))];

    public static List<NivelDeAtendimento> NiveisDeclarados(EtapaProcessoInput input) =>
        [.. (input.EscalaDeAtendimento ?? []).Select(static n => new NivelDeAtendimento(n.Rotulo, n.Fracao))];

    /// <summary>As recusas do caráter declarado contra o que o tipo da etapa admite, já com a posição.</summary>
    public static IReadOnlyList<FieldError> ErrosDeCarater(
        CaraterEtapa carater, bool admitePontuacao, bool admiteEliminacao, string nomeDoTipo, int indice) =>
        ComIndice(EtapaProcesso.ValidarCaraterAdmitido(carater, admitePontuacao, admiteEliminacao, nomeDoTipo), indice);
}
