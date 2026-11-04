namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas da leitura da configuração congelada e da reposição dela.
/// </summary>
internal static class ErrosDaLeituraDoCongelado
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        // Seletor de snapshot vigente (ADR-0075/0076): não há publicação
        // vigente ≤ o instante consultado — 422, nunca retorno silencioso.
        new("Snapshot.VigenteAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.snapshot.vigente_ausente", "Nenhuma publicação vigente para o instante")),
        // Renderização pública do formulário de inscrição contra uma versão vigente
        // congelada antes de a apresentação existir no envelope.
        new("FormularioInscricao.VersaoSemApresentacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_inscricao.versao_sem_apresentacao", "A versão publicada vigente não tem apresentação de formulário")),
        // A versão resolvida (pela divulgação ou pela fixada numa inscrição) não tem
        // formulário para a finalidade pedida. Quem lê pela divulgação colapsa no não encontrado
        // único (ver ObterFormularioRenderizavelQueryHandler); quem lê pela inscrição propaga este
        // código, porque a versão fixada já foi provada existente ao iniciar a inscrição.
        new("FormularioInscricao.FinalidadeSemFormulario", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_inscricao.finalidade_sem_formulario", "A versão resolvida não tem formulário para a finalidade pedida")),
        // Leitura pública do certame contra uma configuração congelada cujo bloco não tem a forma
        // que a projeção espera. É falha de leitura, não ausência: não colapsa no não encontrado,
        // pelo mesmo motivo da versão de envelope desconhecida — um certame publicado e visível
        // que some da consulta pública é defeito, e defeito tem de aflorar.
        new("CertamePublicado.EnvelopeInesperado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.certame_publicado.envelope_inesperado", "A configuração congelada não tem a forma que a projeção pública espera")),
        // Os códigos de recusa dos parâmetros de consulta (ConsultaErrorCodes) NÃO se registram
        // aqui: são compartilhados com as listagens de Configuração e vivem uma única vez em
        // KernelDomainErrorRegistration, com taxonomia sem prefixo de módulo. Registrá-los por
        // módulo faria o último da ordem de DI sobrescrever o code do outro no registry global.
        // Reposição da configuração congelada (ADR-0110 D2). Todos 422: são
        // regras de negócio, e o operador que dispara um descarte precisa saber por que ele
        // foi recusado — reidratar mal é pior do que não reidratar.
        new("ProcessoSeletivo.RestauracaoForaDePublicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.restauracao_fora_de_publicado", "Só um processo publicado tem configuração congelada a restaurar")),
        new("ProcessoSeletivo.IdEtapaAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.id_etapa_ausente", "Toda etapa restaurada declara o Id congelado no envelope")),
        // Árvore de satisfação restaurada (ADR-0110 D2) — checagens
        // estruturais equivalentes ao que o banco garante na escrita normal
        // (ux_nos_exigencia_raiz_ordem/ux_nos_exigencia_irmaos_ordem) mais o que só a raiz
        // consegue provar (folha referencia um DocumentoExigido que existe no grafo).
        new("ProcessoSeletivo.IdNoExigenciaDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.id_no_exigencia_duplicado", "O mesmo Id de nó da árvore de satisfação não pode aparecer mais de uma vez na configuração restaurada")),
        new("ProcessoSeletivo.OrdemRaizNoExigenciaDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ordem_raiz_no_exigencia_duplicada", "Cada raiz da árvore de satisfação deve ter uma ordem única dentro do processo")),
        new("ProcessoSeletivo.OrdemIrmaoNoExigenciaDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ordem_irmao_no_exigencia_duplicada", "Cada filho de um mesmo nó pai deve ter uma ordem única entre os irmãos")),
        new("ProcessoSeletivo.NoExigenciaFolhaSemDocumentoExigido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.no_exigencia_folha_sem_documento_exigido", "Uma folha da árvore de satisfação restaurada referencia um DocumentoExigido que não existe na configuração restaurada")),
        new("ProcessoSeletivo.NoExigenciaDocumentoExigidoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.no_exigencia_documento_exigido_duplicado", "Duas folhas da árvore de satisfação restaurada não podem referenciar o mesmo DocumentoExigido")),
        new("ProcessoSeletivo.NoExigenciaFolhaComFilhos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.no_exigencia_folha_com_filhos", "Uma folha da árvore de satisfação restaurada não pode ter filhos")),
        new("ProcessoSeletivo.NoExigenciaTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.no_exigencia_tipo_invalido", "Um nó da árvore de satisfação restaurada tem um tipo desconhecido")),
        new("ProcessoSeletivo.NoExigenciaQuantidadeMinimaAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.no_exigencia_quantidade_minima_ausente", "Uma folha ou grupo OU/N-de da árvore de satisfação restaurada não declara quantidadeMinima")),
        new("ProcessoSeletivo.NoExigenciaConsequenciaNaoCanonica", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.no_exigencia_consequencia_nao_canonica", "Um nó da árvore de satisfação restaurada declara uma consequência que não está na forma canônica")),
        new("ProcessoSeletivo.NoExigenciaGrupoComCampoDeFolha", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.no_exigencia_grupo_com_campo_de_folha", "Um grupo da árvore de satisfação restaurada carrega chaveDistincao, dataReferencia ou ocorrenciasEsperadas — campos exclusivos de folha")),
        new("ProcessoSeletivo.NoExigenciaFolhaComBaseLegal", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.no_exigencia_folha_com_base_legal", "Uma folha da árvore de satisfação restaurada carrega base legal — campo exclusivo de grupo OU/N-de")),
        new("VersaoConfiguracao.VersaoDeOutroProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.versao_de_outro_processo", "A configuração congelada de um certame não se repõe em outro")),
    ];
}
