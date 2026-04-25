namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Spike;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// SPIKE V3 — handler Wolverine via convenção (método Handle/HandleAsync).
/// Pipeline canônico: IMessageBus.SendAsync → AutoApplyTransactions enrola
/// SaveChanges → PublishDomainEventsFromEntityFrameworkCore drena domain events
/// → envelope persistido em wolverine.* na mesma transação.
/// </summary>
[SuppressMessage("Performance", "CA1515:Consider making public types internal",
    Justification = "SPIKE: Wolverine convenção exige descoberta por reflection — public é seguro aqui.")]
public static class PublicarEditalSpikeHandler
{
    [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "SPIKE")]
    public static async Task Handle(
        PublicarEditalSpikeCommand command,
        SelecaoDbContext db,
        CancellationToken ct)
    {
        Result<NumeroEdital> numeroResult = NumeroEdital.Criar(command.Numero, command.Ano);
        Edital edital = Edital.Criar(numeroResult.Value!, command.Titulo, TipoProcesso.SiSU);
        edital.Publicar();
        db.Editais.Add(edital);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "SPIKE")]
    public static async Task Handle(
        FalharAposPublicarSpikeCommand command,
        SelecaoDbContext db,
        CancellationToken ct)
    {
        Result<NumeroEdital> numeroResult = NumeroEdital.Criar(command.Numero, command.Ano);
        Edital edital = Edital.Criar(numeroResult.Value!, command.Titulo, TipoProcesso.SiSU);
        edital.Publicar();
        db.Editais.Add(edital);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        // Joga DEPOIS do SaveChanges para exercitar rollback do AutoApplyTransactions.
        // Atomicidade real do outbox exige que entity + envelope sumam juntos.
        throw new InvalidOperationException("SPIKE: simulação de falha pós-SaveChanges para teste de rollback.");
    }
}
