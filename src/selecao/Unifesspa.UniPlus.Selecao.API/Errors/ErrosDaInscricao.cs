namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas das opções declaradas pelo processo e da inscrição do candidato.
/// </summary>
internal static class ErrosDaInscricao
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        // Opções declaradas pelo processo (issue #1619).
        new(OpcaoDeclaradaFatoErrorCodes.CodigoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.opcao_declarada_fato.codigo_invalido", "O código da opção é obrigatório e respeita o tamanho máximo")),
        new(OpcaoDeclaradaFatoErrorCodes.RotuloInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.opcao_declarada_fato.rotulo_invalido", "O rótulo da opção é obrigatório e respeita o tamanho máximo")),
        new(OpcaoDeclaradaFatoErrorCodes.ListaVazia, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.opcao_declarada_fato.lista_vazia", "Declare ao menos uma opção para o fato")),
        new(OpcaoDeclaradaFatoErrorCodes.CodigoRepetido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.opcao_declarada_fato.codigo_repetido", "Cada opção do fato precisa de um código próprio")),
        new(OpcaoDeclaradaFatoErrorCodes.GeridasPelaOfertaDeAtendimento, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.opcao_declarada_fato.geridas_pela_oferta_de_atendimento", "As opções deste fato vêm da oferta de atendimento especializado")),
        new(OpcaoDeclaradaFatoErrorCodes.ReferenciadaPorExigenciaViva, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.opcao_declarada_fato.referenciada_por_exigencia_viva", "Uma exigência documental cita uma opção que deixaria de existir")),
        new(OpcaoDeclaradaFatoErrorCodes.FonteNaoEhDoProcesso, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.opcao_declarada_fato.fonte_nao_eh_do_processo", "Só se declaram opções para um fato cuja fonte dos valores é o processo")),
        new(OpcaoDeclaradaFatoErrorCodes.EdicaoEnemForaDeProcessoEnem, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.opcao_declarada_fato.edicao_enem_fora_de_processo_enem", "As edições do ENEM só podem ser declaradas quando a classificação do processo é baseada em ENEM")),
        // Inscrição do candidato (issue #1858).
        new(InscricaoErrorCodes.SubObrigatorio, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.selecao.inscricao.sub_obrigatorio", "O token precisa trazer o claim sub")),
        new(InscricaoErrorCodes.EmissorObrigatorio, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.selecao.inscricao.emissor_obrigatorio", "O token precisa trazer o emissor")),
        new(InscricaoErrorCodes.DescarteSoDeRascunho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.descarte_so_de_rascunho", "Só o rascunho da inscrição pode ser descartado")),
        new(InscricaoErrorCodes.CancelamentoSoDeEnviada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.cancelamento_so_de_enviada", "Só a inscrição enviada pode ser cancelada")),
        new(InscricaoErrorCodes.InscricaoCancelada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.inscricao_cancelada", "A inscrição cancelada não pode mais ser alterada")),
        new(InscricaoErrorCodes.MotivoDoCancelamentoLongo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.motivo_do_cancelamento_longo", "O motivo do cancelamento excede o tamanho máximo")),
        new(InscricaoErrorCodes.PrecondicaoRequerida, new DomainErrorMapping(StatusCodes.Status428PreconditionRequired, "uniplus.selecao.inscricao.precondicao_requerida", "A mutação da inscrição exige o If-Match com o ETag corrente")),
        new(InscricaoErrorCodes.PrecondicaoFalhou, new DomainErrorMapping(StatusCodes.Status412PreconditionFailed, "uniplus.selecao.inscricao.precondicao_falhou", "O If-Match não corresponde ao estado atual da inscrição")),
        new(InscricaoErrorCodes.DonoDivergente, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.selecao.inscricao.dono_divergente", "Esta inscrição pertence a outro candidato")),
        new("Inscricao.CpfAusenteNoToken", new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.selecao.inscricao.cpf_ausente_no_token", "O token precisa trazer o claim cpf")),
        new("Inscricao.CorridaInconsistente", new DomainErrorMapping(StatusCodes.Status500InternalServerError, "uniplus.selecao.inscricao.corrida_inconsistente", "A inscrição colidiu no índice único, mas não foi possível relê-la")),
        new(InscricaoErrorCodes.NaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.inscricao.nao_encontrada", "Inscrição não encontrada neste processo seletivo")),
        new(InscricaoErrorCodes.CampoNaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.inscricao.campo_nao_encontrado", "Campo da inscrição não encontrado")),
        new(InscricaoErrorCodes.FaseNaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.inscricao.fase_nao_encontrada", "A fase pedida não está no cronograma da versão da inscrição")),
        new(InscricaoErrorCodes.RespostaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.resposta_invalida", "A resposta não atende a uma restrição do formulário")),
        new(InscricaoErrorCodes.DataForaDaFaixa, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.data_fora_da_faixa", "A data informada fica fora dos limites que o campo admite")),
        new(InscricaoErrorCodes.FatoForaDoFormulario, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.fato_fora_do_formulario", "A resposta é de um campo que o formulário não coleta")),
        new(InscricaoErrorCodes.GrupoForaDoFormulario, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.grupo_fora_do_formulario", "A resposta é de um grupo que o formulário não coleta")),
        new(InscricaoErrorCodes.FatoForaDoGrupo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.fato_fora_do_grupo", "A resposta da ocorrência é de um campo que não pertence ao grupo")),
        new(InscricaoErrorCodes.FatoSoDeGrupo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.fato_so_de_grupo", "A resposta é de um campo que só existe dentro de um grupo")),
        new(InscricaoErrorCodes.GrupoMalFormado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.grupo_mal_formado", "O grupo veio sem a lista de ocorrências ou com uma ocorrência nula")),
        new(InscricaoErrorCodes.AceiteMalFormado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.aceite_mal_formado", "O aceite veio nulo ou sem o código do termo ou o hash da versão")),
        new(InscricaoErrorCodes.AceiteRepetido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.aceite_repetido", "O mesmo termo foi aceito mais de uma vez")),
        new(InscricaoErrorCodes.AceiteForaDoFormulario, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.aceite_fora_do_formulario", "O aceite é de um termo que o formulário não exige")),
        new(InscricaoErrorCodes.AceiteDeOutraVersao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.aceite_de_outra_versao", "O aceite não é da versão do termo que o formulário apresenta")),
        new(InscricaoErrorCodes.ValorForaDoTipo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.valor_fora_do_tipo", "A resposta não tem o tipo que o campo pede")),
        new(InscricaoErrorCodes.CpfDiferenteDoDono, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.cpf_diferente_do_dono", "O CPF informado não é o do titular da conta")),
        new(InscricaoErrorCodes.CpfNaoPodeSerRemovido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.cpf_nao_pode_ser_removido", "O CPF da inscrição não pode ser removido")),
        new(InscricaoErrorCodes.NomeDiferenteDoLogin, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.nome_diferente_do_login", "O nome informado não é o da conta do candidato")),
        new(InscricaoErrorCodes.NomeAusenteNoLogin, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.nome_ausente_no_login", "O login não traz o nome completo do candidato")),
        new(InscricaoErrorCodes.NomeSocialDiferenteDaConta, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.nome_social_diferente_da_conta", "O nome social informado não é o da conta do candidato")),
        new(InscricaoErrorCodes.NomeSocialAusenteNaConta, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.nome_social_ausente_na_conta", "A conta do candidato não tem nome social cadastrado")),
        new(InscricaoErrorCodes.EnderecoSemCep, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.endereco_sem_cep", "O endereço só é aceito com CEP")),
        new(InscricaoErrorCodes.EnderecoComplementoLongo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.endereco_complemento_longo", "O complemento do endereço passa de 300 caracteres")),
        new(InscricaoErrorCodes.EnderecoCepNaoEncontrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.endereco_cep_nao_encontrado", "O CEP informado não foi encontrado")),
        new(InscricaoErrorCodes.EnderecoCepDeOutraCidade, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.endereco_cep_de_outra_cidade", "O CEP informado pertence a outra cidade que a declarada")),
        new(InscricaoErrorCodes.EnderecoCepNaoConfirmado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.endereco_cep_nao_confirmado", "O CEP só resolvido por faixa precisa da confirmação do candidato")),
        new(InscricaoErrorCodes.EnderecoLogradouroObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.endereco_logradouro_obrigatorio", "O logradouro é obrigatório quando o CEP não identifica a rua")),
        new(InscricaoErrorCodes.EnderecoBairroObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.endereco_bairro_obrigatorio", "O bairro é obrigatório quando o CEP não identifica a rua")),
        new(InscricaoErrorCodes.OfertaForaDoProcesso, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.oferta_fora_do_processo", "A oferta de curso escolhida não é deste processo seletivo")),
        new(InscricaoErrorCodes.OfertaObrigatoria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.oferta_obrigatoria", "A inscrição precisa de uma oferta de curso")),
        new(InscricaoErrorCodes.SegundaOpcaoNaoAdmitida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.segunda_opcao_nao_admitida", "A versão do processo em que a inscrição foi aberta admite uma só opção de curso")),
        new(InscricaoErrorCodes.SegundaOpcaoIgualAPrimeira, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.segunda_opcao_igual_a_primeira", "A 2ª opção de curso é a mesma da 1ª")),
        new(InscricaoErrorCodes.SegundaOpcaoSemPrimeira, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.segunda_opcao_sem_primeira", "A 2ª opção de curso só existe com a 1ª opção")),
        new(InscricaoErrorCodes.RascunhoSoAntesDoEnvio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.rascunho_so_antes_do_envio", "Depois do envio, a inscrição só muda por um novo envio")),
        new(InscricaoErrorCodes.ForaDoPeriodo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.fora_do_periodo", "A inscrição está fora do período de inscrição")),
        new(InscricaoErrorCodes.ProcessoSemInscricao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.processo_sem_inscricao", "Este processo seletivo não recebe inscrição pelo sistema")),
        // Veredito de submissão (issue #1859) — só avaliados no POST .../submissao.
        new(InscricaoErrorCodes.ItemObrigatorioAusente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.item_obrigatorio_ausente", "Um item visível e obrigatório está sem resposta")),
        new(InscricaoErrorCodes.Impedimento, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.impedimento", "Uma resposta impede a submissão desta inscrição")),
        new(InscricaoErrorCodes.GrupoContagemInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.grupo_contagem_invalida", "O número de ocorrências do grupo não respeita o mínimo ou o máximo exigido")),
        new(InscricaoErrorCodes.GrupoCandidatoAusente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.grupo_candidato_ausente", "O grupo inclui o candidato, mas nenhuma ocorrência o representa")),
        new(InscricaoErrorCodes.GrupoIdDuplicado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.grupo_id_duplicado", "Duas ocorrências do grupo repetem o mesmo id")),
        new(InscricaoErrorCodes.TermoNaoAceito, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.termo_nao_aceito", "Um termo obrigatório não foi aceito na versão da inscrição")),
        new(InscricaoErrorCodes.NenhumaModalidade, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.nenhuma_modalidade", "As respostas não dão nenhuma das modalidades ofertadas")),
        new(InscricaoErrorCodes.DocumentoObrigatorioAusente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.documento_obrigatorio_ausente", "Falta enviar um documento obrigatório")),
        new(InscricaoErrorCodes.DocumentoObrigatorioEmVerificacao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.documento_obrigatorio_em_verificacao", "Há documento obrigatório com o arquivo ainda em verificação")),
        new(InscricaoErrorCodes.ModalidadeNaoDecidida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.modalidade_nao_decidida", "As respostas ainda não decidem as modalidades em que o candidato concorre")),
        // Derivados do sistema (issue #1859, ADR-0136) — calculados só na submissão.
        new("Inscricao.DataNascimentoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.data_nascimento_invalida", "A data de nascimento não é uma data válida")),
        new("Inscricao.EnderecoResidencialInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.endereco_residencial_invalido", "O endereço residencial não tem a cidade que a conferência no Geo exige")),
        new(InscricaoErrorCodes.ConflitoDeConcorrencia, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.inscricao.conflito_de_concorrencia", "Esta inscrição foi alterada por outra operação", RetryableConflict: true)),
        // Comprovante por número (issue #1860).
        new(InscricaoErrorCodes.ComprovanteNaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.inscricao.comprovante_nao_encontrado", "A inscrição não tem o comprovante com este número")),
    ];
}
