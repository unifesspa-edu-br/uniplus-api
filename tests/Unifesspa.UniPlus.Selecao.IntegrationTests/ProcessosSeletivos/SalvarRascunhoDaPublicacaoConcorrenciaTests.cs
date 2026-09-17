namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Xunit;

/// <summary>
/// As duas corridas que a gravação do rascunho sofre, e como cada uma termina em gravação
/// concluída em vez de erro na cara do operador.
/// </summary>
/// <remarks>
/// <para>
/// <b>Corrida no índice único:</b> duas gravações do mesmo operador no mesmo processo chegam
/// juntas — duas abas, um duplo clique, uma retentativa do cliente. As duas leem que não há
/// rascunho, as duas inserem, e <c>ux_rascunhos_publicacao_processo_operador</c> deixa passar
/// uma só.
/// </para>
/// <para>
/// <b>Corrida com a varredura de vencidos:</b> o dono volta a um rascunho já vencido e a
/// renovação do prazo fica só na entidade rastreada até o flush; nesse intervalo a varredura
/// de qualquer outra gravação, que roda SQL imediato em transação própria, enxerga a data
/// velha e apaga a linha que o flush ia atualizar.
/// </para>
/// <para>
/// Mora aqui, e não nos testes de Application, porque precisa das exceções do EF Core —
/// <c>DbUpdateException</c> e <c>DbUpdateConcurrencyException</c> —, que os detectores
/// reconhecem pelo nome do tipo, por reflexão, justamente para que a camada Application não
/// dependa do ORM.
/// </para>
/// </remarks>
public sealed class SalvarRascunhoDaPublicacaoConcorrenciaTests
{
    private const string Operador = "operador-do-rascunho";
    private const string IndiceDoOperador = "ux_rascunhos_publicacao_processo_operador";

    [Fact(DisplayName = "Rascunho vencido apagado pela varredura alheia durante a renovação é recriado")]
    public async Task Handle_VarreduraAlheiaApagaALinhaEmRenovacao_Recria()
    {
        Guid processoId = Guid.CreateVersion7();

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);

        // O dono volta a um rascunho JÁ VENCIDO. A renovação do prazo fica só na entidade
        // rastreada até o flush — e nesse intervalo a varredura de vencidos de outra gravação,
        // que roda SQL imediato numa transação própria, enxerga a data velha e apaga a linha.
        RascunhoDePublicacao vencido = RascunhoDePublicacao.Criar(
            processoId, Operador, """{"ato":{"assinante":"do mês passado"}}""", 1,
            DateTimeOffset.UtcNow.AddDays(-40), RascunhoDePublicacao.Prazo).Value!;

        IRascunhoDePublicacaoRepository rascunhoRepository = Substitute.For<IRascunhoDePublicacaoRepository>();
        // A primeira leitura ainda vê a linha; a releitura da retentativa já não — é isso que
        // a varredura alheia provocou, e é o que autoriza recriar em vez de substituir.
        rascunhoRepository.ObterDoOperadorAsync(processoId, Operador, Arg.Any<CancellationToken>())
            .Returns(vencido, (RascunhoDePublicacao?)null);

