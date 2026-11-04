namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas dos documentos enviados, dos anexos do edital e dos atos do processo.
/// </summary>
internal static class ErrosDosDocumentosEAtos
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        // Documento que o processo envia para publicar (PDF, DOCX ou ODT) — envio direto por URL
        // pré-assinada. NaoEncontrado é 404 (inexistente ou de outro processo); as demais são
        // recusas de validação (422).
        new(DocumentoAPublicarErrorCodes.NaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.documento_a_publicar.nao_encontrado", "Documento não encontrado")),
        new(DocumentoAPublicarErrorCodes.NaoConfirmado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_a_publicar.nao_confirmado", "Documento ainda não confirmado")),
        new(DocumentoAPublicarErrorCodes.StatusInvalidoParaConfirmacao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_a_publicar.status_invalido_para_confirmacao", "Somente um documento pendente pode ser confirmado")),
        new(DocumentoAPublicarErrorCodes.ObjetoNaoEncontrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_a_publicar.objeto_nao_encontrado", "Arquivo ainda não enviado ao armazenamento ou expirado")),
        new(DocumentoAPublicarErrorCodes.TamanhoExcedido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_a_publicar.tamanho_excedido", "Documento excede o tamanho máximo permitido")),
        new(DocumentoAPublicarErrorCodes.ArquivoVazio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_a_publicar.arquivo_vazio", "O arquivo enviado está vazio")),
        new(DocumentoAPublicarErrorCodes.TipoDeMidiaNaoAceito, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_a_publicar.tipo_de_midia_nao_aceito", "O tipo de mídia deve ser PDF, DOCX ou ODT")),
        new(DocumentoAPublicarErrorCodes.ConteudoDivergeDoFormato, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_a_publicar.conteudo_diverge_do_formato", "O conteúdo do arquivo não corresponde ao tipo de mídia declarado")),
        new(DocumentoAPublicarErrorCodes.ContemMacro, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_a_publicar.contem_macro", "O documento não pode conter macro")),
        new(DocumentoAPublicarErrorCodes.NomeArquivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_a_publicar.nome_arquivo_invalido", "Nome do arquivo do documento inválido")),
        new(DocumentoAPublicarErrorCodes.FinalidadeInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_a_publicar.finalidade_invalida", "A finalidade do documento é obrigatória")),
        new(DocumentoAPublicarErrorCodes.FormatoNaoAceitoParaAFinalidade, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_a_publicar.formato_nao_aceito_para_a_finalidade", "O formato do documento não é aceito para a finalidade declarada")),
        // Documento que o candidato envia para atender uma exigência da inscrição — envio direto por URL
        // pré-assinada. Arquivo de outra pessoa responde 404 como o inexistente.
        new(DocumentoDaInscricaoErrorCodes.ExigenciaNaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.documento_da_inscricao.exigencia_nao_encontrada", "Exigência não encontrada na versão da inscrição")),
        new(DocumentoDaInscricaoErrorCodes.ApresentacaoNaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.documento_da_inscricao.apresentacao_nao_encontrada", "Apresentação não encontrada para a exigência")),
        new(DocumentoDaInscricaoErrorCodes.ArquivoNaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.documento_da_inscricao.arquivo_nao_encontrado", "Arquivo não encontrado na inscrição")),
        new(DocumentoDaInscricaoErrorCodes.OcorrenciaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_da_inscricao.ocorrencia_invalida", "A ocorrência não confere com a exigência")),
        new(DocumentoDaInscricaoErrorCodes.FormatoNaoAceitoPelaExigencia, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_da_inscricao.formato_nao_aceito_pela_exigencia", "O formato do arquivo não é aceito pela exigência")),
        new(DocumentoDaInscricaoErrorCodes.TamanhoExcedido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_da_inscricao.tamanho_excedido", "O arquivo excede o tamanho máximo da exigência")),
        new(DocumentoDaInscricaoErrorCodes.ArquivoVazio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_da_inscricao.arquivo_vazio", "O arquivo enviado está vazio")),
        new(DocumentoDaInscricaoErrorCodes.ObjetoNaoEncontrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_da_inscricao.objeto_nao_encontrado", "Arquivo ainda não enviado ao armazenamento ou expirado")),
        new(DocumentoDaInscricaoErrorCodes.StatusInvalidoParaConfirmacao, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.documento_da_inscricao.status_invalido_para_confirmacao", "Somente um arquivo pendente pode ser confirmado")),
        new(DocumentoDaInscricaoErrorCodes.ETagObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_da_inscricao.etag_obrigatorio", "O ETag do envio é obrigatório")),
        new(DocumentoDaInscricaoErrorCodes.ArquivoNaoVerificado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_da_inscricao.arquivo_nao_verificado", "O arquivo ainda não foi verificado")),
        new(DocumentoDaInscricaoErrorCodes.RemocaoSoNoRascunho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_da_inscricao.remocao_so_no_rascunho", "O arquivo de uma inscrição enviada é retido")),
        new(DocumentoDaInscricaoErrorCodes.InscricaoAlterada, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.documento_da_inscricao.inscricao_alterada", "A inscrição mudou enquanto o arquivo era processado", RetryableConflict: true)),
        // Modelo de documento da exigência — envio direto por URL pré-assinada, como o documento a
        // publicar. NaoEncontrado é 404 (inexistente ou de outro processo); as demais são recusas
        // de validação (422).
        new(ModeloDeDocumentoErrorCodes.NaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.modelo_de_documento.nao_encontrado", "Modelo de documento não encontrado")),
        new(ModeloDeDocumentoErrorCodes.NaoConfirmado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modelo_de_documento.nao_confirmado", "Modelo de documento ainda não confirmado")),
        new(ModeloDeDocumentoErrorCodes.StatusInvalidoParaConfirmacao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modelo_de_documento.status_invalido_para_confirmacao", "Somente um modelo pendente pode ser confirmado")),
        new(ModeloDeDocumentoErrorCodes.ObjetoNaoEncontrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modelo_de_documento.objeto_nao_encontrado", "Arquivo ainda não enviado ao storage ou expirado")),
        new(ModeloDeDocumentoErrorCodes.TamanhoExcedido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modelo_de_documento.tamanho_excedido", "Modelo excede o tamanho máximo permitido")),
        new(ModeloDeDocumentoErrorCodes.FormatoNaoEditavel, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modelo_de_documento.formato_nao_editavel", "O modelo é um documento editável: DOCX ou ODT")),
        new(ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modelo_de_documento.conteudo_diverge_do_formato", "O conteúdo do arquivo não é um documento de texto do formato declarado")),
        new(ModeloDeDocumentoErrorCodes.ContemMacro, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modelo_de_documento.contem_macro", "O modelo não pode conter macro")),
        new(ModeloDeDocumentoErrorCodes.NomeArquivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modelo_de_documento.nome_arquivo_invalido", "Nome do arquivo do modelo inválido")),
        // Anexos do edital na publicação e na retificação.
        new(AnexoDoEditalErrorCodes.NaoEncontrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.anexo_do_edital.nao_encontrado", "Documento do anexo não encontrado ou não pertence a este processo")),
        new(AnexoDoEditalErrorCodes.NaoConfirmado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.anexo_do_edital.nao_confirmado", "Somente um documento confirmado pode ser anexado ao edital")),
        new(AnexoDoEditalErrorCodes.Repetido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.anexo_do_edital.repetido", "O mesmo documento não pode constar duas vezes no edital")),
        new(AnexoDoEditalErrorCodes.FinalidadeIncompativel, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.anexo_do_edital.finalidade_incompativel", "Somente um documento enviado como anexo do edital pode ser anexado ao edital")),
        new(AnexoDoEditalErrorCodes.NomeInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.anexo_do_edital.nome_invalido", "Nome do anexo inválido")),
        new(AnexoDoEditalErrorCodes.QuantidadeExcedida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.anexo_do_edital.quantidade_excedida", "O edital aceita no máximo 20 anexos")),
        // Ato avulso do processo e correção de ato publicado (ADR-0103; UNI-REQ-0155 e UNI-REQ-0156).
        new(AtoDoProcessoErrorCodes.CertameNaoDivulgado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.certame_nao_divulgado", "O processo ainda não tem página pública para receber o ato")),
        new(AtoDoProcessoErrorCodes.TipoCongelaConfiguracao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.tipo_congela_configuracao", "O tipo congela configuração e não é documento avulso")),
        new(AtoDoProcessoErrorCodes.TipoNaoPublicavelComoAvulso, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.tipo_nao_publicavel_como_avulso", "O tipo não é publicável como documento avulso")),
        new(AtoDoProcessoErrorCodes.TipoExigeAtoCorrigido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.tipo_exige_ato_corrigido", "O tipo corrige sempre outro ato: informe o ato corrigido e o motivo")),
        new(AtoDoProcessoErrorCodes.DocumentoNaoEncontrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.documento_nao_encontrado", "Documento do ato não encontrado ou não pertence a este processo")),
        new(AtoDoProcessoErrorCodes.DocumentoNaoConfirmado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.documento_nao_confirmado", "Somente um documento confirmado pode ser referenciado no ato")),
        new(AtoDoProcessoErrorCodes.DocumentoNaoEnviadoComoDocumentoDeAto, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.documento_nao_enviado_como_documento_de_ato", "Somente um documento enviado para um ato pode ser usado no ato")),
        new(AtoDoProcessoErrorCodes.DocumentoPrincipalNaoEhPdf, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.documento_principal_nao_eh_pdf", "O documento principal do ato deve ser um PDF")),
        new(AtoDoProcessoErrorCodes.DocumentoRepetido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.documento_repetido", "O mesmo documento não pode constar duas vezes no ato")),
        new(AtoDoProcessoErrorCodes.DocumentoJaUsadoEmOutroAto, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.documento_ja_usado_em_outro_ato", "O documento já foi usado em outro ato do processo")),
        new(AtoDoProcessoErrorCodes.AnexoNomeInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.anexo_nome_invalido", "Nome do anexo do ato inválido")),
        new(AtoDoProcessoErrorCodes.AnexoNomeRepetido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.anexo_nome_repetido", "Dois anexos do ato têm o mesmo nome")),
        new(AtoDoProcessoErrorCodes.QuantidadeDeAnexosExcedida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.quantidade_de_anexos_excedida", "O ato aceita no máximo 20 anexos")),
        new(AtoDoProcessoErrorCodes.AtoCorrigidoNaoEncontrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.ato_corrigido_nao_encontrado", "O ato corrigido não é um ato publicado por um processo seletivo")),
        new(AtoDoProcessoErrorCodes.AtoCorrigidoDeOutroProcesso, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.ato_corrigido_de_outro_processo", "O ato corrigido é de outro processo seletivo")),
        new(AtoDoProcessoErrorCodes.AtoCorrigidoDeOutraClasse, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.ato_corrigido_de_outra_classe", "O ato corrigido congela configuração e só se corrige por retificação do edital")),
        new(AtoDoProcessoErrorCodes.AtoCorrigidoJaCorrigido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.ato_corrigido_ja_corrigido", "O ato corrigido já foi corrigido; a cadeia de correção é linear")),
        new(AtoDoProcessoErrorCodes.AtoCorrigidoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.ato_corrigido_obrigatorio", "Informe o ato corrigido: o motivo da correção só existe junto dele")),
        new(AtoDoProcessoErrorCodes.AtoCorrigidoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.ato_corrigido_invalido", "O ato corrigido informado é inválido")),
        new(AtoDoProcessoErrorCodes.MotivoDaCorrecaoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.motivo_da_correcao_obrigatorio", "O motivo da correção é obrigatório quando o ato corrige outro")),
        // Publicação de item do edital e correção de resultado (UNI-REQ-0157, UNI-REQ-0158, UNI-REQ-0162, UNI-REQ-0163).
        new(AtoDoProcessoErrorCodes.ProdutoNaoDeclarado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.produto_nao_declarado", "O item não consta na versão vigente do edital")),
        new(AtoDoProcessoErrorCodes.ProdutoJaPublicado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.produto_ja_publicado", "O item do edital já foi publicado; a correção é errata ou retificação do ato")),
        new(AtoDoProcessoErrorCodes.ProdutoAguardaRegistro, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.produto_aguarda_registro", "O item do edital foi enviado e aguarda a confirmação do registro")),
        new(AtoDoProcessoErrorCodes.PreliminarNaoPublicado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.preliminar_nao_publicado", "O resultado definitivo só sai depois do preliminar da mesma matéria")),
        new(AtoDoProcessoErrorCodes.PreliminarAguardaRegistro, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.preliminar_aguarda_registro", "O resultado preliminar da mesma matéria aguarda a confirmação do registro")),
        new(AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.politica_de_divulgacao_nao_confirmada", "A publicação de resultado exige a confirmação da política de divulgação do certame")),
        new(AtoDoProcessoErrorCodes.UsuarioNaoIdentificado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.usuario_nao_identificado", "O token não identifica quem confirma a política de divulgação")),
        new(AtoDoProcessoErrorCodes.AtoCorrigidoNaoEhDoProduto, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.ato_corrigido_nao_eh_do_produto", "O ato corrigido não é uma publicação do item informado")),
        new(AtoDoProcessoErrorCodes.PreliminarFechadoPeloDefinitivo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.preliminar_fechado_pelo_definitivo", "O resultado preliminar não se corrige depois do definitivo; corrija o definitivo")),
        new(AtoDoProcessoErrorCodes.AtoCorrigidoEhItemDoEdital, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.ato_do_processo.ato_corrigido_eh_item_do_edital", "O ato corrigido é um item do edital e só se corrige pela publicação do item")),
    ];
}
