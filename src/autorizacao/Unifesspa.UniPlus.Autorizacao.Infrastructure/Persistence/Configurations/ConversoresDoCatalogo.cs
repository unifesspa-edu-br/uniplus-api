namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>Os conversores de texto compartilhados pelas tabelas da reclassificação do catálogo.</summary>
internal static class ConversoresDoCatalogo
{
    /// <summary>O código da permissão como o texto gravado; ler um texto fora do formato falha alto.</summary>
    public static readonly ValueConverter<CodigoDePermissao, string> Codigo = new(
        codigo => codigo.Valor,
        texto => CodigoDePermissao.De(texto));
}
