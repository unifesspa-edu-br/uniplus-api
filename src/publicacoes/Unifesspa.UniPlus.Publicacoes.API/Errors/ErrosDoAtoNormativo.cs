namespace Unifesspa.UniPlus.Publicacoes.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>Mapeamentos HTTP dos erros do ato normativo e da sua cadeia de retificação.</summary>
internal static class ErrosDoAtoNormativo
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new(AtoNormativoErrorCodes.TipoSemVersaoVigente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.tipo_sem_versao_vigente",
                "Não há versão vigente do tipo de ato na data da publicação")),

        new(AtoNormativoErrorCodes.VersaoDoObjetoIncompleta,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.versao_do_objeto_incompleta",
                "A versão do objeto deve trazer o par (id, hash) completo ou nenhum dos dois")),

        new(AtoNormativoErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.publicacoes.ato_normativo.nao_encontrado",
                "Ato normativo não encontrado")),

        new(AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.ato_retificado_nao_encontrado",
                "O ato retificado não corresponde a nenhum ato registrado")),

        new(AtoNormativoErrorCodes.ClasseDeCongelamentoDivergente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.classe_congelamento_divergente",
                "A classe de congelamento do ato que retifica deve coincidir com a do ato retificado")),

        // 409 e não 422: é conflito com o estado já gravado — o ato-alvo já tem um
        // retificador —, o mesmo critério que classifica a vigência sobreposta como 409.
        new(AtoNormativoErrorCodes.AtoJaRetificado,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.publicacoes.ato_normativo.ato_ja_retificado",
                "O ato que se tentou retificar já foi retificado por outro (a cadeia é linear)")),

        // 409 pelo mesmo critério: o payload está coerente, mas o objeto vinculado já é
        // tratado por outra linhagem de atos deste tipo (ADR-0107).
        new(AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.publicacoes.ato_normativo.objeto_ja_tem_ato_vivo_do_tipo",
                "O tipo admite um único ato vivo por objeto, e a entidade vinculada já é tratada por outra linhagem")),

        // 422: o pedido quer registrar por aqui um ato que só o domínio dono do objeto
        // registra — pelo vínculo declarado ou pelo herdado do ato retificado. Não é
        // conflito de vaga nem de versão: nenhuma repetição do pedido o tornaria aceito.
        new(AtoNormativoErrorCodes.VinculoExigeRegistroPeloDominio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.vinculo_exige_registro_pelo_dominio",
                "Ato vinculado a esta entidade só pode ser registrado pelo módulo dono do objeto")),

        // 400 e não 422: o rótulo chega na rota de uma consulta, não num payload.
        new(AtoNormativoErrorCodes.TipoDeEntidadeInvalido,
            new DomainErrorMapping(
                StatusCodes.Status400BadRequest,
                "uniplus.publicacoes.ato_normativo.tipo_de_entidade_invalido",
                "Tipo de entidade inválido")),

        // 422: o próprio pedido recusa a forma do registro do ato.
        new(AtoNormativoErrorCodes.TextoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.texto_invalido",
                "Um texto do ato está vazio ou acima do tamanho máximo")),

        new(AtoNormativoErrorCodes.AnoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.ano_invalido",
                "O ano do ato deve ser maior que zero")),

        new(AtoNormativoErrorCodes.HashDoDocumentoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.hash_do_documento_invalido",
                "O hash do documento deve ser um SHA-256 em hexadecimal minúsculo")),

        new(AtoNormativoErrorCodes.RetificacaoIncompleta,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.retificacao_incompleta",
                "A retificação deve trazer o ato retificado e o motivo, ou nenhum dos dois")),

        new(AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.vinculo_tipo_de_entidade_invalido",
                "O tipo da entidade vinculada está vazio ou fora da forma canônica")),

        new(AtoNormativoErrorCodes.VinculoSemIdentificadorDaEntidade,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.vinculo_sem_identificador_da_entidade",
                "A entidade vinculada deve ter identificador")),

        new(AtoNormativoErrorCodes.VinculoRepetido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.vinculo_repetido",
                "A mesma entidade foi vinculada mais de uma vez ao ato")),

        new(AtoNormativoErrorCodes.VinculoNulo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.vinculo_nulo",
                "A lista de vínculos não admite elemento nulo")),
    ];
}
