namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
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

    /// <summary>O campo da unidade responsável da fase, pelo nome que <see cref="ParteRecusadaEm"/> reconhece.</summary>
    public const string CampoDaUnidadeResponsavel = "unidadeResponsavel";

    /// <summary>O campo da regra de recurso da fase, pelo nome que <see cref="ParteRecusadaEm"/> reconhece.</summary>
    public const string CampoDaRegraDeRecurso = "regraRecurso";

    /// <summary>A lista dos procedimentos da regra de recurso da fase, pelo caminho que <see cref="ParteRecusadaEm"/> reconhece.</summary>
    public const string ListaDosProcedimentosDeRecurso = "regraRecurso.procedimentos";

    /// <summary>O campo da regra de homologação das inscrições da fase, pelo nome que <see cref="ParteRecusadaEm"/> reconhece.</summary>
    public const string CampoDaRegraDeHomologacao = "regraDeHomologacao";

    private readonly IReadOnlySet<string> _caminhosComParteRecusada;
    private readonly DecisoesDeCronogramaDaFase _decisoes;

    private FaseDeclarada(
        Guid id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        AtosDaFase atos,
        BancasDaFase bancas,
        IReadOnlyList<FieldError> recusas,
        IReadOnlySet<string> caminhosComParteRecusada)
    {
        Id = id;
        OrigemId = origem.OrigemId;
        Codigo = origem.Codigo.Trim();
        _decisoes = decisoes;
        Janela = new JanelaDeTempo(decisoes.Inicio, decisoes.Fim);
        DataFixadaNoEdital = origem.OrigemData == OrigemDataFase.NoEdital;
        AgrupaEtapas = origem.AgrupaEtapas;
        ColetaInscricao = origem.ColetaInscricao;
        ColetaSolicitacaoIsencao = origem.ColetaSolicitacaoIsencao;
        Atos = atos;
        Bancas = bancas;
        Recusas = recusas;
        _caminhosComParteRecusada = caminhosComParteRecusada;
    }

    public Guid Id { get; }

    public Guid OrigemId { get; }

    public string Codigo { get; }

    public int Ordem => _decisoes.Ordem;

    /// <summary>Se a fase acontece no mesmo momento da fase que a antecede na lista.</summary>
    public bool NoMesmoMomentoDaAnterior => _decisoes.NoMesmoMomentoDaAnterior;

    public JanelaDeTempo Janela { get; }

    /// <summary>Se o período da fase é fixado no edital, e por isso o período inteiro é obrigatório.</summary>
    public bool DataFixadaNoEdital { get; }

    public bool TemUnidadeResponsavel => _decisoes.UnidadeResponsavel is not null;

    public bool AgrupaEtapas { get; }

    public bool ColetaInscricao { get; }

    public bool ColetaSolicitacaoIsencao { get; }

    public AtosDaFase Atos { get; }

    public BancasDaFase Bancas { get; }

    public IReadOnlyList<FieldError> Recusas { get; }

    /// <summary>
    /// Se a leitura do documento recusou alguma parte da fase, pelo caminho dela relativo à fase: a lista (<see cref="ListaDosProdutos"/>,
    /// <see cref="ListaDasBancasRequeridas"/>) ou o campo (<see cref="CampoDaUnidadeResponsavel"/>, <see cref="CampoDaRegraDeRecurso"/>, <see cref="ListaDosProcedimentosDeRecurso"/>, <see cref="CampoDaRegraDeHomologacao"/>). O que a declaração tem desse caminho
    /// não é o que o autor escreveu, e a regra que o lê não o julga.
    /// </summary>
    public bool ParteRecusadaEm(string caminho) => _caminhosComParteRecusada.Contains(caminho);

    public static FaseDeclarada Declarar(
        Guid id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase decisoes,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancas,
        IReadOnlySet<string>? caminhosComParteRecusada = null)
    {
        ConferenciaDaDeclaracaoDaFase.ExigirDeclaracaoPresente(origem, decisoes, publicacoes, bancas);
        ConferenciaDaDeclaracaoDaFase.ExigirIdCongelado(id);
        IReadOnlySet<string> suspensas = caminhosComParteRecusada ?? FrozenSet<string>.Empty;

        return new FaseDeclarada(
            id,
            origem,
            decisoes,
            new AtosDaFase(publicacoes),
            new BancasDaFase(bancas),
            AtoRepetidoNaListaInteira(ConferenciaDaDeclaracaoDaFase.Violacoes(origem, decisoes, publicacoes, bancas, suspensas)),
            suspensas);
    }

    // A declaração lida de um documento não tem posição de entrada: o ato repetido sai uma vez só, na lista dos produtos inteira.
    private static List<FieldError> AtoRepetidoNaListaInteira(List<FieldError> violacoes)
    {
        FieldError? primeira = violacoes.Find(ConferenciaDaDeclaracaoDaFase.EhAtoDuplicado);

        return primeira is null
            ? violacoes
            : [.. violacoes
                .Where(violacao => !ConferenciaDaDeclaracaoDaFase.EhAtoDuplicado(violacao) || violacao == primeira)
                .Select(violacao => violacao == primeira ? violacao with { Field = ListaDosProdutos } : violacao)];
    }
}
