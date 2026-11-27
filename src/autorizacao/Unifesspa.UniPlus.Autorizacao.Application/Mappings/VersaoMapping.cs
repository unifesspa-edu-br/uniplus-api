namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Mapeamento das versões do perfil e dos documentos que as instituem para os DTOs.</summary>
public static class VersaoMapping
{
    public static DocumentoDaVersaoDto ToDto(DocumentoDaVersao documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return new DocumentoDaVersaoDto(
            documento.Id,
            documento.VersaoId,
            CodigoComRotulo<SituacaoDoAnexo>.De(documento.Situacao),
            documento.TamanhoDeclarado,
            documento.EnviadoEm);
    }

    internal static ComposicaoEmVigorDto VigenteDe(Perfil perfil, VersaoDoPerfil vigente, CatalogoDaAplicacao catalogo) =>
        new(
            vigente.Id,
            vigente.Numero,
            Descrever(vigente.Codigos(), catalogo),
            vigente.Protocolo,
            DocumentosDoPerfil.De(perfil, vigente)?.VerificadoEm,
            perfil.Vigencias.FirstOrDefault(v => v.VersaoId == vigente.Id && v.Aberta)?.Desde);

    internal static VersaoPendenteDto? PendenteDe(Perfil perfil, CatalogoDaAplicacao catalogo, Identidade? leitor) =>
        perfil.VersaoPendente is { } pendente && DocumentosDoPerfil.De(perfil, pendente) is { } documento
            ? Pendente(perfil, pendente, documento, catalogo, leitor)
            : null;

    internal static UltimaVersaoEncerradaSemVigorDto? UltimaEncerradaSemVigorDe(Perfil perfil) =>
        perfil.UltimaVersaoEncerradaSemVigor is { } versao
            ? new UltimaVersaoEncerradaSemVigorDto(
                versao.Numero,
                CodigoComRotulo<SituacaoDaVersao>.De(versao.Situacao),
                CodigoComRotulo<EtapaDaVersao>.De(EtapaDeParada(versao)),
                InstanteDoFim(versao))
            : null;

    private static VersaoPendenteDto Pendente(
        Perfil perfil, VersaoDoPerfil pendente, DocumentoDaVersao documento, CatalogoDaAplicacao catalogo, Identidade? leitor)
    {
        (IReadOnlyList<string> acrescimo, bool exige) = DiferencaDe(perfil, pendente, catalogo);

        return new VersaoPendenteDto(
            pendente.Id, pendente.Numero, pendente.Nome, pendente.Descricao, Descrever(pendente.Codigos(), catalogo), pendente.Protocolo,
            documento.Id, CodigoComRotulo<SituacaoDoAnexo>.De(documento.Situacao), pendente.CriadaEm, pendente.ExpiraEm!.Value,
            CodigoComRotulo<EtapaDaVersao>.De(EtapaAtual(pendente, exige)),
            [.. SuspensaoDoPerfil.EtapasExigidas(exige).Select(CodigoComRotulo<EtapaDaVersao>.De)],
            exige, Descrever(acrescimo, catalogo),
            Descrever((perfil.VersaoVigente?.Codigos() ?? []).Except(pendente.Codigos(), StringComparer.Ordinal), catalogo),
            [.. pendente.DecisoesDoCiclo().Select(DecisaoDe)],
            pendente.CriadorTemConcessao,
            leitor is not null && pendente.CriadaPor == leitor);
    }

    private static (IReadOnlyList<string> Acrescimo, bool Exige) DiferencaDe(
        Perfil perfil, VersaoDoPerfil pendente, CatalogoDaAplicacao catalogo)
    {
        IReadOnlyList<string> acrescimo = perfil.VersaoVigente is { } vigente ? pendente.AcrescimosSobre(vigente) : pendente.Codigos();

        return (acrescimo, acrescimo.Any(c => PerfilMapping.SensibilidadeDe(c, catalogo) >= ClassificacaoDeProtecao.Pessoal));
    }

    private static DecisaoDaVersaoDto DecisaoDe(DecisaoDaVersao decisao) =>
        new(
            CodigoComRotulo<EtapaDaVersao>.De(decisao.Etapa),
            CodigoComRotulo<ResultadoDaDecisao>.De(decisao.Resultado),
            decisao.DecisorNome,
            decisao.DecisorTemConcessao,
            decisao.Instante);

    private static EtapaDaVersao EtapaAtual(VersaoDoPerfil versao, bool exigeProtecaoDeDados) =>
        versao.AprovadaPelaGestao && exigeProtecaoDeDados ? EtapaDaVersao.ProtecaoDeDados : EtapaDaVersao.GestaoDeAcessos;

    // A recusa para na etapa que a recusou; o cancelamento e a expiração, na que faltava decidir.
    private static EtapaDaVersao EtapaDeParada(VersaoDoPerfil versao) =>
        versao.DecisoesDoCiclo().LastOrDefault(static d => d.Resultado == ResultadoDaDecisao.Recusado)?.Etapa
            ?? EtapaAtual(versao, versao.ExigeProtecaoDeDados);

    private static DateTimeOffset InstanteDoFim(VersaoDoPerfil versao) => versao.Situacao switch
    {
        SituacaoDaVersao.Cancelada => versao.CanceladaEm!.Value,
        SituacaoDaVersao.Expirada => versao.ExpiradaEm!.Value,
        _ => versao.DecisoesDoCiclo().Max(static d => d.Instante),
    };

    internal static PermissaoDoPerfilDto[] Descrever(IEnumerable<string> codigos, CatalogoDaAplicacao catalogo) =>
        [.. codigos.Select(c => PerfilMapping.Descrever(c, catalogo)).OrderBy(static p => p.Codigo, StringComparer.Ordinal)];
}
