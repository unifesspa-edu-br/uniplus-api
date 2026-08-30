namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

// Mapeia CodigoCategoriaDocumento ↔ string (varchar). O comprimento da coluna é
// definido na própria propriedade via HasMaxLength no IEntityTypeConfiguration. A
// reidratação falha explicitamente (com contexto) caso a coluna seja corrompida
// fora do fluxo da aplicação, em vez do NullReferenceException tardio de
// `CodigoCategoriaDocumento.Criar(v).Value!`. O fail-fast é inlinado porque o
// helper compartilhado ValueObjectMaterialization é internal ao assembly
// Infrastructure.Core (este converter mora no módulo, pois o código da categoria é
// vocabulário específico de Configuração).
public sealed class CodigoCategoriaDocumentoValueConverter : ValueConverter<CodigoCategoriaDocumento, string>
{
    public CodigoCategoriaDocumentoValueConverter()
        : base(
            codigo => codigo.Valor,
            valor => Reidratar(valor))
    {
    }

    private static CodigoCategoriaDocumento Reidratar(string valor)
    {
        Result<CodigoCategoriaDocumento> resultado = CodigoCategoriaDocumento.Criar(valor);
        if (resultado.IsFailure || resultado.Value is null)
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(CodigoCategoriaDocumento)}: " +
                $"{resultado.Error?.Code ?? "Desconhecido"}. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return resultado.Value;
    }
}
