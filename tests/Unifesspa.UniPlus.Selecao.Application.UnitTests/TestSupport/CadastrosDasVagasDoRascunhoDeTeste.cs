namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using NSubstitute;
using NSubstitute.ClearExtensions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os cadastros vivos que as vagas do rascunho citam, nos leitores do cenário: duas ofertas de curso, a ampla concorrência com a
/// modalidade de ação afirmativa que sai dela e a regra institucional de distribuição. O código que o leitor não conhece não volta.
/// </summary>
internal static class CadastrosDasVagasDoRascunhoDeTeste
{
    public const string OfertaDePedagogia = "PEDAGOGIA_MARABA";
    public const string OfertaDeMedicina = "MEDICINA_MARABA";
    public const string Ampla = "AC";
    public const string AmplaComDeficiencia = "AC_PCD";

    public static string RegraInstitucional { get; } = RegraDeDistribuicaoDeVagas.Institucional.Codigo();

    public static void Cadastrar(CenarioDoRascunho cenario)
    {
        ArgumentNullException.ThrowIfNull(cenario);

        cenario.Ofertas.ListarVivasPorCodigosAsync(default!, default).ReturnsForAnyArgs(chamada =>
            Task.FromResult<IReadOnlyList<OfertaCursoView>>([.. Ofertas().Where(oferta => chamada.Arg<IReadOnlyCollection<string>>().Contains(oferta.Codigo))]));
        CadastrarModalidades(cenario, Modalidades());
        cenario.Regras.ListarPorReferenciasAsync(default!, default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<RegraCatalogo>>([Regra()]));
    }

    /// <summary>Troca o que o leitor de modalidades devolve, como se o cadastro tivesse mudado depois da leitura anterior.</summary>
    public static void CadastrarModalidades(CenarioDoRascunho cenario, IReadOnlyList<ModalidadeView> modalidades)
    {
        ArgumentNullException.ThrowIfNull(cenario);

        cenario.Modalidades.ClearSubstitute(ClearOptions.ReturnValues);
        cenario.Modalidades.ListarVivosPorCodigosAsync(default!, default).ReturnsForAnyArgs(chamada =>
            Task.FromResult<IReadOnlyList<ModalidadeView>>([.. modalidades.Where(modalidade => chamada.Arg<IReadOnlyCollection<string>>().Contains(modalidade.Codigo))]));
    }

    /// <summary>As modalidades do cadastro, a que sai da ampla concorrência com a composição trocada para vagas somadas ao total.</summary>
    public static ModalidadeView[] ModalidadesComAcaoAfirmativaSuplementar() =>
    [
        Modalidades()[0],
        Modalidades()[1] with { ComposicaoVagas = "SUPLEMENTAR_AO_TOTAL", ComposicaoOrigem = null },
    ];

    /// <summary>A distribuição da oferta com a ampla concorrência e a modalidade que sai dela, declaradas fora da ordem do código.</summary>
    public static string Distribuicao(string ofertaCodigo, int vagasOfertadas = 10) =>
        $$"""
        { "ofertaCursoCodigo": "{{ofertaCodigo}}", "vagasOfertadasBase": {{vagasOfertadas}},
          "regraDistribuicao": { "codigo": "{{RegraInstitucional}}", "versao": "v1" },
          "modalidades": [ { "codigo": "{{AmplaComDeficiencia}}", "quantidadeDeclarada": 4 }, { "codigo": "{{Ampla}}", "quantidadeDeclarada": 6 } ] }
        """;

    public static string Documento(params string[] distribuicoes) =>
        $$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "distribuicaoVagas": [ {{string.Join(", ", distribuicoes)}} ] }""";

    private static OfertaCursoView[] Ofertas() => [Oferta(OfertaDePedagogia, "Pedagogia"), Oferta(OfertaDeMedicina, "Medicina")];

    private static OfertaCursoView Oferta(string codigo, string curso) => new(
        Guid.CreateVersion7(), codigo, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "CAMAR", "Campus Universitário de Marabá", "CAMPUS",
        "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["NOTURNO"], null, null, null, null, null, null, curso, "Bacharelado");

    private static ModalidadeView[] Modalidades() =>
    [
        Modalidade(Ampla, "Ampla concorrência", "AMPLA", "RESTANTE_DAS_VAGAS_OFERTADAS", null),
        Modalidade(AmplaComDeficiencia, "Ampla concorrência para pessoas com deficiência", "ACAO_AFIRMATIVA", "RETIRADA_DE_OUTRA_MODALIDADE", Ampla),
    ];

    private static ModalidadeView Modalidade(string codigo, string descricao, string natureza, string composicao, string? origem) => new(
        Guid.CreateVersion7(), codigo, descricao, natureza, composicao, origem, null, null, null, null,
        RegrasDeAplicabilidadeDeTeste.DaNatureza(CatalogoDe<NaturezaLegalModalidade>.Obter(natureza)), null, "Lei de teste");

    private static RegraCatalogo Regra()
    {
        using JsonDocument vazio = JsonDocument.Parse("{}");
        using JsonDocument invariantes = JsonDocument.Parse("[]");
        return RegraCatalogo.Criar(
            RegraInstitucional, "v1", TipoRegra.RegraDistribuicaoVagas, vazio.RootElement.Clone(), invariantes.RootElement.Clone(), "Regra de teste");
    }
}
