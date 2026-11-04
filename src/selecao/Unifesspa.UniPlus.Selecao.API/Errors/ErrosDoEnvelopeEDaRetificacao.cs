namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Recusas do codec do envelope e da sessão editorial de retificação.
/// </summary>
internal static class ErrosDoEnvelopeEDaRetificacao
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        // Codec do envelope (Story #859, ADR-0110 D1/D8). A recusa é NOMEADA: um descarte
        // que falha precisa distinguir uma versão que o sistema não conhece, uma que ele
        // conhece e não reidrata, e uma evidência que não prova o que diz provar.
        new("EnvelopeCodec.VersaoDesconhecida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.versao_desconhecida", "A versão do envelope congelado não está no registro de codecs")),
        new("EnvelopeCodec.VersaoNaoReidratavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.versao_nao_reidratavel", "A versão do envelope é conhecida, mas não pode ser reidratada")),
        new("EnvelopeCodec.AlgoritmoNaoSuportado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.algoritmo_nao_suportado", "O algoritmo de hash da versão não é o que o codec daquela forma emite")),
        new("EnvelopeCodec.IntegridadeViolada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.integridade_violada", "Os bytes congelados não produzem o hash registrado na versão")),
        new("EnvelopeCodec.EnvelopeMalformado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.envelope_malformado", "O envelope congelado não tem a forma que a versão dele declara")),
        new("EnvelopeCodec.EnvelopeIncoerenteComAVersao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.envelope_incoerente_com_a_versao", "O envelope contradiz a versão que o guarda")),
        new("EnvelopeCodec.BlocosDerivadosIncoerentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.blocos_derivados_incoerentes", "Distribuição, modalidades e ofertas não declaram o mesmo conjunto de ofertas de curso")),
        new("EnvelopeCodec.RegraDesconhecida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.regra_desconhecida", "O envelope referencia um código de regra fora do rol conhecido")),
        new("EnvelopeCodec.RoundTripDivergente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.round_trip_divergente", "A configuração reidratada não reproduz os bytes congelados")),
        // Sessão editorial de retificação (Story #860, ADR-0110 D3/D5). A precondição é a
        // ÚNICA família de erros do módulo que não é 4xx-de-negócio: 412 e 428 são de
        // PROTOCOLO — a operação não chegou a ser tentada. É também por isso que elas são
        // as únicas respostas < 500 que a idempotência NÃO armazena (D6): cachear um 412
        // prenderia por 24h o cliente que apenas releu o ETag e retentou corretamente.
        new("Precondicao.Requerida", new DomainErrorMapping(StatusCodes.Status428PreconditionRequired, "uniplus.selecao.precondicao_requerida", "Há uma retificação em curso — a mutação exige o If-Match com o ETag da sessão")),
        new("Precondicao.Falhou", new DomainErrorMapping(StatusCodes.Status412PreconditionFailed, "uniplus.selecao.precondicao_falhou", "O If-Match não corresponde ao estado atual da sessão editorial")),
        new("Precondicao.Malformada", new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.selecao.precondicao_malformada", "If-Match sintaticamente inválido (RFC 9110 §8.8.3)")),
        // Rascunho do bloco do ato no passo de Revisão. São só duas recusas de forma: o
        // conteúdo em si não é validado, porque rascunho de formulário pela metade é o caso
        // de uso, e a validação forte continua no ato de publicar.
        new("RascunhoDePublicacao.VersaoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.rascunho_publicacao.versao_invalida", "Versão do formato do rascunho inválida")),
        new("RascunhoDePublicacao.ConteudoMuitoGrande", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.rascunho_publicacao.conteudo_muito_grande", "Rascunho da publicação acima do tamanho permitido")),
        // Conflito transitório entre duas gravações do mesmo operador, e não corpo inválido:
        // 409, como a ADR-0119 fixa para concorrência. A segunda tentativa encontra a linha
        // que a primeira criou e a substitui.
        new("RascunhoDaPublicacao.GravacaoConcorrente", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.rascunho_publicacao.gravacao_concorrente", "Outra gravação do mesmo rascunho chegou primeiro", RetryableConflict: true)),
        new("RascunhoDePublicacao.SemUsuarioAutenticado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.rascunho_publicacao.sem_usuario_autenticado", "O rascunho da publicação pertence a quem o escreve")),
        new("RascunhoRetificacao.JaAberta", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.retificacao_ja_aberta", "Já existe uma retificação em curso neste processo")),
        new("RascunhoRetificacao.NaoAberta", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.retificacao_nao_aberta", "Não há retificação em curso neste processo")),
        new("RascunhoRetificacao.MotivoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.motivo_obrigatorio", "O motivo da retificação é obrigatório")),
        new("RascunhoRetificacao.MotivoMuitoLongo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.motivo_muito_longo", "O motivo da retificação excede o limite de caracteres")),
        // Fatos das versões publicadas (UNI-REQ-0144): o candidato pode ter preenchido um formulário
        // em qualquer versão publicada, então a sessão não usa, de outra finalidade, fato que não foi
        // coletado em todas elas, e não retira da inscrição o fato que ela já coletava.
        new("RascunhoRetificacao.FatoAusenteDeVersaoPublicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.fato_ausente_de_versao_publicada", "Fato de outra finalidade não coletado em todas as versões publicadas")),
        new("RascunhoRetificacao.FatoJaColetadoRemovido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.fato_ja_coletado_removido", "A retificação não retira da inscrição fato que ela já coleta")),
        // O candidato pode ter respondido o fato em qualquer versão publicada: a retificação não troca
        // o eixo que a versão publicada congelou, e a mudança vale para processo novo.
        new("RascunhoRetificacao.EixoDoFatoJaColetadoAlterado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.eixo_do_fato_ja_coletado_alterado", "A retificação não troca o eixo de fato já coletado por versão publicada")),
        // A base do rascunho deixou de ser o topo da cadeia. Hoje é inalcançável por
        // invariante (o atalho recusa com sessão aberta; o fechamento encerra a sessão na
        // mesma transação; o FOR UPDATE serializa) — é guard rail contra o dia em que um
        // caminho novo quebre isso. Restaurar ou emendar a versão ERRADA é o tipo de defeito
        // que o round-trip NÃO pega: os bytes de uma versão sempre batem com ela mesma.
        new("RascunhoRetificacao.BaseDesatualizada", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.retificacao_base_desatualizada", "A versão sobre a qual a retificação foi aberta não é mais o topo da cadeia")),
        // O agregado recusa encerrar a sessão sem que a configuração congelada tenha sido
        // reposta — descartar sem repor deixaria o certame servindo a configuração EDITADA
        // como se ela tivesse sido publicada. São 500 de propósito: não é decisão do usuário
        // nem input dele, é erro de PROGRAMAÇÃO (um handler que esqueceu a reposição), e um
        // 422 o disfarçaria de recusa de negócio.
        new("ProcessoSeletivo.DescarteSemRestauracao", new DomainErrorMapping(StatusCodes.Status500InternalServerError, "uniplus.selecao.processo_seletivo.descarte_sem_restauracao", "A sessão editorial não pode ser encerrada sem repor a configuração congelada")),
        new("ProcessoSeletivo.DescarteComVersaoErrada", new DomainErrorMapping(StatusCodes.Status500InternalServerError, "uniplus.selecao.processo_seletivo.descarte_com_versao_errada", "A configuração reposta não é a da versão base da retificação")),
        // A allowlist que falha FECHADA (D4): antes, um processo Encerrado ou Cancelado
        // aceitava mutação da configuração em silêncio — a trava era uma denylist de um
        // elemento só, e todo estado novo nascia mutável por omissão.
        new("ProcessoSeletivo.MutacaoForaDeEstadoEditavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.mutacao_fora_de_estado_editavel", "O processo não está em um estado que aceite mutação da configuração")),
    ];
}
