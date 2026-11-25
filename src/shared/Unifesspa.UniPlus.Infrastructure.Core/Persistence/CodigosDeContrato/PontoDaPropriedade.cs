namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

internal sealed class PontoDaPropriedade(IMutableProperty propriedade) : IPontoDeConversao
{
    public Type TipoDeclarado => propriedade.ClrType;

    public string Descricao => $"A propriedade '{propriedade.Name}' de '{propriedade.DeclaringType.DisplayName()}'";

    public ValueConverter? Conversor => propriedade.GetValueConverter();

    public Type? TipoNoProvedor => propriedade.GetProviderClrType();

    public string? Comentario => propriedade.GetComment();

    public void DefinirComentario(string comentario) => propriedade.SetComment(comentario);

    public void DefinirConversor(ValueConverter conversor) => propriedade.SetValueConverter(conversor);
}
