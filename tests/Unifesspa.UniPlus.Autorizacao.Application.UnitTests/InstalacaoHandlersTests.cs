namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Verificador.Contratos;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

public sealed class InstalacaoHandlersTests
{
    [Fact(DisplayName = "O código de confirmação tem oito dígitos")]
    public void Gerar_TemOitoDigitos()
    {
        for (int i = 0; i < 50; i++)
        {
            CodigoDeConfirmacao.TemAForma(CodigoDeConfirmacao.Gerar()).Should().BeTrue();
        }
    }

    [Theory(DisplayName = "Só texto com a forma de código chega ao cálculo do resumo")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("1234567a")]
    [InlineData("１２３４５６７８")]
    public void TemAForma_FormaInvalida_Recusa(string? texto)
    {
        CodigoDeConfirmacao.TemAForma(texto).Should().BeFalse();
    }

    [Theory(DisplayName = "O veredicto indefinido, ou verificado sem o SHA-256, é defeito do contrato")]
    [InlineData(VeredictoDaVerificacao.NaoDefinido, null)]
    [InlineData(VeredictoDaVerificacao.Verificado, null)]
    [InlineData(VeredictoDaVerificacao.Verificado, "  ")]
    public async Task ResultadoMalformado_LancaAntesDeTocarNoRepositorio(VeredictoDaVerificacao veredicto, string? hash)
    {
        ResultadoDaVerificacaoDeArquivo resultado = new(Guid.NewGuid(), veredicto, MotivoDaRecusa.NaoSeAplica, hash, null, null);

        Func<Task> acao = () => RegistrarResultadoAsync(resultado, new RepositorioSemInstalacao());

        await acao.Should().ThrowAsync<ResultadoDaVerificacaoInvalidoException>();
    }

    [Fact(DisplayName = "Resultado de instalação que não existe é descartado sem gravar")]
    public async Task ResultadoDeInstalacaoInexistente_Descarta()
    {
        ResultadoDaVerificacaoDeArquivo resultado = new(
            Guid.NewGuid(), VeredictoDaVerificacao.Recusado, MotivoDaRecusa.PdfComSenha, null, null, null);

        await RegistrarResultadoAsync(resultado, new RepositorioSemInstalacao());
    }

    [Fact(DisplayName = "Resultado repetido de instalação que já saiu da verificação é ignorado sem gravar")]
    public async Task ResultadoRepetido_Ignora()
    {
        Instalacao instalacao = Iniciada();
        ResultadoDaVerificacaoDeArquivo resultado = new(
            instalacao.Id, VeredictoDaVerificacao.Recusado, MotivoDaRecusa.PdfComSenha, null, null, null);

        await RegistrarResultadoAsync(resultado, new RepositorioComInstalacao(instalacao));

        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.AguardandoDocumento);
    }

    [Fact(DisplayName = "O pedido ao verificador usa o tamanho declarado do PDF como limite")]
    public async Task ConfirmarDocumento_PedidoUsaOTamanhoDeclaradoComoLimite()
    {
        Instalacao instalacao = Iniciada(tamanho: 2048);

        (Result<InstalacaoDto> resposta, IEnumerable<object> mensagens) = await ConfirmarDocumentoDaInstalacaoCommandHandler.Handle(
            new ConfirmarDocumentoDaInstalacaoCommand(instalacao.Id, "etag"), new RepositorioDoExecutor(instalacao), new UnidadeDeTrabalho(), new Usuario(Executor), CancellationToken.None);

        resposta.IsSuccess.Should().BeTrue();
        mensagens.Should().ContainSingle().Which.Should().BeOfType<PedidoDeVerificacaoDeArquivo>()
            .Which.TamanhoMaximoBytes.Should().Be(2048);
    }

    [Fact(DisplayName = "Iniciar de novo cancela a pendência anterior e grava a nova numa única gravação")]
    public async Task Iniciar_ComPendenciaAnterior_GravaCancelamentoENovaJuntos()
    {
        Instalacao anterior = Iniciada();
        RepositorioComPendencia instalacoes = new(anterior);
        UnidadeDeTrabalho unidade = new();

        Result<InstalacaoIniciadaDto> resultado = await IniciarAsync(Gestores(), instalacoes, unidade);

        resultado.IsSuccess.Should().BeTrue();
        anterior.Situacao.Should().Be(SituacaoDaInstalacao.Cancelada);
        instalacoes.Adicionada.Should().NotBeNull();
        unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Gestor com e-mail fora do domínio institucional recusa o início, sem gravar nada")]
    public async Task Iniciar_GestorComEmailNaoInstitucional_Recusa()
    {
        UnidadeDeTrabalho unidade = new();
        IdentidadeInformada[] gestores = [new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@gmail.com"), Gestores()[1]];

        Result<InstalacaoIniciadaDto> resultado = await IniciarAsync(gestores, new RepositorioComPendencia(null), unidade);

        resultado.Errors.Single().Error.Code.Should().Be(InstalacaoErrorCodes.GestoresInvalidos);
        unidade.Gravacoes.Should().Be(0);
    }

    private static Task<IEnumerable<EnviarEmail>> RegistrarResultadoAsync(ResultadoDaVerificacaoDeArquivo resultado, RepositorioSemInstalacao instalacoes) =>
        RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler.Handle(
            resultado, DoublesDeAvisos.Receptores(instalacoes, new PedidosComAnexo(null), new UnidadeDeTrabalhoIntocavel()), NullLogger.Instance, CancellationToken.None);

    private static Task<Result<InstalacaoIniciadaDto>> IniciarAsync(
        IdentidadeInformada[] gestores, RepositorioComPendencia instalacoes, UnidadeDeTrabalho unidade) =>
        IniciarInstalacaoCommandHandler.Handle(
            new IniciarInstalacaoCommand(gestores, "456", 2048),
            instalacoes,
            new ConcessoesDaInstalacao(gestoresAtivos: 0, travas: []),
            new UrlFixa(),
            new DominiosDeTeste(),
            unidade,
            new Usuario(Executor),
            TimeProvider.System,
            CancellationToken.None);
}
