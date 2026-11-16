namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

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
    private FaseDeclarada(
        Guid id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        AtosDaFase atos,
        BancasDaFase bancas,
        IReadOnlyList<FieldError> recusas)
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

    public static FaseDeclarada Declarar(
        Guid id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancas)
    {
        ConferenciaDaDeclaracaoDaFase.ExigirDeclaracaoPresente(origem, decisoes, publicacoes, bancas);
        ConferenciaDaDeclaracaoDaFase.ExigirIdCongelado(id);

        return new FaseDeclarada(
            id,
            origem,
            decisoes,
            new AtosDaFase(publicacoes),
            new BancasDaFase(bancas),
            ConferenciaDaDeclaracaoDaFase.Violacoes(origem, decisoes, publicacoes, bancas));
    }
}
