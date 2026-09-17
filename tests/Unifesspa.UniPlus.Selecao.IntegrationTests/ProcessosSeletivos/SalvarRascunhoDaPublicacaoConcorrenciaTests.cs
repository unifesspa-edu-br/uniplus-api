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
/// Duas gravações do mesmo operador no mesmo processo podem chegar juntas — duas abas, um
/// duplo clique, uma retentativa do cliente. As duas leem que não há rascunho, as duas
/// inserem, e <c>ux_rascunhos_publicacao_processo_operador</c> deixa passar uma só.
/// </summary>
/// <remarks>
/// Mora aqui, e não nos testes de Application, porque precisa da <c>DbUpdateException</c> do
/// EF Core: <c>UniqueConstraintViolation</c> a reconhece pelo nome do tipo, por reflexão,
/// justamente para que a camada Application não dependa do ORM.
/// </remarks>
public sealed class SalvarRascunhoDaPublicacaoConcorrenciaTests
{
    private const string Operador = "operador-do-rascunho";
    private const string IndiceDoOperador = "ux_rascunhos_publicacao_processo_operador";

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
