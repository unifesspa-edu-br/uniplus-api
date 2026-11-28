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
    private const string DetalheDaVotacaoIncompleta =
        "A banca decide por ato individual e declara só parte da votação usada no julgamento do recurso. Declare o quórum, o critério e o encerramento dessa votação, ou retire todos os campos dela.";

    internal static List<FieldError> Violacoes(DeclaracaoDaDeliberacao declarada)
    {
        if (declarada.ModoConferido() is null)
        {
            return [Recusa("modo", BancaErrorCodes.ModoDeDeliberacaoInvalido, declarada.Modo is null ? "Informe o modo de deliberação." : "O modo de deliberação não é uma das opções disponíveis: colegiada com votação ou ato individual."), .. DaComposicao(declarada)];
        }

        List<FieldError> violacoes = [.. DaComposicao(declarada)];
        violacoes.AddRange(declarada.ModoConferido() == ModoDeDeliberacao.AtoIndividual
            ? DoAtoIndividual(declarada)
            : DaVotacao(declarada));
        return violacoes;
    }

    private static IEnumerable<FieldError> DaComposicao(DeclaracaoDaDeliberacao declarada) =>
        [.. DosIntegrantes(declarada.Integrantes), .. DosSuplentes(declarada.Suplentes)];

    private static IEnumerable<FieldError> DosIntegrantes(int? integrantes) => integrantes switch
    {
        null => [Obrigatorio("integrantes", "Informe o número de integrantes da banca.")],
        < 1 => [Recusa("integrantes", BancaErrorCodes.IntegrantesInvalidos, "A banca deve ter no mínimo 1 integrante.")],
        _ => [],
    };

    private static IEnumerable<FieldError> DosSuplentes(int? suplentes) => suplentes switch
    {
        null => [Obrigatorio("suplentes", "Informe o número de suplentes da banca; use 0 quando não houver.")],
        < 0 => [Recusa("suplentes", BancaErrorCodes.SuplentesInvalidos, "Os suplentes da banca não podem ser negativos.")],
        _ => [],
    };

    /// <summary>
    /// O ato individual aceita a deliberação sem votação ou com a votação do recurso inteira: quórum, critério e encerramento
    /// juntos. A votação pela metade recusa um campo que falta por vez.
    /// </summary>
    private static IEnumerable<FieldError> DoAtoIndividual(DeclaracaoDaDeliberacao declarada) =>
        declarada.Votacao.DeclaraAlgo()
            ? [.. CamposQueFaltamNaVotacao(declarada).Select(static campo => Recusa(campo, BancaErrorCodes.VotacaoDoRecursoIncompleta, DetalheDaVotacaoIncompleta)), .. DaVotacaoDeclarada(declarada)]
            : [];

    private static IEnumerable<string> CamposQueFaltamNaVotacao(DeclaracaoDaDeliberacao declarada)
    {
        if (!declarada.Votacao.Quorum.HasValue)
        {
            yield return "quorum";
        }

        if (declarada.Votacao.Criterio is null)
        {
            yield return "criterio";
        }

        if (declarada.Votacao.Encerramento is null)
        {
            yield return "encerramento";
        }
    }

    // As regras de cada campo valem para o que a votação do recurso declarou; o que falta já foi recusado como votação incompleta.
    private static IEnumerable<FieldError> DaVotacaoDeclarada(DeclaracaoDaDeliberacao declarada) =>
    [
        .. declarada.Votacao.Quorum.HasValue ? DoQuorum(declarada) : [],
        .. declarada.Votacao.Criterio is not null ? DoCriterio(declarada) : [],
        .. declarada.Votacao.Encerramento is not null ? DoEncerramento(declarada) : [],
        .. DoDesempate(declarada),
        .. DoResultadoSemUnanimidade(declarada),
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
        if (declarada.Integrantes is not >= 1)
        {
            return [];
        }

        if (declarada.Votacao.Quorum is not { } quorum || quorum < 1 || quorum > declarada.Integrantes)
        {
            return [Recusa("quorum", BancaErrorCodes.QuorumForaDoIntervalo, "O quórum deve ficar entre 1 e o número de integrantes da banca.")];
        }

        return declarada.Votacao.EncerramentoConferido() == EncerramentoDaVotacao.TodosOsTitulares && quorum != declarada.Integrantes
            ? [Recusa("quorum", BancaErrorCodes.QuorumIncoerente, "Com o encerramento por todos os titulares, o quórum deve ser igual ao número de integrantes da banca.")]
            : [];
    }

    private static IEnumerable<FieldError> DoCriterio(DeclaracaoDaDeliberacao declarada) =>
        declarada.Votacao.CriterioConferido().HasValue
            ? []
            : [Recusa("criterio", BancaErrorCodes.CriterioObrigatorio, declarada.Votacao.Criterio is not null ? "O critério deve ser maioria simples, maioria absoluta ou unanimidade." : "Informe o critério de votação: maioria simples, maioria absoluta ou unanimidade.")];

    private static IEnumerable<FieldError> DoEncerramento(DeclaracaoDaDeliberacao declarada) =>
        declarada.Votacao.EncerramentoConferido().HasValue
            ? []
            : [Recusa("encerramento", BancaErrorCodes.EncerramentoObrigatorio, declarada.Votacao.Encerramento is not null ? "O encerramento deve ser todos os titulares ou a presidência ao atingir o quórum." : "Informe o encerramento da votação: todos os titulares ou a presidência ao atingir o quórum.")];

    private static IEnumerable<FieldError> DoDesempate(DeclaracaoDaDeliberacao declarada)
    {
        if (!declarada.Votacao.CriterioConferido().HasValue || declarada.Integrantes is null)
        {
            return [];
        }

        bool exige = declarada.Integrantes > 1 && declarada.Votacao.CriterioConferido() != CriterioDeVotacao.Unanimidade;
        if ((exige || declarada.Votacao.Desempate is not null) && !declarada.Votacao.DesempateConferido().HasValue)
        {
            return [Recusa("desempate", BancaErrorCodes.DesempateObrigatorio, declarada.Votacao.Desempate is not null ? "O desempate deve ser voto da presidência ou nova votação." : "Informe o desempate: voto da presidência ou nova votação.")];
        }

        return !exige && declarada.Votacao.Desempate is not null
            ? [Recusa("desempate", BancaErrorCodes.DesempateNaoSeAplica, "O desempate só se aplica aos critérios de maioria, em banca com mais de 1 integrante.")]
            : [];
    }

    private static IEnumerable<FieldError> DoResultadoSemUnanimidade(DeclaracaoDaDeliberacao declarada)
    {
        if (!declarada.Votacao.CriterioConferido().HasValue || declarada.Integrantes is null)
        {
            return [];
        }

        bool exige = declarada.Integrantes > 1 && declarada.Votacao.CriterioConferido() == CriterioDeVotacao.Unanimidade;
        if ((exige || declarada.Votacao.SemUnanimidade is not null) && !declarada.Votacao.SemUnanimidadeConferido().HasValue)
        {
            return [Recusa("semUnanimidade", BancaErrorCodes.SemUnanimidadeObrigatoria, declarada.Votacao.SemUnanimidade is not null ? "O resultado deve ser favorável, desfavorável ou nova votação." : "Informe o resultado quando a unanimidade não é alcançada: favorável, desfavorável ou nova votação.")];
        }

        return !exige && declarada.Votacao.SemUnanimidade is not null
            ? [Recusa("semUnanimidade", BancaErrorCodes.SemUnanimidadeNaoSeAplica, "O resultado sem unanimidade só se aplica ao critério de unanimidade, em banca com mais de 1 integrante.")]
            : [];
    }

    private static FieldError Obrigatorio(string campo, string mensagem) =>
        Recusa(campo, CodigosDeValidacao.Obrigatorio, mensagem);

    private static FieldError Recusa(string campo, string codigo, string mensagem) =>
        new(campo, new DomainError(codigo, mensagem));
}
