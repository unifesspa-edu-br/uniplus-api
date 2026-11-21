namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma etapa como foi declarada: os dados normalizados e as recusas da própria declaração, pelos mesmos predicados que
/// a gravação da etapa aplica. A declaração com recusa continua existindo, para o diagnóstico do processo poder ler o
/// que foi declarado; uma regra só deixa de avaliar a etapa quando lê o valor de um dado que a declaração recusou.
/// </summary>
public sealed record EtapaDeclarada
{
    private static readonly Dictionary<DadoDaEtapa, string> CampoDoDado = new()
    {
        [DadoDaEtapa.Nome] = "nome",
        [DadoDaEtapa.Carater] = "carater",
        [DadoDaEtapa.Peso] = "peso",
        [DadoDaEtapa.NotaMinima] = "notaMinima",
        [DadoDaEtapa.NotaMaxima] = "notaMaxima",
        [DadoDaEtapa.PassoDoLancamento] = "passoDoLancamento",
        [DadoDaEtapa.Ordem] = "ordem",
        [DadoDaEtapa.ItensDeAvaliacao] = "itensDeAvaliacao",
        [DadoDaEtapa.EscalaDeAtendimento] = "escalaDeAtendimento",
        [DadoDaEtapa.FormaDeAvaliacao] = "formaDeAvaliacao",
        [DadoDaEtapa.RegraConsolidacao] = "regraConsolidacao",
        [DadoDaEtapa.Conceito] = "conceito",
    };

    /// <summary>A lista dos produtos da etapa, pelo nome que <see cref="ParteRecusadaEm"/> reconhece.</summary>
    public const string ListaDosProdutos = "produtos";

    /// <summary>A lista das bancas da etapa, pelo nome que <see cref="ParteRecusadaEm"/> reconhece.</summary>
    public const string ListaDasBancas = "bancas";

    /// <summary>A lista das janelas recursais da etapa, pelo nome que <see cref="ParteRecusadaEm"/> reconhece.</summary>
    public const string ListaDosRecursos = "recursos";

    private readonly IdentificacaoDaEtapa _identificacao;
    private readonly AvaliacaoDeclaradaDaEtapa _avaliacao;
    private readonly IReadOnlySet<string> _caminhosComParteRecusada;

    private EtapaDeclarada(
        Guid id,
        IdentificacaoDaEtapa identificacao,
        AvaliacaoDeclaradaDaEtapa avaliacao,
        JanelaDeTempo janela,
        (AtosDaEtapa Atos, BancasDaEtapa Bancas) filhas,
        IReadOnlyList<FieldError> recusas,
        IReadOnlySet<string> caminhosComParteRecusada)
    {
        Id = id;
        _identificacao = identificacao;
        _avaliacao = avaliacao;
        Janela = janela;
        Atos = filhas.Atos;
        Bancas = filhas.Bancas;
        Recusas = recusas;
        _caminhosComParteRecusada = caminhosComParteRecusada;
    }

    public Guid Id { get; }

    public string Nome => _identificacao.Nome;

    public int? Ordem => _identificacao.Ordem;

    public string? FaseCodigo => _identificacao.FaseCodigo;

    public CaraterEtapa Carater => _identificacao.Carater;

    public bool NotaVemDoEnem => _identificacao.TipoEtapa.NotaDeOrigemNoEnem;

    public decimal? Peso => _avaliacao.Declaracao.Peso;

    public decimal? NotaMinima => _avaliacao.Declaracao.NotaMinima;

    public decimal? NotaMaxima => _avaliacao.Declaracao.NotaMaxima;

    public decimal? PassoDoLancamento => _avaliacao.Declaracao.PassoDoLancamento;

    public FormaDeAvaliacao? Forma => _avaliacao.Declaracao.FormaDeAvaliacao;

    public ReferenciaRegra? RegraConsolidacao => _avaliacao.Declaracao.RegraConsolidacao;

    public IReadOnlyList<decimal> PontuacoesDosItens => [.. _avaliacao.Itens.Select(static item => item.PontuacaoMaxima)];

    public bool TemEscalaDeAtendimento => _avaliacao.Declaracao.EscalaDeAtendimento is { Count: > 0 };

    public ConceitoDaEtapa? Conceito => _avaliacao.Conceito;

    public JanelaDeTempo Janela { get; }

    public AtosDaEtapa Atos { get; }

    public BancasDaEtapa Bancas { get; }

    public IReadOnlyList<FieldError> Recusas { get; }

    /// <summary>Se a etapa compõe a média: o caráter pontua e o peso foi declarado.</summary>
    public bool ComponeNota => CaraterDaEtapa.Pontua(Carater) && Peso.HasValue;

    /// <summary>Se os dados que decidem o modo de avaliação foram aceitos; só então o modo existe.</summary>
    public bool ModoDeterminado =>
        !Recusado(DadoDaEtapa.Carater) && !Recusado(DadoDaEtapa.Peso)
        && !Recusado(DadoDaEtapa.NotaMinima) && !Recusado(DadoDaEtapa.Conceito);

    /// <summary>Se os dados que decidem se a etapa compõe a média foram aceitos.</summary>
    internal bool ComposicaoDaMediaDeterminada => !Recusado(DadoDaEtapa.Carater) && !Recusado(DadoDaEtapa.Peso);

