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
    private static readonly IReadOnlyDictionary<string, FatoResolvido> SemFatos = new Dictionary<string, FatoResolvido>(StringComparer.Ordinal);

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

    private static Dictionary<string, FatoResolvido> Fatos(string fato, bool valor) =>
        new(StringComparer.Ordinal) { [fato] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(valor)) };

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> Apresenta(params DocumentoExigido[] documentos) =>
        documentos.ToDictionary(
            static d => d.Id,
            static IReadOnlyList<ApresentacaoDocumento> (d) => [new ApresentacaoDocumento(Guid.CreateVersion7())]);

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> ApresentaComChaves(
        DocumentoExigido documento, params string?[] chavesDistincao) =>
        new()
        {
            [documento.Id] = [.. chavesDistincao.Select(static chave => new ApresentacaoDocumento(Guid.CreateVersion7(), chave))],
        };

    private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> SemApresentacoes =
        new Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>();

    private static ArvoreExigenciasCongelada Arvore(params NoExigencia[] raizes) =>
        ArvoreExigenciasCongelada.DeGrafoReidratado(raizes);

    private static Result<ResultadoResolucaoArvore> Resolver(
        ArvoreExigenciasCongelada arvore,
        IReadOnlyDictionary<string, FatoResolvido>? fatos = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>? apresentacoes = null,
        IReadOnlyDictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>>? instancias = null) =>
        ResolvedorArvoreSatisfacao.Resolver(arvore, fatos ?? SemFatos, apresentacoes ?? SemApresentacoes, instancias);

    private static InstanciaEntidade Instancia(string entidadeId, params (string Fato, bool Valor)[] atributos) =>
        new(entidadeId, atributos.ToDictionary(
            static a => a.Fato,
            static a => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(a.Valor)),
            StringComparer.Ordinal));

    private static Dictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> InstanciasDe(
        TipoEntidade tipo, params InstanciaEntidade[] instancias) =>
        new() { [tipo] = instancias };

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> ApresentaDeEntidade(
        DocumentoExigido documento, string entidadeId) =>
        new() { [documento.Id] = [new ApresentacaoDocumento(Guid.CreateVersion7(), EntidadeId: entidadeId)] };

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

    [Fact(DisplayName = "Story #926: fato ausente e fato NAO_APLICAVEL levam a estados opostos na fronteira do resolvedor")]
    public void Folha_FatoAusenteVersusNaoAplicavel_EstadosOpostos()
    {
        DocumentoExigido condicional = DocumentoCondicional("CONCORRER_PCD");
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, null, null, [], [NoExigencia.CriarFolha(condicional, 0).Value!]).Value!;

        // Ausente do dicionário: nada se sabe sobre o fato, a exigência segue pendente.
        Result<ResultadoResolucaoArvore> comFatoAusente = Resolver(Arvore(raiz));

        // Declarado não-aplicável: a pré-condição do campo é falsa, a exigência está resolvida
        // como não-exigida — e o candidato não é cobrado por um documento que não lhe cabe.
        Dictionary<string, FatoResolvido> naoAplicavel = new(StringComparer.Ordinal)
        {
            ["CONCORRER_PCD"] = FatoResolvido.NaoAplicavel(),
        };
        Result<ResultadoResolucaoArvore> comFatoNaoAplicavel = Resolver(Arvore(raiz), naoAplicavel);

        comFatoAusente.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Indeterminado);
        comFatoNaoAplicavel.Value!.EstadosPorNo[raiz.Id].Should().Be(
            EstadoSatisfacao.NaoAplicavel,
            "a ausência do fato é pendência; a inaplicabilidade declarada é resposta definitiva");
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

    // ── Cardinalidade qualificada (Story #921) ───────────────────────────────────────────

    [Fact(DisplayName = "Folha sem chaveDistincao: contagem bruta — 2 apresentações satisfazem N=2")]
    public void Cardinalidade_SemChave_ContagemBrutaSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(documento, 0, quantidadeMinima: 2).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaComChaves(documento, null, null));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Satisfeito);
    }

    [Fact(DisplayName = "Folha sem chaveDistincao: 3 arquivos da mesma competência sem tag não satisfazem N=2 se só 1 apresentação")]
    public void Cardinalidade_SemChave_UmaApresentacaoNaoSatisfazDois()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(documento, 0, quantidadeMinima: 2).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaComChaves(documento, (string?)null));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
    }

    [Fact(DisplayName = "CompetenciaMensal: 3 competências regulares corretas satisfaz")]
    public void Cardinalidade_CompetenciaMensal_TresRegularesSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        DateOnly ancora = new(2026, 3, 15);
        NoExigencia raiz = NoExigencia.CriarFolha(
            documento, 0, quantidadeMinima: 3, chaveDistincao: ChaveDistincao.CompetenciaMensal, dataReferencia: ancora).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaComChaves(documento, "2026-03", "2026-02", "2026-01"));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Satisfeito);
    }

    [Fact(DisplayName = "CompetenciaMensal: falta a competência mais recente fica pendente mesmo com N apresentações")]
    public void Cardinalidade_CompetenciaMensal_FaltaMaisRecentePendente()
    {
        DocumentoExigido documento = DocumentoGeral();
        DateOnly ancora = new(2026, 3, 15);
        NoExigencia raiz = NoExigencia.CriarFolha(
            documento, 0, quantidadeMinima: 3, chaveDistincao: ChaveDistincao.CompetenciaMensal, dataReferencia: ancora).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaComChaves(documento, "2026-02", "2026-01", "2025-12"));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
    }

    [Fact(DisplayName = "Ocorrencia com ocorrenciasEsperadas: cobre os slots concretos satisfaz")]
    public void Cardinalidade_OcorrenciaComLista_CobreSlotsSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia,
            ocorrenciasEsperadas: ["eleicao_2026_1", "eleicao_2026_2"]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaComChaves(documento, "eleicao_2026_1", "eleicao_2026_2"));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Satisfeito);
    }

    [Fact(DisplayName = "Ocorrencia com ocorrenciasEsperadas: falta um slot concreto fica pendente")]
    public void Cardinalidade_OcorrenciaComLista_FaltaSlotPendente()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia,
            ocorrenciasEsperadas: ["eleicao_2026_1", "eleicao_2026_2"]).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaComChaves(documento, "eleicao_2026_1"));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
    }

    [Fact(DisplayName = "Ocorrencia com ocorrenciasEsperadas: a mesma apresentação (mesmo Id) não cobre dois slots ao mesmo tempo")]
    public void Cardinalidade_OcorrenciaComLista_MesmaIdentidadeNaoCobreDoisSlots()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia,
            ocorrenciasEsperadas: ["eleicao_2026_1", "eleicao_2026_2"]).Value!;
        Guid idApresentacao = Guid.CreateVersion7();
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = new()
        {
            [documento.Id] =
            [
                new ApresentacaoDocumento(idApresentacao, "eleicao_2026_1"),
                new ApresentacaoDocumento(idApresentacao, "eleicao_2026_2"),
            ],
        };

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: apresentacoes);

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
    }

    [Fact(DisplayName = "Ocorrencia sem ocorrenciasEsperadas: distinção pura por N tags diferentes satisfaz")]
    public void Cardinalidade_OcorrenciaSemLista_DistincaoPuraSatisfaz()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaComChaves(documento, "eleicao_x", "eleicao_y"));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Satisfeito);
    }

    [Fact(DisplayName = "Ocorrencia sem ocorrenciasEsperadas: mesma tag repetida não conta duas vezes")]
    public void Cardinalidade_OcorrenciaSemLista_TagRepetidaNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaComChaves(documento, "eleicao_x", "eleicao_x"));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
    }

    [Fact(DisplayName = "Ocorrencia sem ocorrenciasEsperadas: tag nula/em branco não conta como distinção")]
    public void Cardinalidade_OcorrenciaSemLista_TagNulaNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaComChaves(documento, "eleicao_x", null, "   "));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
    }

    [Fact(DisplayName = "Ocorrencia sem ocorrenciasEsperadas: a mesma apresentação relatada duas vezes com tags diferentes não conta como duas")]
    public void Cardinalidade_OcorrenciaSemLista_MesmaIdentidadeComTagsDiferentesNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(
            documento, 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia).Value!;
        Guid idApresentacao = Guid.CreateVersion7();
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = new()
        {
            [documento.Id] =
            [
                new ApresentacaoDocumento(idApresentacao, "eleicao_x"),
                new ApresentacaoDocumento(idApresentacao, "eleicao_y"),
            ],
        };

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: apresentacoes);

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
    }

    [Fact(DisplayName = "Sem chaveDistincao: a mesma apresentação relatada duas vezes (mesmo Id) não conta como duas")]
    public void Cardinalidade_SemChave_MesmaIdentidadeRepetidaNaoConta()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(documento, 0, quantidadeMinima: 2).Value!;
        ApresentacaoDocumento apresentacao = new(Guid.CreateVersion7());
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = new()
        {
            [documento.Id] = [apresentacao, apresentacao],
        };

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: apresentacoes);

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
    }

    // ── Repetição por entidade (Story #922) ──────────────────────────────────────────────

    [Fact(DisplayName = "Story #926: atributo NAO_APLICAVEL da instância sobrescreve o mesmo fato resolvido do candidato")]
    public void RepeticaoPorEntidade_AtributoNaoAplicavelSobrescreveFatoDoCandidato()
    {
        DocumentoExigido condicional = DocumentoCondicional("SEM_RENDA");
        NoExigencia raiz = NoExigencia.CriarFolha(
            condicional, 0, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar).Value!;

        // O candidato declarou o fato, mas para ESTE membro do núcleo familiar ele não se
        // aplica — o sujeito do gatilho é a instância, não o candidato.
        Dictionary<string, FatoResolvido> fatosDoCandidato = new(StringComparer.Ordinal)
        {
            ["SEM_RENDA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true)),
        };
        InstanciaEntidade membro = new("membro_1", new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)
        {
            ["SEM_RENDA"] = FatoResolvido.NaoAplicavel(),
        });

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz),
            fatos: fatosDoCandidato,
            instancias: InstanciasDe(TipoEntidade.MembroNucleoFamiliar, membro));

        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == condicional.Id
                && s.EntidadeId == "membro_1"
                && s.Status == StatusResolucaoExigencia.NaoAplicavel,
            "o atributo da instância tem precedência — sem isso, o fato do candidato exigiria o documento de um membro a quem ele não cabe");
    }

    [Fact(DisplayName = "Documento correlacionado à instância certa: RG do membro 2 satisfaz só (RG, MEMBRO_NUCLEO_FAMILIAR, membro_2)")]
    public void RepeticaoPorEntidade_DocumentoCorrelacionadoAInstanciaCerta()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        NoExigencia raiz = NoExigencia.CriarFolha(rg, 0, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar).Value!;

        Dictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            TipoEntidade.MembroNucleoFamiliar, Instancia("membro_1"), Instancia("membro_2"), Instancia("membro_3"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaDeEntidade(rg, "membro_2"), instancias: instancias);

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
        resultado.Value!.ConsequenciasVigentes.Should().HaveCount(2);
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.EntidadeId == "membro_1" && c.Consequencia == "ELIMINA");
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.EntidadeId == "membro_3" && c.Consequencia == "ELIMINA");
        resultado.Value!.ConsequenciasVigentes.Should().NotContain(c => c.EntidadeId == "membro_2");
        resultado.Value!.StatusPorEntidade.Should().HaveCount(3);
        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == rg.Id && s.EntidadeId == "membro_1" && s.Status == StatusResolucaoExigencia.Pendente);
        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == rg.Id && s.EntidadeId == "membro_2" && s.Status == StatusResolucaoExigencia.Satisfeita);
        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == rg.Id && s.EntidadeId == "membro_3" && s.Status == StatusResolucaoExigencia.Pendente);
    }

    [Fact(DisplayName = "Repetição por entidade: folha sem ConsequenciaIndeferimento continua visível por instância via StatusPorEntidade")]
    public void RepeticaoPorEntidade_SemConsequencia_StatusPorEntidadeSinalizaInstanciaPendente()
    {
        DocumentoExigido comprovante = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(comprovante, 0, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar).Value!;

        Dictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            TipoEntidade.MembroNucleoFamiliar, Instancia("membro_1"), Instancia("membro_2"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaDeEntidade(comprovante, "membro_1"), instancias: instancias);

        // Sem consequência configurada, nada é emitido em ConsequenciasVigentes — o único
        // sinal de QUAL instância está pendente é StatusPorEntidade.
        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == comprovante.Id && s.EntidadeId == "membro_1" && s.Status == StatusResolucaoExigencia.Satisfeita);
        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == comprovante.Id && s.EntidadeId == "membro_2" && s.Status == StatusResolucaoExigencia.Pendente);
    }

    [Fact(DisplayName = "Gatilho por atributo da entidade: declaração de isento só é exigida do membro adulto e sem renda")]
    public void RepeticaoPorEntidade_GatilhoPorAtributoDaEntidade()
    {
        DocumentoExigido declaracaoIsento = DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "DECL_ISENTO", "Declaração de isento", "CAT",
            Aplicabilidade.Condicional, obrigatorio: false, "ELIMINA",
            [
                CondicaoGatilho.Criar(0, "MAIOR_IDADE", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
                CondicaoGatilho.Criar(0, "SEM_RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
            ],
            [], null, Qualquer, null).Value!;
        NoExigencia raiz = NoExigencia.CriarFolha(declaracaoIsento, 0, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar).Value!;

        Dictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            TipoEntidade.MembroNucleoFamiliar,
            Instancia("membro_adulto_sem_renda", ("MAIOR_IDADE", true), ("SEM_RENDA", true)),
            Instancia("membro_adulto_com_renda", ("MAIOR_IDADE", true), ("SEM_RENDA", false)),
            Instancia("membro_menor", ("MAIOR_IDADE", false), ("SEM_RENDA", true)));

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), instancias: instancias);

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.EntidadeId == "membro_adulto_sem_renda" && c.Consequencia == "ELIMINA");
    }

    [Fact(DisplayName = "PF + PJ vinculada repete por empresa: extrato da PJ 1 satisfaz só essa PJ — PJ 2 continua com extrato e IRPJ pendentes")]
    public void RepeticaoPorEntidade_PessoaJuridicaVinculada_CorrelacaoPorPj()
    {
        DocumentoExigido extrato = DocumentoGeral("ELIMINA");
        DocumentoExigido irpj = DocumentoGeral("ELIMINA");
        NoExigencia folhaExtrato = NoExigencia.CriarFolha(extrato, 0).Value!;
        NoExigencia folhaIrpj = NoExigencia.CriarFolha(irpj, 1).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [folhaExtrato, folhaIrpj],
            repetePorEntidade: TipoEntidade.PessoaJuridicaVinculada).Value!;

        Dictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            TipoEntidade.PessoaJuridicaVinculada, Instancia("pj_1"), Instancia("pj_2"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(grupo), apresentacoes: ApresentaDeEntidade(extrato, "pj_1"), instancias: instancias);

        resultado.Value!.EstadosPorNo[grupo.Id].Should().Be(EstadoSatisfacao.Pendente);
        resultado.Value!.ConsequenciasVigentes.Should().HaveCount(3);
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.EntidadeId == "pj_1" && c.NoExigenciaId == folhaIrpj.Id);
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.EntidadeId == "pj_2" && c.NoExigenciaId == folhaExtrato.Id);
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.EntidadeId == "pj_2" && c.NoExigenciaId == folhaIrpj.Id);
        resultado.Value!.ConsequenciasVigentes.Should().NotContain(c => c.EntidadeId == "pj_1" && c.NoExigenciaId == folhaExtrato.Id);
    }

    [Fact(DisplayName = "Repetição por entidade sem nenhuma instância declarada é não-aplicável")]
    public void RepeticaoPorEntidade_SemInstanciasDeclaradas_NaoAplicavel()
    {
        DocumentoExigido documento = DocumentoGeral("ELIMINA");
        NoExigencia raiz = NoExigencia.CriarFolha(documento, 0, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar).Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.NaoAplicavel);
        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Repetição por entidade: todas as instâncias satisfeitas suprime consequências")]
    public void RepeticaoPorEntidade_TodasSatisfeitas_Suprime()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        NoExigencia raiz = NoExigencia.CriarFolha(rg, 0, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar).Value!;

        Dictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            TipoEntidade.MembroNucleoFamiliar, Instancia("membro_1"), Instancia("membro_2"));

        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = new()
        {
            [rg.Id] =
            [
                new ApresentacaoDocumento(Guid.CreateVersion7(), EntidadeId: "membro_1"),
                new ApresentacaoDocumento(Guid.CreateVersion7(), EntidadeId: "membro_2"),
            ],
        };

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: apresentacoes, instancias: instancias);

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Satisfeito);
        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Repetição por entidade: PendenciasDeOrientacao de um grupo OU opaco repetido carrega o EntidadeId correto por instância")]
    public void RepeticaoPorEntidade_GrupoOuOpaco_PendenciasDeOrientacaoComEntidadeId()
    {
        DocumentoExigido docA = DocumentoGeral();
        DocumentoExigido docB = DocumentoGeral();
        NoExigencia folhaA = NoExigencia.CriarFolha(docA, 0).Value!;
        NoExigencia folhaB = NoExigencia.CriarFolha(docB, 1).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 2, "ELIMINA", [BaseLegalResolvida()], [folhaA, folhaB],
            repetePorEntidade: TipoEntidade.PessoaJuridicaVinculada).Value!;

        Dictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            TipoEntidade.PessoaJuridicaVinculada, Instancia("pj_1"), Instancia("pj_2"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(grupo), apresentacoes: ApresentaDeEntidade(docA, "pj_1"), instancias: instancias);

        resultado.Value!.PendenciasDeOrientacao.Should().HaveCount(3);
        resultado.Value!.PendenciasDeOrientacao.Should().Contain(p => p.NoExigenciaId == folhaB.Id && p.EntidadeId == "pj_1");
        resultado.Value!.PendenciasDeOrientacao.Should().Contain(p => p.NoExigenciaId == folhaA.Id && p.EntidadeId == "pj_2");
        resultado.Value!.PendenciasDeOrientacao.Should().Contain(p => p.NoExigenciaId == folhaB.Id && p.EntidadeId == "pj_2");
        resultado.Value!.ConsequenciasVigentes.Should().HaveCount(2);
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.NoExigenciaId == grupo.Id && c.EntidadeId == "pj_1");
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.NoExigenciaId == grupo.Id && c.EntidadeId == "pj_2");
    }

    [Fact(DisplayName = "Árvore congelada com repetição por entidade aninhada (só alcançável via Reidratar) é recusada")]
    public void RepeticaoPorEntidade_AninhadaViaReidratar_Recusa()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia folhaInterna = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, null, null, null, null, null,
            TipoEntidade.PessoaJuridicaVinculada, [], []);
        NoExigencia grupoExterno = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoE, 0, null, null, null, null, null, null, null,
            TipoEntidade.MembroNucleoFamiliar, [], [folhaInterna]);

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(grupoExterno));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ResolvedorArvoreSatisfacao.ArvoreEstruturalmenteInvalida");
    }

    [Fact(DisplayName = "Instâncias de entidade com EntidadeId duplicado são recusadas")]
    public void RepeticaoPorEntidade_InstanciasComIdDuplicado_Recusa()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(documento, 0, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar).Value!;
        Dictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            TipoEntidade.MembroNucleoFamiliar, Instancia("membro_1"), Instancia("membro_1"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), instancias: instancias);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ResolvedorArvoreSatisfacao.InstanciaEntidadeInvalida");
    }

    [Fact(DisplayName = "Instância de entidade com EntidadeId vazio é recusada")]
    public void RepeticaoPorEntidade_InstanciaComIdVazio_Recusa()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(documento, 0, repetePorEntidade: TipoEntidade.MembroNucleoFamiliar).Value!;
        Dictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            TipoEntidade.MembroNucleoFamiliar, Instancia(""));

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), instancias: instancias);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ResolvedorArvoreSatisfacao.InstanciaEntidadeInvalida");
    }

    private static NoExigenciaBaseLegal BaseLegalResolvida() =>
        NoExigenciaBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;
}
