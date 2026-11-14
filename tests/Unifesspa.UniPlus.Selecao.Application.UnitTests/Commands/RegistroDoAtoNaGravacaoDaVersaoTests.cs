namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;

/// <summary>
/// O ato que cria uma versão passa pela porta de escrita do registro central dentro da mesma transação que grava a
/// versão: a recusa da porta e o conflito que só a gravação enxerga devolvem a recusa nomeada, descartam o que foi
/// rastreado e não deixam evento, versão, ato nem divulgação para trás.
/// </summary>
public sealed class RegistroDoAtoNaGravacaoDaVersaoTests
{
    private const string IndiceDoNumeroDaVersao = "ux_versoes_configuracao_processo_numero";
    private const string IndiceDoIdentificadorLegivelDivulgado = "ux_certames_divulgados_identificador_legivel";
    private const string IndiceDaVagaDoObjeto = "ux_vagas_de_linhagem_objeto_tipo";

    private static readonly DomainError AtoJaRetificado = new("AtoNormativo.AtoJaRetificado", "O ato EDITAL nº 1/2026 já foi retificado.");

    public static TheoryData<OperacaoQueCongela> Atos() =>
        [OperacaoQueCongela.Publicar, OperacaoQueCongela.Retificar, OperacaoQueCongela.FecharRetificacao];

    [Theory(DisplayName = "A porta é chamada depois do ato do processo e antes da divulgação, e a remoção do rascunho e a gravação única vêm depois dela")]
    [MemberData(nameof(Atos))]
    public async Task Sucesso_ChamaAPortaNaOrdemDaTransacao(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        Received.InOrder(() =>
        {
            _ = cenario.Servicos.Repositorio.AdicionarVersaoConfiguracaoAsync(Arg.Any<VersaoConfiguracao>(), Arg.Any<CancellationToken>());
            _ = cenario.Servicos.Atos.AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
            _ = cenario.Servicos.Registro.RegistrarAsync(Arg.Any<AtoNormativoPublicado>(), Arg.Any<CancellationToken>());
            _ = cenario.Servicos.Certames.ObterParaMutacaoAsync(cenario.Processo.Id, Arg.Any<CancellationToken>());
            _ = cenario.Servicos.Rascunhos.RemoverDoProcessoAsync(cenario.Processo.Id, Arg.Any<CancellationToken>());
            _ = cenario.Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Theory(DisplayName = "A porta recebe o ato da versão gravada, com o instante, a versão do objeto, o vínculo e a retificação")]
    [MemberData(nameof(Atos))]
    public async Task Sucesso_EntregaAPortaOAtoDaVersao(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);

        (VersaoConfiguracao gravada, AtoNormativoPublicado entregue) = await ExecutarCapturandoAsync(cenario);

        entregue.AtoId.Should().Be(gravada.AtoCriadorId);
        entregue.PublicadoEm.Should().Be(gravada.VigenteAPartirDe);
        entregue.VersaoDoObjeto.Should().Be(new VersaoDoObjetoDoAto(gravada.Id, gravada.HashConfiguracao));
        entregue.Vinculos.Should().ContainSingle().Which.Should().Be(
            new VinculoDoAtoPublicado(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, cenario.Processo.Id));
        (entregue.Retificacao is null).Should().Be(operacao == OperacaoQueCongela.Publicar);
    }

    private static async Task<(VersaoConfiguracao Gravada, AtoNormativoPublicado Entregue)> ExecutarCapturandoAsync(CenarioDeRecusaNoCongelamento cenario)
    {
        VersaoConfiguracao? gravada = null;
        AtoNormativoPublicado? entregue = null;
        cenario.Servicos.Repositorio.AdicionarVersaoConfiguracaoAsync(Arg.Do<VersaoConfiguracao>(v => gravada = v), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        cenario.Servicos.Registro.RegistrarAsync(Arg.Do<AtoNormativoPublicado>(a => entregue = a), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await cenario.ExecutarAsync();

        return (gravada!, entregue!);
    }

    [Theory(DisplayName = "A recusa da porta devolve a recusa nomeada, descarta o rastreado e não deixa evento nem remove o rascunho")]
    [MemberData(nameof(Atos))]
    public async Task RecusaDaPorta_DescartaESemEvento(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        cenario.Servicos.Registro.RegistrarAsync(Arg.Any<AtoNormativoPublicado>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(AtoJaRetificado));

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Error.Should().Be(AtoJaRetificado);
        eventos.Should().BeEmpty();
        cenario.Servicos.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.Servicos.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cenario.Servicos.Rascunhos.DidNotReceive().RemoverDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "A recusa da porta vem antes da divulgação: o certame não é montado")]
    [MemberData(nameof(Atos))]
    public async Task RecusaDaPorta_NaoMontaADivulgacao(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        cenario.Servicos.Registro.RegistrarAsync(Arg.Any<AtoNormativoPublicado>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(AtoJaRetificado));

        await cenario.ExecutarAsync();

        await cenario.Servicos.Certames.DidNotReceive().ObterParaMutacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "O conflito que só a gravação enxerga e a porta reconhece devolve a recusa da porta e descarta o rastreado")]
    [MemberData(nameof(Atos))]
    public async Task ConflitoReconhecidoPelaPorta_DevolveARecusaDaPorta(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        Exception violacao = ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade(IndiceDaVagaDoObjeto);
        cenario.Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(violacao);
        cenario.Servicos.Registro.TraduzirConflitoDaGravacao(violacao).Returns(AtoJaRetificado);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Error.Should().Be(AtoJaRetificado);
        eventos.Should().BeEmpty();
        cenario.Servicos.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Theory(DisplayName = "A exceção que nem a Seleção nem a porta reconhecem propaga ao Wolverine")]
    [MemberData(nameof(Atos))]
    public async Task ExcecaoNaoReconhecida_Propaga(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        cenario.Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Conexão perdida."));

        Func<Task> executar = cenario.ExecutarAsync;

        await executar.Should().ThrowAsync<InvalidOperationException>();
        cenario.Servicos.Registro.Received(1).TraduzirConflitoDaGravacao(Arg.Any<InvalidOperationException>());
    }

    [Theory(DisplayName = "O conflito da versão e o do identificador legível são da Seleção, e a porta nem é consultada")]
    [InlineData(IndiceDoNumeroDaVersao, "VersaoConfiguracao.NumeroDuplicado")]
    [InlineData(IndiceDoIdentificadorLegivelDivulgado, "ProcessoSeletivo.IdentificadorLegivelEmUso")]
    public async Task ConflitoDaSelecao_NaoConsultaAPorta(string constraint, string codigoEsperado)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Publicar);
        cenario.Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade(constraint));

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be(codigoEsperado);
        eventos.Should().BeEmpty();
        cenario.Servicos.Registro.DidNotReceive().TraduzirConflitoDaGravacao(Arg.Any<Exception>());
    }
}