    public static EtapaDeclarada Declarar(
        Guid id,
        IdentificacaoDaEtapa identificacao,
        AvaliacaoDeclaradaDaEtapa avaliacao,
        JanelaDeTempo janela,
        PublicacoesDaEtapa publicacoes,
        IReadOnlyList<BancaDaEtapa> bancas,
        IReadOnlySet<string>? caminhosComParteRecusada = null)
    {
        ConferenciaDaDeclaracaoDaEtapa.ExigirIdCongelado(id);
        ConferenciaDaDeclaracaoDaEtapa.ExigirDeclaracaoPresente(identificacao, avaliacao, janela, publicacoes, bancas);
        IReadOnlySet<string> suspensas = caminhosComParteRecusada ?? FrozenSet<string>.Empty;

        return new EtapaDeclarada(
            id,
            identificacao.Normalizada(),
            avaliacao,
            janela,
            (new AtosDaEtapa(publicacoes), new BancasDaEtapa(bancas)),
            [.. RecusasDaAvaliacao(identificacao, avaliacao), .. RecusasDasPublicacoes(identificacao, janela, publicacoes, suspensas)],
            suspensas);
    }

    /// <summary>
    /// Se a leitura do documento recusou alguma parte da lista da etapa (<see cref="ListaDosProdutos"/>, <see cref="ListaDasBancas"/>,
    /// <see cref="ListaDosRecursos"/>): a lista declarada não é a que o autor escreveu, e a regra que a quantifica não a julga.
    /// </summary>
    public bool ParteRecusadaEm(string caminho) => _caminhosComParteRecusada.Contains(caminho);

    /// <summary>Se a declaração recusou o dado: há recusa cujo campo raiz é o dele.</summary>
    public bool Recusado(DadoDaEtapa dado) =>
        Recusas.Any(recusa => string.Equals(RaizDoCampo(recusa.Field), CampoDoDado[dado], StringComparison.Ordinal));

    /// <summary>O modo em que a etapa é avaliada, decidido só pelos dados aceitos e pela citação da nota na configuração.</summary>
    /// <exception cref="InvalidOperationException">Algum dado que decide o modo foi recusado.</exception>
    public ModoDeAvaliacaoDaEtapa ModoDeAvaliacao(CitacaoDaEtapa citacao)
    {
        ArgumentNullException.ThrowIfNull(citacao);
        if (!ModoDeterminado)
        {
            throw new InvalidOperationException("O modo de avaliação só existe quando os dados que o decidem foram aceitos.");
        }

        return NotaVemDoEnem ? ModoDeAvaliacaoDaEtapa.NotaDoEnem
            : Conceito?.Natureza == NaturezaConceito.Categorico ? ModoDeAvaliacaoDaEtapa.ConceitoCategorico
            : Conceito is not null || ComponeNota || NotaMinima.HasValue || citacao.CitaANota ? ModoDeAvaliacaoDaEtapa.NotaNumerica
            : ModoDeAvaliacaoDaEtapa.SemNotaNemConceito;
    }

    private static string RaizDoCampo(string? campo) => campo?.Split('[', '.')[0] ?? string.Empty;

    private static IEnumerable<FieldError> RecusasDaAvaliacao(IdentificacaoDaEtapa identificacao, AvaliacaoDeclaradaDaEtapa avaliacao) =>
    [
        .. ConferenciaDaDeclaracaoDaEtapa.Violacoes(identificacao, avaliacao.Declaracao, avaliacao.Itens),
        .. CoerenciaDoConceitoDaEtapa.ValidarDeclarado(
            avaliacao.Conceito, avaliacao.Declaracao, avaliacao.Itens.Count > 0, identificacao.TipoEtapa.NotaDeOrigemNoEnem),
    ];

    private static IEnumerable<FieldError> RecusasDasPublicacoes(
        IdentificacaoDaEtapa identificacao,
        JanelaDeTempo janela,
        PublicacoesDaEtapa publicacoes,
        IReadOnlySet<string> suspensas) =>
    [
        .. Recusa(ListaDosProdutos, AtoRepetidoNaListaInteira(publicacoes.Produtos)),
        .. Recusa("fim", ConferenciaDaDeclaracaoDaEtapa.JanelaInvertida(janela)),
        .. TiposDeDecisaoDeclarados.Validar(publicacoes.TiposDeDecisao, TiposDeDecisaoDeclarados.Campo),
        .. RecusaDosRecursos(identificacao, publicacoes, suspensas),
    ];

    // A âncora de um recurso num produto preliminar lê a lista dos produtos e a dos recursos inteiras.
    private static IEnumerable<FieldError> RecusaDosRecursos(
        IdentificacaoDaEtapa identificacao, PublicacoesDaEtapa publicacoes, IReadOnlySet<string> suspensas) =>
        suspensas.Contains(ListaDosProdutos) || suspensas.Contains(ListaDosRecursos)
            ? []
            : Recusa(
                ListaDosRecursos,
                JanelasRecursaisDaEtapa.Recusa(
                    publicacoes.Produtos, identificacao.TipoEtapa.NotaDeOrigemNoEnem, publicacoes.EmiteParecerIndividual, publicacoes.Recursos));

    private static IEnumerable<FieldError> Recusa(string campo, DomainError? erro) =>
        erro is null ? [] : [new FieldError(campo, erro)];

    // A declaração lida de um documento não tem posição de entrada: o ato repetido sai uma vez só, na lista dos produtos inteira.
    private static DomainError? AtoRepetidoNaListaInteira(IReadOnlyList<ProdutoDaEtapa> produtos) =>
        ConferenciaDaDeclaracaoDaEtapa.AtosRepetidos(produtos) is [{ } primeira, ..] ? primeira.Error : null;
}
