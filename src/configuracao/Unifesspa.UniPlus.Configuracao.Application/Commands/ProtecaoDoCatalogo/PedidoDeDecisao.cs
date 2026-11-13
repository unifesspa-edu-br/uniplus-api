namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão pedida, conferida na forma antes de qualquer leitura do item (ADR-0125): o item, a decisão com os seus
/// fundamentos, a proteção que o Representante do controlador conferiu e o que ele leu do circuito. Todas as recusas de
/// forma saem num único lote.
/// </summary>
/// <param name="Item">O cadastro e o identificador do item.</param>
/// <param name="Decisao">A decisão, a classificação recomendada (só no ajuste) e a justificativa.</param>
/// <param name="ProtecaoConferida">A proteção que o Representante do controlador viu; a decisão sobre outra responde conflito.</param>
/// <param name="Lida">O ciclo e a contagem de manifestações lidos; a decisão sobre outro estado responde conflito.</param>
public sealed record PedidoDeDecisao(ItemDaProtecao Item, DecisaoFundamentada Decisao, ProtecaoDeDados ProtecaoConferida, LeituraDoCircuito Lida)
{
    private const string Aprovar = DecisoesDaProtecaoDoCatalogo.Aprovar;
    private const string RecomendarAjuste = DecisoesDaProtecaoDoCatalogo.RecomendarAjuste;
    private const string Reprovar = DecisoesDaProtecaoDoCatalogo.Reprovar;

    private static readonly FieldError DecisaoInvalida = new("decisao", new DomainError(
        ProtecaoDoCatalogoErrorCodes.DecisaoInvalida, $"A decisão deve ser {Aprovar}, {RecomendarAjuste} ou {Reprovar}."));

    private static readonly FieldError RecomendacaoNaoCabe = new("classificacaoRecomendada", new DomainError(
        ProtecaoDoCatalogoErrorCodes.DadoDaDecisaoNaoCabe,
        "Só o ajuste recomendado indica uma classificação: retire a classificação recomendada desta decisão."));

    /// <summary>
    /// O pedido conferido, ou o lote das recusas: cadastro e decisão fora do vocabulário, classificação recomendada
    /// ausente no ajuste ou presente nas demais decisões, justificativa ausente na reprovação ou longa demais, proteção
    /// conferida ausente ou fora do vocabulário, e ciclo ou contagem de manifestações lidos ausentes ou inválidos.
    /// </summary>
    public static Result<PedidoDeDecisao> Ler(DecidirProtecaoDoCatalogoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<FieldError> erros = [];
        ItemDaProtecao? item = ItemDaProtecao.Ler(command.Item.Cadastro, command.Item.ItemId, erros);
        DecisaoFundamentada? decisao = LerDecisao(command.Decisao, erros);
        ProtecaoDeDados? conferida = LerProtecaoConferida(command.ProtecaoConferida, erros);
        LeituraDoCircuito? lida = LeituraDoCircuito.Ler(command.Lida.Ciclo, command.Lida.ManifestacoesNoCiclo, erros);

        return erros.Count > 0
            ? Result<PedidoDeDecisao>.ValidationFailure(erros)
            : Result<PedidoDeDecisao>.Success(new PedidoDeDecisao(item!.Value, decisao!, conferida!.Value, lida!.Value));
    }

    private static DecisaoFundamentada? LerDecisao(DecisaoInformada informada, List<FieldError> erros)
    {
        AtoDaProtecao? ato = LerAto(informada.Decisao, erros);
        ClassificacaoDeProtecao? recomendada = LerRecomendacao(ato, informada.ClassificacaoRecomendada, erros);
        Fundamentacao? justificativa = Fundamentacao.Ler(
            informada.Justificativa, obrigatoria: ato == AtoDaProtecao.Reprovacao, RecusasDaFundamentacao.DaJustificativa, erros);

        return ato is null ? null : new DecisaoFundamentada(ato.Value, recomendada, justificativa);
    }

    private static AtoDaProtecao? LerAto(string? decisao, List<FieldError> erros)
    {
        AtoDaProtecao? ato = decisao?.Trim() switch
        {
            Aprovar => AtoDaProtecao.Aprovacao,
            RecomendarAjuste => AtoDaProtecao.AjusteRecomendado,
            Reprovar => AtoDaProtecao.Reprovacao,
            _ => null,
        };

        if (ato is null)
        {
            erros.Add(DecisaoInvalida);
        }

        return ato;
    }

    // Aqui só se converte o token. A regra da classificação recomendada é do domínio: no ajuste, obrigatória e da escala
    // (a mesma que a transição aplica); nas demais decisões, ela não cabe.
    private static ClassificacaoDeProtecao? LerRecomendacao(AtoDaProtecao? ato, string? token, List<FieldError> erros)
    {
        ClassificacaoDeProtecao? lida = TokensDoFato.Classificacao(token);
        if (ato == AtoDaProtecao.AjusteRecomendado)
        {
            Result conferida = AprovacaoDaProtecao.ConferirClassificacaoRecomendada(lida);
            erros.AddRange(conferida.Errors);
            return conferida.IsSuccess ? lida : null;
        }

        if (ato is AtoDaProtecao.Aprovacao or AtoDaProtecao.Reprovacao && lida is not null)
        {
            erros.Add(RecomendacaoNaoCabe);
        }

        return null;
    }

    private static ProtecaoDeDados? LerProtecaoConferida(ProtecaoInformada informada, List<FieldError> erros)
    {
        ClassificacaoDeProtecao? classificacao = TokensDoFato.Classificacao(informada.ClassificacaoProtecao);
        HipoteseLegalTratamento? hipotese = TokensDoFato.HipoteseLegal(informada.HipoteseLegal);
        string? finalidade = string.IsNullOrWhiteSpace(informada.FinalidadeTratamento) ? null : informada.FinalidadeTratamento.Trim();
        int antes = erros.Count;
        ExigirConferida("classificacaoProtecao", classificacao is not null and not (ClassificacaoDeProtecao)TokensDoFato.ForaDoVocabulario, erros);
        ExigirConferida("hipoteseLegal", hipotese is not null and not (HipoteseLegalTratamento)TokensDoFato.ForaDoVocabulario, erros);
        ExigirConferida("finalidadeTratamento", finalidade is not null, erros);

        return erros.Count == antes ? new ProtecaoDeDados(classificacao!.Value, finalidade!, hipotese!.Value) : null;
    }

    private static void ExigirConferida(string campo, bool conferida, List<FieldError> erros)
    {
        if (!conferida)
        {
            erros.Add(new(campo, new DomainError(
                ProtecaoDoCatalogoErrorCodes.ProtecaoConferidaInvalida,
                "A decisão exige a proteção que o Representante do controlador conferiu — classificação, hipótese legal e finalidade —, dentro do vocabulário fechado.")));
        }
    }
}
