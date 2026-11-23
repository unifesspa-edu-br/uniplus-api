namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

internal static class ErrosDaProvaPresencial
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new(ProvaPresencialErrorCodes.PorDeclarar, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prova_presencial.por_declarar", "Prova presencial ainda não declarada")),
        new(ProvaPresencialErrorCodes.SemMunicipio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prova_presencial.sem_municipio", "Município de prova não informado")),
        new(ProvaPresencialErrorCodes.MunicipiosSemProvaPresencial, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prova_presencial.municipios_sem_prova_presencial", "Municípios informados sem prova presencial")),
        new(ProvaPresencialErrorCodes.MunicipioRepetido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prova_presencial.municipio_repetido", "Município de prova repetido")),
        new(ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prova_presencial.municipios_acima_do_limite", "Limite de municípios de prova excedido")),
        new(ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prova_presencial.municipio_nao_conferido_no_geo", "Município de prova não encontrado no Geo")),
        new(ProvaPresencialErrorCodes.InscricaoSemCidadeDeProva, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prova_presencial.inscricao_sem_cidade_de_prova", "Cidade de prova ausente no formulário")),
        new(ProvaPresencialErrorCodes.CidadeDeProvaSemProvaPresencial, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prova_presencial.cidade_de_prova_sem_prova_presencial", "Cidade de prova sem prova presencial")),
    ];
}
