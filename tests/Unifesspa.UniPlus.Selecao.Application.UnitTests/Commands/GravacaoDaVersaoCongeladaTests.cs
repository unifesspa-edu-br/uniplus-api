namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// O que a gravação da versão congelada escreve, em que ordem, e o que faz quando a escrita é recusada: a versão e o
/// ato nascem juntos, a remoção do rascunho entra no rastreador antes do único flush, e a recusa não deixa a transcrição do operador para trás.
/// </summary>
public sealed class GravacaoDaVersaoCongeladaTests
{
    private const string IndiceDoNumeroDaVersao = "ux_versoes_configuracao_processo_numero";

    [Fact(DisplayName = "No sucesso grava a versão e o ato, remove o rascunho no rastreador, salva uma vez e devolve os eventos")]
    public async Task Sucesso_GravaRemoveORascunhoESalvaNessaOrdem()
    {
        Preparo preparo = await Preparo.NovoAsync();

        (Result resposta, IEnumerable<object> eventos) = await preparo.GravarAsync();

        resposta.IsSuccess.Should().BeTrue();
        eventos.Should().NotBeEmpty();
        Received.InOrder(() =>
        {
            _ = preparo.Servicos.Repositorio.AdicionarVersaoConfiguracaoAsync(preparo.Versao, Arg.Any<CancellationToken>());
            _ = preparo.Servicos.Atos.AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
            _ = preparo.Servicos.Certames.AdicionarAsync(Arg.Any<CertameDivulgado>(), Arg.Any<CancellationToken>());
            _ = preparo.Servicos.Rascunhos.RemoverDoProcessoAsync(preparo.Processo.Id, Arg.Any<CancellationToken>());
            _ = preparo.Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "A divulgação grava com o instante da versão e do ato, e a entrada do ato nasce sem endereço")]
    public async Task Sucesso_ADivulgacaoVaiNaMesmaGravacaoComOMesmoInstante()
    {
        Preparo preparo = await Preparo.NovoAsync();
        CertameDivulgado? divulgado = null;
        await preparo.Servicos.Certames.AdicionarAsync(Arg.Do<CertameDivulgado>(c => divulgado = c), Arg.Any<CancellationToken>());

        await preparo.GravarAsync();

        divulgado.Should().NotBeNull();
        divulgado!.DivulgadoEm.Should().Be(preparo.Versao.VigenteAPartirDe).And.Be(preparo.AtoGravado().PublicadoEm);
        divulgado.AtosDivulgados.Should().ContainSingle()
            .Which.Should().Be(AtoDivulgado.SemEndereco(preparo.Versao.AtoCriadorId, preparo.Versao.NumeroVersao));
        await preparo.Servicos.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O identificador legível já divulgado recusa a publicação e devolve só a recusa")]
    public async Task IdentificadorLegivelEmUso_RecusaSemEventos()
    {
        Preparo preparo = await Preparo.NovoAsync();
        preparo.FalharNoFlushCom(ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade("ux_certames_divulgados_identificador_legivel"));

        (Result resposta, IEnumerable<object> eventos) = await preparo.GravarAsync();

        resposta.Error!.Code.Should().Be("ProcessoSeletivo.IdentificadorLegivelEmUso");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O ato gravado tem a identidade da versão, o processo e o documento do edital")]
    public async Task Sucesso_OAtoGravadoReferenciaAVersao()
    {
        Preparo preparo = await Preparo.NovoAsync();

        await preparo.GravarAsync();

        AtoDoProcesso ato = preparo.AtoGravado();
        ato.Id.Should().Be(preparo.Versao.AtoCriadorId);
        ato.ProcessoSeletivoId.Should().Be(preparo.Processo.Id);
        ato.DocumentoPrincipalId.Should().Be(preparo.Conferidos.Documento.Id);
        ato.AtoCorrigidoId.Should().BeNull();
    }

    [Fact(DisplayName = "Na retificação o ato gravado emenda o ato anterior com o motivo")]
    public async Task Retificacao_OAtoGravadoEmendaOAnterior()
    {
        Preparo preparo = await Preparo.NovoAsync();
        var retificacao = new RetificacaoDoEdital(Guid.CreateVersion7(), "Correção do prazo");

        await preparo.GravarAsync(retificacao);

        AtoDoProcesso ato = preparo.AtoGravado();
        ato.AtoCorrigidoId.Should().Be(retificacao.EditalRetificadoId);
        ato.MotivoDaCorrecao.Should().Be(retificacao.Motivo);
    }

    [Fact(DisplayName = "O registro do ato recusado devolve erros com campo e não escreve nada")]
    public async Task RegistroRecusado_NaoEscreveNada()
    {
        Preparo preparo = await Preparo.NovoAsync();

        (Result resposta, IEnumerable<object> eventos) = await preparo.GravarAsync(new RetificacaoDoEdital(Guid.Empty, "Correção do prazo"));

        resposta.Errors.Should().ContainSingle().Which.Field.Should().Be("atoCorrigidoId");
        eventos.Should().BeEmpty();
        await preparo.Servicos.Repositorio.DidNotReceive().AdicionarVersaoConfiguracaoAsync(Arg.Any<VersaoConfiguracao>(), Arg.Any<CancellationToken>());
        await preparo.Servicos.Atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
        await preparo.Servicos.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O flush que viola a unicidade do número da versão recusa e devolve só a recusa")]
    public async Task UnicidadeNoFlush_RecusaSemEventos()
    {
        Preparo preparo = await Preparo.NovoAsync();
        preparo.FalharNoFlushCom(ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade(IndiceDoNumeroDaVersao));

        (Result resposta, IEnumerable<object> eventos) = await preparo.GravarAsync();

        resposta.Error!.Code.Should().Be("VersaoConfiguracao.NumeroDuplicado");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A violação de outro índice único propaga ao Wolverine")]
    public async Task OutroIndiceUnico_Propaga()
    {
        Preparo preparo = await Preparo.NovoAsync();
        preparo.FalharNoFlushCom(ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade("ux_outro_indice"));

        Func<Task> gravar = () => preparo.GravarAsync();

        await gravar.Should().ThrowAsync<Exception>();
    }

    [Fact(DisplayName = "A falha de banco que não é violação de unicidade propaga ao Wolverine")]
    public async Task FalhaDeBanco_Propaga()
    {
        Preparo preparo = await Preparo.NovoAsync();
        preparo.FalharNoFlushCom(new InvalidOperationException("Conexão perdida."));

        Func<Task> gravar = () => preparo.GravarAsync();

        await gravar.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "No sucesso da publicação o rastreamento não é descartado")]
    public async Task Publicacao_ComSucesso_NaoDescarta()
    {
        DependenciasDosHandlersQueCongelam publicacao = DependenciasDosHandlersQueCongelam.Para(NovoProcessoConforme());

        (Result resposta, _) = await publicacao.PublicarAsync();

        resposta.IsSuccess.Should().BeTrue();
        publicacao.Servicos.UnitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
        await publicacao.Servicos.Rascunhos.Received(1).RemoverDoProcessoAsync(publicacao.Processo.Id, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A recusa de um portão descarta o rastreamento e não escreve nem apaga nada")]
    public async Task Publicacao_RecusadaPeloPortao_Descarta()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Publicar).Com(DefeitoDoCongelamento.Cronograma);

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.IsFailure.Should().BeTrue();
        cenario.Servicos.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.Servicos.Rascunhos.DidNotReceive().RemoverDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A falha de gravação depois da transição descarta o rastreamento")]
    public async Task Publicacao_ComUnicidadeViolada_Descarta()
    {
        DependenciasDosHandlersQueCongelam publicacao = DependenciasDosHandlersQueCongelam.Para(NovoProcessoConforme());
        publicacao.Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade(IndiceDoNumeroDaVersao));

        (Result resposta, _) = await publicacao.PublicarAsync();

        resposta.IsFailure.Should().BeTrue();
        publicacao.Servicos.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private sealed class Preparo
    {
        private readonly DadosEdital _dados = DadosDoEdital();
        private AtoDoProcesso? _gravado;

        private Preparo(ServicosDoCongelamentoDeTeste servicos, ProcessoSeletivo processo, VersaoConfiguracao versao, AtoEDocumentoConferidos conferidos)
        {
            Servicos = servicos;
            Processo = processo;
            Versao = versao;
            Conferidos = conferidos;
            servicos.Atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => _gravado = a), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        }

        public ServicosDoCongelamentoDeTeste Servicos { get; }

        public ProcessoSeletivo Processo { get; }

        public VersaoConfiguracao Versao { get; }

        public AtoEDocumentoConferidos Conferidos { get; }

        public static async Task<Preparo> NovoAsync()
        {
            (ProcessoSeletivo processo, VersaoConfiguracao versao) = ProcessoPublicado();
            ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
            Result<AtoEDocumentoConferidos> conferidos = await servicos.AtoEDocumento()
                .ConferirAsync(processo.Id, Guid.CreateVersion7(), NovoAto(), "publicação", CancellationToken.None);
            conferidos.IsSuccess.Should().BeTrue(conferidos.Error?.Message);

            return new Preparo(servicos, processo, versao, conferidos.Value);
        }

        public void FalharNoFlushCom(Exception falha) =>
            Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(falha);

        public Task<(Result Resposta, IEnumerable<object> Eventos)> GravarAsync(RetificacaoDoEdital? retificacao = null) =>
            Servicos.Gravacao().GravarAsync(Processo, Versao, Conferidos, _dados, retificacao, CancellationToken.None);

        public AtoDoProcesso AtoGravado() => _gravado ?? throw new InvalidOperationException("Nenhum ato foi gravado.");
    }
}
