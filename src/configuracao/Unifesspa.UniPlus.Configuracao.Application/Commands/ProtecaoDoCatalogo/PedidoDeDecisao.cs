namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão pedida, conferida na forma antes de qualquer leitura do item (ADR-0125): o item, a decisão com os seus
/// fundamentos e a proteção que o aprovador conferiu. Todas as recusas de forma saem num único lote.
/// </summary>
/// <param name="Item">O cadastro e o identificador do item.</param>
/// <param name="Decisao">A decisão, a classificação recomendada (só no ajuste) e a justificativa.</param>
/// <param name="ProtecaoConferida">A proteção que o aprovador viu; a decisão sobre outra responde conflito.</param>
public sealed record PedidoDeDecisao(ItemDaProtecao Item, DecisaoFundamentada Decisao, ProtecaoDeDados ProtecaoConferida)
{
    private const string Aprovar = DecisoesDaProtecaoDoCatalogo.Aprovar;
    private const string RecomendarAjuste = DecisoesDaProtecaoDoCatalogo.RecomendarAjuste;
    private const string Reprovar = DecisoesDaProtecaoDoCatalogo.Reprovar;

    private static readonly FieldError DecisaoInvalida = new("decisao", new DomainError(
        ProtecaoDoCatalogoErrorCodes.DecisaoInvalida, $"A decisão deve ser {Aprovar}, {RecomendarAjuste} ou {Reprovar}."));

    private static readonly FieldError RecomendacaoAusente = new("classificacaoRecomendada", new DomainError(
        ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria,
        "O ajuste exige a classificação de proteção de dados recomendada, dentro do vocabulário fechado."));

    private static readonly FieldError RecomendacaoNaoCabe = new("classificacaoRecomendada", new DomainError(
        ProtecaoDoCatalogoErrorCodes.DadoDaDecisaoNaoCabe,
        "Só o ajuste recomendado indica uma classificação: retire a classificação recomendada desta decisão."));

    /// <summary>
    /// O pedido conferido, ou o lote das recusas: cadastro e decisão fora do vocabulário, classificação recomendada
    /// ausente no ajuste ou presente nas demais decisões, justificativa ausente na reprovação ou longa demais, e proteção
    /// conferida ausente ou fora do vocabulário.
    /// </summary>
    public static Result<PedidoDeDecisao> Ler(DecidirProtecaoDoCatalogoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<FieldError> erros = [];
        ItemDaProtecao? item = ItemDaProtecao.Ler(command.Cadastro, command.ItemId, erros);
        DecisaoFundamentada? decisao = LerDecisao(command, erros);
        ProtecaoDeDados? conferida = LerProtecaoConferida(command, erros);

        return erros.Count > 0
            ? Result<PedidoDeDecisao>.ValidationFailure(erros)
            : Result<PedidoDeDecisao>.Success(new PedidoDeDecisao(item!.Value, decisao!, conferida!.Value));
    }

    private static DecisaoFundamentada? LerDecisao(DecidirProtecaoDoCatalogoCommand command, List<FieldError> erros)
    {
        AtoDaProtecao? ato = LerAto(command.Decisao, erros);
        ClassificacaoDeProtecao? recomendada = LerRecomendacao(ato, command.ClassificacaoRecomendada, erros);
        Fundamentacao? justificativa = Fundamentacao.Ler(
            command.Justificativa, obrigatoria: ato == AtoDaProtecao.Reprovacao, RecusasDaFundamentacao.DaJustificativa, erros);

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

    // A classificação recomendada é do ajuste: nele é obrigatória e do vocabulário; nas demais decisões não cabe.
    private static ClassificacaoDeProtecao? LerRecomendacao(AtoDaProtecao? ato, string? token, List<FieldError> erros)
    {
        ClassificacaoDeProtecao? lida = TokensDoFato.Classificacao(token);
        bool valida = lida is not null and not (ClassificacaoDeProtecao)TokensDoFato.ForaDoVocabulario;
        if (ato == AtoDaProtecao.AjusteRecomendado && !valida)
        {
            erros.Add(RecomendacaoAusente);
        }
        else if (ato is AtoDaProtecao.Aprovacao or AtoDaProtecao.Reprovacao && lida is not null)
        {
            erros.Add(RecomendacaoNaoCabe);
        }

        return ato == AtoDaProtecao.AjusteRecomendado && valida ? lida : null;
    }

    private static ProtecaoDeDados? LerProtecaoConferida(DecidirProtecaoDoCatalogoCommand command, List<FieldError> erros)
    {
        ClassificacaoDeProtecao? classificacao = TokensDoFato.Classificacao(command.ClassificacaoProtecao);
        HipoteseLegalTratamento? hipotese = TokensDoFato.HipoteseLegal(command.HipoteseLegal);
        string? finalidade = string.IsNullOrWhiteSpace(command.FinalidadeTratamento) ? null : command.FinalidadeTratamento.Trim();
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
                "A decisão exige a proteção que o aprovador conferiu — classificação, hipótese legal e finalidade —, dentro do vocabulário fechado.")));
        }
    }
}
