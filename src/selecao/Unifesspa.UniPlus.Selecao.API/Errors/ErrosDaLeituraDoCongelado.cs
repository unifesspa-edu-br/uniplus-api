namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas da leitura da configuração congelada.
/// </summary>
internal static class ErrosDaLeituraDoCongelado
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        // Seletor de snapshot vigente (ADR-0075/0076): não há publicação
        // vigente ≤ o instante consultado — 422, nunca retorno silencioso.
        new("Snapshot.VigenteAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.snapshot.vigente_ausente", "Nenhuma publicação vigente para o instante")),
        // Leitura pública do certame contra uma configuração congelada cujo bloco não tem a forma
        // que a projeção espera. É falha de leitura, não ausência: não colapsa no não encontrado,
        // pelo mesmo motivo da versão de envelope desconhecida — um certame publicado e visível
        // que some da consulta pública é defeito, e defeito tem de aflorar.
        new("CertamePublicado.EnvelopeInesperado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.certame_publicado.envelope_inesperado", "A configuração congelada não tem a forma que a projeção pública espera")),
        // Os códigos de recusa dos parâmetros de consulta (ConsultaErrorCodes) NÃO se registram
        // aqui: são compartilhados com as listagens de Configuração e vivem uma única vez em
        // KernelDomainErrorRegistration, com taxonomia sem prefixo de módulo. Registrá-los por
        // módulo faria o último da ordem de DI sobrescrever o code do outro no registry global.
    ];
}
