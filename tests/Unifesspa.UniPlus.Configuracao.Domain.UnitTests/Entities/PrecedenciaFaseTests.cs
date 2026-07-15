namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As três guardas do grafo (self-loop, aresta duplicada, ciclo) dependem do
/// conjunto de arestas vivas no momento da escrita — por isso são exercitadas
/// contra listas de <see cref="PrecedenciaFase"/> construídas via <see cref="Criar"/>,
/// nunca por navegação/consulta do domínio (ADR-0042).
/// </summary>
public sealed class PrecedenciaFaseTests
{
    private static Result<PrecedenciaFase> Criar(
        string antecessora = "INSCRICAO",
        string sucessora = "HOMOLOGACAO",
        bool permiteSobreposicao = false,
        IReadOnlyList<PrecedenciaFase>? arestasVivas = null) =>
        PrecedenciaFase.Criar(antecessora, sucessora, permiteSobreposicao, arestasVivas ?? []);

    private static PrecedenciaFase Aresta(string antecessora, string sucessora) =>
        Criar(antecessora, sucessora).Value!;

    // ── Factory válida ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Aresta válida preenche os campos e fica ativa com Guid v7")]
    public void Criar_Valida_Aceita()
    {
        PrecedenciaFase aresta = Criar("INSCRICAO", "HOMOLOGACAO", permiteSobreposicao: true).Value!;

        aresta.Id.Should().NotBe(Guid.Empty);
        aresta.AntecessoraCodigo.Should().Be("INSCRICAO");
        aresta.SucessoraCodigo.Should().Be("HOMOLOGACAO");
        aresta.PermiteSobreposicao.Should().BeTrue();
        aresta.IsDeleted.Should().BeFalse();
    }

    // ── Formato e domínio canônico dos códigos ─────────────────────────────────

    [Theory(DisplayName = "Código de antecessora ausente ou fora do formato é rejeitado")]
    [InlineData("")]
    [InlineData("inscricao")]
    public void Criar_AntecessoraInvalida_Falha(string antecessora)
    {
        Result<PrecedenciaFase> r = Criar(antecessora: antecessora);

        r.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Código de antecessora fora do conjunto canônico é rejeitado")]
    public void Criar_AntecessoraForaDoCanonico_Falha()
    {
        Result<PrecedenciaFase> r = Criar(antecessora: "ENTREVISTA_FINAL");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.AntecessoraForaDoConjuntoCanonico);
    }

    [Fact(DisplayName = "Código de sucessora fora do conjunto canônico é rejeitado")]
    public void Criar_SucessoraForaDoCanonico_Falha()
    {
        Result<PrecedenciaFase> r = Criar(sucessora: "ENTREVISTA_FINAL");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.SucessoraForaDoConjuntoCanonico);
    }

    // ── Self-loop ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Self-loop (antecessora igual à sucessora) é rejeitado")]
    public void Criar_SelfLoop_Falha()
    {
        Result<PrecedenciaFase> r = Criar(antecessora: "INSCRICAO", sucessora: "INSCRICAO");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.SelfLoop);
    }

    // ── Aresta duplicada ───────────────────────────────────────────────────────

    [Fact(DisplayName = "Aresta duplicada (mesmo par já vivo) é rejeitada")]
    public void Criar_ArestaDuplicada_Falha()
    {
        PrecedenciaFase existente = Aresta("INSCRICAO", "HOMOLOGACAO");

        Result<PrecedenciaFase> r = Criar(
            "INSCRICAO", "HOMOLOGACAO", arestasVivas: [existente]);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.ArestaDuplicada);
    }

    [Fact(DisplayName = "Mesma antecessora com sucessora diferente não é duplicata")]
    public void Criar_MesmaAntecessoraSucessoraDiferente_Aceita()
    {
        PrecedenciaFase existente = Aresta("INSCRICAO", "HOMOLOGACAO");

        Result<PrecedenciaFase> r = Criar(
            "INSCRICAO", "ENSALAMENTO", arestasVivas: [existente]);

        r.IsSuccess.Should().BeTrue();
    }

    // ── Ciclo ──────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Aresta que fecha ciclo direto (A→B, B→A) é rejeitada")]
    public void Criar_CicloDireto_Falha()
    {
        PrecedenciaFase existente = Aresta("INSCRICAO", "HOMOLOGACAO");

        Result<PrecedenciaFase> r = Criar(
            "HOMOLOGACAO", "INSCRICAO", arestasVivas: [existente]);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.CicloDetectado);
    }

    [Fact(DisplayName = "Aresta que fecha ciclo transitivo (A→B→C, C→A) é rejeitada")]
    public void Criar_CicloTransitivo_Falha()
    {
        PrecedenciaFase ab = Aresta("INSCRICAO", "HOMOLOGACAO");
        PrecedenciaFase bc = Aresta("HOMOLOGACAO", "AVALIACAO");

        Result<PrecedenciaFase> r = Criar(
            "AVALIACAO", "INSCRICAO", arestasVivas: [ab, bc]);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.CicloDetectado);
    }

    [Fact(DisplayName = "Aresta que não fecha ciclo (grafo acíclico) é aceita")]
    public void Criar_SemCiclo_Aceita()
    {
        PrecedenciaFase ab = Aresta("INSCRICAO", "HOMOLOGACAO");
        PrecedenciaFase bc = Aresta("HOMOLOGACAO", "AVALIACAO");

        Result<PrecedenciaFase> r = Criar(
            "INSCRICAO", "AVALIACAO", arestasVivas: [ab, bc]);

        r.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "As seis arestas de §3.3 formam um grafo acíclico entre si")]
    public void Criar_SeisArestasEstruturais_FormamGrafoAciclico()
    {
        var seis = new (string Antecessora, string Sucessora)[]
        {
            ("INSCRICAO", "HOMOLOGACAO"),
            ("RESULTADO_PRELIMINAR", "RECURSOS"),
            ("RECURSOS", "RESULTADO_FINAL"),
            ("RESULTADO_FINAL", "HABILITACAO"),
            ("HABILITACAO", "MATRICULA"),
            ("HETEROIDENTIFICACAO", "HOMOLOGACAO_RESULTADO_FINAL"),
        };

        List<PrecedenciaFase> vivas = [];
        foreach ((string antecessora, string sucessora) in seis)
        {
            Result<PrecedenciaFase> r = PrecedenciaFase.Criar(antecessora, sucessora, false, vivas);
            r.IsSuccess.Should().BeTrue($"a aresta {antecessora}→{sucessora} não deveria fechar ciclo com as anteriores");
            vivas.Add(r.Value!);
        }

        vivas.Should().HaveCount(6);
    }

    // ── Atualização ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Atualizar troca PermiteSobreposicao mantendo o par imutável")]
    public void Atualizar_TrocaSobreposicao_MantemParImutavel()
    {
        PrecedenciaFase aresta = Aresta("INSCRICAO", "HOMOLOGACAO");
        Guid idOriginal = aresta.Id;

        aresta.Atualizar(true);

        aresta.PermiteSobreposicao.Should().BeTrue();
        aresta.AntecessoraCodigo.Should().Be("INSCRICAO");
        aresta.SucessoraCodigo.Should().Be("HOMOLOGACAO");
        aresta.Id.Should().Be(idOriginal);
    }
}
