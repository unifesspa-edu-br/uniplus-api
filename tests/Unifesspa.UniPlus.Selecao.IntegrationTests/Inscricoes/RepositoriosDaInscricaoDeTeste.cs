namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

internal static class RepositoriosDaInscricaoDeTeste
{
    public static InscricaoRepository Inscricoes(UniPlusDbContext db, InscricaoDbFixture fixture, IUniPlusEncryptionService? encryption = null) =>
        new(db, encryption ?? fixture.Encryption, new CalculadorDoIndiceCegoDoCpf(fixture.BlindIndex), TimeProvider.System, Comprovantes(db, fixture, encryption));

    public static ComprovanteDaInscricaoRepository Comprovantes(UniPlusDbContext db, InscricaoDbFixture fixture, IUniPlusEncryptionService? encryption = null) =>
        new(db, encryption ?? fixture.Encryption, fixture.BlindIndex);
}
