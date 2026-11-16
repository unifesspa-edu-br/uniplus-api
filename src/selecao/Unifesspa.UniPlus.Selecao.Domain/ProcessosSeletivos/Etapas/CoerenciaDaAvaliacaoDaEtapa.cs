namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusa a contradição entre o que a etapa declara sobre como a nota é obtida, em rascunho também: a
/// falta (de forma, de regra, de passo, de banca) é tolerada, porque o gate de publicação a cobra, e a
/// recusa que depende do grafo, como a etapa sem nota que não declara forma, também é do gate.
/// </summary>
/// <remarks>
/// A etapa de nota do ENEM não declara forma nem regra: a nota vem do INEP. Quando recusa por isso, não
/// repete as demais recusas, que seriam consequência da mesma causa. Recebe só o código da regra, para a
/// Application poder rodar a coerência de todas as etapas antes de ler o catálogo (ADR-0125); que a
/// regra exista nele e seja do tipo de consolidação é conferido por ela (ADR-0042). O código ser do rol
/// conhecido é a defesa do domínio, a mesma que o decodificador do envelope aplica ao reidratar.
/// </remarks>
public static class CoerenciaDaAvaliacaoDaEtapa
{
    public static List<FieldError> Validar(AvaliacaoParaCoerencia avaliacao)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);
        if (avaliacao.NotaDeOrigemNoEnem)
        {
            return RecusasDaAvaliacaoNaEtapaDoEnem(avaliacao);
        }

        FieldError?[] recusas =
        [
            RecusaDaRegra(avaliacao.Forma, avaliacao.RegraConsolidacaoCodigo),
            RecusaDoPasso(avaliacao.Forma, avaliacao.PassoDoLancamento),
            RecusaDaFormaNoTipoQueExigeCorrecao(avaliacao),
            RecusaDaRegraNoTipoQueExigeCorrecao(avaliacao),
        ];
        List<FieldError> erros = [.. recusas.OfType<FieldError>()];
        if (avaliacao.DeclaraEscala)
        {
            erros.AddRange(RecusasDaEscalaDeAtendimento(avaliacao).OfType<FieldError>());
        }

        return erros;
    }

    private static List<FieldError> RecusasDaAvaliacaoNaEtapaDoEnem(AvaliacaoParaCoerencia avaliacao) =>
        [.. RecusasDaFormaEDaRegraNoEnem(avaliacao)
            .Concat(RecusasDosItensEDaEscalaNoEnem(avaliacao))
            .OfType<FieldError>()];

    private static FieldError?[] RecusasDaFormaEDaRegraNoEnem(AvaliacaoParaCoerencia avaliacao) =>
    [
        Recusa(avaliacao.Forma is not null, "formaDeAvaliacao", EtapaProcessoErrorCodes.AvaliacaoNaEtapaDoEnem,
            "A etapa de nota do ENEM não declara forma de avaliação: a nota vem do INEP."),
        Recusa(avaliacao.RegraConsolidacaoCodigo is not null, "regraConsolidacao", EtapaProcessoErrorCodes.AvaliacaoNaEtapaDoEnem,
            "A etapa de nota do ENEM não declara regra de consolidação: a nota vem do INEP."),
    ];

    private static FieldError?[] RecusasDosItensEDaEscalaNoEnem(AvaliacaoParaCoerencia avaliacao) =>
    [
        Recusa(avaliacao.DeclaraItens, "itensDeAvaliacao", EtapaProcessoErrorCodes.ItensNaEtapaDoEnem,
            "A etapa de nota do ENEM não declara itens de avaliação: a nota vem do INEP."),
        Recusa(avaliacao.DeclaraEscala, "escalaDeAtendimento", EtapaProcessoErrorCodes.ItensNaEtapaDoEnem,
            "A etapa de nota do ENEM não declara escala de atendimento: a nota vem do INEP."),
    ];

    private static FieldError? RecusaDaRegra(FormaDeAvaliacao? forma, string? regraConsolidacaoCodigo)
    {
        if (regraConsolidacaoCodigo is null)
        {
            return null;
        }

        return Recusa(forma != FormaDeAvaliacao.Humana, "regraConsolidacao", EtapaProcessoErrorCodes.RegraDeConsolidacaoForaDaFormaHumana,
                "A regra de consolidação só se aplica à avaliação humana: com mais de um avaliador é que as notas precisam ser combinadas.")
            ?? Recusa(!RegraConsolidacaoCodigo.Rol.Contains(regraConsolidacaoCodigo), "regraConsolidacao",
                EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida,
                $"A regra '{regraConsolidacaoCodigo}' não é uma regra de consolidação do catálogo.");
    }

    private static FieldError? RecusaDaFormaNoTipoQueExigeCorrecao(AvaliacaoParaCoerencia avaliacao) =>
        Recusa(avaliacao.ExigeCorrecaoIndependente && avaliacao.Forma == FormaDeAvaliacao.Objetiva, "formaDeAvaliacao",
            EtapaProcessoErrorCodes.FormaObjetivaEmTipoQueExigeCorrecaoIndependente,
            "O tipo da etapa exige correção independente, que só existe na avaliação humana: na avaliação objetiva ninguém corrige.");

    private static FieldError? RecusaDaRegraNoTipoQueExigeCorrecao(AvaliacaoParaCoerencia avaliacao) =>
        Recusa(avaliacao.ExigeCorrecaoIndependente && avaliacao.RegraConsolidacaoCodigo == RegraConsolidacaoCodigo.AvaliacaoUnica,
            "regraConsolidacao", EtapaProcessoErrorCodes.AvaliacaoUnicaEmTipoQueExigeCorrecaoIndependente,
            "O tipo da etapa exige correção independente, e a avaliação única tem um só corretor: use uma regra de consolidação de mais de um corretor.");

    private static FieldError? RecusaDoPasso(FormaDeAvaliacao? forma, decimal? passoDoLancamento) =>
        Recusa(forma == FormaDeAvaliacao.Objetiva && passoDoLancamento is not null, "passoDoLancamento",
            EtapaProcessoErrorCodes.PassoNaFormaObjetiva,
            "O passo do lançamento só se aplica à avaliação humana: na avaliação objetiva ninguém lança a nota.");

    private static FieldError?[] RecusasDaEscalaDeAtendimento(AvaliacaoParaCoerencia avaliacao) =>
    [
        Recusa(!avaliacao.DeclaraItens, "escalaDeAtendimento", EtapaProcessoErrorCodes.EscalaSemItens,
            "A escala de atendimento é aplicada a cada item: sem itens de avaliação, não há o que atender."),
        Recusa(avaliacao.Forma == FormaDeAvaliacao.Objetiva, "escalaDeAtendimento", EtapaProcessoErrorCodes.EscalaNaFormaObjetiva,
            "A escala de atendimento só se aplica à avaliação humana: na avaliação objetiva ninguém escolhe o nível."),
        Recusa(avaliacao.PassoDoLancamento is not null, "passoDoLancamento", EtapaProcessoErrorCodes.PassoComEscalaDeAtendimento,
            "Com escala de atendimento a banca escolhe o nível e não digita a nota, então a etapa não declara passo do lançamento."),
    ];

    private static FieldError? Recusa(bool recusar, string campo, string codigo, string mensagem) =>
        recusar ? new FieldError(campo, new DomainError(codigo, mensagem)) : null;
}
