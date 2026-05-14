namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

// Mapeia AreaCodigo <-> string (varchar) no banco, com fail-fast nos dois
// sentidos:
//  - Escrita: default(AreaCodigo) tem Value null e é estado inválido (nunca
//    produzido por From). Persistir como NULL ficaria indistinguível de
//    "sem proprietário" numa coluna AreaCodigo? — então falha alto na escrita.
//  - Leitura: dado corrompido (alteração manual da coluna) falha alto via
//    InvalidOperationException com contexto, em vez de produzir um
//    default(AreaCodigo) e NRE tardia no primeiro uso.
//
// ValueObjectMaterialization.Reidratar não é reutilizável aqui: tem
// restrição `where T : class` e AreaCodigo é um record struct. O fail-fast
// é replicado localmente com a mesma semântica.
public sealed class AreaCodigoValueConverter : ValueConverter<AreaCodigo, string>
{
    public AreaCodigoValueConverter()
        : base(
            codigo => ParaProvider(codigo),
            valor => Reidratar(valor))
    {
    }

    private static string ParaProvider(AreaCodigo codigo)
    {
        if (codigo.Value is null)
        {
            throw new InvalidOperationException(
                "Tentativa de persistir default(AreaCodigo): valor não inicializado. "
                + "AreaCodigo válido só é produzido por AreaCodigo.From; "
                + "uma coluna sem proprietário deve usar AreaCodigo? nulo, não o struct default.");
        }

        return codigo.Value;
    }

    private static AreaCodigo Reidratar(string valor)
    {
        Result<AreaCodigo> resultado = AreaCodigo.From(valor);
        if (resultado.IsFailure)
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar AreaCodigo: '{valor}' "
                + $"({resultado.Error!.Code}). "
                + "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return resultado.Value!;
    }
}
