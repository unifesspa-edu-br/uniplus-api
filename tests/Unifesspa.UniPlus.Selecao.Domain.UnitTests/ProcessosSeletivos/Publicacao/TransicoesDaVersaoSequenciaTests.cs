namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A sequência de cada transição que congela uma versão (publicar, retificar de uma vez, fechar a
/// sessão): quais argumentos lançam, quais recusas vêm antes e o que nenhuma delas muda. Fixa o
/// comportamento observável que a organização interna das transições não pode alterar.
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
            Dados(), Bytes, "1.1", "canonical-json/sha256@v1", Hash, "user-sub-123", new RelogioFixo(Agora),
            ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

    private static Result<VersaoConfiguracao> Retificar(ProcessoSeletivo processo, VersaoConfiguracao versao) =>
        processo.Retificar(
            Dados(), versao, Bytes, "1.1", "canonical-json/sha256@v1", Hash, "user-sub-123", "Correção do prazo",
            new RelogioFixo(Agora), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo,
            TiposDeAtoUnicosDeTeste.Nenhum);

    [Theory(DisplayName = "Publicar lança pelo argumento nulo, com o nome do parâmetro, mesmo num processo já publicado")]
    [InlineData("dados")]
    [InlineData("configuracaoCongeladaCanonica")]
    [InlineData("clock")]
    [InlineData("contexto")]
    [InlineData("tiposDeAtoUnicos")]
    public void Publicar_ArgumentoNulo_LancaAntesDoStatus(string parametro)
    {
        ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _);

        Action chamada = () => publicado.Publicar(
            parametro == "dados" ? null! : Dados(),
            parametro == "configuracaoCongeladaCanonica" ? null! : Bytes, "1.1", "canonical-json/sha256@v1", Hash, "user-sub-123",
            parametro == "clock" ? null! : new RelogioFixo(Agora),
            parametro == "contexto" ? null! : ContextoDeContagemDePrazos.SemCalendario,
            FatosDeModalidadeDeTeste.DoCatalogo,
            parametro == "tiposDeAtoUnicos" ? null! : TiposDeAtoUnicosDeTeste.Nenhum);

        chamada.Should().Throw<ArgumentNullException>().WithParameterName(parametro);
    }

    [Fact(DisplayName = "Publicar um processo já publicado recusa pelo status, sem exigir precondição")]
    public void Publicar_JaPublicado_RecusaPeloStatus()
    {
        ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _);

        Result<VersaoConfiguracao> resultado = Publicar(publicado);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
    }

    [Fact(DisplayName = "Publicar um rascunho conforme não exige precondição nem abre sessão")]
    public void Publicar_Rascunho_NaoExigePrecondicaoNemAbreSessao()
    {
        ProcessoSeletivo rascunho = ProcessoConformeFactory.Criar();

        Result<VersaoConfiguracao> resultado = Publicar(rascunho);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        rascunho.Rascunho.Should().BeNull();
    }

    [Fact(DisplayName = "Retificar de uma vez um processo publicado sem sessão tem sucesso, sem a guarda de mutação")]
    public void Retificar_PublicadoSemSessao_TemSucesso()
    {
        ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);

        Result<VersaoConfiguracao> resultado = Retificar(publicado, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        publicado.Rascunho.Should().BeNull();
    }

    [Fact(DisplayName = "Retificar com contexto nulo num processo em rascunho recusa pelo status, sem lançar")]
    public void Retificar_ContextoNuloComStatusErrado_RecusaPeloStatus()
    {
        ProcessoSeletivo rascunho = ProcessoConformeFactory.Criar();
        VersaoConfiguracao versao = VersaoDe(rascunho);

        Result<VersaoConfiguracao> resultado = rascunho.Retificar(
            Dados(), versao, Bytes, "1.1", "canonical-json/sha256@v1", Hash, "user-sub-123", "Correção do prazo",
            new RelogioFixo(Agora), null!, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
    }

    [Theory(DisplayName = "Retificar lança pelo argumento nulo que confere antes do status")]
    [InlineData("dados")]
    [InlineData("versaoAtual")]
    [InlineData("configuracaoCongeladaCanonica")]
    [InlineData("clock")]
    public void Retificar_ArgumentoNuloAntesDoStatus_Lanca(string parametro)
    {
        ProcessoSeletivo rascunho = ProcessoConformeFactory.Criar();
        VersaoConfiguracao versao = VersaoDe(rascunho);

        Action chamada = () => rascunho.Retificar(
            parametro == "dados" ? null! : Dados(),
            parametro == "versaoAtual" ? null! : versao,
            parametro == "configuracaoCongeladaCanonica" ? null! : Bytes, "1.1", "canonical-json/sha256@v1", Hash, "user-sub-123",
            "Correção do prazo",
            parametro == "clock" ? null! : new RelogioFixo(Agora),
            ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

        chamada.Should().Throw<ArgumentNullException>().WithParameterName(parametro);
    }

    [Theory(DisplayName = "Retificar lança por contexto ou tipos de ato nulos só depois de passar o status e a sessão")]
    [InlineData("contexto")]
    [InlineData("tiposDeAtoUnicos")]
    public void Retificar_ArgumentoNuloDepoisDoStatus_Lanca(string parametro)
    {
        ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);

        Action chamada = () => publicado.Retificar(
            Dados(), versao, Bytes, "1.1", "canonical-json/sha256@v1", Hash, "user-sub-123", "Correção do prazo",
            new RelogioFixo(Agora),
            parametro == "contexto" ? null! : ContextoDeContagemDePrazos.SemCalendario,
            FatosDeModalidadeDeTeste.DoCatalogo,
            parametro == "tiposDeAtoUnicos" ? null! : TiposDeAtoUnicosDeTeste.Nenhum);

        chamada.Should().Throw<ArgumentNullException>().WithParameterName(parametro);
    }

    [Fact(DisplayName = "Retificar de uma vez com sessão aberta recusa pela sessão")]
    public void Retificar_ComSessaoAberta_Recusa()
    {
        ProcessoSeletivo emSessao = ProcessoEmEdicaoDeTeste.EmSessao();

        Result<VersaoConfiguracao> resultado = Retificar(emSessao, VersaoDe(emSessao));

        resultado.Error!.Code.Should().Be("RascunhoRetificacao.JaAberta");
    }

    [Fact(DisplayName = "Fechar sem sessão aberta recusa pela inexistência dela, não pela precondição inválida")]
    public void Fechar_SemSessao_RecusaPelaInexistenciaDaSessao()
    {
        ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);

        Result<VersaoConfiguracao> resultado = Fechar(publicado, versao, PrecondicaoIfMatch.DeTags(["\"invalida:9\""]));

        resultado.Error!.Code.Should().Be("RascunhoRetificacao.NaoAberta");
    }

    [Fact(DisplayName = "Fechar sem precondição lança antes de qualquer recusa")]
    public void Fechar_PrecondicaoNula_Lanca()
    {
        ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);

        Action chamada = () => Fechar(publicado, versao, null!);

        chamada.Should().Throw<ArgumentNullException>().WithParameterName("precondicao");
    }

    [Fact(DisplayName = "Fechar recusado por portão deixa a sessão aberta na mesma revisão")]
    public void Fechar_RecusadoPorPortao_MantemASessaoNaMesmaRevisao()
    {
        ProcessoSeletivo emSessao = ProcessoEmEdicaoDeTeste.EmSessao();
        VersaoConfiguracao deOutroProcesso = VersaoDe(ProcessoConformeFactory.Criar());
        PrecondicaoIfMatch atual = PrecondicaoIfMatch.DeTags([emSessao.ETagDaSessaoEditorial!]);

        Result<VersaoConfiguracao> resultado = Fechar(emSessao, deOutroProcesso, atual);

        resultado.IsFailure.Should().BeTrue();
        emSessao.Rascunho!.Revisao.Should().Be(1);
    }

    private static Result<VersaoConfiguracao> Fechar(ProcessoSeletivo p, VersaoConfiguracao versao, PrecondicaoIfMatch precondicao) =>
        p.FecharRetificacao(
            Dados(), versao, Bytes, "1.1", "canonical-json/sha256@v1", Hash, "user-sub-123", precondicao,
            new RelogioFixo(Agora), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

    private static VersaoConfiguracao VersaoDe(ProcessoSeletivo processo) =>
        VersaoConfiguracao.Abrir(processo.Id, Bytes, "1.1", "canonical-json/sha256@v1", Guid.CreateVersion7(), Hash, "user-sub-123", Agora);
}
