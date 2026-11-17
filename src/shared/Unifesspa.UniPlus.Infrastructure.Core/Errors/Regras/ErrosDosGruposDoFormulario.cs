namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Errors;

/// <summary>Códigos dos grupos repetíveis do formulário e dos campos que eles contêm.</summary>
internal static class ErrosDosGruposDoFormulario
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new(GrupoFormularioErrorCodes.CodigoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.codigo_invalido", "O código do grupo é obrigatório e tem tamanho limitado")),
        new(GrupoFormularioErrorCodes.OrdemInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.ordem_invalida", "A ordem do grupo não pode ser negativa")),
        new(GrupoFormularioErrorCodes.RotuloInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.rotulo_invalido", "O rótulo do grupo é obrigatório e tem tamanho limitado")),
        new(GrupoFormularioErrorCodes.ContagemIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.contagem_incoerente", "O mínimo e o máximo de ocorrências do grupo são incoerentes")),
        new(GrupoFormularioErrorCodes.SubitensForaDoLimite, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.subitens_fora_do_limite", "A quantidade de campos do grupo está fora do limite")),
        new(GrupoFormularioErrorCodes.RegraAutorreferente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.regra_autorreferente", "A regra do grupo cita o próprio grupo")),
        new(GrupoFormularioErrorCodes.CampoComSecaoPropria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.campo_com_secao_propria", "O campo do grupo segue a seção do grupo")),
        new(GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.candidato_como_membro_incompleto", "O grupo que inclui o candidato não identifica a ocorrência dele")),
        new(GrupoFormularioErrorCodes.CampoComImpedimento, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grupo_formulario.campo_com_impedimento", "O campo do grupo não tem impedimento")),
    ];
}
