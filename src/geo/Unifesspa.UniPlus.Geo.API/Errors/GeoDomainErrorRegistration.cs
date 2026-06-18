namespace Unifesspa.UniPlus.Geo.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Registry de mapeamentos de erros de domínio do módulo Geo para wire codes /
/// status HTTP. As entidades de localidade (reference data) não têm CRUD de usuário;
/// os mapeamentos são do registro de execução do ETL (Story #674).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, GeoDomainErrorRegistration>().")]
internal sealed class GeoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new(GeoImportacaoErrorCodes.VersaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.geo.importacao.versao_obrigatoria",
                "Versão do dataset é obrigatória")),

        new(GeoImportacaoErrorCodes.VersaoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.geo.importacao.versao_formato_invalido",
                "Versão do dataset deve estar no formato AAAAMM")),

        new(GeoImportacaoErrorCodes.DisparadoPorObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.geo.importacao.disparado_por_obrigatorio",
                "A identificação de quem disparou a carga é obrigatória")),

        new(GeoImportacaoErrorCodes.ImportacaoEmAndamento,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.geo.importacao.em_andamento",
                "Já existe uma importação do Geo em andamento")),

        new(GeoImportacaoErrorCodes.NaoEnfileirada,
            new DomainErrorMapping(
                StatusCodes.Status503ServiceUnavailable,
                "uniplus.geo.importacao.nao_enfileirada",
                "Não foi possível enfileirar a importação (serviço em desligamento)")),

        new(GeoImportacaoErrorCodes.VersaoNaoProgressiva,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.geo.importacao.versao_nao_progressiva",
                "A versão informada é anterior à última release já aplicada")),

        new(GeoImportacaoErrorCodes.TransicaoInvalida,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.geo.importacao.transicao_invalida",
                "Transição de estado da importação inválida")),
    ];
}
