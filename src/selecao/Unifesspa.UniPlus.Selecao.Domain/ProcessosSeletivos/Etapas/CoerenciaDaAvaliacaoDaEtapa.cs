namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

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
    public static List<FieldError> Validar(
        bool notaDeOrigemNoEnem,
        decimal? passoDoLancamento,
        FormaDeAvaliacao? forma,
        string? regraConsolidacaoCodigo,
        bool declaraItens = false,
        bool declaraEscala = false)
    {
        if (notaDeOrigemNoEnem)
        {
            return RecusasDaAvaliacaoNaEtapaDoEnem(forma, regraConsolidacaoCodigo, declaraItens, declaraEscala);
        }

        FieldError?[] recusas = [RecusaDaRegra(forma, regraConsolidacaoCodigo), RecusaDoPasso(forma, passoDoLancamento)];
        List<FieldError> erros = [.. recusas.OfType<FieldError>()];
        if (declaraEscala)
        {
            erros.AddRange(RecusasDaEscalaDeAtendimento(forma, passoDoLancamento, declaraItens).OfType<FieldError>());
        }

        return erros;
    }

    private static List<FieldError> RecusasDaAvaliacaoNaEtapaDoEnem(
        FormaDeAvaliacao? forma, string? regraConsolidacaoCodigo, bool declaraItens, bool declaraEscala) =>
        [.. RecusasDaFormaEDaRegraNoEnem(forma, regraConsolidacaoCodigo)
            .Concat(RecusasDosItensEDaEscalaNoEnem(declaraItens, declaraEscala))
            .OfType<FieldError>()];

    private static FieldError?[] RecusasDaFormaEDaRegraNoEnem(FormaDeAvaliacao? forma, string? regraConsolidacaoCodigo) =>
    [
        Recusa(forma is not null, "formaDeAvaliacao", EtapaProcesso.AvaliacaoNaEtapaDoEnem,
            "A etapa de nota do ENEM não declara forma de avaliação: a nota vem do INEP."),
        Recusa(regraConsolidacaoCodigo is not null, "regraConsolidacao", EtapaProcesso.AvaliacaoNaEtapaDoEnem,
            "A etapa de nota do ENEM não declara regra de consolidação: a nota vem do INEP."),
    ];

    private static FieldError?[] RecusasDosItensEDaEscalaNoEnem(bool declaraItens, bool declaraEscala) =>
    [
        Recusa(declaraItens, "itensDeAvaliacao", EtapaProcesso.ItensNaEtapaDoEnem,
            "A etapa de nota do ENEM não declara itens de avaliação: a nota vem do INEP."),
        Recusa(declaraEscala, "escalaDeAtendimento", EtapaProcesso.ItensNaEtapaDoEnem,
            "A etapa de nota do ENEM não declara escala de atendimento: a nota vem do INEP."),
    ];

    private static FieldError? RecusaDaRegra(FormaDeAvaliacao? forma, string? regraConsolidacaoCodigo)
    {
        if (regraConsolidacaoCodigo is null)
        {
            return null;
        }

        return Recusa(forma != FormaDeAvaliacao.Humana, "regraConsolidacao", EtapaProcesso.RegraDeConsolidacaoForaDaFormaHumana,
                "A regra de consolidação só se aplica à avaliação humana: com mais de um avaliador é que as notas precisam ser combinadas.")
            ?? Recusa(!RegraConsolidacaoCodigo.Rol.Contains(regraConsolidacaoCodigo), "regraConsolidacao",
                EtapaProcesso.RegraDeConsolidacaoInvalida,
                $"A regra '{regraConsolidacaoCodigo}' não é uma regra de consolidação do catálogo.");
    }

    private static FieldError? RecusaDoPasso(FormaDeAvaliacao? forma, decimal? passoDoLancamento) =>
        Recusa(forma == FormaDeAvaliacao.Objetiva && passoDoLancamento is not null, "passoDoLancamento",
            EtapaProcesso.PassoNaFormaObjetiva,
            "O passo do lançamento só se aplica à avaliação humana: na avaliação objetiva ninguém lança a nota.");

    private static FieldError?[] RecusasDaEscalaDeAtendimento(FormaDeAvaliacao? forma, decimal? passoDoLancamento, bool declaraItens) =>
    [
        Recusa(!declaraItens, "escalaDeAtendimento", EtapaProcesso.EscalaSemItens,
            "A escala de atendimento é aplicada a cada item: sem itens de avaliação, não há o que atender."),
        Recusa(forma == FormaDeAvaliacao.Objetiva, "escalaDeAtendimento", EtapaProcesso.EscalaNaFormaObjetiva,
            "A escala de atendimento só se aplica à avaliação humana: na avaliação objetiva ninguém escolhe o nível."),
        Recusa(passoDoLancamento is not null, "passoDoLancamento", EtapaProcesso.PassoComEscalaDeAtendimento,
            "Com escala de atendimento a banca escolhe o nível e não digita a nota, então a etapa não declara passo do lançamento."),
    ];

    private static FieldError? Recusa(bool recusar, string campo, string codigo, string mensagem) =>
        recusar ? new FieldError(campo, new DomainError(codigo, mensagem)) : null;
}
