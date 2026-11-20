namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.ArchTests.TestSupport;

/// <summary>
/// Regras de arquitetura da conversão dos enums de contrato (ADR-0176): em todo <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
/// da solução, toda propriedade escalar, de tipo próprio ou de tipo complexo, e todo elemento de coleção primitiva cujo tipo
/// é enum com código de contrato grava pelo conversor do código, e nenhuma coleção de enum com código fica sem tipo de
/// elemento. A leitura é do modelo finalizado, o que o EF aplica, e não do texto da configuração. Como nenhum modelo da
/// solução guarda enum com código hoje, a mesma regra roda sobre modelos sintéticos, com e sem a convenção, para que a
/// conferência não aprove por conjunto vazio.
/// </summary>
public sealed class ConversaoDosEnumsDeContratoNoModeloTests
{
    private static readonly string[] PropriedadesComEnumDeContrato =
        ["Modalidade", "Alternativa", "Uso", "Formato", "Impressao", "Preferidas"];

    [Fact(DisplayName = "A descoberta encontra os `DbContext` do monólito e do portal")]
    public void DescobertaEncontraOsDbContexts() =>
        ModelosDaSolucao.Modelos().Select(modelo => modelo.Contexto).Should()
            .Contain("MonolitoDbContext").And.Contain("PortalDbContext");

    [Fact(DisplayName = "O percurso encontra propriedades no modelo do monólito")]
    public void PercursoEncontraPropriedadesNoModeloDoMonolito()
    {
        IModel monolito = ModelosDaSolucao.Modelos().Single(modelo => modelo.Contexto == "MonolitoDbContext").Modelo;

        PontosDoModelo.Em(monolito).Should().NotBeEmpty("um percurso vazio aprovaria a regra sem olhar o modelo");
    }

    [Fact(DisplayName = "Todo enum com código de contrato dos `DbContext` da solução grava pelo conversor do código")]
    public void TodoEnumComCodigoDosModelosDaSolucaoGravaPeloConversorDoCodigo()
    {
        string[] violacoes =
        [
            .. ModelosDaSolucao.Modelos().SelectMany(modelo =>
                PontosDoModelo.QueNaoGravamPeloCodigo(modelo.Modelo).Select(ponto => $"{modelo.Contexto}: {ponto.Descricao} é do enum '{ponto.EnumDeContrato?.Name}'")),
        ];

        violacoes.Should().BeEmpty(
            "o código de contrato é a única representação gravada do enum: sem o conversor do código o banco guardaria o número ou o nome. Violações:\n"
            + string.Join("\n", violacoes));
    }

    [Fact(DisplayName = "O percurso encontra o enum com código em escalar, opcional, tipo próprio, tipo complexo aninhado e coleção primitiva")]
    public void PercursoEncontraOEnumComCodigoEmTodosOsLugaresDoModeloSintetico()
    {
        IReadOnlyList<PontoDoModelo> pontos = PontosDoModelo.Em(ModeloSinteticoComEnumsDeContrato.ModeloComAConvencao());

        IEnumerable<PontoDoModelo> comEnumDeContrato = pontos.Where(ponto => ponto.GuardaEnumDeContrato);

        comEnumDeContrato.Should().HaveCount(6);
        comEnumDeContrato.Select(ponto => ponto.Propriedade).Should().BeEquivalentTo(PropriedadesComEnumDeContrato);
    }

    [Fact(DisplayName = "Com a convenção aplicada, todos os pontos de enum com código do modelo sintético usam o conversor do código")]
    public void ModeloSinteticoComAConvencao_NaoTemViolacao()
    {
        IModel modelo = ModeloSinteticoComEnumsDeContrato.ModeloComAConvencao();

        PontosDoModelo.QueNaoGravamPeloCodigo(modelo).Should().BeEmpty();
        PontosDoModelo.Em(modelo).Where(ponto => ponto.GuardaEnumDeContrato)
            .Should().OnlyContain(ponto => ponto.UsaOConversorDoCodigo);
    }

    [Fact(DisplayName = "Sem a convenção, a regra acusa todos os pontos de enum com código do modelo sintético")]
    public void ModeloSinteticoSemAConvencao_AcusaTodosOsPontos()
    {
        IReadOnlyList<PontoDoModelo> violacoes = PontosDoModelo.QueNaoGravamPeloCodigo(ModeloSinteticoComEnumsDeContrato.ModeloSemAConvencao());

        violacoes.Should().HaveCount(6);
        violacoes.Select(ponto => ponto.Propriedade).Should().BeEquivalentTo(PropriedadesComEnumDeContrato);
    }

    [Fact(DisplayName = "A coleção de enum com código com o conversor na coleção inteira, sem tipo de elemento, é acusada")]
    public void ColecaoComConversorNaColecaoInteira_EAcusada()
    {
        IReadOnlyList<PontoDoModelo> violacoes = PontosDoModelo.QueNaoGravamPeloCodigo(ModeloSinteticoComColecaoInteiraDeEnumDeContrato.Montar());

        violacoes.Should().HaveCount(2);
        violacoes.Select(ponto => ponto.Propriedade).Should().BeEquivalentTo(["Preferidas", "Eventuais"]);
    }
}
