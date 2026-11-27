namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ProcessosSeletivos.Etapas;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

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
        if (EtapasDoProcesso.PosicoesComIdRepetido(etapas.Select(static etapa => etapa.Id)).Count > 0)
        {
            return Result.Failure(EtapasDoProcesso.IdRepetido());
        }

        List<FieldError> erros = [.. etapas.SelectMany(ErrosDoPayloadDaEtapa)];

        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    /// <summary>Prefixa o campo de cada erro com a posição da etapa no payload.</summary>
    public static IReadOnlyList<FieldError> ComIndice(IEnumerable<FieldError> erros, int indice) =>
        [.. erros.Select(erro => erro.NoCaminhoDe($"etapas[{indice}]"))];

    public static List<ItemDeAvaliacaoDeclarado> ItensDeclarados(EtapaProcessoInput input) =>
        [.. (input.ItensDeAvaliacao ?? []).Select(static i => new ItemDeAvaliacaoDeclarado(i.Id, i.Rotulo, i.PontuacaoMaxima))];

    public static List<NivelDeAtendimentoDeclarado> NiveisDeclarados(EtapaProcessoInput input) =>
        [.. (input.EscalaDeAtendimento ?? []).Select(static n => new NivelDeAtendimentoDeclarado(n.Rotulo, n.Fracao, n.Chave))];

    /// <summary>
    /// Como a etapa do payload declara a avaliação da nota, com a forma e a regra de consolidação que a
    /// conferência resolveu; antes de resolvê-las, a conferência da forma passa ambas nulas.
    /// </summary>
    public static DeclaracaoDaAvaliacaoDaEtapa DeclaracaoDaAvaliacao(
        EtapaProcessoInput input, FormaDeAvaliacao? forma, ReferenciaRegra? regraConsolidacao) =>
        new(input.Peso, input.NotaMinima, input.NotaMaxima, input.PassoDoLancamento, forma, regraConsolidacao, NiveisDeclarados(input));

    private static IReadOnlyList<FieldError> ErrosDoPayloadDaEtapa(EtapaProcessoInput etapa, int indice) =>
        ComIndice(FormaDaEtapa(etapa), indice);

    private static List<FieldError> FormaDaEtapa(EtapaProcessoInput etapa) =>
        ConferenciaDaDeclaracaoDaEtapa.ValidarFormaBasica(
            etapa.Nome, CodigoLidoDoPedido.Ler<CaraterEtapa>(etapa.Carater), etapa.Ordem,
            DeclaracaoDaAvaliacao(etapa, forma: null, regraConsolidacao: null), ItensDeclarados(etapa));
}
