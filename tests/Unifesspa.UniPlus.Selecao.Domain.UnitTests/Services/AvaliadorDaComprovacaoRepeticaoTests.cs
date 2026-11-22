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

using static RegrasDeComprovacaoDeTeste;

public sealed class AvaliadorDaComprovacaoRepeticaoTests
{
    [Fact]
    public void Resolver_RegraReidratadaSemDocumento_RecusaConfiguracaoInvalida()
    {
        RegraDeComprovacao regra = RegraDeComprovacao.ReidratarGrupo(
            Guid.CreateVersion7(), 0, new CombinacaoDoGrupo(FormaDeComprovacao.Documento, null, null), [], [], null);

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regra));

        resultado.Error!.Code.Should().Be("AvaliadorDaComprovacao.RegraSemDocumento");
    }

    [Fact(DisplayName = "RG e CPF são pedidos de cada membro, e a certidão de guarda só do membro sob guarda")]
    public void RepeticaoPorGrupo_DocumentosPorMembroECertidaoDoMenorSobGuarda()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        DocumentoExigido cpf = DocumentoGeral("ELIMINA");
        DocumentoExigido certidaoDeGuarda = DocumentoCondicional("SOB_GUARDA", "ELIMINA");
        RegraDeComprovacao regraSemGrupo = GrupoFamiliar(rg, cpf, certidaoDeGuarda);

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo),
            ocorrencias: OcorrenciasDe(
                "COMPOSICAO_FAMILIAR", Ocorrencia("membro_1", ("SOB_GUARDA", false)), Ocorrencia("membro_2", ("SOB_GUARDA", true))));

        ConferirDocumentosPendentesDosMembros(resultado.Value!, rg, cpf, certidaoDeGuarda);
    }

    [Fact(DisplayName = "Atributo não aplicável da ocorrência prevalece sobre o fato do candidato")]
    public void RepeticaoPorGrupo_AtributoNaoAplicavelSobrescreveFatoDoCandidato()
    {
        DocumentoExigido condicional = DocumentoCondicional("SEM_RENDA");
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(condicional, 0, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;

        // O candidato declarou o fato, mas para ESTE membro da composição familiar ele não se
        // aplica — o sujeito do gatilho é a ocorrência, não o candidato.
        Dictionary<string, FatoResolvido> fatosDoCandidato = Fatos("SEM_RENDA", true);
        OcorrenciaDoGrupo membro = new("membro_1", new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["SEM_RENDA"] = FatoResolvido.NaoAplicavel() });

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), fatosDoCandidato, ocorrencias: OcorrenciasDe("COMPOSICAO_FAMILIAR", membro));

        resultado.Value!.SituacoesDeExigenciaPorOcorrencia.Should().Contain(
            s => s.DocumentoExigidoId == condicional.Id && s.OcorrenciaId == "membro_1" && s.Situacao == StatusResolucaoExigencia.NaoExigida);
    }

    [Fact(DisplayName = "Documento associado à ocorrência correspondente: RG do membro 2 satisfaz só (RG, COMPOSICAO_FAMILIAR, membro_2)")]
    public void RepeticaoPorGrupo_DocumentoAssociadoAOcorrenciaCorrespondente()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(rg, 0, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;

        Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> ocorrencias = OcorrenciasDe(
            "COMPOSICAO_FAMILIAR", Ocorrencia("membro_1"), Ocorrencia("membro_2"), Ocorrencia("membro_3"));

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), apresentacoes: ApresentacoesDaOcorrencia(rg, "membro_2"), ocorrencias: ocorrencias);

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
        resultado.Value.ConsequenciasVigentes.Select(static c => (c.OcorrenciaId, c.Consequencia))
            .Should().BeEquivalentTo([("membro_1", "ELIMINA"), ("membro_3", "ELIMINA")]);
        ConferirStatusDosMembros(resultado.Value, rg.Id, "membro_2", ["membro_1", "membro_3"]);
    }

    [Fact(DisplayName = "Regra de documento sem consequência permanece visível em cada ocorrência")]
    public void RepeticaoPorGrupo_SemConsequencia_SituacoesDeExigenciaPorOcorrenciaSinalizaOcorrenciaPendente()
    {
        DocumentoExigido comprovante = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(comprovante, 0, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;

        Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> ocorrencias = OcorrenciasDe(
            "COMPOSICAO_FAMILIAR", Ocorrencia("membro_1"), Ocorrencia("membro_2"));

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(regraSemGrupo), apresentacoes: ApresentacoesDaOcorrencia(comprovante, "membro_1"), ocorrencias: ocorrencias);

        // Sem consequência configurada, nada é emitido em ConsequenciasVigentes — o único
        // sinal de QUAL ocorrência está pendente é SituacoesDeExigenciaPorOcorrencia.
        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
        ConferirStatusDosMembros(resultado.Value!, comprovante.Id, "membro_1", ["membro_2"]);
    }

    [Fact(DisplayName = "Gatilho por atributo da ocorrência: declaração de isento só é exigida do membro adulto e sem renda")]
    public void RepeticaoPorGrupo_GatilhoPorAtributoDaEntidade()
    {
        DocumentoExigido declaracaoIsento = DeclaracaoDeIsento();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(declaracaoIsento, 0, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;

        Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> ocorrencias = OcorrenciasDe(
            "COMPOSICAO_FAMILIAR",
            Ocorrencia("membro_adulto_sem_renda", ("MAIOR_IDADE", true), ("SEM_RENDA", true)),
            Ocorrencia("membro_adulto_com_renda", ("MAIOR_IDADE", true), ("SEM_RENDA", false)),
            Ocorrencia("membro_menor", ("MAIOR_IDADE", false), ("SEM_RENDA", true)));

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), ocorrencias: ocorrencias);

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.OcorrenciaId == "membro_adulto_sem_renda" && c.Consequencia == "ELIMINA");
    }

    [Fact(DisplayName = "PF + PJ vinculada repete por empresa: extrato da PJ 1 satisfaz só essa PJ — PJ 2 continua com extrato e IRPJ pendentes")]
    public void RepeticaoPorGrupo_PessoaJuridicaVinculada_CorrelacaoPorPj()
    {
        DocumentoExigido extrato = DocumentoGeral("ELIMINA");
        DocumentoExigido irpj = DocumentoGeral("ELIMINA");
        RegraDeComprovacao regraDeExtrato = RegraDeComprovacao.CriarRegraDeDocumento(extrato, 0).Value!;
        RegraDeComprovacao regraDeImposto = RegraDeComprovacao.CriarRegraDeDocumento(irpj, 1).Value!;
        RegraDeComprovacao grupo = GrupoJuridico(regraDeExtrato, regraDeImposto);

        Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> ocorrencias = OcorrenciasDe(
            "PESSOAS_JURIDICAS", Ocorrencia("pj_1"), Ocorrencia("pj_2"));

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(grupo), apresentacoes: ApresentacoesDaOcorrencia(extrato, "pj_1"), ocorrencias: ocorrencias);

        resultado.Value!.SituacoesPorRegra[grupo.Id].Should().Be(SituacaoDaComprovacao.PendenteDeComprovacao);
        resultado.Value.ConsequenciasVigentes.Select(static c => (c.OcorrenciaId, c.RegraDeComprovacaoId)).Should().BeEquivalentTo([("pj_1", regraDeImposto.Id), ("pj_2", regraDeExtrato.Id), ("pj_2", regraDeImposto.Id)]);
    }

    [Fact(DisplayName = "Repetição sem ocorrências declaradas não é exigida")]
    public void RepeticaoPorGrupo_SemOcorrenciasDeclaradas_NaoAplicavel()
    {
        DocumentoExigido documento = DocumentoGeral("ELIMINA");
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo));

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.NaoExigida);
        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Repetição com todas as ocorrências cumpridas não aplica consequências")]
    public void RepeticaoPorGrupo_TodasSatisfeitas_Suprime()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(rg, 0, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;

        Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> ocorrencias = OcorrenciasDe(
            "COMPOSICAO_FAMILIAR", Ocorrencia("membro_1"), Ocorrencia("membro_2"));

        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = ApresentacoesDosMembros(rg, "membro_1", "membro_2");

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), apresentacoes: apresentacoes, ocorrencias: ocorrencias);

        resultado.Value!.SituacoesPorRegra[regraSemGrupo.Id].Should().Be(SituacaoDaComprovacao.Cumprida);
        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Orientações do grupo repetido identificam a ocorrência correspondente")]
    public void RepeticaoPorGrupo_GrupoComQuantidadeMinima_PendenciasDeOrientacaoComOcorrenciaId()
    {
        DocumentoExigido primeiroDocumento = DocumentoGeral();
        DocumentoExigido segundoDocumento = DocumentoGeral();
        RegraDeComprovacao primeiraRegra = RegraDeComprovacao.CriarRegraDeDocumento(primeiroDocumento, 0).Value!;
        RegraDeComprovacao segundaRegra = RegraDeComprovacao.CriarRegraDeDocumento(segundoDocumento, 1).Value!;
        RegraDeComprovacao grupo = GrupoDeQuantidadeMinimaJuridico(primeiraRegra, segundaRegra);

        Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> ocorrencias = OcorrenciasDe(
            "PESSOAS_JURIDICAS", Ocorrencia("pj_1"), Ocorrencia("pj_2"));

        Result<ResultadoDaComprovacao> resultado = Resolver(
            Regras(grupo), apresentacoes: ApresentacoesDaOcorrencia(primeiroDocumento, "pj_1"), ocorrencias: ocorrencias);

        ConferirOrientacoesDoGrupo(resultado.Value!, grupo, primeiraRegra, segundaRegra);
    }

    [Fact(DisplayName = "Regras reidratadas com repetição dentro de repetição são recusadas")]
    public void RepeticaoPorGrupo_AninhadaViaReidratar_Recusa()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraInterna = RegraDeComprovacao.ReidratarRegraDeDocumento(
            Guid.CreateVersion7(), 0, documento, null, null, "PESSOAS_JURIDICAS");
        RegraDeComprovacao grupoExterno = RegraDeComprovacao.ReidratarGrupo(
            Guid.CreateVersion7(), 0, new CombinacaoDoGrupo(FormaDeComprovacao.TodosOsItens, null, null), [], [regraInterna], "COMPOSICAO_FAMILIAR");

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(grupoExterno));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.RepeticaoDentroDeRepeticao");
    }

    [Fact(DisplayName = "Ocorrências com identificador repetido são recusadas")]
    public void RepeticaoPorGrupo_OcorrenciasComIdentificadorRepetido_Recusa()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;
        Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> ocorrencias = OcorrenciasDe(
            "COMPOSICAO_FAMILIAR", Ocorrencia("membro_1"), Ocorrencia("membro_1"));

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), ocorrencias: ocorrencias);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("AvaliadorDaComprovacao.OcorrenciaDoGrupoInvalida");
    }

    [Fact(DisplayName = "Ocorrência sem identificador é recusada")]
    public void RepeticaoPorGrupo_OcorrenciaSemIdentificador_Recusa()
    {
        DocumentoExigido documento = DocumentoGeral();
        RegraDeComprovacao regraSemGrupo = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;
        Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> ocorrencias = OcorrenciasDe(
            "COMPOSICAO_FAMILIAR", Ocorrencia(""));

        Result<ResultadoDaComprovacao> resultado = Resolver(Regras(regraSemGrupo), ocorrencias: ocorrencias);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("AvaliadorDaComprovacao.OcorrenciaDoGrupoInvalida");
    }

    private static void ConferirDocumentosPendentesDosMembros(
        ResultadoDaComprovacao resultado, DocumentoExigido rg, DocumentoExigido cpf, DocumentoExigido certidao)
    {
        resultado.SituacoesDeExigenciaPorOcorrencia.Where(static s => s.Situacao == StatusResolucaoExigencia.PendenteDeComprovacao)
            .Select(static s => (s.DocumentoExigidoId, s.OcorrenciaId)).Should().BeEquivalentTo(
                [(rg.Id, "membro_1"), (rg.Id, "membro_2"), (cpf.Id, "membro_1"), (cpf.Id, "membro_2"), (certidao.Id, "membro_2")]);
    }
    private static void ConferirStatusDosMembros(ResultadoDaComprovacao resultado, Guid documentoId, string satisfeito, string[] pendentes)
    {
        resultado.SituacoesDeExigenciaPorOcorrencia.Select(static s => (s.DocumentoExigidoId, s.OcorrenciaId, s.Situacao)).Should().BeEquivalentTo(
            pendentes.Select(id => (documentoId, id, StatusResolucaoExigencia.PendenteDeComprovacao))
                .Append((documentoId, satisfeito, StatusResolucaoExigencia.Cumprida)));
    }
    private static DocumentoExigido DeclaracaoDeIsento() => DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "DECL_ISENTO", "Declaração de isento", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: false, "ELIMINA",
            [
                CondicaoGatilho.Criar(0, "MAIOR_IDADE", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
                CondicaoGatilho.Criar(0, "SEM_RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
            ],
            [], null, Qualquer, null).Value!;
    private static RegraDeComprovacao GrupoFamiliar(params DocumentoExigido[] documentos) => RegraDeComprovacao.CriarGrupo(
        FormaDeComprovacao.TodosOsItens, 0, null, null, [], [.. documentos.Select(static (d, i) => RegraDeComprovacao.CriarRegraDeDocumento(d, i).Value!)], "COMPOSICAO_FAMILIAR").Value!;

    private static RegraDeComprovacao GrupoJuridico(params RegraDeComprovacao[] itens) =>
        RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 0, null, null, [], itens, "PESSOAS_JURIDICAS").Value!;

    private static RegraDeComprovacao GrupoDeQuantidadeMinimaJuridico(params RegraDeComprovacao[] itens) =>
        RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 2, "ELIMINA", [BaseLegalResolvida()], itens, "PESSOAS_JURIDICAS").Value!;

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> ApresentacoesDosMembros(DocumentoExigido documento, params string[] membros) =>
        new() { [documento.Id] = [.. membros.Select(static membro => new ApresentacaoDocumento(Guid.CreateVersion7(), OcorrenciaId: membro))] };

    private static void ConferirOrientacoesDoGrupo(ResultadoDaComprovacao resultado, RegraDeComprovacao grupo, RegraDeComprovacao primeiraRegra, RegraDeComprovacao segundaRegra)
    {
        resultado.PendenciasDeOrientacao.Select(static p => (p.RegraDeComprovacaoId, p.OcorrenciaId)).Should()
            .BeEquivalentTo([(segundaRegra.Id, "pj_1"), (primeiraRegra.Id, "pj_2"), (segundaRegra.Id, "pj_2")]);
        resultado.ConsequenciasVigentes.Select(static c => (c.RegraDeComprovacaoId, c.OcorrenciaId)).Should()
            .BeEquivalentTo([(grupo.Id, "pj_1"), (grupo.Id, "pj_2")]);
    }
}
