namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma fase como foi declarada: os dados normalizados e as recusas da própria declaração, pelos mesmos
/// predicados que a criação da fase aplica. A declaração com recusa continua existindo, para o
/// diagnóstico do cronograma poder ler o que foi declarado.
/// </summary>
public sealed record FaseDeclarada
{
    /// <summary>A lista dos produtos da fase, pelo nome que <see cref="ParteRecusadaEm"/> reconhece.</summary>
    public const string ListaDosProdutos = "produtos";

    /// <summary>A lista das bancas que a fase requer, pelo nome que <see cref="ParteRecusadaEm"/> reconhece.</summary>
    public const string ListaDasBancasRequeridas = "bancasRequeridas";

    private readonly IReadOnlySet<string> _colecoesComParteRecusada;

    private FaseDeclarada(
        Guid id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        AtosDaFase atos,
        BancasDaFase bancas,
        IReadOnlyList<FieldError> recusas,
        IReadOnlySet<string> colecoesComParteRecusada)
    {
        Id = id;
        OrigemId = origem.OrigemId;
        Codigo = origem.Codigo.Trim();
        Ordem = decisoes.Ordem;
        Janela = new JanelaDeTempo(decisoes.Inicio, decisoes.Fim);
        FaseConcluinteCodigo = CodigoDeFase.Normalizado(decisoes.FaseConcluinteCodigo);
        TemUnidadeResponsavel = decisoes.UnidadeResponsavel is not null;
        AgrupaEtapas = origem.AgrupaEtapas;
        ColetaInscricao = origem.ColetaInscricao;
        ColetaSolicitacaoIsencao = origem.ColetaSolicitacaoIsencao;
        Atos = atos;
        Bancas = bancas;
        Recusas = recusas;
        _colecoesComParteRecusada = colecoesComParteRecusada;
    }

    public Guid Id { get; }

    public Guid OrigemId { get; }

    public string Codigo { get; }

    public int Ordem { get; }

    public JanelaDeTempo Janela { get; }

    public string? FaseConcluinteCodigo { get; }

    public bool TemUnidadeResponsavel { get; }

    public bool AgrupaEtapas { get; }

    public bool ColetaInscricao { get; }

    public bool ColetaSolicitacaoIsencao { get; }

    public AtosDaFase Atos { get; }

    public BancasDaFase Bancas { get; }

    public IReadOnlyList<FieldError> Recusas { get; }

    /// <summary>
    /// Se a leitura do documento recusou alguma parte da lista da fase (<see cref="ListaDosProdutos"/>, <see cref="ListaDasBancasRequeridas"/>):
    /// a lista declarada não é a que o autor escreveu, e a regra que a quantifica não a julga.
    /// </summary>
    public bool ParteRecusadaEm(string colecao) => _colecoesComParteRecusada.Contains(colecao);

    public static FaseDeclarada Declarar(
        Guid id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancas,
        IReadOnlySet<string>? colecoesComParteRecusada = null)
    {
        ConferenciaDaDeclaracaoDaFase.ExigirDeclaracaoPresente(origem, decisoes, publicacoes, bancas);
        ConferenciaDaDeclaracaoDaFase.ExigirIdCongelado(id);
        IReadOnlySet<string> suspensas = colecoesComParteRecusada ?? FrozenSet<string>.Empty;

        return new FaseDeclarada(
            id,
            origem,
            decisoes,
            new AtosDaFase(publicacoes),
            new BancasDaFase(bancas),
            ConferenciaDaDeclaracaoDaFase.Violacoes(origem, decisoes, publicacoes, bancas, suspensas),
            suspensas);
    }
}
