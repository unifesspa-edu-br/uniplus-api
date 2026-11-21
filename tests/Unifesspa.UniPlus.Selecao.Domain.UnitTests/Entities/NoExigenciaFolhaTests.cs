namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class NoExigenciaFolhaTests
{
    private static DocumentoExigido Documento(
        Guid? faseId = null, string? consequencia = null, bool obrigatorio = true, Aplicabilidade aplicabilidade = Aplicabilidade.Geral,
        FinalidadeFormulario? finalidade = null) =>
        DocumentoExigido.Criar(
            faseId ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "IDENTIDADE",
            "Documento de identidade",
            "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade,
            obrigatorio,
            consequencia,
            [],
            [],
            null,
            FormatosPermitidos.Criar(true, null).Value!,
            null,
            finalidade: finalidade).Value!;

    private static BaseLegalDaComprovacao BaseLegalResolvida() =>
        BaseLegalDaComprovacao.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;

    [Fact(DisplayName = "CriarFolha aceita um DocumentoExigido válido")]
    public void CriarFolha_DocumentoValido_Aceita()
    {
        DocumentoExigido documento = Documento();

        Result<NoExigencia> resultado = NoExigencia.CriarFolha(documento, ordem: 0);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Tipo.Should().Be(TipoNo.Folha);
        resultado.Value!.DocumentoExigidoId.Should().Be(documento.Id);
        resultado.Value!.DocumentoExigido.Should().BeSameAs(documento);
        resultado.Value!.Filhos.Should().BeEmpty();
    }

    [Fact(DisplayName = "CriarFolha recusa ordem negativa")]
    public void CriarFolha_OrdemNegativa_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(Documento(), ordem: -1);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OrdemInvalida");
    }

    [Fact(DisplayName = "CriarFolha recusa quantidadeMinima menor que 1")]
    public void CriarFolha_QuantidadeMinimaMenorQueUm_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(Documento(), ordem: 0, quantidadeMinima: 0);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaDeFolhaInvalida");
        resultado.Error.Message.Should().Be("A quantidade mínima de apresentações deste documento está fora dos limites. Informe um valor entre 1 e 366.");
    }

    [Fact(DisplayName = "CriarFolha sem chaveDistincao recusa dataReferencia")]
    public void CriarFolha_SemChave_ComDataReferencia_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, dataReferencia: new DateOnly(2026, 1, 1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.DataReferenciaIndevidaParaChave");
    }

    [Fact(DisplayName = "CriarFolha sem chaveDistincao recusa ocorrenciasEsperadas")]
    public void CriarFolha_SemChave_ComOcorrenciasEsperadas_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, ocorrenciasEsperadas: ["a"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasIndevidasParaChave");
    }

    [Theory(DisplayName = "CriarFolha com chave de calendário exige dataReferencia")]
    [InlineData(ChaveDistincao.CompetenciaMensal)]
    [InlineData(ChaveDistincao.ExercicioAnual)]
    public void CriarFolha_ChaveDeCalendario_SemDataReferencia_Recusa(ChaveDistincao chave)
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(Documento(), ordem: 0, chaveDistincao: chave);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.DataReferenciaObrigatoriaParaChaveCalendario");
        resultado.Error.Message.Should().Be("Este documento é distinguido por mês ou ano e precisa de uma data de referência. Informe a data.");
    }

    [Fact(DisplayName = "CriarFolha com chave de calendário recusa ocorrenciasEsperadas")]
    public void CriarFolha_ChaveDeCalendario_ComOcorrenciasEsperadas_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0,
            chaveDistincao: ChaveDistincao.CompetenciaMensal,
            dataReferencia: new DateOnly(2026, 1, 1),
            ocorrenciasEsperadas: ["a"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasIndevidasParaChave");
    }

    [Fact(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia recusa dataReferencia")]
    public void CriarFolha_Ocorrencia_ComDataReferencia_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, chaveDistincao: ChaveDistincao.Ocorrencia, dataReferencia: new DateOnly(2026, 1, 1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.DataReferenciaIndevidaParaChave");
    }

    [Fact(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia recusa ocorrenciasEsperadas vazia")]
    public void CriarFolha_Ocorrencia_ListaVazia_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, chaveDistincao: ChaveDistincao.Ocorrencia, ocorrenciasEsperadas: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasVazia");
        resultado.Error.Message.Should().Be("A lista de ocorrências esperadas está vazia. Informe ao menos uma ocorrência ou deixe a lista ausente.");
    }

    [Theory(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia recusa id vazio ou em branco")]
    [InlineData("")]
    [InlineData("   ")]
    public void CriarFolha_Ocorrencia_IdVazioOuEmBranco_Recusa(string idInvalido)
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2,
            chaveDistincao: ChaveDistincao.Ocorrencia, ocorrenciasEsperadas: ["a", idInvalido]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasComIdVazio");
        resultado.Error.Message.Should().Be("Uma ocorrência esperada não tem identificador. Informe um identificador para cada ocorrência.");
    }

    [Fact(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia recusa ids duplicados")]
    public void CriarFolha_Ocorrencia_IdsDuplicados_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2,
            chaveDistincao: ChaveDistincao.Ocorrencia, ocorrenciasEsperadas: ["a", "a"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasComIdsDuplicados");
        resultado.Error.Message.Should().Be("Há ocorrências esperadas com o mesmo identificador. Informe identificadores distintos.");
    }

    [Fact(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia recusa quantidadeMinima divergente do tamanho da lista")]
    public void CriarFolha_Ocorrencia_QuantidadeMinimaDivergente_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 3,
            chaveDistincao: ChaveDistincao.Ocorrencia, ocorrenciasEsperadas: ["a", "b"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.OcorrenciasEsperadasQuantidadeMinimaDivergente");
    }

    [Fact(DisplayName = "CriarFolha com ChaveDistincao.Ocorrencia sem lista é modo distinção pura — aceita")]
    public void CriarFolha_Ocorrencia_SemLista_Aceita()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia);

        resultado.IsSuccess.Should().BeTrue();
        (resultado.Value!.Distincao?.IdentificacoesDasApresentacoesExigidas(resultado.Value.QuantidadeMinima!.Value)).Should().BeNull();
    }

    [Fact(DisplayName = "IdentificacoesDasApresentacoesExigidas para CompetenciaMensal deriva as N competências regulares mais recentes")]
    public void IdentificacoesDasApresentacoesExigidas_CompetenciaMensal_DerivaAsMaisRecentes()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 3,
            chaveDistincao: ChaveDistincao.CompetenciaMensal, dataReferencia: new DateOnly(2026, 3, 15));

        (resultado.Value!.Distincao?.IdentificacoesDasApresentacoesExigidas(resultado.Value.QuantidadeMinima!.Value)).Should().Equal("2026-03", "2026-02", "2026-01");
    }

    [Fact(DisplayName = "IdentificacoesDasApresentacoesExigidas para ExercicioAnual deriva os N exercícios regulares mais recentes")]
    public void IdentificacoesDasApresentacoesExigidas_ExercicioAnual_DerivaOsMaisRecentes()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 3,
            chaveDistincao: ChaveDistincao.ExercicioAnual, dataReferencia: new DateOnly(2026, 3, 15));

        (resultado.Value!.Distincao?.IdentificacoesDasApresentacoesExigidas(resultado.Value.QuantidadeMinima!.Value)).Should().Equal("2026", "2025", "2024");
    }

    [Fact(DisplayName = "IdentificacoesDasApresentacoesExigidas sem chaveDistincao é nulo — contagem bruta")]
    public void IdentificacoesDasApresentacoesExigidas_SemChave_Nulo()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(Documento(), ordem: 0);

        (resultado.Value!.Distincao?.IdentificacoesDasApresentacoesExigidas(resultado.Value.QuantidadeMinima!.Value)).Should().BeNull();
    }

    [Fact(DisplayName = "IdentificacoesDasApresentacoesExigidas para CompetenciaMensal na âncora mínima representável (ano 1) não lança e não excede o calendário")]
    public void IdentificacoesDasApresentacoesExigidas_CompetenciaMensal_AncoraMinima_NaoLanca()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 1,
            chaveDistincao: ChaveDistincao.CompetenciaMensal, dataReferencia: DateOnly.MinValue);

        resultado.IsSuccess.Should().BeTrue();
        (resultado.Value!.Distincao?.IdentificacoesDasApresentacoesExigidas(resultado.Value.QuantidadeMinima!.Value)).Should().Equal("0001-01");
    }

    [Fact(DisplayName = "CriarFolha com CompetenciaMensal recusa quantidadeMinima que excede o calendário representável")]
    public void CriarFolha_CompetenciaMensal_QuantidadeExcedeCalendario_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2,
            chaveDistincao: ChaveDistincao.CompetenciaMensal, dataReferencia: DateOnly.MinValue);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaExcedeJanelaRepresentavel");
    }

    [Fact(DisplayName = "CriarFolha com ExercicioAnual recusa quantidadeMinima que excede o calendário representável")]
    public void CriarFolha_ExercicioAnual_QuantidadeExcedeCalendario_Recusa()
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2,
            chaveDistincao: ChaveDistincao.ExercicioAnual, dataReferencia: new DateOnly(1, 6, 1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaExcedeJanelaRepresentavel");
    }

    [Theory(DisplayName = "CriarFolha recusa quantidadeMinima fora do teto operacional")]
    [InlineData(0)]
    [InlineData(367)]
    public void CriarFolha_QuantidadeMinimaForaDoTeto_Recusa(int quantidadeInvalida)
    {
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(Documento(), ordem: 0, quantidadeMinima: quantidadeInvalida);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaDeFolhaInvalida");
        resultado.Error.Message.Should().Be("A quantidade mínima de apresentações deste documento está fora dos limites. Informe um valor entre 1 e 366.");
    }

    [Fact(DisplayName = "CriarFolha copia ocorrenciasEsperadas defensivamente — mutação da lista original não afeta o nó")]
    public void CriarFolha_Ocorrencia_ListaOriginalMutada_NaoAfetaNo()
    {
        List<string> listaOriginal = ["a", "b"];
        Result<NoExigencia> resultado = NoExigencia.CriarFolha(
            Documento(), ordem: 0, quantidadeMinima: 2,
            chaveDistincao: ChaveDistincao.Ocorrencia, ocorrenciasEsperadas: listaOriginal);

        listaOriginal.Add("c");

        resultado.Value!.Distincao!.OcorrenciasEsperadas.Should().Equal("a", "b");
    }

}
