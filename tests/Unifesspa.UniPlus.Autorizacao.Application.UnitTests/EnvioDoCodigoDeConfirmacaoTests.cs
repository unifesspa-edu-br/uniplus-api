namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

public sealed class EnvioDoCodigoDeConfirmacaoTests
{
    private const string EmailInstitucional = "paulo@uniplus-test.local";

    [Fact(DisplayName = "O primeiro envio guarda o resumo, grava uma vez e devolve o e-mail como mensagem de saída")]
    public async Task PrimeiroEnvio_GravaEDevolveOEmail()
    {
        Instalacao instalacao = Verificada();
        UnidadeDeTrabalho unidade = new();
        RepositorioDoEnvio instalacoes = new(instalacao);

        (Result<InstalacaoDto> resposta, IEnumerable<object> mensagens) = await EnviarAsync(instalacoes, unidade, Pessoa(EmailInstitucional, verificado: true));

        resposta.IsSuccess.Should().BeTrue();
        mensagens.Should().ContainSingle().Which.Should().BeOfType<EnviarEmail>();
        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.AguardandoCodigo);
        unidade.Gravacoes.Should().Be(1);
        instalacoes.Tentativa.Should().BeNull();
    }

    [Fact(DisplayName = "O reenvio registra a tentativa de reenvio na mesma gravação")]
    public async Task Reenvio_RegistraATentativa()
    {
        UnidadeDeTrabalho unidade = new();
        RepositorioDoEnvio instalacoes = new(AguardandoCodigo());

        (Result<InstalacaoDto> resposta, _) = await EnviarAsync(instalacoes, unidade, Pessoa(EmailInstitucional, verificado: true));

        resposta.IsSuccess.Should().BeTrue();
        instalacoes.Tentativa!.Resultado.Should().Be(ResultadoDaTentativaDeInstalacao.CodigoReenviado);
        unidade.Gravacoes.Should().Be(1);
    }

    [Theory(DisplayName = "O código só vai a e-mail verificado de domínio institucional, e a recusa não grava")]
    [InlineData("paulo@gmail.com", true)]
    [InlineData(EmailInstitucional, false)]
    [InlineData(null, true)]
    public async Task EmailNaoInstitucional_RecusaSemGravar(string? email, bool verificado)
    {
        UnidadeDeTrabalho unidade = new();

        (Result<InstalacaoDto> resposta, IEnumerable<object> mensagens) = await EnviarAsync(new RepositorioDoEnvio(Verificada()), unidade, Pessoa(email, verificado));

        resposta.Error!.Code.Should().Be(InstalacaoErrorCodes.EmailNaoInstitucional);
        mensagens.Should().BeEmpty();
        unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "A situação que não aceita o envio é recusada antes do e-mail")]
    public async Task SituacaoInvalida_VenceOEmail()
    {
        (Result<InstalacaoDto> resposta, _) = await EnviarAsync(new RepositorioDoEnvio(Iniciada()), new UnidadeDeTrabalho(), Pessoa("paulo@gmail.com", verificado: false));

        resposta.Error!.Code.Should().Be(InstalacaoErrorCodes.SituacaoInvalida);
    }

    [Fact(DisplayName = "A instalação que não é do executor responde como inexistente")]
    public async Task InstalacaoInexistente_Recusa()
    {
        (Result<InstalacaoDto> resposta, _) = await EnviarAsync(new RepositorioDoEnvio(null), new UnidadeDeTrabalho(), Pessoa(EmailInstitucional, verificado: true));

        resposta.Error!.Code.Should().Be(InstalacaoErrorCodes.NaoEncontrada);
    }

    [Fact(DisplayName = "O conflito de concorrência na gravação descarta o rastreamento e recusa")]
    public async Task ConflitoNaGravacao_DescartaERecusa()
    {
        UnidadeDeTrabalho unidade = UnidadeDeTrabalho.ComConflito();

        (Result<InstalacaoDto> resposta, _) = await EnviarAsync(new RepositorioDoEnvio(Verificada()), unidade, Pessoa(EmailInstitucional, verificado: true));

        resposta.Error!.Code.Should().Be(InstalacaoErrorCodes.ConflitoDeConcorrencia);
        unidade.Descartou.Should().BeTrue();
    }

    private static PessoaNoToken Pessoa(string? email, bool verificado) => new(Executor, null, email, verificado);

    private static Instalacao Verificada()
    {
        Instalacao instalacao = Iniciada();
        instalacao.ConfirmarDocumento("etag");
        instalacao.RegistrarDocumentoVerificado("sha", DateTimeOffset.UtcNow);

        return instalacao;
    }

    private static Task<(Result<InstalacaoDto> Resposta, IEnumerable<object> Mensagens)> EnviarAsync(
        RepositorioSemInstalacao instalacoes, UnidadeDeTrabalho unidade, PessoaNoToken pessoa) =>
        new EnvioDoCodigoDeConfirmacao(instalacoes, new ResumoFixo(), unidade, new DominiosDeTeste(), TimeProvider.System)
            .EnviarAsync(Guid.NewGuid(), pessoa, CancellationToken.None);

    private sealed class RepositorioDoEnvio(Instalacao? instalacao) : RepositorioSemInstalacao
    {
        public TentativaDeInstalacao? Tentativa { get; private set; }

        public override Task<Instalacao?> ObterDoExecutorAsync(Guid id, Identidade executor, CancellationToken cancellationToken = default) =>
            Task.FromResult<Instalacao?>(instalacao);

        public override Task AdicionarTentativaAsync(TentativaDeInstalacao tentativa, CancellationToken cancellationToken = default)
        {
            Tentativa = tentativa;
            return Task.CompletedTask;
        }
    }
}
