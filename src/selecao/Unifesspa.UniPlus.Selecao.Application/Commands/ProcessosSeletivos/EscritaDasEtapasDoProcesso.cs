namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.MotivosDeDecisao;
using Domain.ProcessosSeletivos.Etapas;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// A escrita das etapas do processo que só olha o payload, sem cadastro nem agregado: o <c>Id</c> repetido
/// a forma e os tipos de decisão de TODAS as etapas, acumulados (ADR-0125). Roda antes de qualquer leitura externa — a validação
/// vence o I/O —, para que uma etapa com nome vazio não fique escondida atrás da recusa de catálogo de
/// outra. Também lê uma etapa do payload na forma que o domínio recebe.
/// </summary>
internal static class EscritaDasEtapasDoProcesso
{
    public static Result Conferir(IReadOnlyList<EtapaProcessoInput> etapas)
    {
        if (EtapasDoProcesso.PosicoesComIdRepetido(etapas.Select(static etapa => etapa.Id)).Count > 0)
        {
            return Result.Failure(EtapasDoProcesso.IdRepetido());
        }

        List<FieldError> erros = [.. etapas.SelectMany(ErrosDoPayloadDaEtapa)];

        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    /// <summary>Prefixa o campo de cada erro com a posição da etapa no payload.</summary>
    public static IReadOnlyList<FieldError> ComIndice(IEnumerable<FieldError> erros, int indice) =>
        [.. erros.Select(erro => erro.Field is null ? erro : erro with { Field = $"etapas[{indice}].{erro.Field}" })];

    public static List<ItemDeAvaliacaoDeclarado> ItensDeclarados(EtapaProcessoInput input) =>
        [.. (input.ItensDeAvaliacao ?? []).Select(static i => new ItemDeAvaliacaoDeclarado(i.Id, i.Rotulo, i.PontuacaoMaxima))];

    public static List<NivelDeAtendimento> NiveisDeclarados(EtapaProcessoInput input) =>
        [.. (input.EscalaDeAtendimento ?? []).Select(static n => new NivelDeAtendimento(n.Rotulo, n.Fracao))];

    /// <summary>
    /// Como a etapa do payload declara a avaliação da nota, com a forma e a regra de consolidação que a
    /// conferência resolveu; antes de resolvê-las, a conferência da forma passa ambas nulas.
    /// </summary>
    public static DeclaracaoDaAvaliacaoDaEtapa DeclaracaoDaAvaliacao(
        EtapaProcessoInput input, FormaDeAvaliacao? forma, ReferenciaRegra? regraConsolidacao) =>
        new(input.Peso, input.NotaMinima, input.NotaMaxima, input.PassoDoLancamento, forma, regraConsolidacao, NiveisDeclarados(input));

    /// <summary>As recusas do caráter declarado contra o que o tipo da etapa admite, já com a posição.</summary>
    public static IReadOnlyList<FieldError> ErrosDeCarater(
        CaraterEtapa carater, bool admitePontuacao, bool admiteEliminacao, string nomeDoTipo, int indice) =>
        ComIndice(CaraterDaEtapa.ValidarAdmitido(carater, admitePontuacao, admiteEliminacao, nomeDoTipo), indice);

    private static IReadOnlyList<FieldError> ErrosDoPayloadDaEtapa(EtapaProcessoInput etapa, int indice) =>
        ComIndice(
            [.. FormaDaEtapa(etapa), .. TiposDeDecisaoDeclarados.Validar(etapa.TiposDeDecisao, TiposDeDecisaoDeclarados.Campo)],
            indice);

    private static List<FieldError> FormaDaEtapa(EtapaProcessoInput etapa) =>
        ConferenciaDaDeclaracaoDaEtapa.ValidarFormaBasica(
            etapa.Nome, etapa.Carater, etapa.Ordem,
            DeclaracaoDaAvaliacao(etapa, forma: null, regraConsolidacao: null), ItensDeclarados(etapa));
}
