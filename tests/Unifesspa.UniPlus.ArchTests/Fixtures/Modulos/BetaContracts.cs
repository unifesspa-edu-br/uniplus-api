namespace Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Beta.Contracts;

/// <summary>Contrato do módulo sintético Beta: a única porta que os outros módulos podem referenciar.</summary>
public interface IContratoDeBeta
{
    Task<int> ContarAsync(CancellationToken cancellationToken);
}
