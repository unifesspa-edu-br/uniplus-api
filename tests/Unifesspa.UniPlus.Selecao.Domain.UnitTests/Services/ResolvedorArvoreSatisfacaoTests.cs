namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ResolvedorArvoreSatisfacao"/> (Story #920) — álgebra ternária de 5
/// estados e emissão de consequência por fronteira ativa, todos os cenários BDD da issue #920
/// e da spec <c>documentos-exigidos-composicao</c> (requirements "Álgebra ternária de
/// satisfação" e "Consequência por nó e fronteira ativa de emissão"). Substitui
/// <c>ResolvedorExigenciasDocumentaisTests</c> (grupo plano, removido).
/// </summary>
public sealed class ResolvedorArvoreSatisfacaoTests
{
    private static readonly Guid FaseId = Guid.CreateVersion7();
    private static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;
    private static readonly IReadOnlyDictionary<string, JsonElement> SemFatos = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    private static DocumentoExigido DocumentoGeral(string? consequencia = null) =>
        DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "COD", "Nome", "CAT",
            Aplicabilidade.Geral, obrigatorio: true, consequencia, [], [], null, Qualquer, null).Value!;

    private static DocumentoExigido DocumentoCondicional(string fato, string? consequencia = null) =>
        DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "COD", "Nome", "CAT",
            Aplicabilidade.Condicional, obrigatorio: false, consequencia,
            [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            [], null, Qualquer, null).Value!;

    private static Dictionary<string, JsonElement> Fatos(string fato, bool valor) =>
        new(StringComparer.Ordinal) { [fato] = JsonSerializer.SerializeToElement(valor) };

    private static Dictionary<Guid, ApresentacaoDocumento> Apresenta(params DocumentoExigido[] documentos) =>
        documentos.ToDictionary(static d => d.Id, static d => new ApresentacaoDocumento(Guid.CreateVersion7()));

    private static readonly IReadOnlyDictionary<Guid, ApresentacaoDocumento> SemApresentacoes =
        new Dictionary<Guid, ApresentacaoDocumento>();

    private static ArvoreExigenciasCongelada Arvore(params NoExigencia[] raizes) =>
        ArvoreExigenciasCongelada.DeGrafoReidratado(raizes);

    private static Result<ResultadoResolucaoArvore> Resolver(
        ArvoreExigenciasCongelada arvore,
        IReadOnlyDictionary<string, JsonElement>? fatos = null,
        IReadOnlyDictionary<Guid, ApresentacaoDocumento>? apresentacoes = null) =>
        ResolvedorArvoreSatisfacao.Resolver(arvore, fatos ?? SemFatos, apresentacoes ?? SemApresentacoes);

    [Fact(DisplayName = "Árvore ausente retorna ArvoreAusente")]
    public void Resolver_ArvoreAusente_RetornaErroNomeado()
    {
        Result<ResultadoResolucaoArvore> resultado = ResolvedorArvoreSatisfacao.Resolver(null, SemFatos, SemApresentacoes);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ResolvedorArvoreSatisfacao.ArvoreAusente");
    }

    // ── (RG E CPF) OU CIN — cenário central da issue #920 ──────────────────────────────

    [Fact(DisplayName = "OU[E[RG,CPF],CIN]: apresentar RG e CPF satisfaz")]
    public void ArvoreCentral_ApresentaRgECpf_Satisfaz()
    {
        DocumentoExigido rg = DocumentoGeral();
        DocumentoExigido cpf = DocumentoGeral();
        DocumentoExigido cin = DocumentoGeral();
        NoExigencia noRg = NoExigencia.CriarFolha(rg, 0).Value!;
        NoExigencia noCpf = NoExigencia.CriarFolha(cpf, 1).Value!;
        NoExigencia grupoE = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [noRg, noCpf]).Value!;
        NoExigencia noCin = NoExigencia.CriarFolha(cin, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [], [grupoE, noCin]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(rg, cpf));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Satisfeito);
    }

    [Fact(DisplayName = "OU[E[RG,CPF],CIN]: apresentar só a CIN também satisfaz")]
    public void ArvoreCentral_ApresentaSoCin_Satisfaz()
    {
        DocumentoExigido rg = DocumentoGeral();
        DocumentoExigido cpf = DocumentoGeral();
        DocumentoExigido cin = DocumentoGeral();
        NoExigencia grupoE = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [NoExigencia.CriarFolha(rg, 0).Value!, NoExigencia.CriarFolha(cpf, 1).Value!]).Value!;
        NoExigencia noCin = NoExigencia.CriarFolha(cin, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [], [grupoE, noCin]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(cin));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Satisfeito);
    }

    [Fact(DisplayName = "OU[E[RG,CPF],CIN]: apresentar só o RG não satisfaz")]
    public void ArvoreCentral_ApresentaSoRg_NaoSatisfaz()
    {
        DocumentoExigido rg = DocumentoGeral();
        DocumentoExigido cpf = DocumentoGeral();
        DocumentoExigido cin = DocumentoGeral();
        NoExigencia grupoE = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [NoExigencia.CriarFolha(rg, 0).Value!, NoExigencia.CriarFolha(cpf, 1).Value!]).Value!;
        NoExigencia noCin = NoExigencia.CriarFolha(cin, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [], [grupoE, noCin]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(rg));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
    }

    // ── OU plano (caso degenerado) ──────────────────────────────────────────────────────

    [Fact(DisplayName = "OU plano continua válido — uma apresentação satisfaz o grupo")]
    public void OuPlano_UmaApresentacao_Satisfaz()
    {
        DocumentoExigido certidao = DocumentoGeral();
        DocumentoExigido declaracao = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, null, null, [],
            [NoExigencia.CriarFolha(certidao, 0).Value!, NoExigencia.CriarFolha(declaracao, 1).Value!]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(declaracao));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Satisfeito);
    }

    // ── N-de (N > 1) ─────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "N-de com N=2: duas de três apresentadas satisfaz")]
    public void NDe_DuasDeTresApresentadas_Satisfaz()
    {
        DocumentoExigido a = DocumentoGeral();
        DocumentoExigido b = DocumentoGeral();
        DocumentoExigido c = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 2, null, [],
            [NoExigencia.CriarFolha(a, 0).Value!, NoExigencia.CriarFolha(b, 1).Value!, NoExigencia.CriarFolha(c, 2).Value!]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(a, b));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Satisfeito);
    }

    [Fact(DisplayName = "N-de com N=2: só uma de três apresentada fica pendente")]
    public void NDe_UmaDeTresApresentada_Pendente()
    {
        DocumentoExigido a = DocumentoGeral();
        DocumentoExigido b = DocumentoGeral();
        DocumentoExigido c = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 2, null, [],
            [NoExigencia.CriarFolha(a, 0).Value!, NoExigencia.CriarFolha(b, 1).Value!, NoExigencia.CriarFolha(c, 2).Value!]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(a));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
    }

    // ── Aplicabilidade ternária ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "OU com todos os filhos não-aplicáveis é não-aplicável, não pendente permanente")]
    public void Ou_TodosNaoAplicaveis_NaoAplicavel()
    {
        DocumentoExigido a = DocumentoCondicional("FATO_X");
        DocumentoExigido b = DocumentoCondicional("FATO_X");
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, null, null, [], [NoExigencia.CriarFolha(a, 0).Value!, NoExigencia.CriarFolha(b, 1).Value!]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), fatos: Fatos("FATO_X", false));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.NaoAplicavel);
    }

    [Fact(DisplayName = "OU cujo máximo atingível é menor que N é IMPOSSIVEL sinalizado")]
    public void Ou_MaximoAtingivelMenorQueN_Impossivel()
    {
        DocumentoExigido satisfeita = DocumentoGeral();
        DocumentoExigido naoAplicavel1 = DocumentoCondicional("FATO_X");
        DocumentoExigido naoAplicavel2 = DocumentoCondicional("FATO_X");
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 2, null, [],
            [NoExigencia.CriarFolha(satisfeita, 0).Value!,
             NoExigencia.CriarFolha(naoAplicavel1, 1).Value!,
             NoExigencia.CriarFolha(naoAplicavel2, 2).Value!]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), fatos: Fatos("FATO_X", false), apresentacoes: Apresenta(satisfeita));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Impossivel);
    }

    [Fact(DisplayName = "Folha indeterminada mantém o grupo pendente — nunca some (não-exigido)")]
    public void Ou_FolhaIndeterminada_MantemPendente()
    {
        DocumentoExigido indeterminada = DocumentoCondicional("FATO_X");
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, null, null, [], [NoExigencia.CriarFolha(indeterminada, 0).Value!]).Value!;

        // FATO_X ausente do dicionário de fatos resolvidos — Ternario.Indeterminado.
        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Indeterminado);
    }

    [Fact(DisplayName = "E ignora alternativa não-aplicável")]
    public void E_AlternativaNaoAplicavelIgnorada()
    {
        DocumentoExigido a = DocumentoGeral();
        DocumentoExigido b = DocumentoCondicional("FATO_X");
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [NoExigencia.CriarFolha(a, 0).Value!, NoExigencia.CriarFolha(b, 1).Value!]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), fatos: Fatos("FATO_X", false), apresentacoes: Apresenta(a));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Satisfeito);
    }

    [Fact(DisplayName = "E com filho IMPOSSIVEL é IMPOSSIVEL")]
    public void E_ComFilhoImpossivel_EhImpossivel()
    {
        DocumentoExigido a = DocumentoGeral();
        DocumentoExigido satisfeita = DocumentoGeral();
        DocumentoExigido naoAplicavel = DocumentoCondicional("FATO_X");
        NoExigencia ouImpossivel = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 2, null, [],
            [NoExigencia.CriarFolha(satisfeita, 0).Value!, NoExigencia.CriarFolha(naoAplicavel, 1).Value!]).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [NoExigencia.CriarFolha(a, 0).Value!, ouImpossivel]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), fatos: Fatos("FATO_X", false), apresentacoes: Apresenta(a, satisfeita));

        resultado.Value!.EstadosPorNo[ouImpossivel.Id].Should().Be(EstadoSatisfacao.Impossivel);
        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Impossivel);
    }

    // ── Consequência por nó + fronteira ativa ────────────────────────────────────────────

    [Fact(DisplayName = "Ramo satisfeito suprime pendências e consequências abaixo")]
    public void Fronteira_RamoSatisfeito_SuprimeSubarvore()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        DocumentoExigido cpf = DocumentoGeral("ELIMINA");
        DocumentoExigido cin = DocumentoGeral("RECLASSIFICA_AC");
        NoExigencia grupoE = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [NoExigencia.CriarFolha(rg, 0).Value!, NoExigencia.CriarFolha(cpf, 1).Value!]).Value!;
        NoExigencia noCin = NoExigencia.CriarFolha(cin, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [], [grupoE, noCin]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(cin));

        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
        resultado.Value!.PendenciasDeOrientacao.Should().BeEmpty();
    }

    [Fact(DisplayName = "OU opaco pendente emite a própria consequência, não uma derivação das folhas")]
    public void Fronteira_OuOpacoPendente_EmiteAPropria()
    {
        DocumentoExigido a = DocumentoGeral("ELIMINA");
        DocumentoExigido b = DocumentoGeral("PENDENCIA_REENVIO");
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "RECLASSIFICA_AC", [BaseLegalResolvida()],
            [NoExigencia.CriarFolha(a, 0).Value!, NoExigencia.CriarFolha(b, 1).Value!]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz));

        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.NoExigenciaId == raiz.Id && c.TipoOrigem == TipoNo.GrupoOu && c.Consequencia == "RECLASSIFICA_AC");
        resultado.Value!.PendenciasDeOrientacao.Should().HaveCount(2);
    }

    [Fact(DisplayName = "E transparente emite só os filhos não satisfeitos, incluindo INDETERMINADO — sem dupla emissão")]
    public void Fronteira_ETransparente_EmiteSoFilhosNaoSatisfeitos()
    {
        DocumentoExigido docA = DocumentoCondicional("FATO_X", "ELIMINA");
        DocumentoExigido docB = DocumentoGeral("RECLASSIFICA_AC");
        NoExigencia noA = NoExigencia.CriarFolha(docA, 0).Value!;
        NoExigencia noB = NoExigencia.CriarFolha(docB, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [noA, noB]).Value!;

        // docB satisfeito (apresentado); docA indeterminado (FATO_X não resolvido).
        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(docB));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Indeterminado);
        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.NoExigenciaId == noA.Id && c.TipoOrigem == TipoNo.Folha && c.Consequencia == "ELIMINA");
    }

    [Fact(DisplayName = "Folha solteira (sem grupo) pendente emite a própria consequência — caso degenerado")]
    public void Fronteira_FolhaSolteiraPendente_EmiteAPropria()
    {
        DocumentoExigido documento = DocumentoGeral("ELIMINA");
        NoExigencia raiz = NoExigencia.CriarFolha(documento, 0).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz));

        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.NoExigenciaId == raiz.Id && c.TipoOrigem == TipoNo.Folha && c.Consequencia == "ELIMINA");
        resultado.Value!.StatusPorExigencia[documento.Id].Should().Be(StatusResolucaoExigencia.Pendente);
    }

    private static NoExigenciaBaseLegal BaseLegalResolvida() =>
        NoExigenciaBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;
}
