namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A fase que os testes da atualização e da criação declaram, com o valor neutro de uma fase conforme para o que não declaram.</summary>
internal static class FaseDeTesteDaAtualizacao
{
    internal const string Resultado = "RESULTADO_CLASSIFICACAO";
    internal static readonly DateTimeOffset InicioDaJanela = new(2027, 1, 25, 11, 0, 0, TimeSpan.Zero);

    internal sealed record DadosDaFase
    {
        public int Ordem { get; init; } = 1;
        public string Codigo { get; init; } = "RESULTADO_PRELIMINAR";
        public OrigemDataFase OrigemData { get; init; } = OrigemDataFase.NaExecucao;
        public bool ColetaInscricao { get; init; }
        public bool ColetaSolicitacaoIsencao { get; init; }
        public DateTimeOffset? Inicio { get; init; }
        public DateTimeOffset? Fim { get; init; }
        public IReadOnlyList<ProdutoDaFase> Produtos { get; init; } = [];
        public bool EmiteParecerIndividual { get; init; }
        public IReadOnlyList<BancaRequerida> Bancas { get; init; } = [];
        public RegraRecursoFase? RegraRecurso { get; init; }
        public UnidadeResponsavelSnapshot? Unidade { get; init; } = UnidadesDeTeste.Ceps;
    }

    internal static Result<FaseCronograma> Criar(DadosDaFase dados) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(Guid.CreateVersion7(), dados.Codigo, dados.OrigemData, AgrupaEtapas: false, PermiteComplementacao: false, dados.ColetaInscricao, dados.ColetaSolicitacaoIsencao),
        new DecisoesDeCronogramaDaFase(dados.Ordem, dados.Inicio, dados.Fim, dados.Unidade),
        new PublicacoesDaFase(dados.Produtos, dados.EmiteParecerIndividual, dados.RegraRecurso),
        dados.Bancas);

    internal static FaseCronograma Fase(DadosDaFase dados) => Criar(dados).Value!;

    internal static FaseCronograma Reidratar(Guid id, DadosDaFase dados) => FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        id,
        new OrigemCanonicaDaFase(Guid.CreateVersion7(), dados.Codigo, dados.OrigemData, AgrupaEtapas: false, PermiteComplementacao: false, dados.ColetaInscricao, dados.ColetaSolicitacaoIsencao),
        new DecisoesDeCronogramaDaFase(dados.Ordem, dados.Inicio, dados.Fim, dados.Unidade),
        new PublicacoesDaFase(dados.Produtos, dados.EmiteParecerIndividual, dados.RegraRecurso),
        dados.Bancas);

    internal static void Atualizar(FaseCronograma viva, FaseCronograma nova) =>
        viva.AtualizarDe(nova, [.. nova.Produtos]);

    internal static RegraRecursoFase Recurso(Guid produtoAncoraId) => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)),
        ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
        produtoAncoraId).Value!;

    internal static BancaRequerida Banca(TipoDeBanca tipo, params string[] categorias) =>
        BancaRequerida.Criar(
            TiposDeBancaCopiadosDeTeste.De(tipo),
            [.. categorias.Select(static c => CategoriaJulgada.Criar(Guid.CreateVersion7(), c))]);

    internal static BancaRequerida NoLugarDe(BancaRequerida viva) =>
        BancaQueSubstitui(viva.Id, Guid.CreateVersion7(), viva.Tipo.Codigo, [.. viva.CandidatosAtendidos.Select(static c => c.Codigo)]);

    internal static BancaRequerida BancaQueSubstitui(Guid idDaViva, Guid origemDoTipo, TipoDeBanca tipo, params string[] categorias) =>
        BancaRequerida.Criar(
            TiposDeBancaCopiadosDeTeste.De(tipo, origemDoTipo),
            [.. categorias.Select(static c => CategoriaJulgada.Criar(Guid.CreateVersion7(), c))],
            idDaBancaQueSubstitui: idDaViva);

    internal static ProdutoDaFase Preliminar(string ato) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Preliminar);

    internal static ProdutoDaFase Definitivo(string ato) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Definitivo);

    internal static (string? Campo, string Codigo)[] Recusas(Result<FaseCronograma> resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];
}
