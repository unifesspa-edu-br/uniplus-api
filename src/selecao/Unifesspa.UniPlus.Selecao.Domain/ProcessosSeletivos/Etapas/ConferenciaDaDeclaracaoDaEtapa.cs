namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que uma declaração de etapa precisa cumprir para valer: a forma básica, que não depende do cadastro
/// de tipos de etapa, e o que o tipo resolvido admite: o caráter e a coerência da avaliação. As recusas se
/// acumulam (ADR-0125).
/// </summary>
public static class ConferenciaDaDeclaracaoDaEtapa
{
    /// <summary>
    /// Confere nome, caráter, ordem, peso, nota mínima, escala e itens sem precisar do tipo de etapa, para
    /// o handler confirmar a forma de todas as etapas do payload antes de resolver o tipo no cadastro. A
    /// coerência entre forma de avaliação e regra de consolidação é de <see cref="CoerenciaDaAvaliacaoDaEtapa"/>.
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(
        string? nome,
        CaraterEtapa carater,
        int? ordem,
        DeclaracaoDaAvaliacaoDaEtapa avaliacao,
        IReadOnlyList<ItemDeAvaliacaoDeclarado?>? itens)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);

        List<FieldError> erros = ValidarNome(nome);
        erros.AddRange(CaraterDaEtapa.ValidarDeclarado(carater, avaliacao.Peso, avaliacao.NotaMinima));
        decimal? notaMinimaAceita = erros.Exists(static e => e.Field == "notaMinima") ? null : avaliacao.NotaMinima;
        erros.AddRange(EscalaDoLancamento.Validar(notaMinimaAceita, avaliacao.NotaMaxima, avaliacao.PassoDoLancamento));
        erros.AddRange(ValidarOrdem(ordem));
        erros.AddRange(ItemDeAvaliacao.Validar(itens, avaliacao.PassoDoLancamento));
        erros.AddRange(NiveisDeAtendimento.Validar(avaliacao.EscalaDeAtendimento));

        return erros;
    }

    internal static List<FieldError> Violacoes(
        IdentificacaoDaEtapa identificacao, DeclaracaoDaAvaliacaoDaEtapa avaliacao, IReadOnlyList<ItemDeAvaliacaoDeclarado>? itens)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(identificacao.TipoEtapa, nameof(identificacao));

        TipoEtapaSnapshot tipo = identificacao.TipoEtapa;
        List<FieldError> erros = ValidarFormaBasica(identificacao.Nome, identificacao.Carater, identificacao.Ordem, avaliacao, itens);
        erros.AddRange(CaraterDaEtapa.ValidarAdmitido(identificacao.Carater, tipo.AdmitePontuacao, tipo.AdmiteEliminacao, tipo.Nome));
        erros.AddRange(CoerenciaDaAvaliacaoDaEtapa.Validar(new AvaliacaoParaCoerencia(
            tipo.NotaDeOrigemNoEnem, avaliacao.FormaDeAvaliacao, avaliacao.RegraConsolidacao?.Codigo,
            avaliacao.PassoDoLancamento, DeclaraItens: itens?.Count > 0, DeclaraEscala: avaliacao.EscalaDeAtendimento?.Count > 0,
            ExigeCorrecaoIndependente: tipo.ExigeCorrecaoIndependente)));

        return erros;
    }

    internal static void ExigirIdentidadeCongelada(Guid id, string nome, CaraterEtapa carater, TipoEtapaSnapshot tipoEtapa)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentNullException.ThrowIfNull(tipoEtapa);
        ExigirIdCongelado(id);
        if (carater == CaraterEtapa.Nenhum)
        {
            throw new ArgumentException(
                "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas).", nameof(carater));
        }
    }

    internal static void ExigirDeclaracaoPresente(
        IdentificacaoDaEtapa identificacao,
        AvaliacaoDeclaradaDaEtapa avaliacao,
        JanelaDeTempo janela,
        PublicacoesDaEtapa publicacoes,
        IReadOnlyList<BancaDaEtapa> bancas)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(publicacoes);
        ArgumentNullException.ThrowIfNull(bancas);
    }

    internal static void ExigirIdCongelado(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A etapa reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }
    }

    /// <summary>
    /// A recusa do mesmo par ato e papel declarado duas vezes: dois registros idênticos tornariam ambíguo o
    /// que o recurso ancora. O ato é comparado em NFC, como o documento canônico o emite.
    /// </summary>
    internal static DomainError? AtoRepetido(IEnumerable<ProdutoDaEtapa> produtos) =>
        ProdutosPublicados.PrimeiroParRepetido(produtos, HashCanonicalComputer.NormalizeNfc) is { } duplicado
            ? new DomainError(
                "EtapaProcesso.AtoDuplicadoNaEtapa",
                $"A etapa declara o ato '{duplicado.AtoCodigo}' mais de uma vez no mesmo papel — o par ato e papel é declarado uma única vez por etapa.")
            : null;

    internal static DomainError? JanelaInvertida(JanelaDeTempo janela) =>
        janela.Invertida
            ? new DomainError("EtapaProcesso.JanelaInvertida", "A data de término do período da etapa é anterior à data de início.")
            : null;

    private static List<FieldError> ValidarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return [new("nome", new DomainError("EtapaProcesso.NomeObrigatorio", "O nome da etapa é obrigatório."))];
        }

        return nome.Trim().Length > EtapaProcesso.NomeMaxLength
            ? [new("nome", new DomainError(
                "EtapaProcesso.NomeTamanho", $"O nome da etapa deve ter no máximo {EtapaProcesso.NomeMaxLength} caracteres."))]
            : [];
    }

    private static List<FieldError> ValidarOrdem(int? ordem) =>
        ordem is <= 0
            ? [new("ordem", new DomainError(
                "EtapaProcesso.OrdemInvalida", "A ordem da etapa, quando informada, deve ser maior que zero."))]
            : [];
}
