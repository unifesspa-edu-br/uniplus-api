namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Identificador legível do processo seletivo: livre antes da primeira publicação, exigido para
/// gerar versão e imutável depois de constar em versão publicada. Na sessão de retificação aberta
/// sobre versão congelada sem ele, o identificador é declarado e pode ser corrigido até o
/// fechamento.
/// </summary>
public sealed class ProcessoSeletivoIdentificadorLegivelTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly byte[] BytesCanonicos = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Em rascunho, o identificador pode ser declarado, trocado e removido")]
    public void Rascunho_IdentificadorLivre()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        IdentificadorLegivel outro = Identificador("psiq-2026");

        processo.DefinirIdentificadorLegivel(outro, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.IdentificadorLegivel.Should().Be(outro);

        processo.DefinirIdentificadorLegivel(Identificador("psiq-2026b"), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.IdentificadorLegivel!.Value.Valor.Should().Be("psiq-2026b");

        processo.DefinirIdentificadorLegivel(null, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.IdentificadorLegivel.Should().BeNull();
    }

    [Fact(DisplayName = "Publicar sem identificador legível é recusado com erro nomeado")]
    public void Publicar_SemIdentificador_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirIdentificadorLegivel(null, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente);
        processo.Status.Should().Be(StatusProcesso.Rascunho);
    }

    [Fact(DisplayName = "O checklist de conformidade aponta o identificador ausente, e só ele")]
    public void Conformidade_SemIdentificador_ApontaOItem()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirIdentificadorLegivel(null, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IReadOnlyList<ItemConformidade> itens = processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario);

        itens.Where(static i => !i.Ok).Select(static i => i.Codigo)
            .Should().Equal("identificador_legivel_nao_declarado");
        itens.Single(static i => i.Codigo == "identificador_legivel_nao_declarado").Dimensao
            .Should().Be(DimensaoConformidade.Identificacao);
    }

    [Fact(DisplayName = "Identificador que consta na versão publicada não muda, nem dentro da sessão de retificação")]
    public void Publicado_ComIdentificador_Imutavel()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirIdentificadorLegivel(Identificador("psiq-2026"), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        VersaoConfiguracao versao = Publicar(processo).Value!;
        processo.AbrirRetificacao("Correção do prazo", versao, identificadorDaVersaoBase: processo.IdentificadorLegivel, "user-sub-1", Agora).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirIdentificadorLegivel(
            Identificador("psiq-2026b"), PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel);
        processo.IdentificadorLegivel!.Value.Valor.Should().Be("psiq-2026");
    }

    [Fact(DisplayName = "Remover o identificador de processo publicado também é recusado")]
    public void Publicado_RemoverIdentificador_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        VersaoConfiguracao versao = Publicar(processo).Value!;
        processo.AbrirRetificacao("Correção do prazo", versao, identificadorDaVersaoBase: processo.IdentificadorLegivel, "user-sub-1", Agora).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirIdentificadorLegivel(
            null, PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!]));

        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel);
    }

    [Fact(DisplayName = "Fora da sessão de retificação, processo publicado recusa pelo guarda geral")]
    public void Publicado_SemSessao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        IdentificadorLegivel original = processo.IdentificadorLegivel!.Value;
        Publicar(processo).IsSuccess.Should().BeTrue();

        processo.DefinirIdentificadorLegivel(Identificador("psiq-2026"), PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.IdentificadorLegivel.Should().Be(original);
    }

    [Fact(DisplayName = "Reenviar o mesmo identificador de processo publicado também é recusado")]
    public void Publicado_MesmoIdentificador_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        IdentificadorLegivel identificador = processo.IdentificadorLegivel!.Value;
        VersaoConfiguracao versao = Publicar(processo).Value!;
        processo.AbrirRetificacao("Correção do prazo", versao, identificadorDaVersaoBase: processo.IdentificadorLegivel, "user-sub-1", Agora).IsSuccess.Should().BeTrue();
        string etag = processo.ETagDaSessaoEditorial!;

        processo.DefinirIdentificadorLegivel(identificador, PrecondicaoIfMatch.DeTags([etag]))
            .Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel);
        processo.ETagDaSessaoEditorial.Should().Be(etag, "a recusa não move a revisão da sessão");
    }

    [Fact(DisplayName = "Fechar retificação de processo publicado sem identificador é recusado, sem gerar versão")]
    public void FecharRetificacao_SemIdentificador_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        VersaoConfiguracao versao = Publicar(processo).Value!;
        RetirarIdentificador(processo);
        processo.AbrirRetificacao("Correção do prazo", versao, identificadorDaVersaoBase: null, "user-sub-1", Agora).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.FecharRetificacao(
            ProcessoConformeFactory.Dados(), versao, BytesCanonicos, "1.1", "canonical-json/sha256@v1", HashFixo,
            "user-sub-1", PrecondicaoIfMatch.Curinga, new RelogioFixo(Agora.AddMinutes(1)),
            ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue("nenhuma versão nova é aberta");
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente);
        processo.Rascunho.Should().NotBeNull("a sessão continua aberta para quem corrigir");
    }

    [Fact(DisplayName = "Retificar processo publicado sem identificador é recusado, sem gerar versão")]
    public void Retificar_SemIdentificador_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        VersaoConfiguracao versao = Publicar(processo).Value!;
        RetirarIdentificador(processo);

        Result<VersaoConfiguracao> resultado = processo.Retificar(
            ProcessoConformeFactory.Dados(), versao, BytesCanonicos, "1.1", "canonical-json/sha256@v1", HashFixo,
            "user-sub-1", motivo: "Correção", clock: new RelogioFixo(Agora.AddMinutes(1)),
            ContextoDeContagemDePrazos.SemCalendario);

        resultado.IsFailure.Should().BeTrue("nenhuma versão nova é aberta");
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente);
    }

    [Fact(DisplayName = "Sessão aberta sobre versão sem identificador o declara, corrige e remove, movendo a revisão")]
    public void SessaoSobreVersaoSemIdentificador_Declara()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        VersaoConfiguracao versao = Publicar(processo).Value!;
        RetirarIdentificador(processo);
        processo.AbrirRetificacao("Declara o endereço público", versao, identificadorDaVersaoBase: null, "user-sub-1", Agora).IsSuccess.Should().BeTrue();

        string etagInicial = processo.ETagDaSessaoEditorial!;
        processo.DefinirIdentificadorLegivel(Identificador("medicina-2072"), PrecondicaoIfMatch.DeTags([etagInicial]))
            .IsSuccess.Should().BeTrue("a versão base não congelou identificador — a sessão o declara");
        processo.ETagDaSessaoEditorial.Should().NotBe(etagInicial, "declarar é mutação da sessão e move a revisão");

        processo.DefinirIdentificadorLegivel(
                Identificador("medicina-2027"), PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!]))
            .IsSuccess.Should().BeTrue("o valor ainda não consta em versão publicada — corrigir a digitação é aceito");
        processo.IdentificadorLegivel!.Value.Valor.Should().Be("medicina-2027");

        processo.DefinirIdentificadorLegivel(null, PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!]))
            .IsSuccess.Should().BeTrue("remover dentro da mesma sessão também é aceito");
        processo.IdentificadorLegivel.Should().BeNull();
    }

    [Fact(DisplayName = "Versão vigente que congelou o identificador recusa a declaração, mesmo com a raiz viva sem ele")]
    public void VersaoComIdentificador_RaizVivaSem_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        IdentificadorLegivel congelado = processo.IdentificadorLegivel!.Value;
        VersaoConfiguracao versao = Publicar(processo).Value!;
        RetirarIdentificador(processo);
        processo.AbrirRetificacao("Correção do prazo", versao, identificadorDaVersaoBase: congelado, "user-sub-1", Agora)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirIdentificadorLegivel(
            Identificador("medicina-2027"), PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!]));

        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel,
            "a imutabilidade é conferida contra a versão publicada, e não contra o valor vivo da raiz");
        processo.IdentificadorLegivel.Should().BeNull("a recusa não altera a raiz");
    }

    [Fact(DisplayName = "O identificador declarado na sessão entra na versão que ela gera e não muda mais")]
    public void SessaoSobreVersaoSemIdentificador_FechamentoCongela()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        VersaoConfiguracao versao = Publicar(processo).Value!;
        RetirarIdentificador(processo);
        processo.AbrirRetificacao("Declara o endereço público", versao, identificadorDaVersaoBase: null, "user-sub-1", Agora).IsSuccess.Should().BeTrue();
        processo.DefinirIdentificadorLegivel(
                Identificador("medicina-2027"), PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!]))
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> fechamento = processo.FecharRetificacao(
            ProcessoConformeFactory.Dados(), versao, BytesCanonicos, "1.1", "canonical-json/sha256@v1", HashFixo,
            "user-sub-1", PrecondicaoIfMatch.Curinga, new RelogioFixo(Agora.AddMinutes(1)),
            ContextoDeContagemDePrazos.SemCalendario);
        fechamento.IsSuccess.Should().BeTrue(fechamento.Error?.Message);

        processo.AbrirRetificacao("Nova correção", fechamento.Value!, identificadorDaVersaoBase: Identificador("medicina-2027"), "user-sub-1", Agora.AddMinutes(2))
            .IsSuccess.Should().BeTrue();
        Result troca = processo.DefinirIdentificadorLegivel(
            Identificador("medicina-2028"), PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!]));

        troca.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel,
            "agora o identificador consta na versão publicada que a sessão anterior gerou");
        processo.IdentificadorLegivel!.Value.Valor.Should().Be("medicina-2027");
    }

    /// <summary>
    /// O estado de um certame publicado sem identificador não é alcançável pela publicação, que o
    /// exige; é montado por reflexão para provar que a sucessão de versão o recusa e que a sessão
    /// de retificação o declara.
    /// </summary>
    private static void RetirarIdentificador(ProcessoSeletivo processo) =>
        typeof(ProcessoSeletivo)
            .GetProperty(nameof(ProcessoSeletivo.IdentificadorLegivel))!
            .SetValue(processo, null);

    private static IdentificadorLegivel Identificador(string valor) => IdentificadorLegivel.Criar(valor).Value;

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        ProcessoConformeFactory.Dados(), BytesCanonicos, "1.1", "canonical-json/sha256@v1", HashFixo, "user-sub-1",
        new RelogioFixo(Agora), ContextoDeContagemDePrazos.SemCalendario);

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
