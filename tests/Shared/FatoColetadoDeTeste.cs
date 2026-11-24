namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Monta o item coletado dos testes: sem dizer nada além do fato e da ordem, é um campo booleano de
/// obrigatoriedade <c>Nunca</c>, sem regras, que copia do catálogo a classificação pessoal. Cada método
/// devolve um construtor novo, então um ponto de partida compartilhado não vaza para os demais.
/// </summary>
internal sealed record FatoColetadoDeTeste
{
    private string FatoCodigo { get; init; } = string.Empty;

    private int Ordem { get; init; }

    private string? Rotulo { get; init; }

    private TipoRenderizacao Tipo { get; init; } = TipoRenderizacao.SimOuNao;

    private Obrigatoriedade Obrigatoriedade { get; init; } = Obrigatoriedade.Nunca;

    private PredicadoDnf? Exibicao { get; init; }

    private IReadOnlyList<RestricaoValor>? Restricoes { get; init; }

    private Impedimento? Impedimento { get; init; }

    private string? Secao { get; init; }

    private FinalidadeFormulario? Finalidade { get; init; } = FinalidadeFormulario.Inscricao;

    private string? Ajuda { get; init; }

    private bool Confirmacao { get; init; }

    private CopiaDoItemNoCatalogo Copia { get; init; } = ProtecaoDoFatoDeTeste.Copia();

    /// <summary>O item do fato na ordem dada, com o rótulo igual ao código do fato até o teste dar outro.</summary>
    public static FatoColetadoDeTeste Coletando(string fatoCodigo, int ordem) => new() { FatoCodigo = fatoCodigo, Ordem = ordem };

    /// <summary>O item igual ao dado, para o teste que o move de seção ou de ordem sem refazê-lo.</summary>
    public static FatoColetadoDeTeste Como(FatoColetado fato) =>
        Coletando(fato.FatoCodigo, fato.Ordem) with
        {
            Rotulo = fato.Rotulo,
            Tipo = fato.TipoRenderizacao,
            Obrigatoriedade = fato.Obrigatoriedade,
            Exibicao = fato.Exibicao,
            Restricoes = fato.Restricoes,
            Impedimento = fato.Impedimento,
            Secao = fato.EtapaCodigo,
            Finalidade = fato.Finalidade,
            Ajuda = fato.Ajuda,
            Confirmacao = fato.PedirConfirmacao,
            Copia = ProtecaoDoFatoDeTeste.Copia(fato.ClassificacaoProtecao, fato.OrigemValores, fato.Formato, fato.ComponenteExibicao),
        };

    public FatoColetadoDeTeste ComRotulo(string rotulo) => this with { Rotulo = rotulo };

    public FatoColetadoDeTeste DoTipo(TipoRenderizacao tipo) => this with { Tipo = tipo };

    /// <summary>Campo de texto com o formato que o catálogo dá ao fato.</summary>
    public FatoColetadoDeTeste DeTexto(string? formato) => DoTipo(TipoRenderizacao.Texto).ComFormato(formato);

    /// <summary>O formato da cópia do catálogo, para o teste em que o tipo do campo vem de fora.</summary>
    public FatoColetadoDeTeste ComFormato(string? formato) => this with { Copia = Copia with { Formato = formato } };

    public FatoColetadoDeTeste ComObrigatoriedade(Obrigatoriedade obrigatoriedade) => this with { Obrigatoriedade = obrigatoriedade };

    public FatoColetadoDeTeste ExibidoQuando(PredicadoDnf? exibicao) => this with { Exibicao = exibicao };

    public FatoColetadoDeTeste ComRestricoes(params IReadOnlyList<RestricaoValor>? restricoes) => this with { Restricoes = restricoes };

    public FatoColetadoDeTeste ComImpedimento(Impedimento impedimento) => this with { Impedimento = impedimento };

    public FatoColetadoDeTeste NaSecao(string? secao) => this with { Secao = secao };

    public FatoColetadoDeTeste NaOrdem(int ordem) => this with { Ordem = ordem };

    public FatoColetadoDeTeste DaFinalidade(FinalidadeFormulario finalidade) => this with { Finalidade = finalidade };

    public FatoColetadoDeTeste ComAjuda(string? ajuda) => this with { Ajuda = ajuda };

    public FatoColetadoDeTeste PedindoConfirmacao(bool pedir = true) => this with { Confirmacao = pedir };

    public FatoColetadoDeTeste ComClassificacao(string classificacaoProtecao) => this with { Copia = Copia with { ClassificacaoProtecao = classificacaoProtecao } };

    public FatoColetadoDeTeste ComOrigem(OrigemValoresColeta origem) => this with { Copia = Copia with { OrigemValores = origem } };

    public FatoColetadoDeTeste ComComponente(string? componenteExibicao) => this with { Copia = Copia with { ComponenteExibicao = componenteExibicao } };

    /// <summary>Toda a cópia do catálogo, para o teste que discute a hipótese legal, a finalidade ou a descrição.</summary>
    public FatoColetadoDeTeste ComCopia(CopiaDoItemNoCatalogo copia) => this with { Copia = copia };

    /// <summary>Cria o item pela fábrica, para o teste que olha a recusa.</summary>
    public Result<FatoColetado> Criar() =>
        FatoColetado.Criar(
            FatoCodigo,
            Ordem,
            Secao,
            new ApresentacaoDoCampo(Rotulo ?? FatoCodigo, Tipo, Ajuda, Confirmacao),
            new RegrasDoCampo(Exibicao, Obrigatoriedade, Restricoes ?? [], Impedimento),
            Copia,
            Finalidade);

    /// <summary>O item criado, para o teste que parte de um item válido.</summary>
    public FatoColetado Construir() => Criar().Value!;
}
