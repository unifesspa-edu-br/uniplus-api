namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>
/// O prazo da pendente é materializado sob a trava em toda operação sobre o perfil, e a expiração grava ainda que a
/// operação seguinte recuse.
/// </summary>
public sealed class PrazoDaVersaoTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Descricao = "Descrição válida.";

    private static readonly CatalogoDeTeste Catalogo = new();
    private static readonly DateTimeOffset Prazo = PerfisDeTeste.Instante.AddDays(15);

    [Fact(DisplayName = "Confirmar o envio depois do prazo é recusado e grava a expiração")]
    public async Task Confirmar_DepoisDoPrazo_RecusaEGravaAExpiracao()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil, Prazo.AddDays(1));

        Result<DocumentoDaVersao> resultado = await cenario.Documentos.ConfirmarAsync(perfil.Id, perfil.Versoes.Single().Id, "etag", CancellationToken.None);

        resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
        perfil.Versoes.Single().Situacao.Should().Be(SituacaoDaVersao.Expirada);
        cenario.Unidade.Gravacoes.Should().Be(1);
        cenario.Unidade.Descartes.Should().Be(0);
    }

    [Fact(DisplayName = "No instante exato do prazo a pendente já expirou; um instante antes ela ainda vale")]
    public async Task Confirmar_NoInstanteDoPrazo_Expira()
    {
        Perfil noPrazo = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Perfil antes = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);

        Result<DocumentoDaVersao> expirada = await new Cenario(noPrazo, Prazo).Documentos
            .ConfirmarAsync(noPrazo.Id, noPrazo.Versoes.Single().Id, "etag", CancellationToken.None);
        Result<DocumentoDaVersao> valida = await new Cenario(antes, Prazo.AddTicks(-1)).Documentos
            .ConfirmarAsync(antes.Id, antes.Versoes.Single().Id, "etag", CancellationToken.None);

        expirada.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
        valida.IsSuccess.Should().BeTrue();
        antes.Versoes.Single().Situacao.Should().Be(SituacaoDaVersao.Pendente);
    }

    [Fact(DisplayName = "Outro documento e nova URL de envio para a versão vencida são recusados e gravam a expiração")]
    public async Task NovoEReemitir_DepoisDoPrazo_RecusamEGravam()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil, Prazo);
        Guid versaoId = perfil.Versoes.Single().Id;

        Result<EnvioDoDocumentoDto> reemissao = await cenario.Documentos.ReemitirEnvioAsync(perfil.Id, versaoId, CancellationToken.None);
        Result<EnvioDoDocumentoDto> novo = await cenario.Documentos.NovoAsync(perfil.Id, versaoId, 4096, CancellationToken.None);

        reemissao.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
        novo.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.VersaoNaoPendente);
        perfil.Versoes.Single().Situacao.Should().Be(SituacaoDaVersao.Expirada);
        cenario.Armazenamento.ChavesDeEnvio.Should().BeEmpty();
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Criar versão recusada depois do prazo grava a expiração da pendente")]
    public async Task CriarVersao_RecusadaDepoisDoPrazo_GravaAExpiracao()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        perfil.Desativar();
        Cenario cenario = new(perfil, Prazo.AddDays(1));

        Result<EnvioDoDocumentoDto> resultado = await cenario.Versoes.CriarVersaoAsync(
            perfil.Id,
            new DadosDaVersaoRequest("Analista de apoio", Descricao, [Consultar], PerfisDeTeste.Protocolo, PerfisDeTeste.TamanhoDoDocumento),
            CancellationToken.None);

        resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.Inativo);
        perfil.Versoes.Single().Situacao.Should().Be(SituacaoDaVersao.Expirada);
        cenario.Unidade.Gravacoes.Should().Be(1);
        cenario.Unidade.Descartes.Should().Be(0);
    }

    [Fact(DisplayName = "A falha de um perfil na rotina de expiração não impede os demais")]
    public async Task Rotina_FalhaDeUmPerfil_NaoImpedeOsDemais()
    {
        Perfil falha = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Perfil expira = PerfisDeTeste.Pendente("Analista de apoio", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(new PerfisNaMemoria(falha, expira), Prazo.AddDays(1));

        int expiradas = await cenario.Expiracao(falha.Id).ExecutarAsync(CancellationToken.None);

        expiradas.Should().Be(1);
        expira.VersaoPendente.Should().BeNull();
        falha.VersaoPendente.Should().NotBeNull();
    }

    private sealed class Cenario
    {
        private readonly PerfisNaMemoria _perfis;
        private readonly OperacaoSobreOPerfil _operacao;
        private readonly TimeProvider _relogio;

        public Cenario(Perfil perfil, DateTimeOffset agora)
            : this(new PerfisNaMemoria(perfil), agora)
        {
        }

        public Cenario(PerfisNaMemoria perfis, DateTimeOffset agora)
        {
            _perfis = perfis;
            _relogio = new CenarioDaGovernancaDaVersao.RelogioFixo(agora);
            Unidade = new UnidadeDeTrabalho();
            Armazenamento = new Armazenamento();
            _operacao = new OperacaoSobreOPerfil(_perfis, Unidade, _relogio);
            AutoriaDaOperacao autoria = new(new Usuario(PerfisDeTeste.Autor), _relogio, new ConcessoesNaMemoria());
            EnvioDoDocumento envio = new(Armazenamento, _relogio);
            Documentos = new DocumentosDasVersoes(_operacao, envio, autoria, _perfis, Armazenamento, Unidade);
            Versoes = new VersoesDoPerfil(_operacao, new LeitorDoContextoDaVersao(_perfis, new ConcessoesNaMemoria()), envio, autoria, Catalogo);
        }

        public UnidadeDeTrabalho Unidade { get; }

        public Armazenamento Armazenamento { get; }

        public DocumentosDasVersoes Documentos { get; }

        public VersoesDoPerfil Versoes { get; }

        public ExpiracaoDasVersoesPendentes Expiracao(Guid perfilQueFalha) =>
            new(_perfis, new BarramentoQueFalhaNoPerfil(_operacao, perfilQueFalha), _relogio, NullLogger<ExpiracaoDasVersoesPendentes>.Instance);
    }

    private sealed class BarramentoQueFalhaNoPerfil(OperacaoSobreOPerfil operacao, Guid perfilQueFalha) : ICommandBus
    {
        public async Task<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct = default)
        {
            ExpirarVersaoPendenteDoPerfilCommand expirar = (ExpirarVersaoPendenteDoPerfilCommand)(object)command;

            return expirar.PerfilId == perfilQueFalha
                ? throw new InvalidOperationException("Falha simulada.")
                : (TResponse)(object)await ExpirarVersaoPendenteDoPerfilCommandHandler.Handle(expirar, operacao, ct);
        }
    }
}
