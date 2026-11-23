namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os itens do checklist que dizem como a nota de cada etapa é lançada e obtida. Cada item lista as etapas que reprovam e as
/// que ficam indeterminadas: a que depende do valor de um dado recusado nunca fica conforme por exclusão. Os itens são
/// independentes, e uma tentativa de publicar devolve de uma vez tudo o que falta.
/// </summary>
internal static class ConformidadeDaAvaliacaoDasEtapas
{
    private static readonly DadoDaEtapa[] DadosDoModo = [DadoDaEtapa.Carater, DadoDaEtapa.Peso, DadoDaEtapa.NotaMinima, DadoDaEtapa.Conceito];

    private static readonly Verificacao[] ItensDaEscalaVerificados =
    [
        new("etapa_com_nota_sem_nota_maxima", "Nota máxima de cada etapa com nota que não é do ENEM", ForaDoEnem, DadosDoModo,
            static (etapa, citacao) => ModoEm(etapa, citacao, ModoDeAvaliacaoDaEtapa.NotaNumerica) && !etapa.NotaMaxima.HasValue),
        new("etapa_sem_nota_com_escala", "Nota máxima e passo só em etapa com nota", ForaDoEnem, DadosDoModo,
            static (etapa, citacao) => ModoEm(etapa, citacao, ModoDeAvaliacaoDaEtapa.SemNotaNemConceito) && DeclaraEscala(etapa)),
        new("etapa_do_enem_com_escala", "Etapa de nota do ENEM sem nota máxima nem passo", DoEnem, [],
            static (etapa, _) => DeclaraEscala(etapa)),
        new("nota_minima_da_regra_acima_da_nota_maxima", "Nota mínima da regra de eliminação até a nota máxima da etapa", Todas, [DadoDaEtapa.NotaMaxima],
            static (etapa, citacao) => etapa.NotaMaxima is { } notaMaxima && citacao.NotasMinimasDaEliminacao.Any(notaMinima => notaMinima > notaMaxima)),
    ];

    private static readonly Verificacao[] ItensDaAvaliacaoVerificados =
    [
        new("etapa_com_nota_sem_forma_de_avaliacao", "Forma de avaliação de cada etapa com nota ou conceito que não é do ENEM", ForaDoEnem, DadosDoModo,
            static (etapa, citacao) => ComNotaOuConceito(etapa, citacao) && etapa.Forma is null),
        new("etapa_sem_nota_com_forma_de_avaliacao", "Forma de avaliação e regra de consolidação só em etapa com nota ou conceito", ForaDoEnem, DadosDoModo,
            static (etapa, citacao) => SemNota(etapa, citacao) && (etapa.Forma is not null || etapa.RegraConsolidacao is not null)),
        new("etapa_sem_nota_com_itens_de_avaliacao", "Itens de avaliação e escala de atendimento só em etapa com nota", ForaDoEnem, DadosDoModo,
            static (etapa, citacao) => SemNota(etapa, citacao) && (etapa.PontuacoesDosItens.Count > 0 || etapa.TemEscalaDeAtendimento)),
        new("etapa_com_itens_fora_da_nota_maxima", "Soma das pontuações dos itens igual à nota máxima de cada etapa com itens", ForaDoEnem,
            [.. DadosDoModo, DadoDaEtapa.NotaMaxima, DadoDaEtapa.ItensDeAvaliacao],
            static (etapa, citacao) => ModoEm(etapa, citacao, ModoDeAvaliacaoDaEtapa.NotaNumerica) && ItensForaDaNotaMaxima(etapa)),
        new("etapa_humana_sem_regra_de_consolidacao", "Regra de consolidação de cada etapa de avaliação humana", ForaDoEnem, [.. DadosDoModo, DadoDaEtapa.FormaDeAvaliacao],
            static (etapa, citacao) => ComNotaOuConceito(etapa, citacao) && Humana(etapa) && etapa.RegraConsolidacao is null),
        new("etapa_humana_sem_passo_do_lancamento",
            "Passo do lançamento de cada etapa de avaliação humana com nota e sem escala de atendimento", ForaDoEnem,
            [.. DadosDoModo, DadoDaEtapa.FormaDeAvaliacao],
            static (etapa, citacao) => ModoEm(etapa, citacao, ModoDeAvaliacaoDaEtapa.NotaNumerica)
                && Humana(etapa) && etapa.PassoDoLancamento is null && !etapa.TemEscalaDeAtendimento),
        new("etapa_humana_sem_banca", "Ao menos uma banca em cada etapa de avaliação humana", ForaDoEnem, [.. DadosDoModo, DadoDaEtapa.FormaDeAvaliacao],
            static (etapa, citacao) => ComNotaOuConceito(etapa, citacao) && Humana(etapa) && !etapa.Bancas.TemBancas,
            EtapaDeclarada.ListaDasBancas),
    ];

    private enum Situacao
    {
        Conforme,
        Reprovada,
        Indeterminada,
    }

