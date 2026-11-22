namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Portal.Infrastructure.Persistence;

/// <summary>
/// Regras de arquitetura da conversão dos enums de contrato (ADR-0176): em todo <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
/// da solução, toda propriedade escalar, de tipo próprio ou de tipo complexo, e todo elemento de coleção primitiva cujo tipo
/// é enum com código de contrato grava pelo conversor do código, e cada coleção primitiva de enum com código usa o conversor do elemento. A leitura é do modelo finalizado, o que o EF aplica, e não do texto da configuração. Como nenhum modelo da
/// solução guarda enum com código hoje, a mesma regra roda sobre modelos sintéticos, com e sem a convenção, para que a
/// conferência não aprove por conjunto vazio.
/// </summary>
public sealed class ConversaoDosEnumsDeContratoNoModeloTests
{
    private static readonly string[] PropriedadesComEnumDeContrato =
        ["Modalidade", "Alternativa", "Uso", "Formato", "Impressao", "Preferidas"];

    [Fact(DisplayName = "O percurso encontra propriedades no modelo do monólito")]
    public void PercursoEncontraPropriedadesNoModeloDoMonolito()
    {
        using MonolitoDbContext contexto = new MonolitoDbContextDesignTimeFactory().CreateDbContext([]);
        IModel monolito = contexto.GetService<IDesignTimeModel>().Model;

        PontosDoModelo.Em(monolito).Should().NotBeEmpty("um percurso vazio aprovaria a regra sem olhar o modelo");
    }

    [Fact(DisplayName = "Todo enum com código de contrato dos `DbContext` da solução grava pelo conversor do código")]
    public void TodoEnumComCodigoDosModelosDaSolucaoGravaPeloConversorDoCodigo()
    {
        using MonolitoDbContext monolito = new MonolitoDbContextDesignTimeFactory().CreateDbContext([]);
        using PortalDbContext portal = new PortalDbContextDesignTimeFactory().CreateDbContext([]);
        string[] violacoes =
        [
            .. PontosDoModelo.QueNaoGravamPeloCodigo(monolito.GetService<IDesignTimeModel>().Model).Select(ponto => $"MonolitoDbContext: {ponto.Descricao}"),
            .. PontosDoModelo.QueNaoGravamPeloCodigo(portal.GetService<IDesignTimeModel>().Model).Select(ponto => $"PortalDbContext: {ponto.Descricao}"),
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

}
