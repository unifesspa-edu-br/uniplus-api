namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TermoConsentimentoTests
{
    private static readonly DateTimeOffset Agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static TermoConsentimento CriarRevisavel(
        string texto = "Texto do termo", string baseLegal = "Lei 13.709/2018", string? formaAceite = "REGISTRO_DIGITAL_SEM_LOG_IP")
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar("Termo LGPD", texto, baseLegal, formaAceite);
        resultado.IsSuccess.Should().BeTrue();
        return resultado.Value!;
    }

    // ── Criar ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "Criar com rascunho vazio nasce EM_ELABORACAO e forma A_DEFINIR")]
    public void Criar_RascunhoVazio_NasceEmElaboracao()
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar("Declaração de veracidade", null, null, null);

        resultado.IsSuccess.Should().BeTrue();
        TermoConsentimento termo = resultado.Value!;
        termo.Id.Should().NotBe(Guid.Empty);
        termo.Nome.Should().Be("Declaração de veracidade");
        termo.TextoRascunho.Should().BeNull();
        termo.BaseLegalRascunho.Should().BeNull();
        termo.FormaAceiteRascunho.Should().Be(FormaAceite.ADefinir);
        termo.Revisado.Should().BeFalse();
        termo.RevisadoPor.Should().BeNull();
        termo.RevisadoEm.Should().BeNull();
        termo.Versoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Criar com campos iniciais preenche o rascunho")]
    public void Criar_ComCamposIniciais_PreencheRascunho()
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar(
            "Termo de consentimento LGPD", "Texto do termo", "Lei 13.709/2018", "REGISTRO_DIGITAL_SEM_LOG_IP");

        resultado.IsSuccess.Should().BeTrue();
        TermoConsentimento termo = resultado.Value!;
        termo.TextoRascunho.Should().Be("Texto do termo");
        termo.BaseLegalRascunho.Should().Be("Lei 13.709/2018");
        termo.FormaAceiteRascunho.Should().Be(FormaAceite.RegistroDigitalSemLogIp);
        termo.Revisado.Should().BeFalse();
    }

    [Theory(DisplayName = "Criar com nome ausente ou em branco falha")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_Falha(string nome)
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar(nome, null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Criar com nome acima de 200 caracteres falha")]
    public void Criar_NomeMuitoLongo_Falha()
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar(new string('A', 201), null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Criar com texto do rascunho acima de 20000 caracteres falha")]
    public void Criar_TextoMuitoLongo_Falha()
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar("Termo", new string('A', 20_001), null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.TextoTamanho);
    }

    [Fact(DisplayName = "Criar com base legal acima de 500 caracteres falha")]
    public void Criar_BaseLegalMuitoLonga_Falha()
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar("Termo", null, new string('A', 501), null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.BaseLegalTamanho);
    }

    [Fact(DisplayName = "Criar com forma de aceite fora do domínio fechado falha")]
    public void Criar_FormaAceiteInvalida_Falha()
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar("Termo", null, null, "FORMA_INEXISTENTE");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.FormaAceiteInvalida);
    }

    // ── EditarRascunho ─────────────────────────────────────────────────

    [Fact(DisplayName = "EditarRascunho em elaboração aceita a mudança e mantém EM_ELABORACAO")]
    public void EditarRascunho_EmElaboracao_AceitaEMantemStatus()
    {
        TermoConsentimento termo = CriarRevisavel();

        Result resultado = termo.EditarRascunho("Novo texto", "Nova base legal", "A_DEFINIR");

        resultado.IsSuccess.Should().BeTrue();
        termo.TextoRascunho.Should().Be("Novo texto");
        termo.BaseLegalRascunho.Should().Be("Nova base legal");
        termo.FormaAceiteRascunho.Should().Be(FormaAceite.ADefinir);
        termo.Revisado.Should().BeFalse();
    }

    [Fact(DisplayName = "EditarRascunho de termo revisado reverte para EM_ELABORACAO e limpa a marca")]
    public void EditarRascunho_TermoRevisado_ReverteRevisao()
    {
        TermoConsentimento termo = CriarRevisavel();
        termo.MarcarRevisado("usuario.revisor", Agora).IsSuccess.Should().BeTrue();

        Result resultado = termo.EditarRascunho("Texto ajustado", "Lei 13.709/2018", "REGISTRO_DIGITAL_SEM_LOG_IP");

        resultado.IsSuccess.Should().BeTrue();
        termo.Revisado.Should().BeFalse();
        termo.RevisadoPor.Should().BeNull();
        termo.RevisadoEm.Should().BeNull();
    }

    [Fact(DisplayName = "EditarRascunho com forma de aceite inválida falha")]
    public void EditarRascunho_FormaAceiteInvalida_Falha()
    {
        TermoConsentimento termo = CriarRevisavel();

        Result resultado = termo.EditarRascunho("Texto", "Base legal", "FORMA_INEXISTENTE");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.FormaAceiteInvalida);
    }

    // ── MarcarRevisado ─────────────────────────────────────────────────

    [Fact(DisplayName = "MarcarRevisado com texto e base legal grava o ator e o instante")]
    public void MarcarRevisado_ComTextoEBaseLegal_Sucesso()
    {
        TermoConsentimento termo = CriarRevisavel();

        Result resultado = termo.MarcarRevisado("usuario.revisor", Agora);

        resultado.IsSuccess.Should().BeTrue();
        termo.Revisado.Should().BeTrue();
        termo.RevisadoPor.Should().Be("usuario.revisor");
        termo.RevisadoEm.Should().Be(Agora);
    }

    [Fact(DisplayName = "MarcarRevisado sem texto falha")]
    public void MarcarRevisado_SemTexto_Falha()
    {
        TermoConsentimento termo = CriarRevisavel(texto: null!);
        termo.EditarRascunho(null, "Lei 13.709/2018", null);

        Result resultado = termo.MarcarRevisado("usuario.revisor", Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.RevisaoSemTexto);
        termo.Revisado.Should().BeFalse();
    }

    [Fact(DisplayName = "MarcarRevisado sem base legal falha")]
    public void MarcarRevisado_SemBaseLegal_Falha()
    {
        TermoConsentimento termo = CriarRevisavel();
        termo.EditarRascunho("Texto do termo", null, null);

        Result resultado = termo.MarcarRevisado("usuario.revisor", Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.RevisaoSemBaseLegal);
        termo.Revisado.Should().BeFalse();
    }

    // ── Promover ───────────────────────────────────────────────────────

    [Fact(DisplayName = "Promover rascunho revisado cria versão imutável e preserva o rascunho")]
    public void Promover_RascunhoRevisado_CriaVersaoEPreservaRascunho()
    {
        TermoConsentimento termo = CriarRevisavel();
        termo.MarcarRevisado("usuario.revisor", Agora).IsSuccess.Should().BeTrue();

        Result<TermoConsentimentoVersao> resultado = termo.Promover("usuario.revisor", Agora);

        resultado.IsSuccess.Should().BeTrue();
        termo.Versoes.Should().HaveCount(1);
        TermoConsentimentoVersao versao = termo.Versoes[0];
        versao.TermoConsentimentoId.Should().Be(termo.Id);
        versao.Texto.Should().Be("Texto do termo");
        versao.BaseLegal.Should().Be("Lei 13.709/2018");
        versao.FormaAceite.Should().Be(FormaAceite.RegistroDigitalSemLogIp);
        versao.PromovidaPor.Should().Be("usuario.revisor");
        versao.PromovidaEm.Should().Be(Agora);
        versao.Hash.Should().NotBeNullOrWhiteSpace();

        // Rascunho permanece intacto — pode gerar a PRÓXIMA versão no futuro.
        termo.TextoRascunho.Should().Be("Texto do termo");
        termo.BaseLegalRascunho.Should().Be("Lei 13.709/2018");
    }

    [Fact(DisplayName = "Duas promoções do mesmo conteúdo produzem o mesmo hash")]
    public void Promover_MesmoConteudo_HashDeterministico()
    {
        TermoConsentimento termoA = CriarRevisavel();
        termoA.MarcarRevisado("usuario.revisor", Agora);
        termoA.Promover("usuario.revisor", Agora);

        TermoConsentimento termoB = CriarRevisavel();
        termoB.MarcarRevisado("usuario.revisor", Agora);
        termoB.Promover("usuario.revisor", Agora);

        termoA.Versoes[0].Hash.Should().Be(termoB.Versoes[0].Hash);
    }

    [Fact(DisplayName = "Hash distingue splits diferentes que concatenariam para o mesmo texto")]
    public void Promover_SplitDiferenteMesmaConcatenacao_HashDiferente()
    {
        // Codex #1019 P2: sem prefixo de tamanho, ("AB","C") e ("A","BC") produziriam
        // o mesmo payload concatenado — e por isso o mesmo hash — mesmo sendo
        // conteúdos legalmente distintos.
        TermoConsentimento termoA = CriarRevisavel(texto: "AB", baseLegal: "C");
        termoA.MarcarRevisado("usuario.revisor", Agora);
        termoA.Promover("usuario.revisor", Agora);

        TermoConsentimento termoB = CriarRevisavel(texto: "A", baseLegal: "BC");
        termoB.MarcarRevisado("usuario.revisor", Agora);
        termoB.Promover("usuario.revisor", Agora);

        termoA.Versoes[0].Hash.Should().NotBe(termoB.Versoes[0].Hash);
    }

    [Fact(DisplayName = "Promoção recusa rascunho não revisado")]
    public void Promover_NaoRevisado_Falha()
    {
        TermoConsentimento termo = CriarRevisavel();

        Result<TermoConsentimentoVersao> resultado = termo.Promover("usuario.revisor", Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.PromocaoSemRevisao);
        termo.Versoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "EditarRascunho após revisão bloqueia promoção até nova revisão")]
    public void Promover_ApósEditarRascunhoRevisado_ExigeNovaRevisao()
    {
        TermoConsentimento termo = CriarRevisavel();
        termo.MarcarRevisado("usuario.revisor", Agora);
        termo.EditarRascunho("Texto ajustado", "Lei 13.709/2018", null);

        Result<TermoConsentimentoVersao> resultado = termo.Promover("usuario.revisor", Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.PromocaoSemRevisao);
    }
}
