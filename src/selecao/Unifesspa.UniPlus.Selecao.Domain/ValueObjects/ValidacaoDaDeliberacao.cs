namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

/// <summary>
/// As invariantes da regra de deliberação. Os códigos são literais em cada recusa para que a
/// cobertura do registro de erros os enxergue.
/// </summary>
internal static class ValidacaoDaDeliberacao
{
    internal static List<FieldError> Violacoes(DeclaracaoDaDeliberacao declarada)
    {
        if (!Enum.IsDefined(declarada.Modo))
        {
            return [Recusa("modo", BancaErrorCodes.ModoDeDeliberacaoInvalido, "O modo de deliberação deve ser colegiada com votação ou ato individual.")];
        }

        List<FieldError> violacoes = [.. DaComposicao(declarada)];
        violacoes.AddRange(declarada.Modo == ModoDeDeliberacao.AtoIndividual
            ? DoAtoIndividual(declarada)
            : DaVotacao(declarada));
        return violacoes;
    }

    private static IEnumerable<FieldError> DaComposicao(DeclaracaoDaDeliberacao declarada)
    {
        if (declarada.Integrantes < 1)
        {
            yield return Recusa("integrantes", BancaErrorCodes.IntegrantesInvalidos, $"A banca deve ter no mínimo 1 integrante; foram declarados {declarada.Integrantes}.");
        }

        if (declarada.Suplentes < 0)
        {
            yield return Recusa("suplentes", BancaErrorCodes.SuplentesInvalidos, $"Os suplentes da banca não podem ser negativos; foram declarados {declarada.Suplentes}.");
        }
    }

    private static IEnumerable<FieldError> DoAtoIndividual(DeclaracaoDaDeliberacao declarada) =>
        CamposDaVotacao(declarada)
            .Where(static campo => campo.Declarado)
            .Select(static campo => Recusa(campo.Nome, BancaErrorCodes.AtoIndividualSemVotacao, "A deliberação por ato individual não declara quórum, critério, desempate, encerramento nem resultado sem unanimidade."));

    private static IEnumerable<(string Nome, bool Declarado)> CamposDaVotacao(DeclaracaoDaDeliberacao declarada) =>
    [
        ("quorum", declarada.Quorum.HasValue),
        ("criterio", declarada.Criterio.HasValue),
        ("desempate", declarada.Desempate.HasValue),
        ("encerramento", declarada.Encerramento.HasValue),
        ("semUnanimidade", declarada.SemUnanimidade.HasValue),
    ];

    private static IEnumerable<FieldError> DaVotacao(DeclaracaoDaDeliberacao declarada) =>
    [
        .. DoQuorum(declarada),
        .. DoCriterio(declarada),
        .. DoEncerramento(declarada),
        .. DoDesempate(declarada),
        .. DoResultadoSemUnanimidade(declarada),
    ];

    private static IEnumerable<FieldError> DoQuorum(DeclaracaoDaDeliberacao declarada)
    {
        if (declarada.Integrantes < 1)
        {
            return [];
        }

        if (declarada.Quorum is not { } quorum || quorum < 1 || quorum > declarada.Integrantes)
        {
            return [Recusa("quorum", BancaErrorCodes.QuorumForaDoIntervalo, $"O quórum deve ficar entre 1 e {declarada.Integrantes}, o número de integrantes.")];
        }

        return declarada.Encerramento == EncerramentoDaVotacao.TodosOsTitulares && quorum != declarada.Integrantes
            ? [Recusa("quorum", BancaErrorCodes.QuorumIncoerente, $"Com o encerramento por todos os titulares, o quórum deve ser igual ao número de integrantes ({declarada.Integrantes}); foi declarado {quorum}.")]
            : [];
    }

    private static IEnumerable<FieldError> DoCriterio(DeclaracaoDaDeliberacao declarada) =>
        Definido(declarada.Criterio)
            ? []
            : [Recusa("criterio", BancaErrorCodes.CriterioObrigatorio, "Informe o critério de votação: maioria simples, maioria absoluta ou unanimidade.")];

    private static IEnumerable<FieldError> DoEncerramento(DeclaracaoDaDeliberacao declarada) =>
        Definido(declarada.Encerramento)
            ? []
            : [Recusa("encerramento", BancaErrorCodes.EncerramentoObrigatorio, "Informe o encerramento da votação: todos os titulares ou a presidência ao atingir o quórum.")];

    private static IEnumerable<FieldError> DoDesempate(DeclaracaoDaDeliberacao declarada)
    {
        if (!Definido(declarada.Criterio))
        {
            return [];
        }

        bool exige = declarada.Integrantes > 1 && declarada.Criterio != CriterioDeVotacao.Unanimidade;
        if (exige && !Definido(declarada.Desempate))
        {
            return [Recusa("desempate", BancaErrorCodes.DesempateObrigatorio, "Informe o desempate: voto da presidência ou nova votação.")];
        }

        return !exige && declarada.Desempate.HasValue
            ? [Recusa("desempate", BancaErrorCodes.DesempateNaoSeAplica, "O desempate só se aplica aos critérios de maioria, em banca com mais de 1 integrante.")]
            : [];
    }

    private static IEnumerable<FieldError> DoResultadoSemUnanimidade(DeclaracaoDaDeliberacao declarada)
    {
        if (!Definido(declarada.Criterio))
        {
            return [];
        }

        bool exige = declarada.Integrantes > 1 && declarada.Criterio == CriterioDeVotacao.Unanimidade;
        if (exige && !Definido(declarada.SemUnanimidade))
        {
            return [Recusa("semUnanimidade", BancaErrorCodes.SemUnanimidadeObrigatoria, "Informe o resultado quando a unanimidade não é alcançada: favorável, desfavorável ou nova votação.")];
        }

        return !exige && declarada.SemUnanimidade.HasValue
            ? [Recusa("semUnanimidade", BancaErrorCodes.SemUnanimidadeNaoSeAplica, "O resultado sem unanimidade só se aplica ao critério de unanimidade, em banca com mais de 1 integrante.")]
            : [];
    }

    private static bool Definido<TEnum>(TEnum? valor)
        where TEnum : struct, Enum =>
        valor is { } declarado && Enum.IsDefined(declarado);

    private static FieldError Recusa(string campo, string codigo, string mensagem) =>
        new(campo, new DomainError(codigo, mensagem));
}
