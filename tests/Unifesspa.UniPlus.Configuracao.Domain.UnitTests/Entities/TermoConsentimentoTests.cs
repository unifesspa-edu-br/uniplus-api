namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class TermoConsentimentoTests
{
    private static readonly DateTimeOffset Agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static TermoConsentimento CriarRevisavel(
        string texto = "Texto do termo", string baseLegal = "Lei 13.709/2018", string? formaAceite = "REGISTRO_SEM_ENDERECO_IP")
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
            "Termo de consentimento LGPD", "Texto do termo", "Lei 13.709/2018", "REGISTRO_SEM_ENDERECO_IP");

        resultado.IsSuccess.Should().BeTrue();
        TermoConsentimento termo = resultado.Value!;
        termo.TextoRascunho.Should().Be("Texto do termo");
        termo.BaseLegalRascunho.Should().Be("Lei 13.709/2018");
        termo.FormaAceiteRascunho.Should().Be(FormaAceite.RegistroSemEnderecoIp);
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

    [Fact(DisplayName = "Nome ausente, texto longo e forma de aceite inválida acumulam as três violações")]
    public void Criar_TresViolacoesIndependentes_AcumulaAsTres()
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar(
            "", new string('A', 20_001), null, "FORMA_INEXISTENTE");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[1].Field.Should().Be("textoRascunho");
        resultado.Errors[2].Field.Should().Be("formaAceiteRascunho");
    }

    [Fact(DisplayName = "ValidarCamposDoPayload isolado é público e reusável sem instanciar o agregado")]
    public void ValidarCamposDoPayload_CamposValidos_Aceita()
    {
        Result resultado = TermoConsentimento.ValidarCamposDoPayload("Texto", "Base legal", null);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Criar com forma de aceite fora do contrato falha, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("FORMA_INEXISTENTE")]
    [InlineData(" A_DEFINIR")]
    [InlineData("registro_digital_sem_log_ip")]
    public void Criar_FormaAceiteInvalida_Falha(string forma)
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar("Termo", null, null, forma);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.FormaAceiteInvalida);
    }

    [Fact(DisplayName = "Forma de aceite recusada lista os códigos aceitos e não repete o valor recebido")]
    public void Criar_FormaAceiteInvalida_ListaOsCodigosSemRepetirOValor()
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar("Termo", null, null, "FORMA_INEXISTENTE");

        resultado.Error!.Message.Should().Be(
            "Forma de aceite deve ser uma de: A_DEFINIR, REGISTRO_SEM_ENDERECO_IP, REGISTRO_COM_ENDERECO_IP.");
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

        Result resultado = termo.EditarRascunho("Texto ajustado", "Lei 13.709/2018", "REGISTRO_SEM_ENDERECO_IP");

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
        ConferirVersaoPromovida(termo);
        termo.TextoRascunho.Should().Be("Texto do termo", "o rascunho permanece intacto e pode gerar a próxima versão");
        termo.BaseLegalRascunho.Should().Be("Lei 13.709/2018");
        ConferirRevisaoConsumida(termo);
    }

    private static void ConferirVersaoPromovida(TermoConsentimento termo)
    {
        termo.Versoes.Should().HaveCount(1);
        TermoConsentimentoVersao versao = termo.Versoes[0];
        versao.TermoConsentimentoId.Should().Be(termo.Id);
        versao.Texto.Should().Be("Texto do termo");
        versao.BaseLegal.Should().Be("Lei 13.709/2018");
        versao.FormaAceite.Should().Be(FormaAceite.RegistroSemEnderecoIp);
        versao.PromovidaPor.Should().Be("usuario.revisor");
        versao.PromovidaEm.Should().Be(Agora);
        versao.Hash.Should().NotBeNullOrWhiteSpace();
    }

    private static void ConferirRevisaoConsumida(TermoConsentimento termo)
    {
        termo.Revisado.Should().BeFalse("cada versão exige seu próprio sinal de revisão");
        termo.RevisadoPor.Should().BeNull();
        termo.RevisadoEm.Should().BeNull();
    }

    [Fact(DisplayName = "Promover duas vezes sem revisar de novo recusa a segunda promoção")]
    public void Promover_DuasVezesSemRevisarDeNovo_RecusaSegunda()
    {
        // Sem consumir a revisão, um retry acidental da promoção
        // (outra Idempotency-Key, duplo clique) anexaria uma segunda versão
        // idêntica ao histórico forense a partir do MESMO conteúdo já revisado.
        TermoConsentimento termo = CriarRevisavel();
        termo.MarcarRevisado("usuario.revisor", Agora);
        termo.Promover("usuario.revisor", Agora).IsSuccess.Should().BeTrue();

        Result<TermoConsentimentoVersao> segunda = termo.Promover("usuario.revisor", Agora);

        segunda.IsFailure.Should().BeTrue();
        segunda.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.PromocaoSemRevisao);
        termo.Versoes.Should().HaveCount(1, "a segunda tentativa não deve anexar outra versão");
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
        // Sem prefixo de tamanho, ("AB","C") e ("A","BC") produziriam
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

    [Fact(DisplayName = "O código nasce do nome e não muda quando o rascunho é editado")]
    public void Criar_GeraCodigoDoNome_EEditarNaoOMuda()
    {
        TermoConsentimento termo = TermoConsentimento.Criar("Termo de uso e privacidade", null, null, null).Value!;
        string codigo = termo.Codigo.Valor;

        termo.EditarRascunho("Texto", "Lei 13.709/2018", null);

        codigo.Should().Be("TERMO_DE_USO_E_PRIVACIDADE");
        termo.Codigo.Valor.Should().Be(codigo);
    }

    [Fact(DisplayName = "Nome que não forma código é recusado no campo nome")]
    public void Criar_NomeSemLetraNemDigito_RecusaNoCampoNome()
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar("---", null, null, null);

        resultado.Errors.Should().ContainSingle(e => e.Field == "nome" && e.Error.Code == TermoConsentimentoErrorCodes.CodigoFormatoInvalido);
    }

    [Fact(DisplayName = "Nome que forma código acima de 80 caracteres é recusado no campo nome")]
    public void Criar_CodigoAcimaDoLimite_RecusaNoCampoNome()
    {
        Result<TermoConsentimento> resultado = TermoConsentimento.Criar(new string('a', 81), null, null, null);

        resultado.Errors.Should().ContainSingle(e => e.Field == "nome" && e.Error.Code == TermoConsentimentoErrorCodes.CodigoTamanho);
    }

    [Fact(DisplayName = "As versões promovidas recebem números sequenciais a partir de 1")]
    public void Promover_NumeraAsVersoesEmSequencia()
    {
        TermoConsentimento termo = TermoConsentimento.Criar("Termo", "Texto", "Lei 13.709/2018", null).Value!;

        int primeira = Promovido(termo).Numero;
        int segunda = Promovido(termo).Numero;

        (primeira, segunda).Should().Be((1, 2));
    }

    private static TermoConsentimentoVersao Promovido(TermoConsentimento termo)
    {
        DateTimeOffset agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);
        termo.MarcarRevisado("revisor", agora);
        return termo.Promover("revisor", agora).Value!;
    }
}
