namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// A escrita das etapas do processo que só olha o payload, sem cadastro nem agregado: o <c>Id</c> repetido
/// e a forma de TODAS as etapas, acumulada (ADR-0125). Roda antes de qualquer leitura externa — a validação
/// vence o I/O —, para que uma etapa com nome vazio não fique escondida atrás da recusa de catálogo de
/// outra. Também lê uma etapa do payload na forma que o domínio recebe.
/// </summary>
internal static class EscritaDasEtapasDoProcesso
{
    public static Result Conferir(IReadOnlyList<EtapaProcessoInput> etapas)
    {
        List<Guid> idsInformados = [.. etapas.Where(e => e.Id.HasValue).Select(e => e.Id!.Value)];
        if (idsInformados.Distinct().Count() != idsInformados.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.IdEtapaDuplicado",
                "O mesmo Id de etapa não pode ser informado mais de uma vez no mesmo payload."));
        }

        List<FieldError> erros = [];
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            erros.AddRange(ComIndice(FormaDaEtapa(etapas[indice]), indice));
        }

        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

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

    private static List<FieldError> FormaDaEtapa(EtapaProcessoInput etapa) =>
        EtapaProcesso.ValidarFormaBasica(
            etapa.Nome, etapa.Carater, etapa.Peso, etapa.NotaMinima, etapa.Ordem,
            etapa.NotaMaxima, etapa.PassoDoLancamento,
            ItensDeclarados(etapa), NiveisDeclarados(etapa));
}
