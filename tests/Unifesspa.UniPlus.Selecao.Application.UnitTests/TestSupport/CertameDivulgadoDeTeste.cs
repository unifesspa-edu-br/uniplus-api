namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Certame divulgado e a sua projeção prontos para os testes de leitura pública.
/// </summary>
internal static class CertameDivulgadoDeTeste
{
    internal static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Registra a entrada do ato como a divulgação a grava: pendente, e com endereço se a cópia já ocorreu.</summary>
    internal static void Registrar(CertameDivulgado divulgado, AtoDivulgado edital)
    {
        ArgumentNullException.ThrowIfNull(divulgado);
        ArgumentNullException.ThrowIfNull(edital);

        divulgado.AbrirEntradaDoAto(edital.AtoId, edital.NumeroVersao);
        if (edital.UrlDownload is not null)
        {
            divulgado.RegistrarEnderecos(edital.AtoId, edital.UrlDownload, edital.Documentos ?? []);
        }
    }

    internal static CertameDivulgado Divulgado(
        Guid processoId,
        CertamePublicadoDto projecao,
        DateTimeOffset? inscricoesDe = null,
        DateTimeOffset? inscricoesAte = null) =>
        CertameDivulgado.Criar(
            processoId,
            numeroVersao: 1,
            projecao.AtoCriadorId,
            new string('a', 64),
            ProjecaoDoCertamePublicado.Versao,
            new FacetasDoCertameDivulgado(
                projecao.IdentificadorLegivel,
                projecao.Nome,
                projecao.Periodo.Numero,
                projecao.ModalidadesOfertadas,
                inscricoesDe ?? Agora.AddDays(-1),
                inscricoesAte ?? Agora.AddDays(20)),
            JsonSerializer.Serialize(projecao, ProjecaoDoCertamePublicado.OpcoesDoDocumento),
            Agora);

    internal static CertamePublicadoDto Projecao(Guid processoId) => new(
        processoId, IdentificadoresDeTeste.Novo().Valor, Guid.CreateVersion7(), "SISU 2026.1", ProjecaoDoCertamePublicado.Versao, new string('a', 64),
        new TipoCatalogadoCertameDto("SISU", "Sistema de Seleção Unificada"),
        new PeriodoInscricaoCertameDto("001/2026", Agora.AddDays(-1), Agora.AddDays(20)),
        new LocalidadeCertameDto("1504208", "Marabá", "PA", FusoHorarioDto.Institucional),
        new UnidadeAdministradoraCertameDto("UNIFESSPA", "Universidade", "Autarquia", "Marabá", "PA"),
        new DocumentoEditalCertameDto(Guid.CreateVersion7(), new string('b', 64), new Uri("https://acervo.teste/edital.pdf")),
        [], new OpcoesDeCursoCertameDto(1), ["AC"], [new QuadroDeVagasCertameDto(Guid.CreateVersion7(), [], 100)],
        [], "Externa", [], [], new AtendimentoCertameDto([], [], []), null, null);
}