        // O UPDATE não encontra a linha; a recriação passa.
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        bool primeiroFlush = true;
        unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
            {
                if (!primeiroFlush)
                {
                    return Task.FromResult(1);
                }

                primeiroFlush = false;
                throw new DbUpdateConcurrencyException("a linha não existe mais");
            });

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Operador);

        Result resultado = await SalvarRascunhoDaPublicacaoCommandHandler.Handle(
            new SalvarRascunhoDaPublicacaoCommand(processoId, 1, JsonDocument.Parse("""{"ato":{"assinante":"de agora"}}""").RootElement),
            processoRepository,
            rascunhoRepository,
            unitOfWork,
            userContext,
            TimeProvider.System,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(
            $"o conteúdo continua em mãos, e recriar é o que a leitura teria feito se chegasse "
            + $"depois da varredura. Veio '{resultado.Error?.Code}'");

        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await rascunhoRepository.Received(1).AdicionarAsync(
            Arg.Is<RascunhoDePublicacao>(r => r.UsuarioSub == Operador && r.Conteudo.Contains("de agora")),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(2).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Se outra gravação do operador ocupou o lugar da linha varrida, a retentativa substitui em vez de inserir")]
    public async Task Handle_VarreduraAlheiaApagaEOutraGravacaoOcupaOLugar_SubstituiSemDuplicar()
    {
        Guid processoId = Guid.CreateVersion7();

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);

        RascunhoDePublicacao vencido = RascunhoDePublicacao.Criar(
            processoId, Operador, """{"ato":{"assinante":"do mês passado"}}""", 1,
            DateTimeOffset.UtcNow.AddDays(-40), RascunhoDePublicacao.Prazo).Value!;

        // Entre a varredura alheia que apagou a linha vencida e esta retentativa, a outra aba
        // do mesmo operador leu que não havia nada e inseriu a dela. Inserir de novo esbarraria
        // em ux_rascunhos_publicacao_processo_operador, e a violação nascida dentro do catch
        // não teria quem a traduzisse — viraria falha de servidor.
        RascunhoDePublicacao daOutraAba = RascunhoDePublicacao.Criar(
            processoId, Operador, """{"ato":{"assinante":"da outra aba"}}""", 1,
            DateTimeOffset.UtcNow, RascunhoDePublicacao.Prazo).Value!;

        IRascunhoDePublicacaoRepository rascunhoRepository = Substitute.For<IRascunhoDePublicacaoRepository>();
        rascunhoRepository.ObterDoOperadorAsync(processoId, Operador, Arg.Any<CancellationToken>())
            .Returns(vencido, daOutraAba);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        bool primeiroFlush = true;
        unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
            {
                if (!primeiroFlush)
                {
                    return Task.FromResult(1);
                }

                primeiroFlush = false;
                throw new DbUpdateConcurrencyException("a linha não existe mais");
            });

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Operador);

        Result resultado = await SalvarRascunhoDaPublicacaoCommandHandler.Handle(
            new SalvarRascunhoDaPublicacaoCommand(processoId, 1, JsonDocument.Parse("""{"ato":{"assinante":"de agora"}}""").RootElement),
            processoRepository,
            rascunhoRepository,
            unitOfWork,
            userContext,
            TimeProvider.System,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(
            $"a linha que ocupou o lugar é do mesmo dono e desta mesma gravação — substituí-la é "
            + $"o que a leitura teria feito um instante depois. Veio '{resultado.Error?.Code}'");

        await rascunhoRepository.DidNotReceive().AdicionarAsync(
            Arg.Any<RascunhoDePublicacao>(), Arg.Any<CancellationToken>());
        rascunhoRepository.Received(1).Atualizar(daOutraAba);
        daOutraAba.Conteudo.Should().Contain("de agora",
            "o conteúdo desta gravação substitui o que a outra aba tinha deixado");
    }

    [Fact(DisplayName = "Quem perde a corrida relê a linha do vencedor e substitui — a gravação conclui")]
    public async Task Handle_CorridaNoIndiceDoOperador_SubstituiOVencedor()
    {
        Guid processoId = Guid.CreateVersion7();

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);

        // A primeira leitura não vê nada; depois da colisão, a releitura encontra a linha que
        // a outra gravação criou — é a sequência que o operador provoca com duas abas abertas.
        RascunhoDePublicacao vencedor = RascunhoDePublicacao.Criar(
            processoId, Operador, """{"ato":{"assinante":"da outra aba"}}""", 1,
            DateTimeOffset.UtcNow, RascunhoDePublicacao.Prazo).Value!;

        IRascunhoDePublicacaoRepository rascunhoRepository = Substitute.For<IRascunhoDePublicacaoRepository>();
        rascunhoRepository.ObterDoOperadorAsync(processoId, Operador, Arg.Any<CancellationToken>())
            .Returns((RascunhoDePublicacao?)null, vencedor);

        // O primeiro flush colide; o segundo, já na linha do vencedor, passa.
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        bool primeiroFlush = true;
        unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
            {
                if (!primeiroFlush)
                {
                    return Task.FromResult(1);
                }

                primeiroFlush = false;
                throw new DbUpdateException(
                    "violação de índice único",
                    PostgresExceptionFactory.Create("23505", IndiceDoOperador));
            });

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Operador);

        Result resultado = await SalvarRascunhoDaPublicacaoCommandHandler.Handle(
            new SalvarRascunhoDaPublicacaoCommand(processoId, 1, JsonDocument.Parse("""{"ato":{}}""").RootElement),
            processoRepository,
            rascunhoRepository,
            unitOfWork,
            userContext,
            TimeProvider.System,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(
            $"perder a corrida diz que a linha existe agora — basta substituí-la, que é o caminho "
            + $"que a leitura teria tomado um instante depois. Veio '{resultado.Error?.Code}'");

        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        rascunhoRepository.Received(1).Atualizar(vencedor);
        vencedor.Conteudo.Should().Contain("ato",
            "o conteúdo desta gravação substitui o que a outra aba tinha deixado");
    }
}
