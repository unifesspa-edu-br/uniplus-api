namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using AwesomeAssertions;

using Confluent.Kafka;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Após erro fatal o produtor idempotente não volta a funcionar; o registro o troca para as próximas requisições,
/// e a requisição que viu o erro responde com a falha de registro.
/// </summary>
public sealed class RegistroDeAcessoKafkaRecriacaoTests
{
    private static readonly TimeProvider Relogio = new RelogioFixo(new DateTimeOffset(2026, 11, 1, 12, 30, 0, TimeSpan.Zero));

    [Fact(DisplayName = "Erro fatal do produtor recria o produtor: a requisição corrente falha e a seguinte grava")]
    public async Task ErroFatal_RecriaOProdutor()
    {
        IProducer<string, string> falho = ProdutorComErro(new Error(ErrorCode.Local_Fatal, "fatal", isFatal: true));
        IProducer<string, string> novo = ProdutorQueEntrega();
        Queue<IProducer<string, string>> produtores = new([falho, novo]);
        using RegistroDeAcessoKafka registro = new(produtores.Dequeue, "topico", TimeSpan.FromSeconds(1), Relogio);

        Func<Task> primeira = () => registro.RegistrarAsync([Registro()], default);
        await primeira.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
        await registro.RegistrarAsync([Registro()], default);

        // O descarte do produtor antigo é em segundo plano.
        await Esperar(() => falho.ReceivedCalls().Any(static c => c.GetMethodInfo().Name == nameof(IDisposable.Dispose)));
        await novo.Received(1).ProduceAsync(Arg.Any<string>(), Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Erro não fatal mantém o produtor")]
    public async Task ErroNaoFatal_MantemOProdutor()
    {
        IProducer<string, string> produtor = ProdutorComErro(new Error(ErrorCode.Local_MsgTimedOut, "prazo"));
        int criados = 0;
        using RegistroDeAcessoKafka registro = new(() => { criados++; return produtor; }, "topico", TimeSpan.FromSeconds(1), Relogio);

        Func<Task> acao = () => registro.RegistrarAsync([Registro()], default);
        await acao.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();

        criados.Should().Be(1);
        produtor.DidNotReceive().Dispose();
    }

    [Fact(DisplayName = "Falha ao criar o novo produtor sai como falha de registro, e o descarte não bloqueia a requisição")]
    public async Task FalhaNaFabrica_ViraFalhaDeRegistro()
    {
        IProducer<string, string> falho = ProdutorComErro(new Error(ErrorCode.Local_Fatal, "fatal", isFatal: true));
        int chamadas = 0;
        using RegistroDeAcessoKafka registro = new(
            () => ++chamadas == 1 ? falho : throw new InvalidOperationException("sem broker"), "topico", TimeSpan.FromSeconds(1), Relogio);

        Func<Task> acao = () => registro.RegistrarAsync([Registro()], default);

        await acao.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
    }

    private static IProducer<string, string> ProdutorComErro(Error erro)
    {
        IProducer<string, string> produtor = Substitute.For<IProducer<string, string>>();
        produtor.ProduceAsync(Arg.Any<string>(), Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DeliveryResult<string, string>>(
                new ProduceException<string, string>(erro, new DeliveryResult<string, string>())));

        return produtor;
    }

    private static IProducer<string, string> ProdutorQueEntrega()
    {
        IProducer<string, string> produtor = Substitute.For<IProducer<string, string>>();
        produtor.ProduceAsync(Arg.Any<string>(), Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeliveryResult<string, string>()));

        return produtor;
    }

    private static async Task Esperar(Func<bool> condicao)
    {
        for (int i = 0; i < 100 && !condicao(); i++)
        {
            await Task.Delay(20);
        }

        condicao().Should().BeTrue();
    }

    private static RegistroDecisaoAcesso Registro()
    {
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "leandro.viana").Value!, "jti-teste").Value!;
        EffectiveGrant concessao = EffectiveGrant.From("selecao:inscricoes:revelar-cpf", FonteGrant.Concessao, escopoInstitucional: true).Value!;

        return RegistroDecisaoAcesso.De(
            sujeito,
            PermissionRequirement.From("selecao:inscricoes:revelar-cpf", ClassificacaoDeProtecao.Identificador, "Lei nº 13.709/2018 (LGPD), art. 7º, III").Value!,
            ResourceContext.From("inscricao", ClassificacaoDeProtecao.Identificador).Value!,
            AuthorizationRequestContext.From($"req-{Guid.NewGuid():N}", DateTimeOffset.UtcNow, OrigemRequisicao.Api).Value!,
            AuthorizationDecision.Permitir(concessao));
    }
}
