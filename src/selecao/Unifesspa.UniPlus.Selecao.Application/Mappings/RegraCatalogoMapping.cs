namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Projeção <c>RegraCatalogo</c> → <c>RegraCatalogoDto</c>.
/// </summary>
public static class RegraCatalogoMapping
{
    public static RegraCatalogoDto ToDto(RegraCatalogo regra)
    {
        ArgumentNullException.ThrowIfNull(regra);

        return new RegraCatalogoDto(
            Codigo: regra.Codigo,
            Versao: regra.Versao,
            // O código canônico de wire, não o nome do membro em C#: é o mesmo valor que o
            // filtro `tipo` aceita e que o hash content-addressable incorpora, então quem
            // relê um tipo desta resposta e o usa como filtro é atendido.
            Tipo: regra.Tipo.ToCodigo(),
            EsquemaArgs: regra.EsquemaArgs,
            Invariantes: regra.Invariantes,
            BaseLegal: regra.BaseLegal,
            Hash: regra.Hash,
            ModalidadesAdmitidas: ModalidadesAdmitidasDoEsquemaArgs.Extrair(regra));
    }
}
