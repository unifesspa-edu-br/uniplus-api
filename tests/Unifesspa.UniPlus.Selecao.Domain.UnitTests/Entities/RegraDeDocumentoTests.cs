namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RegraDeDocumentoTests
{
    private static DocumentoExigido Documento(
        Guid? faseId = null, string? consequencia = null, bool obrigatorio = true, Aplicabilidade aplicabilidade = Aplicabilidade.Geral,
        FinalidadeFormulario? finalidade = null) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId ?? Guid.CreateVersion7(), null, FormularioDeTeste.CodigoDoPapel(finalidade)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(aplicabilidade, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(obrigatorio, consequencia, []))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    private static BaseLegalDaComprovacao BaseLegalResolvida() =>
        BaseLegalDaComprovacao.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;

    [Fact(DisplayName = "CriarRegraDeDocumento aceita um DocumentoExigido válido")]
    public void CriarRegraDeDocumento_DocumentoValido_Aceita()
    {
        DocumentoExigido documento = Documento();

        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(documento, ordem: 0);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Forma.Should().Be(FormaDeComprovacao.Documento);
        resultado.Value!.DocumentoExigidoId.Should().Be(documento.Id);
        resultado.Value!.DocumentoExigido.Should().BeSameAs(documento);
        resultado.Value!.Itens.Should().BeEmpty();
    }

    [Fact(DisplayName = "CriarRegraDeDocumento recusa ordem negativa")]
    public void CriarRegraDeDocumento_OrdemNegativa_Recusa()
    {
        Action criar = () => RegraDeComprovacao.CriarRegraDeDocumento(Documento(), ordem: -1);

        criar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CriarRegraDeDocumento_DistincaoComDuasFalhas_AcumulaRecusas()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), 0, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.CompetenciaMensal,
            ocorrenciasEsperadas: ["OUTRA"]);

        resultado.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void CriarRegraDeDocumento_QuantidadeEDistincaoInvalidas_AcumulaRecusas()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), 0, 0, CriterioDeDistincaoDasApresentacoes.CompetenciaMensal, ocorrenciasEsperadas: ["OUTRA"]);

        resultado.Errors.Select(static erro => erro.Error.Code).Should().Contain(
            ["RegraDeComprovacao.QuantidadeMinimaDeApresentacoesInvalida", "RegraDeComprovacao.DataReferenciaObrigatoriaParaCalendario",
                "RegraDeComprovacao.OcorrenciasEsperadasIndevidasParaCriterio"]);
    }

    [Fact(DisplayName = "CriarRegraDeDocumento recusa quantidadeMinima menor que 1")]
    public void CriarRegraDeDocumento_QuantidadeMinimaMenorQueUm_Recusa()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), ordem: 0, quantidadeMinima: 0);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.QuantidadeMinimaDeApresentacoesInvalida");
        resultado.Error.Message.Should().Be("A quantidade mínima de apresentações deste documento está fora dos limites. Informe um valor entre 1 e 366.");
    }

    [Fact(DisplayName = "CriarRegraDeDocumento sem criterioDeDistincao recusa dataReferencia")]
    public void CriarRegraDeDocumento_SemChave_ComDataReferencia_Recusa()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, dataReferencia: new DateOnly(2026, 1, 1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.DataReferenciaIndevidaParaCriterio");
    }

    [Fact(DisplayName = "CriarRegraDeDocumento sem criterioDeDistincao recusa ocorrenciasEsperadas")]
    public void CriarRegraDeDocumento_SemChave_ComOcorrenciasEsperadas_Recusa()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, ocorrenciasEsperadas: ["a"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.OcorrenciasEsperadasIndevidasParaCriterio");
    }

    [Theory(DisplayName = "CriarRegraDeDocumento com chave de calendário exige dataReferencia")]
    [InlineData(CriterioDeDistincaoDasApresentacoes.CompetenciaMensal)]
    [InlineData(CriterioDeDistincaoDasApresentacoes.ExercicioAnual)]
    public void CriarRegraDeDocumento_ChaveDeCalendario_SemDataReferencia_Recusa(CriterioDeDistincaoDasApresentacoes chave)
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), ordem: 0, criterioDeDistincao: chave);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.DataReferenciaObrigatoriaParaCalendario");
        resultado.Error.Message.Should().Be("Este documento é distinguido por mês ou ano e precisa de uma data de referência. Informe a data.");
    }

    [Fact(DisplayName = "CriarRegraDeDocumento com chave de calendário recusa ocorrenciasEsperadas")]
    public void CriarRegraDeDocumento_ChaveDeCalendario_ComOcorrenciasEsperadas_Recusa()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.CompetenciaMensal,
            dataReferencia: new DateOnly(2026, 1, 1),
            ocorrenciasEsperadas: ["a"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.OcorrenciasEsperadasIndevidasParaCriterio");
    }

    [Fact(DisplayName = "CriarRegraDeDocumento com CriterioDeDistincaoDasApresentacoes.Ocorrencia recusa dataReferencia")]
    public void CriarRegraDeDocumento_Ocorrencia_ComDataReferencia_Recusa()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia, dataReferencia: new DateOnly(2026, 1, 1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.DataReferenciaIndevidaParaCriterio");
    }

    [Fact(DisplayName = "CriarRegraDeDocumento com CriterioDeDistincaoDasApresentacoes.Ocorrencia recusa ocorrenciasEsperadas vazia")]
    public void CriarRegraDeDocumento_Ocorrencia_ListaVazia_Recusa()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia, ocorrenciasEsperadas: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.OcorrenciasEsperadasVazias");
        resultado.Error.Message.Should().Be("A lista de ocorrências esperadas está vazia. Informe ao menos uma ocorrência ou deixe a lista ausente.");
    }

    [Theory(DisplayName = "CriarRegraDeDocumento com CriterioDeDistincaoDasApresentacoes.Ocorrencia recusa id vazio ou em branco")]
    [InlineData("")]
    [InlineData("   ")]
    public void CriarRegraDeDocumento_Ocorrencia_IdVazioOuEmBranco_Recusa(string idInvalido)
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, quantidadeMinima: 2,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia, ocorrenciasEsperadas: ["a", idInvalido]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.OcorrenciaSemIdentificador");
        resultado.Error.Message.Should().Be("Uma ocorrência esperada não tem identificador. Informe um identificador para cada ocorrência.");
    }

    [Fact(DisplayName = "CriarRegraDeDocumento com CriterioDeDistincaoDasApresentacoes.Ocorrencia recusa ids duplicados")]
    public void CriarRegraDeDocumento_Ocorrencia_IdsDuplicados_Recusa()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, quantidadeMinima: 2,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia, ocorrenciasEsperadas: ["a", "a"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.OcorrenciaComIdentificadorRepetido");
        resultado.Error.Message.Should().Be("Há ocorrências esperadas com o mesmo identificador. Informe identificadores distintos.");
    }

    [Fact(DisplayName = "CriarRegraDeDocumento com CriterioDeDistincaoDasApresentacoes.Ocorrencia recusa quantidadeMinima divergente do tamanho da lista")]
    public void CriarRegraDeDocumento_Ocorrencia_QuantidadeMinimaDivergente_Recusa()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, quantidadeMinima: 3,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia, ocorrenciasEsperadas: ["a", "b"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.QuantidadeMinimaDiferenteDasOcorrencias");
    }

    [Fact(DisplayName = "CriarRegraDeDocumento com CriterioDeDistincaoDasApresentacoes.Ocorrencia sem lista é modo distinção pura — aceita")]
    public void CriarRegraDeDocumento_Ocorrencia_SemLista_Aceita()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, quantidadeMinima: 2, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia);

        resultado.IsSuccess.Should().BeTrue();
        (resultado.Value!.Distincao?.IdentificacoesDasApresentacoesExigidas(resultado.Value.QuantidadeMinima!.Value)).Should().BeNull();
    }

    [Fact(DisplayName = "IdentificacoesDasApresentacoesExigidas para CompetenciaMensal deriva as N competências regulares mais recentes")]
    public void IdentificacoesDasApresentacoesExigidas_CompetenciaMensal_DerivaAsMaisRecentes()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, quantidadeMinima: 3,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.CompetenciaMensal, dataReferencia: new DateOnly(2026, 3, 15));

        (resultado.Value!.Distincao?.IdentificacoesDasApresentacoesExigidas(resultado.Value.QuantidadeMinima!.Value)).Should().Equal("2026-03", "2026-02", "2026-01");
    }

    [Fact(DisplayName = "IdentificacoesDasApresentacoesExigidas para ExercicioAnual deriva os N exercícios regulares mais recentes")]
    public void IdentificacoesDasApresentacoesExigidas_ExercicioAnual_DerivaOsMaisRecentes()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, quantidadeMinima: 3,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.ExercicioAnual, dataReferencia: new DateOnly(2026, 3, 15));

        (resultado.Value!.Distincao?.IdentificacoesDasApresentacoesExigidas(resultado.Value.QuantidadeMinima!.Value)).Should().Equal("2026", "2025", "2024");
    }

    [Fact(DisplayName = "IdentificacoesDasApresentacoesExigidas sem criterioDeDistincao é nulo — contagem bruta")]
    public void IdentificacoesDasApresentacoesExigidas_SemChave_Nulo()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), ordem: 0);

        (resultado.Value!.Distincao?.IdentificacoesDasApresentacoesExigidas(resultado.Value.QuantidadeMinima!.Value)).Should().BeNull();
    }

    [Fact(DisplayName = "IdentificacoesDasApresentacoesExigidas para CompetenciaMensal na data de referência mínima representável (ano 1) não lança e não excede o calendário")]
    public void IdentificacoesDasApresentacoesExigidas_CompetenciaMensal_DataDeReferenciaMinima_NaoLanca()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, quantidadeMinima: 1,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.CompetenciaMensal, dataReferencia: DateOnly.MinValue);

        resultado.IsSuccess.Should().BeTrue();
        (resultado.Value!.Distincao?.IdentificacoesDasApresentacoesExigidas(resultado.Value.QuantidadeMinima!.Value)).Should().Equal("0001-01");
    }

    [Fact(DisplayName = "CriarRegraDeDocumento com CompetenciaMensal recusa quantidadeMinima que excede o calendário representável")]
    public void CriarRegraDeDocumento_CompetenciaMensal_QuantidadeExcedeCalendario_Recusa()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, quantidadeMinima: 2,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.CompetenciaMensal, dataReferencia: DateOnly.MinValue);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.QuantidadeMinimaExcedeCalendario");
    }

    [Fact(DisplayName = "CriarRegraDeDocumento com ExercicioAnual recusa quantidadeMinima que excede o calendário representável")]
    public void CriarRegraDeDocumento_ExercicioAnual_QuantidadeExcedeCalendario_Recusa()
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, quantidadeMinima: 2,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.ExercicioAnual, dataReferencia: new DateOnly(1, 6, 1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.QuantidadeMinimaExcedeCalendario");
    }

    [Theory(DisplayName = "CriarRegraDeDocumento recusa quantidadeMinima fora do teto operacional")]
    [InlineData(0)]
    [InlineData(367)]
    public void CriarRegraDeDocumento_QuantidadeMinimaForaDoTeto_Recusa(int quantidadeInvalida)
    {
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(Documento(), ordem: 0, quantidadeMinima: quantidadeInvalida);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.QuantidadeMinimaDeApresentacoesInvalida");
        resultado.Error.Message.Should().Be("A quantidade mínima de apresentações deste documento está fora dos limites. Informe um valor entre 1 e 366.");
    }

    [Fact(DisplayName = "CriarRegraDeDocumento copia ocorrenciasEsperadas defensivamente — alteração da lista original não afeta a regra")]
    public void CriarRegraDeDocumento_Ocorrencia_ListaOriginalMutada_NaoAfetaNo()
    {
        List<string> listaOriginal = ["a", "b"];
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(
            Documento(), ordem: 0, quantidadeMinima: 2,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia, ocorrenciasEsperadas: listaOriginal);

        listaOriginal.Add("c");

        resultado.Value!.Distincao!.OcorrenciasEsperadas.Should().Equal("a", "b");
    }

}
