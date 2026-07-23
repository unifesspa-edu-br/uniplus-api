namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Story #928, §6 — o grafo de dependência conjunto: campo e fato como nós distintos, as quatro
/// classes de aresta (produção, pré-condição, derivação, gatilho), a aciclicidade sobre as quatro
/// juntas e a ordenação topológica determinística. A validação estrutural das componentes (forma
/// das condições, unicidade) é das factories de cada entidade; aqui prova-se o grafo montado.
/// </summary>
public sealed class GrafoDependenciaConjuntaTests
{
    private static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;

    private static CondicaoPrecondicaoFato Precond(string fato) =>
        CondicaoPrecondicaoFato.Criar(1, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;

    private static FatoColetado Declarado(string codigo, int ordem, params string[] citados) =>
        FatoColetado.Criar(codigo, ordem, [.. citados.Select(Precond)]).Value!;

    private static ConfiguracaoDerivacaoFato Derivado(string codigo, params string[] citados) =>
        ConfiguracaoDerivacaoFato.Criar(codigo,
        [
            RegraDerivacaoConfigurada.Criar(0, "ALGUM_CODIGO",
                [.. citados.Select(static (c, i) => CondicaoRegraDerivacao.Criar(
                    1, c, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
        ]).Value!;

    private static DocumentoExigido ExigenciaGatilhadaPor(string fato) =>
        DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "DOC", "Documento", "CAT",
            Aplicabilidade.Condicional, obrigatorio: false, consequenciaIndeferimento: null,
            [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            [], null, Qualquer, null).Value!;

    private static Result<GrafoDependenciaConjunta> Construir(
        IReadOnlyCollection<FatoColetado>? fatos = null,
        IReadOnlyCollection<ConfiguracaoDerivacaoFato>? derivacoes = null,
        IReadOnlyCollection<DocumentoExigido>? exigencias = null) =>
        GrafoDependenciaConjunta.Construir(fatos ?? [], derivacoes ?? [], exigencias ?? []);

    private static int Posicao(GrafoDependenciaConjunta grafo, ClasseNoGrafo classe, string codigo) =>
        grafo.OrdemTopologica.ToList().FindIndex(n => n.Classe == classe && n.Codigo == codigo);

    [Fact(DisplayName = "Aresta de produção existe e põe o campo antes do fato na ordem topológica")]
    public void Producao_CampoAntesDoFato()
    {
        Result<GrafoDependenciaConjunta> resultado = Construir(fatos: [Declarado("PCD", 0)]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        GrafoDependenciaConjunta grafo = resultado.Value!;

        grafo.Arestas.Should().ContainSingle(a =>
            a.Tipo == TipoArestaGrafo.Producao
            && a.Origem.Classe == ClasseNoGrafo.Campo && a.Origem.Codigo == "PCD"
            && a.Destino.Classe == ClasseNoGrafo.Fato && a.Destino.Codigo == "PCD");
        Posicao(grafo, ClasseNoGrafo.Campo, "PCD").Should().BeLessThan(Posicao(grafo, ClasseNoGrafo.Fato, "PCD"));
    }

    [Fact(DisplayName = "As quatro classes de aresta são montadas das componentes da configuração")]
    public void QuatroClassesDeAresta_Montadas()
    {
        // PCD declarado (produção); CONCORRER_PCD declarado gatado por PCD (pré-condição);
        // MODALIDADE derivado de CONCORRER_PCD (derivação); exigência gatilhada por MODALIDADE (gatilho).
        FatoColetado pcd = Declarado("PCD", 0);
        FatoColetado concorrer = Declarado("CONCORRER_PCD", 1, "PCD");
        ConfiguracaoDerivacaoFato modalidade = Derivado("MODALIDADE", "CONCORRER_PCD");
        DocumentoExigido laudo = ExigenciaGatilhadaPor("MODALIDADE");

        Result<GrafoDependenciaConjunta> resultado = Construir([pcd, concorrer], [modalidade], [laudo]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        IReadOnlyList<ArestaGrafoDependencia> arestas = resultado.Value!.Arestas;
        arestas.Should().Contain(a => a.Tipo == TipoArestaGrafo.Precondicao
            && a.Origem.Codigo == "PCD" && a.Destino.Classe == ClasseNoGrafo.Campo && a.Destino.Codigo == "CONCORRER_PCD");
        arestas.Should().Contain(a => a.Tipo == TipoArestaGrafo.Derivacao
            && a.Origem.Codigo == "CONCORRER_PCD" && a.Destino.Classe == ClasseNoGrafo.Fato && a.Destino.Codigo == "MODALIDADE");
        arestas.Should().Contain(a => a.Tipo == TipoArestaGrafo.Gatilho
            && a.Origem.Codigo == "MODALIDADE" && a.Destino.Classe == ClasseNoGrafo.Exigencia);
        arestas.Should().Contain(a => a.Tipo == TipoArestaGrafo.Producao && a.Origem.Codigo == "PCD");
    }

    [Fact(DisplayName = "Ordem topológica respeita todas as arestas: campo→fato→campo dependente→derivado→exigência")]
    public void OrdemTopologica_RespeitaTodasAsArestas()
    {
        FatoColetado pcd = Declarado("PCD", 0);
        FatoColetado concorrer = Declarado("CONCORRER_PCD", 1, "PCD");
        ConfiguracaoDerivacaoFato modalidade = Derivado("MODALIDADE", "CONCORRER_PCD");
        DocumentoExigido laudo = ExigenciaGatilhadaPor("MODALIDADE");

        GrafoDependenciaConjunta grafo = Construir([pcd, concorrer], [modalidade], [laudo]).Value!;

        Posicao(grafo, ClasseNoGrafo.Fato, "PCD")
            .Should().BeLessThan(Posicao(grafo, ClasseNoGrafo.Campo, "CONCORRER_PCD"), "a pré-condição precede o campo gatado");
        Posicao(grafo, ClasseNoGrafo.Fato, "CONCORRER_PCD")
            .Should().BeLessThan(Posicao(grafo, ClasseNoGrafo.Fato, "MODALIDADE"), "a derivação precede o derivado");
        int exigencia = grafo.OrdemTopologica.ToList().FindIndex(n => n.Classe == ClasseNoGrafo.Exigencia);
        Posicao(grafo, ClasseNoGrafo.Fato, "MODALIDADE")
            .Should().BeLessThan(exigencia, "o gatilho precede a exigência");
    }

    [Fact(DisplayName = "A ordem topológica é determinística: mesma configuração, mesma ordem")]
    public void OrdemTopologica_Deterministica()
    {
        FatoColetado a = Declarado("A", 0);
        FatoColetado b = Declarado("B", 1);
        FatoColetado c = Declarado("C", 2);

        List<string> ordem1 = [.. Construir([a, b, c]).Value!.OrdemTopologica.Select(n => n.Rotulo)];
        List<string> ordem2 = [.. Construir([c, b, a]).Value!.OrdemTopologica.Select(n => n.Rotulo)];

        ordem2.Should().Equal(ordem1, "a ordem não depende da ordem de entrada das componentes");
    }

    [Fact(DisplayName = "A ordem topológica respeita a posição de coleta configurada, não a ordem dos códigos")]
    public void OrdemTopologica_RespeitaOrdemConfigurada_NaoOsCodigos()
    {
        // Dois fatos independentes cujos códigos ordenam ao contrário da configuração: Z na ordem 0,
        // A na ordem 1. A ordem de coleta é a configurada (Z antes de A), não a alfabética.
        FatoColetado z = Declarado("Z", 0);
        FatoColetado a = Declarado("A", 1);

        GrafoDependenciaConjunta grafo = Construir([z, a]).Value!;

        Posicao(grafo, ClasseNoGrafo.Fato, "Z")
            .Should().BeLessThan(Posicao(grafo, ClasseNoGrafo.Fato, "A"), "Z é coletado antes (ordem 0), apesar de 'A' < 'Z'");
        Posicao(grafo, ClasseNoGrafo.Campo, "Z")
            .Should().BeLessThan(Posicao(grafo, ClasseNoGrafo.Campo, "A"));
    }

    [Fact(DisplayName = "Fato derivado que gata um campo intermediário é coletado antes de um campo posterior independente")]
    public void Derivado_HerdaPosicaoDoCampoQueGata()
    {
        // A@0 → deriva D → D gata Campo(B@1); C@2 independente. Sem herdar a posição, D (sentinela)
        // deixaria C@2 furar a fila de B@1. Com a ordem efetiva, D herda a posição de B (1) e B sai
        // antes de C.
        FatoColetado a = Declarado("A", 0);
        ConfiguracaoDerivacaoFato d = Derivado("D", "A");
        FatoColetado b = Declarado("B", 1, "D");
        FatoColetado c = Declarado("C", 2);

        GrafoDependenciaConjunta grafo = Construir([a, b, c], [d]).Value!;

        Posicao(grafo, ClasseNoGrafo.Fato, "D")
            .Should().BeLessThan(Posicao(grafo, ClasseNoGrafo.Campo, "C"), "o derivado que gata B@1 é coletado antes de C@2");
        Posicao(grafo, ClasseNoGrafo.Campo, "B")
            .Should().BeLessThan(Posicao(grafo, ClasseNoGrafo.Campo, "C"), "B@1 não é furado por C@2 apesar de esperar a derivação");
    }

    [Fact(DisplayName = "Nós e arestas expostos são determinísticos — não dependem da ordem de entrada")]
    public void NosEArestas_Deterministicos()
    {
        FatoColetado pcd = Declarado("PCD", 0);
        FatoColetado concorrer = Declarado("CONCORRER_PCD", 1, "PCD");
        ConfiguracaoDerivacaoFato modalidade = Derivado("MODALIDADE", "CONCORRER_PCD");

        GrafoDependenciaConjunta g1 = Construir([pcd, concorrer], [modalidade]).Value!;
        GrafoDependenciaConjunta g2 = Construir([concorrer, pcd], [modalidade]).Value!;

        g2.Nos.Select(n => n.Rotulo).Should().Equal(g1.Nos.Select(n => n.Rotulo));
        g2.Arestas.Select(a => $"{a.Tipo}:{a.Origem.Rotulo}->{a.Destino.Rotulo}")
            .Should().Equal(g1.Arestas.Select(a => $"{a.Tipo}:{a.Origem.Rotulo}->{a.Destino.Rotulo}"));
    }

    [Fact(DisplayName = "Ciclo pelas quatro classes juntas é recusado com erro nomeado")]
    public void CicloConjunto_Recusado()
    {
        // Cross-class: A declarado com pré-condição citando MODALIDADE (Fato(MODALIDADE)→Campo(A));
        // MODALIDADE derivado de A (Fato(A)→Fato(MODALIDADE)); produção Campo(A)→Fato(A).
        // Ciclo: Campo(A) → Fato(A) → Fato(MODALIDADE) → Campo(A). Nenhuma factory de componente o
        // pega (a de FatoColetado só barra auto-referência); o grafo conjunto sim.
        FatoColetado a = Declarado("A", 0, "MODALIDADE");
        ConfiguracaoDerivacaoFato modalidade = Derivado("MODALIDADE", "A");

        Result<GrafoDependenciaConjunta> resultado = Construir([a], [modalidade]);

        resultado.IsFailure.Should().BeTrue("as quatro classes juntas formam um ciclo");
        resultado.Error!.Code.Should().Be(GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo);
    }

    [Fact(DisplayName = "Ciclo só de derivação (D1↔D2) é recusado — o validador conjunto pega além do §2")]
    public void CicloDeDerivacao_Recusado()
    {
        ConfiguracaoDerivacaoFato d1 = Derivado("D1", "D2");
        ConfiguracaoDerivacaoFato d2 = Derivado("D2", "D1");

        Result<GrafoDependenciaConjunta> resultado = Construir(derivacoes: [d1, d2]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo);
    }

    [Fact(DisplayName = "Citação a fato inexistente não vira aresta pendurada — não quebra a construção")]
    public void CitacaoAFatoInexistente_NaoViraAresta()
    {
        // Exigência gatilhada por um fato que o processo não coleta nem deriva: sem nó de fato, sem
        // aresta de gatilho. A recusa por dependência não declarada é do congelamento (§7).
        DocumentoExigido exigencia = ExigenciaGatilhadaPor("FATO_FANTASMA");

        Result<GrafoDependenciaConjunta> resultado = Construir(exigencias: [exigencia]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Arestas.Should().NotContain(a => a.Tipo == TipoArestaGrafo.Gatilho);
    }
}
