namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>Documenta as colunas comuns presentes no modelo.</summary>
public static class ModelBuilderComentariosComunsExtensions
{
    /// <summary>Aplica os comentários depois do registro das entidades.</summary>
    public static void AplicarComentariosDasColunasComuns(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        foreach (IMutableEntityType entidade in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty propriedade in entidade.GetProperties())
            {
                string? comentario = Comentario(propriedade.Name) ?? ComentarioDaVersao(propriedade);
                if (comentario is not null)
                {
                    propriedade.SetComment(comentario);
                }
            }
        }
    }
    private static string? Comentario(string nome) => nome switch
    {
        "CreatedAt" => "Data e hora de criação do registro.",
        "UpdatedAt" => "Data e hora da última alteração, quando houver.",
        "CreatedBy" => "Identificador do responsável pela criação, quando disponível.",
        "UpdatedBy" => "Identificador do responsável pela última alteração, quando disponível.",
        "IsDeleted" => "Indica se o registro foi excluído logicamente.",
        "DeletedAt" => "Data e hora da exclusão lógica, quando houver.",
        "DeletedBy" => "Identificador do responsável pela exclusão lógica, quando disponível.",
        _ => null,
    };

    private static string? ComentarioDaVersao(IMutableProperty propriedade) =>
        propriedade.Name == "Version" && propriedade.ClrType == typeof(uint) && propriedade.IsConcurrencyToken
            ? "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."
            : null;
}
