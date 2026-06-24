namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

// Mapeia GrupoCurso ↔ string (varchar). O comprimento da coluna é definido na
// própria propriedade via HasMaxLength no IEntityTypeConfiguration. A reidratação
// falha explicitamente (com contexto) caso a coluna seja corrompida fora do fluxo
// da aplicação, em vez do NullReferenceException tardio de `GrupoCurso.Criar(v).Value!`.
// O fail-fast é inlinado porque o helper compartilhado ValueObjectMaterialization é
// internal ao assembly Infrastructure.Core (este converter mora no módulo, pois
// GrupoCurso é vocabulário específico de Configuração).
public sealed class GrupoCursoValueConverter : ValueConverter<GrupoCurso, string>
{
    public GrupoCursoValueConverter()
        : base(
            grupo => grupo.Valor,
            valor => Reidratar(valor))
    {
    }

    private static GrupoCurso Reidratar(string valor)
    {
        Result<GrupoCurso> resultado = GrupoCurso.Criar(valor);
        if (resultado.IsFailure || resultado.Value is null)
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(GrupoCurso)}: " +
                $"{resultado.Error?.Code ?? "Desconhecido"}. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return resultado.Value;
    }
}
