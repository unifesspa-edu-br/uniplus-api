using Wolverine;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia.Handlers;

/// <summary>
/// Handler do módulo CRUD: escreve via EF Core (store 'main') e cascateia um evento
/// de integração pelo outbox 'main'. O <c>SaveChangesAsync</c> é aplicado pela
/// transactional middleware EF Core do Wolverine — escrita + envelope atômicos.
/// </summary>
public static class CriarRegistroCrudHandler
{
    public static OutgoingMessages Handle(CriarRegistroCrud comando, CrudDbContext db)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(db);

        db.Registros.Add(new RegistroCrud { Id = comando.Id, Descricao = comando.Descricao });

        return [new RegistroCrudCriado(comando.Id)];
    }
}
