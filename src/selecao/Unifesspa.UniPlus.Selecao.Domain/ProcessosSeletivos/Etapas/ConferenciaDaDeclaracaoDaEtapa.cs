namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que uma declaração de etapa precisa cumprir para valer: a forma básica, que não depende do cadastro
/// de tipos de etapa, e a coerência da avaliação com o tipo resolvido. As recusas se acumulam (ADR-0125).
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
        ExigirTipoResolvido(identificacao.TipoEtapa);

        List<FieldError> erros = ValidarFormaBasica(identificacao.Nome, identificacao.Carater, identificacao.Ordem, avaliacao, itens);
        erros.AddRange(CoerenciaDaAvaliacaoDaEtapa.Validar(new AvaliacaoParaCoerencia(
            identificacao.TipoEtapa.NotaDeOrigemNoEnem, avaliacao.FormaDeAvaliacao, avaliacao.RegraConsolidacao?.Codigo,
            avaliacao.PassoDoLancamento, DeclaraItens: itens?.Count > 0, DeclaraEscala: avaliacao.EscalaDeAtendimento?.Count > 0,
            ExigeCorrecaoIndependente: identificacao.TipoEtapa.ExigeCorrecaoIndependente)));

        return erros;
    }

    internal static void ExigirIdentidadeCongelada(Guid id, string nome, CaraterEtapa carater, TipoEtapaSnapshot tipoEtapa)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ExigirTipoResolvido(tipoEtapa);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A etapa reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }

        if (carater == CaraterEtapa.Nenhum)
        {
            throw new ArgumentException(
                "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas).", nameof(carater));
        }
    }

    private static void ExigirTipoResolvido(TipoEtapaSnapshot tipoEtapa) => ArgumentNullException.ThrowIfNull(tipoEtapa);

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
