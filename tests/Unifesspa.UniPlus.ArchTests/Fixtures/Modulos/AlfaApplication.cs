namespace Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Alfa.Application;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Alfa.Domain;
using Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Beta.Contracts;
using Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Beta.Domain;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Wolverine;

using ICommandBus = Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus;

/// <summary>Comando de teste que os handlers sintéticos despacham.</summary>
internal sealed record ComandoSintetico : ICommand<int>;

/// <summary>Aplicação de Alfa que alcança o domínio de Beta, o que a fronteira proíbe.</summary>
internal sealed class UsaODominioDeBeta
{
    public static Guid Identificar(EntidadeDeBeta entidade) => entidade.Id;
}

/// <summary>Aplicação de Alfa que só usa o contrato de Beta, o que a fronteira permite.</summary>
internal sealed class UsaOContratoDeBeta(IContratoDeBeta contrato)
{
    public Task<int> ContarAsync(CancellationToken cancellationToken) => contrato.ContarAsync(cancellationToken);
}

/// <summary>Aplicação de Alfa que usa o próprio domínio, o que a fronteira permite.</summary>
internal sealed class UsaODominioDeAlfa
{
    public static string Nomear(EntidadeDeAlfa entidade) => entidade.Nome;
}

/// <summary>Aplicação que depende do contexto de persistência, que é só da infraestrutura.</summary>
internal sealed class LeOContextoDiretoNaAplicacao(UniPlusDbContext contexto)
{
    public UniPlusDbContext Contexto => contexto;
}

/// <summary>Handler que despacha outro comando pelo barramento: abriria outra transação.</summary>
internal sealed class DespachaOutroComandoHandler(ICommandBus barramento)
{
    public async Task<int> HandleAsync(ComandoSintetico comando, CancellationToken cancellationToken)
    {
        return await barramento.Send(comando, cancellationToken);
    }
}

/// <summary>Handler que invoca outra mensagem pelo barramento do Wolverine: abriria outra transação.</summary>
internal sealed class InvocaOutraMensagemHandler(IMessageBus barramento)
{
    public async Task HandleAsync(ComandoSintetico comando, CancellationToken cancellationToken)
    {
        await barramento.InvokeAsync(comando, cancellationToken);
    }
}

/// <summary>Handler que só publica um evento, que o Wolverine grava no outbox da mesma transação.</summary>
internal sealed class PublicaEventoNoOutboxHandler(IMessageBus barramento)
{
    public async Task HandleAsync(ComandoSintetico comando, CancellationToken cancellationToken)
    {
        await barramento.PublishAsync(comando);
    }
}
