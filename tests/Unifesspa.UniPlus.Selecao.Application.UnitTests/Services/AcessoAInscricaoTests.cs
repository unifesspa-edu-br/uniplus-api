namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O acesso à inscrição confere o vínculo sem decidir, decide sobre o resource que a rota monta e traduz a recusa num lugar
/// só. A inscrição é dado pessoal em qualquer rota.
/// </summary>
public sealed class AcessoAInscricaoTests
{
    private static readonly Guid Processo = Guid.CreateVersion7();
    private static readonly Guid Unidade = Guid.CreateVersion7();
    private static readonly Guid Inscricao = Guid.CreateVersion7();

    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IInscricaoRepository _inscricoes = Substitute.For<IInscricaoRepository>();

    public AcessoAInscricaoTests() =>
        _processos.ObterUnidadeAdministradoraAsync(Processo, Arg.Any<CancellationToken>()).Returns(Unidade);

    [Fact(DisplayName = "A inscrição enviada deste processo é confirmada com a unidade dona")]
    public async Task ConferirVinculo_Enviada_ConfirmaComAUnidade()
    {
        Vincular(Processo, SituacaoInscricao.Submetida);

        ConferenciaDoVinculo conferencia = await AcessoAInscricao.ConferirVinculoAsync(_processos, _inscricoes, Processo, Inscricao, default);

        conferencia.Should().BeOfType<Vinculo>().Which.UnidadeId.Should().Be(Unidade);
    }

    [Theory(DisplayName = "A inscrição de outro processo, a em rascunho e a que não existe não são encontradas")]
    [InlineData(false, SituacaoInscricao.Submetida)]
    [InlineData(true, SituacaoInscricao.Rascunho)]
    [InlineData(true, null)]
    public async Task ConferirVinculo_ForaDoAlcance_NaoEncontra(bool doProcesso, SituacaoInscricao? situacao)
    {
        if (situacao is { } existente)
        {
            Vincular(doProcesso ? Processo : Guid.CreateVersion7(), existente);
        }

        ConferenciaDoVinculo conferencia = await AcessoAInscricao.ConferirVinculoAsync(_processos, _inscricoes, Processo, Inscricao, default);

        conferencia.Should().Be(new NaoEncontrada(Unidade));
    }

    [Fact(DisplayName = "O processo inexistente não tem unidade dona, e a inscrição não é encontrada")]
    public async Task ConferirVinculo_ProcessoInexistente_NaoEncontra()
    {
        Vincular(Processo, SituacaoInscricao.Submetida);
        _processos.ObterUnidadeAdministradoraAsync(Processo, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        (await AcessoAInscricao.ConferirVinculoAsync(_processos, _inscricoes, Processo, Inscricao, default))
            .Should().Be(new NaoEncontrada(null));
    }

    [Theory(DisplayName = "Decidir devolve permitido ou a recusa com o desfecho que o ponto de decisão deu")]
    [InlineData(ResultadoDoAcesso.Permitido)]
    [InlineData(ResultadoDoAcesso.Negado)]
    [InlineData(ResultadoDoAcesso.Impedido)]
    [InlineData(ResultadoDoAcesso.IdentidadeIncompleta)]
    public async Task Decidir_TraduzODesfecho(ResultadoDoAcesso desfecho)
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(desfecho);
        Vinculo vinculo = UmVinculo(Processo);

        AcessoNaInscricao acesso = await AcessoAInscricao.DecidirAsync(
            verificador, vinculo, PermissaoSelecao.InscricoesConsultar.Requisito,
            AcessoAoProcesso.ResourceDaInscricao(Unidade, Processo, Inscricao), default);

        if (desfecho == ResultadoDoAcesso.Permitido)
        {
            acesso.Should().BeSameAs(Permitido.Instancia);
        }
        else
        {
            acesso.Should().BeOfType<Recusada>().Which.Resultado.Should().Be(desfecho);
        }
    }

    [Theory(DisplayName = "A recusa por falta de permissão responde o erro de não encontrada da rota; identidade e impedimento respondem o que são")]
    [InlineData(ResultadoDoAcesso.Negado, "Inscricao.NaoEncontrada")]
    [InlineData(ResultadoDoAcesso.IdentidadeIncompleta, ProcessoSeletivoErrorCodes.IdentidadeIncompleta)]
    [InlineData(ResultadoDoAcesso.Impedido, ProcessoSeletivoErrorCodes.ImpedidoPorInscricaoNoProcesso)]
    public void Erro_TraduzARecusa(ResultadoDoAcesso desfecho, string codigo)
    {
        DomainError naoEncontrada = new("Inscricao.NaoEncontrada", "Inscrição não encontrada neste processo seletivo.");

        AcessoAInscricao.Erro(new Recusada(desfecho), naoEncontrada).Code.Should().Be(codigo);
    }

    [Fact(DisplayName = "A inscrição é dado pessoal em todo resource que a nomeia: o da listagem, o do campo e o do documento")]
    public void Resources_DaInscricao_SaoPessoais()
    {
        AcessoAoProcesso.ResourceDaInscricao(Unidade, Processo, inscricaoId: null).Sensibilidade.Should().Be(ClassificacaoDeProtecao.Pessoal);
        AcessoAoProcesso.ResourceDaInscricao(Unidade, Processo, Inscricao).InscricaoId.Should().Be(Inscricao);
        AcessoAoProcesso.ResourceDoCampo(Unidade, Processo, Inscricao, "CPF", ocorrencia: null).Sensibilidade
            .Should().Be(ClassificacaoDeProtecao.Pessoal);
        AcessoAoProcesso.ResourceDoDocumento(Unidade, Processo, Inscricao, Guid.CreateVersion7(), ClassificacaoDeProtecao.Publico)
            .Sensibilidade.Should().Be(ClassificacaoDeProtecao.Publico, "o documento vale pela classificação congelada da exigência");
    }

    private void Vincular(Guid processo, SituacaoInscricao situacao) =>
        _inscricoes.ObterVinculoAsync(Inscricao, Arg.Any<CancellationToken>())
            .Returns(new VinculoDaInscricao(Inscricao, processo, situacao, Guid.CreateVersion7(), null, null));

    private static Vinculo UmVinculo(Guid processo) =>
        new(Unidade, new VinculoDaInscricao(Inscricao, processo, SituacaoInscricao.Submetida, Guid.CreateVersion7(), null, null));
}
