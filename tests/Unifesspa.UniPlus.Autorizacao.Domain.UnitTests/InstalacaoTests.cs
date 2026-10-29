namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class InstalacaoTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string SubPaulo = "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001";
    private const string SubDiogo = "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002";
    private const string SubIsabella = "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003";

    private static readonly DateTimeOffset Agora = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Identidade Paulo = new(Emissor, SubPaulo);
    private static readonly byte[] Resumo = [1, 2, 3];

    [Fact(DisplayName = "Instalação iniciada com dois gestores distintos nasce aguardando o documento")]
    public void Iniciar_Valida_NasceAguardandoDocumento()
    {
        Result<Instalacao> resultado = Instalacao.Iniciar(
            Paulo, Gestores(SubDiogo, SubIsabella), Emissor, "  SIPAC 123/2026  ", 1024, Agora);

        resultado.IsSuccess.Should().BeTrue();
        Instalacao instalacao = resultado.Value!;
        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.AguardandoDocumento);
        instalacao.NumeroDoDocumento.Should().Be("SIPAC 123/2026");
        instalacao.ExecutorSub.Should().Be(SubPaulo);
        instalacao.Gestores.Select(g => g.Sub).Should().BeEquivalentTo(SubDiogo, SubIsabella);
        instalacao.IniciadaEm.Should().Be(Agora);
    }

    [Fact(DisplayName = "Quem executa não pode estar entre os gestores")]
    public void Iniciar_ExecutorEntreGestores_Recusa()
    {
        CodigosDaRecusa(Instalacao.Iniciar(Paulo, Gestores(SubPaulo, SubIsabella), Emissor, "123", 1024, Agora))
            .Should().Equal(InstalacaoErrorCodes.ExecutorEntreGestores);
    }

    [Fact(DisplayName = "Os dois gestores têm de ser distintos")]
    public void Iniciar_GestoresRepetidos_Recusa()
    {
        CodigosDaRecusa(Instalacao.Iniciar(Paulo, Gestores(SubDiogo, SubDiogo), Emissor, "123", 1024, Agora))
            .Should().Equal(InstalacaoErrorCodes.GestoresInvalidos);
    }

    [Fact(DisplayName = "Um só gestor, ou mais de dois, é recusado")]
    public void Iniciar_QuantidadeDiferenteDeDois_Recusa()
    {
        CodigosDaRecusa(Instalacao.Iniciar(Paulo, Gestores(SubDiogo), Emissor, "123", 1024, Agora))
            .Should().Equal(InstalacaoErrorCodes.GestoresInvalidos);
        CodigosDaRecusa(Instalacao.Iniciar(
                Paulo, Gestores(SubDiogo, SubIsabella, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0004"), Emissor, "123", 1024, Agora))
            .Should().Equal(InstalacaoErrorCodes.GestoresInvalidos);
    }

    [Fact(DisplayName = "O tamanho declarado do PDF fica gravado na instalação")]
    public void Iniciar_GravaOTamanhoDeclaradoDoPdf()
    {
        Instalacao instalacao = Instalacao.Iniciar(
            Paulo, [new(Emissor, SubDiogo), new(Emissor, SubIsabella)], Emissor, "123", 2048, Agora).Value!;

        instalacao.TamanhoDoPdf.Should().Be(2048);
    }

    [Theory(DisplayName = "Gestor com sub fora do formato UUID ou de outro emissor é recusado")]
    [InlineData("diogo.souza", Emissor)]
    [InlineData("00000000-0000-0000-0000-000000000000", Emissor)]
    [InlineData(SubDiogo, "https://outro.idp/realms/x")]
    [InlineData(null, Emissor)]
    [InlineData(SubDiogo, null)]
    public void Iniciar_GestorMalFormado_Recusa(string? sub, string? emissor)
    {
        IdentidadeInformada[] gestores = [new(emissor, sub), new(Emissor, SubIsabella)];

        CodigosDaRecusa(Instalacao.Iniciar(Paulo, gestores, Emissor, "123", 1024, Agora))
            .Should().Equal(InstalacaoErrorCodes.GestoresInvalidos);
    }

    [Theory(DisplayName = "Número do documento ausente, em branco, longo ou com caractere nulo é recusado")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("12\03")]
    public void Iniciar_NumeroInvalido_Recusa(string? numero)
    {
        CodigosDaRecusa(Instalacao.Iniciar(Paulo, Gestores(SubDiogo, SubIsabella), Emissor, numero, 1024, Agora))
            .Should().Equal(InstalacaoErrorCodes.DocumentoInvalido);
    }

    [Fact(DisplayName = "Número do documento acima de 60 caracteres é recusado")]
    public void Iniciar_NumeroLongo_Recusa()
    {
        CodigosDaRecusa(Instalacao.Iniciar(Paulo, Gestores(SubDiogo, SubIsabella), Emissor, new string('9', 61), 1024, Agora))
            .Should().Equal(InstalacaoErrorCodes.DocumentoInvalido);
    }

    [Theory(DisplayName = "PDF vazio ou acima de 10 MB é recusado")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Instalacao.TamanhoMaximoDoPdfBytes + 1)]
    public void Iniciar_TamanhoDoPdfForaDoLimite_Recusa(long tamanho)
    {
        CodigosDaRecusa(Instalacao.Iniciar(Paulo, Gestores(SubDiogo, SubIsabella), Emissor, "123", tamanho, Agora))
            .Should().Equal(InstalacaoErrorCodes.DocumentoInvalido);
    }

    [Fact(DisplayName = "Violações independentes são acumuladas na mesma recusa")]
    public void Iniciar_VariasViolacoes_Acumula()
    {
        CodigosDaRecusa(Instalacao.Iniciar(Paulo, Gestores(SubDiogo), Emissor, null, 0, Agora))
            .Should().BeEquivalentTo(
                InstalacaoErrorCodes.GestoresInvalidos, InstalacaoErrorCodes.DocumentoInvalido, InstalacaoErrorCodes.DocumentoInvalido);
    }

    [Fact(DisplayName = "Confirmar o documento sem ETag é recusado e não muda a situação")]
    public void ConfirmarDocumento_SemETag_Recusa()
    {
        Instalacao instalacao = Iniciada();

        CodigosDaRecusa(instalacao.ConfirmarDocumento("  ")).Should().Equal(InstalacaoErrorCodes.DocumentoInvalido);
        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.AguardandoDocumento);
    }

    [Fact(DisplayName = "Confirmar o documento fora da situação aguardando documento é recusado")]
    public void ConfirmarDocumento_ForaDaOrdem_Recusa()
    {
        Instalacao instalacao = EmVerificacao();

        instalacao.ConfirmarDocumento("\"outro\"").Error!.Code.Should().Be(InstalacaoErrorCodes.SituacaoInvalida);
        instalacao.DocumentoETag.Should().Be("\"etag\"");
    }

    [Fact(DisplayName = "O veredicto verificado grava o SHA-256 e libera o código")]
    public void RegistrarDocumentoVerificado_EmVerificacao_GravaOHash()
    {
        Instalacao instalacao = EmVerificacao();

        instalacao.RegistrarDocumentoVerificado("abc123", Agora).Should().BeTrue();

        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.DocumentoVerificado);
        instalacao.DocumentoSha256.Should().Be("abc123");
        instalacao.DocumentoVerificadoEm.Should().Be(Agora);
    }

    [Fact(DisplayName = "A recusa ou o pedido de reenvio do verificador encerra o documento")]
    public void RegistrarDocumentoRecusado_EmVerificacao_EncerraODocumento()
    {
        Instalacao instalacao = EmVerificacao();

        instalacao.RegistrarDocumentoRecusado(Agora).Should().BeTrue();

        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.DocumentoRecusado);
        instalacao.DocumentoRecusadoEm.Should().Be(Agora);
        instalacao.Pendente.Should().BeFalse();
    }

    [Fact(DisplayName = "Resultado do verificador para instalação que já saiu da verificação é ignorado")]
    public void RegistrarResultado_ForaDaVerificacao_Ignora()
    {
        Instalacao verificada = Verificada();
        Instalacao recusada = EmVerificacao();
        recusada.RegistrarDocumentoRecusado(Agora);

        verificada.RegistrarDocumentoRecusado(Agora).Should().BeFalse();
        recusada.RegistrarDocumentoVerificado("abc123", Agora).Should().BeFalse();
        verificada.Situacao.Should().Be(SituacaoDaInstalacao.DocumentoVerificado);
        recusada.Situacao.Should().Be(SituacaoDaInstalacao.DocumentoRecusado);
    }

    [Fact(DisplayName = "O código só sai com o documento verificado")]
    public void EnviarCodigo_SemDocumentoVerificado_Recusa()
    {
        Instalacao instalacao = EmVerificacao();

        instalacao.EnviarCodigo(Resumo, Agora).Error!.Code.Should().Be(InstalacaoErrorCodes.SituacaoInvalida);
        instalacao.CodigoResumo.Should().BeNull();
    }

    [Fact(DisplayName = "O código enviado abre o prazo de 2 horas e zera as tentativas")]
    public void EnviarCodigo_DocumentoVerificado_AbreOPrazo()
    {
        Instalacao instalacao = Verificada();

        instalacao.EnviarCodigo(Resumo, Agora).IsSuccess.Should().BeTrue();

        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.AguardandoCodigo);
        instalacao.CodigoExpiraEm.Should().Be(Agora.AddHours(2));
        instalacao.CodigoResumo.Should().Equal(Resumo);
        instalacao.Tentativas.Should().Be(0);
    }

    [Fact(DisplayName = "O reenvio troca o resumo, reabre o prazo e mantém as tentativas já gastas")]
    public void EnviarCodigo_Reenvio_TrocaOResumoEReabreOPrazo()
    {
        Instalacao instalacao = AguardandoCodigo();
        instalacao.RegistrarTentativa(false, Agora.AddMinutes(10));
        byte[] novoResumo = [9, 9, 9];

        instalacao.EnviarCodigo(novoResumo, Agora.AddHours(3)).IsSuccess.Should().BeTrue();

        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.AguardandoCodigo);
        instalacao.CodigoResumo.Should().Equal(novoResumo);
        instalacao.CodigoEnviadoEm.Should().Be(Agora, "o instante do primeiro envio não muda no reenvio");
        instalacao.CodigoExpiraEm.Should().Be(Agora.AddHours(5));
        instalacao.Tentativas.Should().Be(1);
    }

    [Theory(DisplayName = "O reenvio é recusado depois do fim da instalação")]
    [InlineData("confirmada")]
    [InlineData("cancelada")]
    [InlineData("expirada")]
    public void EnviarCodigo_Reenvio_AposOFim_Recusa(string fim)
    {
        Instalacao instalacao = AguardandoCodigo();
        switch (fim)
        {
            case "confirmada":
                instalacao.Confirmar(Agora.AddMinutes(5));
                break;
            case "cancelada":
                instalacao.Cancelar(Agora.AddMinutes(5));
                break;
            default:
                instalacao.RegistrarTentativa(true, Agora.AddHours(2));
                break;
        }

        instalacao.EnviarCodigo(Resumo, Agora.AddHours(3)).Error!.Code.Should().Be(InstalacaoErrorCodes.SituacaoInvalida);
        instalacao.CodigoResumo.Should().BeNull();
    }

    [Fact(DisplayName = "Código certo dentro do prazo é aceito e a confirmação descarta o resumo")]
    public void Confirmar_CodigoCerto_ConfirmaEDescartaOResumo()
    {
        Instalacao instalacao = AguardandoCodigo();

        instalacao.RegistrarTentativa(true, Agora.AddMinutes(30)).Should().Be(ResultadoDaTentativaDeInstalacao.Aceita);
        instalacao.Confirmar(Agora.AddMinutes(30));

        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.Confirmada);
        instalacao.ConfirmadaEm.Should().Be(Agora.AddMinutes(30));
        instalacao.CodigoResumo.Should().BeNull();
    }

    [Fact(DisplayName = "Código informado depois das 2 horas expira a instalação")]
    public void RegistrarTentativa_PrazoVencido_Expira()
    {
        Instalacao instalacao = AguardandoCodigo();

        instalacao.RegistrarTentativa(true, Agora.AddHours(2)).Should().Be(ResultadoDaTentativaDeInstalacao.Expirada);

        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.Expirada);
        instalacao.Pendente.Should().BeFalse();
        instalacao.CodigoResumo.Should().BeNull();
    }

    [Fact(DisplayName = "Código errado consome uma tentativa e a quinta cancela a instalação")]
    public void RegistrarTentativa_QuintoCodigoErrado_Cancela()
    {
        Instalacao instalacao = AguardandoCodigo();

        for (int i = 1; i < Instalacao.MaximoDeTentativas; i++)
        {
            instalacao.RegistrarTentativa(false, Agora.AddMinutes(i)).Should().Be(ResultadoDaTentativaDeInstalacao.CodigoErrado);
            instalacao.Situacao.Should().Be(SituacaoDaInstalacao.AguardandoCodigo);
        }

        instalacao.RegistrarTentativa(false, Agora.AddMinutes(10)).Should().Be(ResultadoDaTentativaDeInstalacao.CodigoErrado);

        instalacao.Tentativas.Should().Be(Instalacao.MaximoDeTentativas);
        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.Cancelada);
        instalacao.CodigoResumo.Should().BeNull();
    }

    [Fact(DisplayName = "Cancelar encerra a pendência e instalação terminal não se cancela")]
    public void Cancelar_Pendente_EncerraETerminalNaoCancela()
    {
        Instalacao instalacao = Verificada();

        instalacao.Cancelar(Agora.AddMinutes(5));

        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.Cancelada);
        instalacao.CanceladaEm.Should().Be(Agora.AddMinutes(5));
        FluentActions.Invoking(() => instalacao.Cancelar(Agora.AddMinutes(6))).Should().Throw<InvalidOperationException>();
    }

    private static IdentidadeInformada[] Gestores(params string[] subs) =>
        [.. subs.Select(sub => new IdentidadeInformada(Emissor, sub))];

    private static Instalacao Iniciada() =>
        Instalacao.Iniciar(Paulo, Gestores(SubDiogo, SubIsabella), Emissor, "123", 1024, Agora).Value!;

    private static Instalacao EmVerificacao()
    {
        Instalacao instalacao = Iniciada();
        instalacao.ConfirmarDocumento("\"etag\"").IsSuccess.Should().BeTrue();
        return instalacao;
    }

    private static Instalacao Verificada()
    {
        Instalacao instalacao = EmVerificacao();
        instalacao.RegistrarDocumentoVerificado("abc123", Agora);
        return instalacao;
    }

    private static Instalacao AguardandoCodigo()
    {
        Instalacao instalacao = Verificada();
        instalacao.EnviarCodigo(Resumo, Agora).IsSuccess.Should().BeTrue();
        return instalacao;
    }

    private static string[] CodigosDaRecusa(Result<Instalacao> resultado)
    {
        resultado.IsFailure.Should().BeTrue();
        return [.. resultado.Errors.Select(e => e.Error.Code)];
    }

    private static string[] CodigosDaRecusa(Result resultado)
    {
        resultado.IsFailure.Should().BeTrue();
        return [.. resultado.Errors.Select(e => e.Error.Code)];
    }
}
