namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Mapeamento das versões do perfil e dos documentos que as instituem para os DTOs.</summary>
public static class VersaoMapping
{
    public static DocumentoDaVersaoDto ToDto(DocumentoDaVersao documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return new DocumentoDaVersaoDto(
            documento.Id, documento.VersaoId, documento.Situacao.ToCodigo(), documento.TamanhoDeclarado, documento.EnviadoEm);
    }

    internal static ComposicaoEmVigorDto VigenteDe(Perfil perfil, VersaoDoPerfil vigente, CatalogoDaAplicacao catalogo) =>
        new(
            vigente.Id,
            vigente.Numero,
            Descrever(vigente.Codigos(), catalogo),
            vigente.Protocolo,
            vigente.Documento?.VerificadoEm,
            perfil.Vigencias.FirstOrDefault(v => v.VersaoId == vigente.Id && v.Aberta)?.Desde);

    internal static VersaoPendenteDto? PendenteDe(Perfil perfil, CatalogoDaAplicacao catalogo) =>
        perfil.VersaoPendente is { Documento: { } documento } pendente
            ? new VersaoPendenteDto(
                pendente.Id,
                pendente.Numero,
                pendente.Nome,
                pendente.Descricao,
                Descrever(pendente.Codigos(), catalogo),
                pendente.Protocolo,
                documento.Id,
                documento.Situacao.ToCodigo(),
                pendente.CriadaEm)
            : null;

    internal static UltimaVersaoRecusadaDto? UltimaRecusadaDe(Perfil perfil) =>
        perfil.UltimaVersaoRecusada is { MotivoDaRecusa: { } motivo, RecusadaEm: { } recusadaEm } recusada
            ? new UltimaVersaoRecusadaDto(recusada.Numero, motivo.ToCodigo(), recusadaEm)
            : null;

    internal static PermissaoDoPerfilDto[] Descrever(IEnumerable<string> codigos, CatalogoDaAplicacao catalogo) =>
        [.. codigos.Select(c => PerfilMapping.Descrever(c, catalogo)).OrderBy(static p => p.Codigo, StringComparer.Ordinal)];
}
