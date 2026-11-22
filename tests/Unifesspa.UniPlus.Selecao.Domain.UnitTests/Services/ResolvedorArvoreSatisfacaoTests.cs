namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ArvoreDeTeste;

/// <summary>
/// Cobertura da álgebra de <see cref="ResolvedorArvoreSatisfacao"/> (Story #920) — álgebra ternária de 5
/// estados e emissão de consequência por fronteira ativa, todos os cenários BDD da issue #920
/// e da spec <c>documentos-exigidos-composicao</c> (requirements "Álgebra ternária de
/// satisfação" e "Consequência por nó e fronteira ativa de emissão"). Substitui
/// <c>ResolvedorExigenciasDocumentaisTests</c> (grupo plano, removido).
/// </summary>
public sealed class ResolvedorArvoreSatisfacaoTests
{
    [Fact(DisplayName = "Árvore nula é erro de programação: a configuração congelada sempre traz a árvore")]
    public void Resolver_ArvoreNula_Lanca()
    {
        Action resolver = () => ResolvedorArvoreSatisfacao.Resolver(null!, SemFatos, SemApresentacoes);

        resolver.Should().Throw<ArgumentNullException>().WithParameterName("arvore");
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

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(rg, cpf));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Satisfeito);
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

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(cin));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Satisfeito);
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

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(rg));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Pendente);
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

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(declaracao));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Satisfeito);
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

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(a, b));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Satisfeito);
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

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(a));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Pendente);
    }

    // ── Aplicabilidade ternária ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "OU com todos os filhos não-aplicáveis é não-aplicável, não pendente permanente")]
    public void Ou_TodosNaoAplicaveis_NaoAplicavel()
    {
        DocumentoExigido a = DocumentoCondicional("FATO_X");
        DocumentoExigido b = DocumentoCondicional("FATO_X");
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, null, null, [], [NoExigencia.CriarFolha(a, 0).Value!, NoExigencia.CriarFolha(b, 1).Value!]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz), fatos: Fatos("FATO_X", false));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.NaoAplicavel);
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

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Arvore(raiz), fatos: Fatos("FATO_X", false), apresentacoes: Apresenta(satisfeita));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Impossivel);
    }

    [Fact(DisplayName = "Folha indeterminada mantém o grupo pendente — nunca some (não-exigido)")]
    public void Ou_FolhaIndeterminada_MantemPendente()
    {
        DocumentoExigido indeterminada = DocumentoCondicional("FATO_X");
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, null, null, [], [NoExigencia.CriarFolha(indeterminada, 0).Value!]).Value!;

        // FATO_X ausente do dicionário de fatos resolvidos — Ternario.Indeterminado.
        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Indeterminado);
    }

    [Fact(DisplayName = "Story #926: fato ausente e fato NAO_APLICAVEL levam a estados opostos na fronteira do resolvedor")]
    public void Folha_FatoAusenteVersusNaoAplicavel_EstadosOpostos()
    {
        NoExigencia raiz = GrupoComDocumentoCondicional("CONCORRER_PCD");

        // Ausente do dicionário: nada se sabe sobre o fato, a exigência segue pendente.
        Result<ResultadoDaComprovacao> comFatoAusente = Resolver(Arvore(raiz));

        // Declarado não-aplicável: a pré-condição do campo é falsa, a exigência está resolvida
        // como não-exigida — e o candidato não é cobrado por um documento que não lhe cabe.
        Dictionary<string, FatoResolvido> naoAplicavel = FatoNaoAplicavel("CONCORRER_PCD");
        Result<ResultadoDaComprovacao> comFatoNaoAplicavel = Resolver(Arvore(raiz), naoAplicavel);

        comFatoAusente.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Indeterminado);
        comFatoNaoAplicavel.Value!.SituacoesPorRegra[raiz.Id].Should().Be(
            SituacaoDaComprovacao.NaoAplicavel,
            "a ausência do fato é pendência; a inaplicabilidade declarada é resposta definitiva");
    }

    [Fact(DisplayName = "E ignora alternativa não-aplicável")]
    public void E_AlternativaNaoAplicavelIgnorada()
    {
        DocumentoExigido a = DocumentoGeral();
        DocumentoExigido b = DocumentoCondicional("FATO_X");
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [NoExigencia.CriarFolha(a, 0).Value!, NoExigencia.CriarFolha(b, 1).Value!]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz), fatos: Fatos("FATO_X", false), apresentacoes: Apresenta(a));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Satisfeito);
    }

    [Theory(DisplayName = "Atendimento especializado: o laudo é exigido da condição de saúde e a certidão da lactante, só de quem solicita o atendimento")]
    [InlineData(true, "PCD", SituacaoDaComprovacao.Pendente, SituacaoDaComprovacao.NaoAplicavel)]
    [InlineData(true, "LACTANTE", SituacaoDaComprovacao.NaoAplicavel, SituacaoDaComprovacao.Pendente)]
    [InlineData(true, "PESSOA_IDOSA", SituacaoDaComprovacao.NaoAplicavel, SituacaoDaComprovacao.NaoAplicavel)]
    [InlineData(false, "PCD", SituacaoDaComprovacao.NaoAplicavel, SituacaoDaComprovacao.NaoAplicavel)]
    public void Folha_DocumentosDoAtendimentoEspecializado_SaoExigidosPeloFato(
        bool solicita, string condicao, SituacaoDaComprovacao laudo, SituacaoDaComprovacao certidao)
    {
        NoExigencia noLaudo = NoExigencia.CriarFolha(DocumentoDeAtendimento("LAUDO_MEDICO", "PCD", "DISLEXIA"), 0).Value!;
        NoExigencia noCertidao = NoExigencia.CriarFolha(DocumentoDeAtendimento("CERTIDAO_NASCIMENTO", "LACTANTE"), 1).Value!;
        Dictionary<string, FatoResolvido> fatos = new(StringComparer.Ordinal)
        {
            ["SOLICITA_ATENDIMENTO_ESPECIALIZADO"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(solicita)),
            ["CONDICAO_ATENDIMENTO"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(new[] { condicao })),
        };

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(noLaudo, noCertidao), fatos);

        resultado.Value!.SituacoesPorRegra[noLaudo.Id].Should().Be(laudo);
        resultado.Value!.SituacoesPorRegra[noCertidao.Id].Should().Be(certidao);
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

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Arvore(raiz), fatos: Fatos("FATO_X", false), apresentacoes: Apresenta(a, satisfeita));

        resultado.Value!.SituacoesPorRegra[ouImpossivel.Id].Should().Be(SituacaoDaComprovacao.Impossivel);
        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Impossivel);
    }
    private static DocumentoExigido DocumentoDeAtendimento(string tipo, params string[] condicoes) => DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), tipo, tipo, "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Condicional, obrigatorio: true, "REMOVE_VANTAGEM",
            [.. condicoes.SelectMany(static (c, clausula) => new[]
            {
                CondicaoGatilho.Criar(clausula, "SOLICITA_ATENDIMENTO_ESPECIALIZADO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
                CondicaoGatilho.Criar(clausula, "CONDICAO_ATENDIMENTO", Operador.Igual, JsonSerializer.SerializeToElement(c)).Value!,
            })],
            [], null, Qualquer, null).Value!;
    private static Dictionary<string, FatoResolvido> FatoNaoAplicavel(string codigo) =>
        new(StringComparer.Ordinal) { [codigo] = FatoResolvido.NaoAplicavel() };

    private static NoExigencia GrupoComDocumentoCondicional(string fato) =>
        NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [],
            [NoExigencia.CriarFolha(DocumentoCondicional(fato), 0).Value!]).Value!;

}
