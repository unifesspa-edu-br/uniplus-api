namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A sequência de cada transição que congela uma versão (publicar e retificar): quais argumentos lançam,
/// quais recusas vêm antes e o que nenhuma delas muda. Fixa o comportamento observável que a organização
/// interna das transições não pode alterar.
/// </summary>
public sealed class TransicoesDaVersaoSequenciaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());
    private static readonly string Hash = new('a', 64);

    private static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        Guid.CreateVersion7()).Value!;

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) =>
        processo.Publicar(
            new CenarioDeCongelamento { Dados = Dados(), Documento = new DocumentoCongelado(Bytes, "1.1", "canonical-json/sha256@v1", Hash), Relogio = new RelogioFixo(Agora) });

    private static Result<VersaoConfiguracao> Retificar(ProcessoSeletivo processo, VersaoConfiguracao versao) =>
        processo.Retificar(
            new CenarioDeCongelamento { Dados = Dados(), Documento = new DocumentoCongelado(Bytes, "1.1", "canonical-json/sha256@v1", Hash), Relogio = new RelogioFixo(Agora) }, versao, "Correção do prazo");

    [Fact(DisplayName = "Publicar um processo já publicado recusa pela situação")]
    public void Publicar_JaPublicado_RecusaPelaSituacao()
    {
        ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.Publicado(out _);

        Result<VersaoConfiguracao> resultado = Publicar(publicado);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        resultado.Error.Message.Should().Contain("situação atual: Publicado");
    }

    [Fact(DisplayName = "Publicar um rascunho conforme tem sucesso")]
    public void Publicar_Rascunho_TemSucesso()
    {
        ProcessoSeletivo rascunho = ProcessoConformeFactory.Criar();

        Result<VersaoConfiguracao> resultado = Publicar(rascunho);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Retificar um processo publicado tem sucesso, sem a guarda de mutação")]
    public void Retificar_Publicado_TemSucesso()
    {
        ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.Publicado(out VersaoConfiguracao versao);

        Result<VersaoConfiguracao> resultado = Retificar(publicado, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "O estado do rascunho admite publicar e não admite retificar")]
    public void ConferirEstado_Rascunho_AdmitePublicarENaoRetificar()
    {
        ProcessoSeletivo rascunho = ProcessoConformeFactory.Criar();
        ProcessoEmEdicaoDeTeste.Publicado(out VersaoConfiguracao deOutro);

        TransicoesDaVersao.ConferirEstadoParaPublicar(rascunho.Situacao).IsSuccess.Should().BeTrue();
        TransicoesDaVersao.ConferirEstadoParaRetificar(rascunho.Id, rascunho.Situacao, deOutro, "Correção do prazo").Error!.Message.Should().Contain("situação atual: Rascunho");
    }

    [Fact(DisplayName = "O estado do processo publicado não admite publicar de novo e exige o motivo da retificação")]
    public void ConferirEstado_Publicado_NaoAdmitePublicarEExigeOMotivo()
    {
        ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.Publicado(out VersaoConfiguracao versao);

        TransicoesDaVersao.ConferirEstadoParaPublicar(publicado.Situacao).Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        TransicoesDaVersao.ConferirEstadoParaRetificar(publicado.Id, publicado.Situacao, versao, " ").Error!.Code.Should().Be("ProcessoSeletivo.MotivoRetificacaoObrigatorio");
        TransicoesDaVersao.ConferirEstadoParaRetificar(publicado.Id, publicado.Situacao, versao, "Correção do prazo").IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A versão corrente de outro processo lança sem mutar o processo")]
    public void ConferirEstado_VersaoDeOutroProcesso_Lanca()
    {
        ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.Publicado(out _);
        ProcessoEmEdicaoDeTeste.Publicado(out VersaoConfiguracao deOutro);

        Action conferir = () => TransicoesDaVersao.ConferirEstadoParaRetificar(publicado.Id, publicado.Situacao, deOutro, "Correção do prazo");

        conferir.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("versaoAtual");
    }

    [Fact(DisplayName = "Retificar lança pela versão atual nula antes de conferir a situação")]
    public void Retificar_VersaoAtualNula_LancaAntesDaSituacao()
    {
        ProcessoSeletivo rascunho = ProcessoConformeFactory.Criar();

        Action chamada = () => rascunho.Retificar(
            new CenarioDeCongelamento { Dados = Dados(), Documento = new DocumentoCongelado(Bytes, "1.1", "canonical-json/sha256@v1", Hash), Relogio = new RelogioFixo(Agora) }, null!, "Correção do prazo");

        chamada.Should().Throw<ArgumentNullException>().WithParameterName("versaoAtual");
    }
}