    internal static IReadOnlyList<ItemConformidade> ItensDaEscala(IReadOnlyList<EtapaDeclarada> etapas, CitacoesDeNota citacoes) =>
        Itens(ItensDaEscalaVerificados, etapas, citacoes);

    internal static IReadOnlyList<ItemConformidade> ItensDaAvaliacao(IReadOnlyList<EtapaDeclarada> etapas, CitacoesDeNota citacoes) =>
        Itens(ItensDaAvaliacaoVerificados, etapas, citacoes);

    private static List<ItemConformidade> Itens(Verificacao[] verificacoes, IReadOnlyList<EtapaDeclarada> etapas, CitacoesDeNota citacoes)
    {
        ArgumentNullException.ThrowIfNull(citacoes);

        return [.. verificacoes.Select(verificacao => Item(verificacao, etapas, citacoes))];
    }

    private static ItemConformidade Item(Verificacao verificacao, IReadOnlyList<EtapaDeclarada> etapas, CitacoesDeNota citacoes)
    {
        (EtapaDeclarada Etapa, Situacao Situacao)[] avaliadas =
            [.. etapas.Where(verificacao.Aplicavel).Select(etapa => (etapa, Avaliar(verificacao, etapa, citacoes.De(etapa.Id))))];
        EtapaDeclarada[] reprovadas = [.. avaliadas.Where(static a => a.Situacao == Situacao.Reprovada).Select(static a => a.Etapa)];
        EtapaDeclarada[] indeterminadas = [.. avaliadas.Where(static a => a.Situacao == Situacao.Indeterminada).Select(static a => a.Etapa)];

        return new ItemConformidade(
            verificacao.Codigo,
            DimensaoConformidade.Etapas,
            verificacao.Mensagem + Eco("etapas", reprovadas) + Eco("etapas com declaração recusada", indeterminadas),
            reprovadas.Length == 0 && indeterminadas.Length == 0);
    }

    private static Situacao Avaliar(Verificacao verificacao, EtapaDeclarada etapa, CitacaoDaEtapa citacao) =>
        verificacao.ValoresLidos.Any(etapa.Recusado) || LeEListaComParteRecusada(verificacao, etapa) ? Situacao.Indeterminada
        : verificacao.Reprova(etapa, citacao) ? Situacao.Reprovada
        : Situacao.Conforme;

    private static bool LeEListaComParteRecusada(Verificacao verificacao, EtapaDeclarada etapa) =>
        verificacao.ListaLida is { } lista && etapa.ParteRecusadaEm(lista);

    // O nome é texto livre do operador e sai ecoado, em ordem estável.
    private static string Eco(string rotulo, EtapaDeclarada[] etapas) =>
        etapas.Length == 0
            ? string.Empty
            : $" ({rotulo}: {string.Join("; ", etapas
                .OrderBy(static etapa => etapa.Ordem ?? int.MaxValue)
                .Select(static etapa => $"\"{CaracteresInvisiveis.ParaEco(etapa.Nome, TextoCongelado.TamanhoMaximoEcoado)}\""))})";

    private static bool ForaDoEnem(EtapaDeclarada etapa) => !etapa.NotaVemDoEnem;

    private static bool DoEnem(EtapaDeclarada etapa) => etapa.NotaVemDoEnem;

    private static bool Todas(EtapaDeclarada etapa) => etapa is not null;

    private static bool DeclaraEscala(EtapaDeclarada etapa) => etapa.NotaMaxima.HasValue || etapa.PassoDoLancamento.HasValue;

    private static bool Humana(EtapaDeclarada etapa) => etapa.Forma == FormaDeAvaliacao.Humana;

    private static bool ModoEm(EtapaDeclarada etapa, CitacaoDaEtapa citacao, ModoDeAvaliacaoDaEtapa modo) =>
        etapa.ModoDeAvaliacao(citacao) == modo;

    private static bool ComNotaOuConceito(EtapaDeclarada etapa, CitacaoDaEtapa citacao) =>
        ModoEm(etapa, citacao, ModoDeAvaliacaoDaEtapa.NotaNumerica) || ModoEm(etapa, citacao, ModoDeAvaliacaoDaEtapa.ConceitoCategorico);

    private static bool SemNota(EtapaDeclarada etapa, CitacaoDaEtapa citacao) =>
        ModoEm(etapa, citacao, ModoDeAvaliacaoDaEtapa.SemNotaNemConceito);

    private static bool ItensForaDaNotaMaxima(EtapaDeclarada etapa) =>
        etapa.PontuacoesDosItens.Count > 0 && etapa.NotaMaxima is { } notaMaxima && etapa.PontuacoesDosItens.Sum() != notaMaxima;

    private sealed record Verificacao(
        string Codigo,
        string Mensagem,
        Func<EtapaDeclarada, bool> Aplicavel,
        DadoDaEtapa[] ValoresLidos,
        Func<EtapaDeclarada, CitacaoDaEtapa, bool> Reprova,
        string? ListaLida = null);
}
