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

/// <summary>
/// Verifica o adiamento de consequências quando regras aguardam dados de outra fase.
/// A espera projeta situação indeterminada na agregação e não aplica consequência antecipada.
/// </summary>
public sealed class AvaliadorDaComprovacaoConsequenciasAdiadasTests
{
    private static readonly Guid FaseId = Guid.CreateVersion7();
    private static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;
    private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> SemApresentacoes =
        new Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>();

    private static DocumentoExigido DocumentoGeral(string? consequencia) =>
        DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "COD", "Nome", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, consequencia, [], [], null, Qualquer, null).Value!;

    private static DocumentoExigido DocumentoCondicional(string fato) =>
        DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "COD", "Nome", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: false, consequenciaIndeferimento: null,
            [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            [], null, Qualquer, null).Value!;

    private static Dictionary<string, FatoResolvido> Fatos(params (string Fato, bool Valor)[] fatos) =>
        fatos.ToDictionary(
            static f => f.Fato,
            static f => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(f.Valor)),
            StringComparer.Ordinal);

    private static RegrasDeComprovacaoDaVersao Regras(params RegraDeComprovacao[] regrasSemGrupo) =>
        RegrasDeComprovacaoDaVersao.DaConfiguracaoPublicada(regrasSemGrupo);

    private static Result<ResultadoDaComprovacao> Resolver(
        RegrasDeComprovacaoDaVersao regras,
        IReadOnlyDictionary<string, FatoResolvido>? fatos = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>? apresentacoes = null,
        IReadOnlySet<Guid>? bloqueadas = null) =>
        AvaliadorDaComprovacao.Resolver(
            regras, fatos ?? new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            apresentacoes ?? SemApresentacoes, null, bloqueadas);

    [Fact(DisplayName = "Regra de documento que aguarda dados não aplica consequência, enquanto o outro item aplica")]
    public void RegraQueAguardaDadosEmGrupoDeTodos_AdiaConsequencia_OutroItemAplica()
    {
        DocumentoExigido bloqueado = DocumentoGeral("ELIMINA");
        DocumentoExigido aberto = DocumentoGeral("REMOVE_VANTAGEM");
        RegraDeComprovacao regraQueAguardaDados = RegraDeComprovacao.CriarRegraDeDocumento(bloqueado, 0).Value!;
        RegraDeComprovacao regraComDados = RegraDeComprovacao.CriarRegraDeDocumento(aberto, 1).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 0, null, null, [], [regraQueAguardaDados, regraComDados]).Value!;

        ResultadoDaComprovacao resultado = Resolver(Regras(regraSemGrupo), bloqueadas: new HashSet<Guid> { bloqueado.Id }).Value!;

        resultado.SituacoesPorRegra[regraQueAguardaDados.Id].Should().Be(SituacaoDaComprovacao.AguardandoDados, "a regra de documento aguarda dados");
        resultado.RegrasComConsequenciaAdiada.Should().Contain(regraQueAguardaDados.Id);
        resultado.ConsequenciasVigentes.Should().ContainSingle(c => c.RegraDeComprovacaoId == regraComDados.Id && c.Consequencia == "REMOVE_VANTAGEM");
        resultado.ConsequenciasVigentes.Should().NotContain(c => c.RegraDeComprovacaoId == regraQueAguardaDados.Id, "a regra que aguarda dados não aplica consequência");
    }

    [Fact(DisplayName = "Grupo com quantidade mínima adia a consequência quando as regras decisivas aguardam dados")]
    public void GrupoComRegrasDecisivasQueAguardamDados_AdiaConsequencia()
    {
        DocumentoExigido bloqueado = DocumentoGeral("ELIMINA");
        (RegraDeComprovacao regraSemGrupo, RegraDeComprovacao grupoDeTodos) = CriarGrupoAlternativoComCondicionais(bloqueado);
        ResultadoDaComprovacao resultado = Resolver(
            Regras(regraSemGrupo), fatos: Fatos(("CONDICAO", false)), bloqueadas: new HashSet<Guid> { bloqueado.Id }).Value!;

        VerificarConsequenciaAdiada(resultado, regraSemGrupo, grupoDeTodos);
    }

    [Fact(DisplayName = "Grupo aplica a consequência quando a regra decisiva pode ser avaliada")]
    public void GrupoComRegraDecisivaAvaliada_Emite()
    {
        DocumentoExigido alcancavel = DocumentoGeral("ELIMINA");
        DocumentoExigido naoAplicavelDentro = DocumentoCondicional("CONDICAO");
        DocumentoExigido naoAplicavelFora = DocumentoCondicional("CONDICAO");
        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(alcancavel, 0).Value!;
        RegraDeComprovacao regraNaoAplicavelDentro = RegraDeComprovacao.CriarRegraDeDocumento(naoAplicavelDentro, 1).Value!;
        RegraDeComprovacao grupoDeTodos = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 0, null, null, [], [regraDeDocumento, regraNaoAplicavelDentro]).Value!;
        RegraDeComprovacao regraNaoAplicavelFora = RegraDeComprovacao.CriarRegraDeDocumento(naoAplicavelFora, 1).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 1, "RECLASSIFICA_AC", [], [grupoDeTodos, regraNaoAplicavelFora]).Value!;
        ResultadoDaComprovacao resultado = Resolver(Regras(regraSemGrupo), fatos: Fatos(("CONDICAO", false))).Value!;

        resultado.RegrasComConsequenciaAdiada.Should().BeEmpty();
        resultado.ConsequenciasVigentes.Should().ContainSingle(c => c.RegraDeComprovacaoId == regraSemGrupo.Id && c.Consequencia == "RECLASSIFICA_AC");
    }

    [Fact(DisplayName = "Grupo sem possibilidade de cumprimento aplica consequência quando não aguarda dados")]
    public void GrupoSemPossibilidadeDeCumprimento_AplicaConsequencia()
    {
        DocumentoExigido satisfeito = DocumentoGeral(null);
        DocumentoExigido naoAplicavel = DocumentoCondicional("CONDICAO");
        RegraDeComprovacao regraCumprida = RegraDeComprovacao.CriarRegraDeDocumento(satisfeito, 0).Value!;
        RegraDeComprovacao regraNaoAplicavel = RegraDeComprovacao.CriarRegraDeDocumento(naoAplicavel, 1).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 2, "ELIMINA", [], [regraCumprida, regraNaoAplicavel]).Value!;

        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresenta = ApresentacaoDe(satisfeito);
        ResultadoDaComprovacao resultado = Resolver(Regras(regraSemGrupo), fatos: Fatos(("CONDICAO", false)), apresentacoes: apresenta).Value!;

        VerificarImpossivelComConsequencia(resultado, regraSemGrupo);
    }

    [Fact(DisplayName = "Consequência adiada é aplicada quando os dados chegam")]
    public void DadosRecebidos_PermitemReavaliar()
    {
        DocumentoExigido documento = DocumentoGeral("ELIMINA");
        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 0, null, null, [], [regraDeDocumento]).Value!;
        RegrasDeComprovacaoDaVersao regras = Regras(regraSemGrupo);

        ResultadoDaComprovacao bloqueado = Resolver(regras, bloqueadas: new HashSet<Guid> { documento.Id }).Value!;
        bloqueado.RegrasComConsequenciaAdiada.Should().Contain(regraDeDocumento.Id);
        bloqueado.ConsequenciasVigentes.Should().BeEmpty("bloqueada, não emite");

        ResultadoDaComprovacao desbloqueado = Resolver(regras).Value!;
        desbloqueado.RegrasComConsequenciaAdiada.Should().BeEmpty("os dados recebidos permitem reavaliar a consequência");
        desbloqueado.ConsequenciasVigentes.Should().ContainSingle(c => c.RegraDeComprovacaoId == regraDeDocumento.Id && c.Consequencia == "ELIMINA");
    }

    [Fact(DisplayName = "Grupo sem possibilidade de cumprimento aplica consequência e mantém adiada a da regra que aguarda dados")]
    public void RegraQueAguardaDadosSobGrupoImpossivel_ContinuaAdiada()
    {
        DocumentoExigido bloqueado = DocumentoGeral("ELIMINA");
        DocumentoExigido satisfeito = DocumentoGeral(null);
        DocumentoExigido naoAplicavel = DocumentoCondicional("CONDICAO");
        RegraDeComprovacao regraQueAguardaDados = RegraDeComprovacao.CriarRegraDeDocumento(bloqueado, 0).Value!;
        RegraDeComprovacao regraCumprida = RegraDeComprovacao.CriarRegraDeDocumento(satisfeito, 0).Value!;
        RegraDeComprovacao regraNaoAplicavel = RegraDeComprovacao.CriarRegraDeDocumento(naoAplicavel, 1).Value!;
        RegraDeComprovacao grupoInterno = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, 2, "RECLASSIFICA_AC", [], [regraCumprida, regraNaoAplicavel]).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 0, null, null, [], [regraQueAguardaDados, grupoInterno]).Value!;

        ResultadoDaComprovacao resultado = ResolverComDocumentoBloqueado(regraSemGrupo, satisfeito, bloqueado);

        VerificarBloqueioSobGrupoImpossivel(resultado, regraSemGrupo, regraQueAguardaDados, grupoInterno);
    }

    [Fact(DisplayName = "Grupo sem possibilidade de cumprimento não orienta item que aguarda dados")]
    public void GrupoSemPossibilidadeDeCumprimento_NaoOrientaItemQueAguardaDados()
    {
        DocumentoExigido satisfeito = DocumentoGeral(null);
        DocumentoExigido naoAplicavel = DocumentoCondicional("CONDICAO");
        DocumentoExigido bloqueado = DocumentoGeral(null);
        RegraDeComprovacao regraCumprida = RegraDeComprovacao.CriarRegraDeDocumento(satisfeito, 0).Value!;
        RegraDeComprovacao regraNaoAplicavel = RegraDeComprovacao.CriarRegraDeDocumento(naoAplicavel, 1).Value!;
        RegraDeComprovacao regraQueAguardaDados = RegraDeComprovacao.CriarRegraDeDocumento(bloqueado, 2).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 3, "ELIMINA", [], [regraCumprida, regraNaoAplicavel, regraQueAguardaDados]).Value!;

        ResultadoDaComprovacao resultado = ResolverComDocumentoBloqueado(regraSemGrupo, satisfeito, bloqueado);

        VerificarOrientacaoBloqueada(resultado, regraSemGrupo, regraQueAguardaDados);
    }

    [Fact(DisplayName = "Regra repetida aguarda dados em cada ocorrência sem aplicar consequência")]
    public void RepeticaoPorMembro_RegraQueAguardaDados_NaoAplicaConsequencia()
    {
        DocumentoExigido bloqueado = DocumentoGeral("ELIMINA");
        DocumentoExigido aberto = DocumentoGeral("REMOVE_VANTAGEM");
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.TodosOsItens, 0, null, null, [],
            [RegraDeComprovacao.CriarRegraDeDocumento(bloqueado, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(aberto, 1).Value!],
            grupoDoFormularioParaRepeticao: "FAMILIA").Value!;
        Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> membros = MembrosDaFamilia();

        ResultadoDaComprovacao resultado = AvaliadorDaComprovacao.Resolver(
            Regras(regraSemGrupo), new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), SemApresentacoes, membros,
            new HashSet<Guid> { bloqueado.Id }).Value!;

        VerificarOcorrencias(resultado, bloqueado);
    }

    [Fact(DisplayName = "Sem regras aguardando dados, a avaliação aplica as consequências previstas")]
    public void SemBloqueio_ComportamentoInalterado()
    {
        DocumentoExigido a = DocumentoGeral("ELIMINA");
        DocumentoExigido b = DocumentoGeral("REMOVE_VANTAGEM");
        RegraDeComprovacao primeiraRegra = RegraDeComprovacao.CriarRegraDeDocumento(a, 0).Value!;
        RegraDeComprovacao segundaRegra = RegraDeComprovacao.CriarRegraDeDocumento(b, 1).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 0, null, null, [], [primeiraRegra, segundaRegra]).Value!;

        ResultadoDaComprovacao resultado = Resolver(Regras(regraSemGrupo)).Value!;

        resultado.RegrasComConsequenciaAdiada.Should().BeEmpty();
        resultado.ConsequenciasVigentes.Should().HaveCount(2, "os dois itens pendentes aplicam suas consequências");
    }
    private static void VerificarConsequenciaAdiada(ResultadoDaComprovacao resultado, RegraDeComprovacao regraSemGrupo, RegraDeComprovacao grupoDeTodos)
    {
        resultado.RegrasComConsequenciaAdiada.Should().Contain(regraSemGrupo.Id, "todas as regras decisivas do grupo aguardam dados");
        resultado.RegrasComConsequenciaAdiada.Should().Contain(grupoDeTodos.Id);
        resultado.ConsequenciasVigentes.Should().BeEmpty("o grupo adia a própria consequência enquanto aguarda dados");
    }

    private static void VerificarImpossivelComConsequencia(ResultadoDaComprovacao resultado, RegraDeComprovacao regraSemGrupo)
    {
        resultado.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.SemPossibilidadeDeCumprimento, "uma regra cumprida não alcança o mínimo de duas regras");
        resultado.RegrasComConsequenciaAdiada.Should().BeEmpty("IMPOSSIVEL decidido emite o inevitável, nunca aguarda dados");
        resultado.ConsequenciasVigentes.Should().ContainSingle(c => c.RegraDeComprovacaoId == regraSemGrupo.Id && c.Consequencia == "ELIMINA");
    }

    private static void VerificarBloqueioSobGrupoImpossivel(
        ResultadoDaComprovacao resultado, RegraDeComprovacao regraSemGrupo, RegraDeComprovacao regraQueAguardaDados, RegraDeComprovacao grupoInterno)
    {
        resultado.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.SemPossibilidadeDeCumprimento);
        resultado.RegrasComConsequenciaAdiada.Should().Contain(regraQueAguardaDados.Id, "a regra que aguarda dados continua com consequência adiada");
        resultado.ConsequenciasVigentes.Should().NotContain(c => c.RegraDeComprovacaoId == regraQueAguardaDados.Id, "a regra que aguarda dados não aplica a eliminação");
        resultado.ConsequenciasVigentes.Should().Contain(c => c.RegraDeComprovacaoId == grupoInterno.Id && c.Consequencia == "RECLASSIFICA_AC", "o grupo sem possibilidade de cumprimento aplica a consequência");
    }

    private static void VerificarOrientacaoBloqueada(ResultadoDaComprovacao resultado, RegraDeComprovacao regraSemGrupo, RegraDeComprovacao regraQueAguardaDados)
    {
        resultado.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.SemPossibilidadeDeCumprimento);
        resultado.RegrasComConsequenciaAdiada.Should().Contain(regraQueAguardaDados.Id);
        resultado.PendenciasDeOrientacao.Should().NotContain(p => p.RegraDeComprovacaoId == regraQueAguardaDados.Id, "a regra de documento bloqueada não gera orientação");
        resultado.ConsequenciasVigentes.Should().Contain(c => c.RegraDeComprovacaoId == regraSemGrupo.Id && c.Consequencia == "ELIMINA");
    }

    private static void VerificarOcorrencias(ResultadoDaComprovacao resultado, DocumentoExigido bloqueado)
    {
        resultado.SituacoesDeExigenciaPorOcorrencia.Where(s => s.DocumentoExigidoId == bloqueado.Id)
            .Should().OnlyContain(static s => s.Situacao == StatusResolucaoExigencia.AguardandoDados);
        resultado.ConsequenciasVigentes.Should().NotContain(static c => c.Consequencia == "ELIMINA", "a regra que aguarda dados não aplica consequência em nenhuma ocorrência");
        resultado.ConsequenciasVigentes.Where(static c => c.Consequencia == "REMOVE_VANTAGEM").Select(static c => c.OcorrenciaId)
            .Should().Equal("m1", "m2");
    }

    private static (RegraDeComprovacao RegraSemGrupo, RegraDeComprovacao Grupo) CriarGrupoAlternativoComCondicionais(DocumentoExigido documento)
    {
        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0).Value!;
        RegraDeComprovacao condicionalDentro = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoCondicional("CONDICAO"), 1).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 0, null, null, [], [regraDeDocumento, condicionalDentro]).Value!;
        RegraDeComprovacao condicionalFora = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoCondicional("CONDICAO"), 1).Value!;
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 1, "RECLASSIFICA_AC", [], [grupo, condicionalFora]).Value!;
        return (regraSemGrupo, grupo);
    }

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> ApresentacaoDe(DocumentoExigido documento) =>
        new() { [documento.Id] = [new ApresentacaoDocumento(Guid.CreateVersion7())] };

    private static Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> MembrosDaFamilia() =>
        new(StringComparer.Ordinal)
        {
            ["FAMILIA"] =
            [
                new OcorrenciaDoGrupo("m1", new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)),
                new OcorrenciaDoGrupo("m2", new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)),
            ],
        };

    private static ResultadoDaComprovacao ResolverComDocumentoBloqueado(
        RegraDeComprovacao regraSemGrupo, DocumentoExigido satisfeito, DocumentoExigido bloqueado) =>
        Resolver(Regras(regraSemGrupo), fatos: Fatos(("CONDICAO", false)), apresentacoes: ApresentacaoDe(satisfeito),
            bloqueadas: new HashSet<Guid> { bloqueado.Id }).Value!;

}
