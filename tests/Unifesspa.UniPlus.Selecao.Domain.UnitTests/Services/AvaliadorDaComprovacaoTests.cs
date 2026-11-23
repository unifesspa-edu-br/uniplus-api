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

using static CenariosDeComprovacao;

/// <summary>Verifica situações e consequências das regras de comprovação.</summary>
public sealed class AvaliadorDaComprovacaoTests
{
    [Fact(DisplayName = "A ausência das regras é erro de programação")]
    public void Resolver_RegrasNulas_Lanca()
    {
        Action resolver = () => AvaliadorDaComprovacao.Resolver(null!, SemFatos, SemApresentacoes);

        resolver.Should().Throw<ArgumentNullException>().WithParameterName("regras");
    }


    [Fact(DisplayName = "RG e CPF juntos cumprem a alternativa à CIN")]
    public void CombinacaoDeDocumentos_ApresentaRgECpf_Cumpre()
    {
        DocumentoExigido rg = DocumentoGeral();
        DocumentoExigido cpf = DocumentoGeral();
        DocumentoExigido cin = DocumentoGeral();
        RegraDeComprovacao regraDeRg = RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!;
        RegraDeComprovacao regraDeCpf = RegraDeComprovacao.CriarRegraDeDocumento(cpf, 1).Value!;
        RegraDeComprovacao grupoDeTodos = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 0, null, null, [], [regraDeRg, regraDeCpf]).Value!;
        RegraDeComprovacao regraDeCin = RegraDeComprovacao.CriarRegraDeDocumento(cin, 1).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, null, null, [], [grupoDeTodos, regraDeCin]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: Apresenta(rg, cpf));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Fact(DisplayName = "CIN cumpre a alternativa a RG e CPF")]
    public void CombinacaoDeDocumentos_ApresentaSoCin_Cumpre()
    {
        DocumentoExigido rg = DocumentoGeral();
        DocumentoExigido cpf = DocumentoGeral();
        DocumentoExigido cin = DocumentoGeral();
        RegraDeComprovacao grupoDeTodos = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.TodosOsItens, 0, null, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(cpf, 1).Value!]).Value!;
        RegraDeComprovacao regraDeCin = RegraDeComprovacao.CriarRegraDeDocumento(cin, 1).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, null, null, [], [grupoDeTodos, regraDeCin]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: Apresenta(cin));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Fact(DisplayName = "RG sozinho não cumpre a alternativa a RG e CPF")]
    public void CombinacaoDeDocumentos_ApresentaSoRg_NaoCumpre()
    {
        DocumentoExigido rg = DocumentoGeral();
        DocumentoExigido cpf = DocumentoGeral();
        DocumentoExigido cin = DocumentoGeral();
        RegraDeComprovacao grupoDeTodos = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.TodosOsItens, 0, null, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(cpf, 1).Value!]).Value!;
        RegraDeComprovacao regraDeCin = RegraDeComprovacao.CriarRegraDeDocumento(cin, 1).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, null, null, [], [grupoDeTodos, regraDeCin]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: Apresenta(rg));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }


    [Fact(DisplayName = "Uma apresentação cumpre o grupo que exige ao menos um item")]
    public void GrupoComQuantidadeMinima_UmaApresentacao_Cumpre()
    {
        DocumentoExigido certidao = DocumentoGeral();
        DocumentoExigido declaracao = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, null, null, [],
            [RegraDeComprovacao.CriarRegraDeDocumento(certidao, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(declaracao, 1).Value!]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: Apresenta(declaracao));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }


    [Fact(DisplayName = "Duas apresentações cumprem o grupo que exige dois de três itens")]
    public void QuantidadeMinima_DuasDeTresApresentadas_Cumpre()
    {
        DocumentoExigido a = DocumentoGeral();
        DocumentoExigido b = DocumentoGeral();
        DocumentoExigido c = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 2, null, [],
            [RegraDeComprovacao.CriarRegraDeDocumento(a, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(b, 1).Value!, RegraDeComprovacao.CriarRegraDeDocumento(c, 2).Value!]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: Apresenta(a, b));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Fact(DisplayName = "Uma apresentação deixa pendente o grupo que exige dois de três itens")]
    public void QuantidadeMinima_UmaDeTresApresentada_Pendente()
    {
        DocumentoExigido a = DocumentoGeral();
        DocumentoExigido b = DocumentoGeral();
        DocumentoExigido c = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 2, null, [],
            [RegraDeComprovacao.CriarRegraDeDocumento(a, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(b, 1).Value!, RegraDeComprovacao.CriarRegraDeDocumento(c, 2).Value!]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: Apresenta(a));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
    }


    [Fact(DisplayName = "Grupo com todos os itens não exigidos também não é exigido")]
    public void Grupo_TodosNaoAplicaveis_NaoAplicavel()
    {
        DocumentoExigido a = DocumentoCondicional("FATO_X");
        DocumentoExigido b = DocumentoCondicional("FATO_X");
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, null, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(a, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(b, 1).Value!]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), fatos: Fatos("FATO_X", false));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.NaoExigida);
    }

    [Fact(DisplayName = "Grupo sem itens suficientes fica sem possibilidade de cumprimento")]
    public void Grupo_MaximoAtingivelMenorQueN_Impossivel()
    {
        DocumentoExigido cumprida = DocumentoGeral();
        DocumentoExigido naoAplicavel1 = DocumentoCondicional("FATO_X");
        DocumentoExigido naoAplicavel2 = DocumentoCondicional("FATO_X");
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 2, null, [],
            [RegraDeComprovacao.CriarRegraDeDocumento(cumprida, 0).Value!,
             RegraDeComprovacao.CriarRegraDeDocumento(naoAplicavel1, 1).Value!,
             RegraDeComprovacao.CriarRegraDeDocumento(naoAplicavel2, 2).Value!]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), fatos: Fatos("FATO_X", false), apresentacoes: Apresenta(cumprida));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.SemPossibilidadeDeCumprimento);
    }

    [Fact(DisplayName = "Regra de documento que aguarda dados mantém a espera do grupo")]
    public void Grupo_RegraQueAguardaDados_MantemPendente()
    {
        DocumentoExigido indeterminada = DocumentoCondicional("FATO_X");
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, null, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(indeterminada, 0).Value!]).Value!;

        // FATO_X ausente do dicionário de fatos resolvidos — Ternario.Indeterminado.
        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.AguardandoDados);
    }

    [Fact(DisplayName = "Fato ausente e fato não aplicável produzem situações distintas")]
    public void RegraDeDocumento_FatoAusenteVersusNaoAplicavel_EstadosOpostos()
    {
        RegraDeComprovacao regraSemGrupo = GrupoComDocumentoCondicional("CONCORRER_PCD");

        // Ausente do dicionário: nada se sabe sobre o fato, a exigência segue pendente.
        Result<ResultadoDaComprovacao> comFatoAusente = Resolver(Regras(regraSemGrupo));

        // Declarado não-aplicável: a pré-condição do campo é falsa, a exigência está resolvida
        // como não-exigida — e o candidato não é cobrado por um documento que não lhe cabe.
        Dictionary<string, FatoResolvido> naoAplicavel = FatoNaoAplicavel("CONCORRER_PCD");
        Result<ResultadoDaComprovacao> comFatoNaoAplicavel = Resolver(Regras(regraSemGrupo), naoAplicavel);

        comFatoAusente.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.AguardandoDados);
        comFatoNaoAplicavel.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(
            SituacaoDaComprovacao.NaoExigida,
            "a ausência do fato é pendência; a inaplicabilidade declarada é resposta definitiva");
    }

    [Fact(DisplayName = "Grupo que exige todos os itens desconsidera item não exigido")]
    public void GrupoDeTodos_AlternativaNaoAplicavelIgnorada()
    {
        DocumentoExigido a = DocumentoGeral();
        DocumentoExigido b = DocumentoCondicional("FATO_X");
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.TodosOsItens, 0, null, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(a, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(b, 1).Value!]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), fatos: Fatos("FATO_X", false), apresentacoes: Apresenta(a));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
    }

    [Theory(DisplayName = "Atendimento especializado: o laudo é exigido da condição de saúde e a certidão da lactante, só de quem solicita o atendimento")]
    [InlineData(true, "PCD", SituacaoDaComprovacao.PendenteDeComprovacao, SituacaoDaComprovacao.NaoExigida)]
    [InlineData(true, "LACTANTE", SituacaoDaComprovacao.NaoExigida, SituacaoDaComprovacao.PendenteDeComprovacao)]
    [InlineData(true, "PESSOA_IDOSA", SituacaoDaComprovacao.NaoExigida, SituacaoDaComprovacao.NaoExigida)]
    [InlineData(false, "PCD", SituacaoDaComprovacao.NaoExigida, SituacaoDaComprovacao.NaoExigida)]
    public void RegraDeDocumento_DocumentosDoAtendimentoEspecializado_SaoExigidosPeloFato(
        bool solicita, string condicao, SituacaoDaComprovacao laudo, SituacaoDaComprovacao certidao)
    {
        RegraDeComprovacao regraDeLaudo = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoDeAtendimento("LAUDO_MEDICO", "PCD", "DISLEXIA"), 0).Value!;
        RegraDeComprovacao regraDeCertidao = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoDeAtendimento("CERTIDAO_NASCIMENTO", "LACTANTE"), 1).Value!;
        Dictionary<string, FatoResolvido> fatos = new(StringComparer.Ordinal)
        {
            ["SOLICITA_ATENDIMENTO_ESPECIALIZADO"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(solicita)),
            ["CONDICAO_ATENDIMENTO"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(new[] { condicao })),
        };

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraDeLaudo, regraDeCertidao), fatos);

        resultado.Value!.SituacoesPorRegra[regraDeLaudo.Id].Should().Be(laudo);
        resultado.Value!.SituacoesPorRegra[regraDeCertidao.Id].Should().Be(certidao);
    }

    [Fact(DisplayName = "Grupo que exige todos os itens não pode ser cumprido quando um item não pode ser cumprido")]
    public void GrupoDeTodos_ComItemImpossivel_EhImpossivel()
    {
        DocumentoExigido a = DocumentoGeral();
        DocumentoExigido cumprida = DocumentoGeral();
        DocumentoExigido naoAplicavel = DocumentoCondicional("FATO_X");
        RegraDeComprovacao grupoSemPossibilidadeDeCumprimento = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 2, null, [],
            [RegraDeComprovacao.CriarRegraDeDocumento(cumprida, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(naoAplicavel, 1).Value!]).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.TodosOsItens, 0, null, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(a, 0).Value!, grupoSemPossibilidadeDeCumprimento]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), fatos: Fatos("FATO_X", false), apresentacoes: Apresenta(a, cumprida));

        resultado.Value!.SituacoesPorRegra[grupoSemPossibilidadeDeCumprimento.Id].Should().Be(SituacaoDaComprovacao.SemPossibilidadeDeCumprimento);
        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.SemPossibilidadeDeCumprimento);
    }
    private static DocumentoExigido DocumentoDeAtendimento(string tipo, params string[] condicoes) => DocumentoExigido.Criar(
        new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), tipo, tipo, "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal),
        new MomentoDaExigencia(FaseId, null, null),
        new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [.. condicoes.SelectMany(static (c, clausula) => new[]
            {
                CondicaoGatilho.Criar(clausula, "SOLICITA_ATENDIMENTO_ESPECIALIZADO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
                CondicaoGatilho.Criar(clausula, "CONDICAO_ATENDIMENTO", Operador.Igual, JsonSerializer.SerializeToElement(c)).Value!,
            })]),
        new IndeferimentoDoDocumento(true, "REMOVE_VANTAGEM", []),
        new RequisitosDoDocumento(Qualquer, null, null, null, null),
        null).Value!;
    private static Dictionary<string, FatoResolvido> FatoNaoAplicavel(string codigo) =>
        new(StringComparer.Ordinal) { [codigo] = FatoResolvido.NaoAplicavel() };

    private static RegraDeComprovacao GrupoComDocumentoCondicional(string fato) =>
        RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, null, null, [],
            [RegraDeComprovacao.CriarRegraDeDocumento(DocumentoCondicional(fato), 0).Value!]).Value!;

}
