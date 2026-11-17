namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas do acesso ao processo, dos catálogos de apoio e da configuração base do processo seletivo. As da
/// etapa e as dos formulários vêm dos assuntos próprios; a ordem vale, porque o registro global guarda o último
/// mapeamento de um código repetido.
/// </summary>
internal static class ErrosDoProcessoSeletivo
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. AcessoEConsultaDoProcesso(),
        .. ContagemDosPrazos(),
        .. CalendarioDeDiasUteisCongelado(),
        .. TiposCongelados(),
        .. CatalogosDeApoio(),
        .. ErrosDaEtapaDoProcesso.Mapeamentos(),
        .. IdentificacaoDoCertame(),
        .. BonusRegionalDoProcesso(),
        .. ErrosDosFormulariosDoProcesso.Mapeamentos(),
        .. AtendimentoEspecializado(),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> AcessoEConsultaDoProcesso() =>
    [
        // Acesso às operações do processo, decidido por concessão na unidade dona (ADR-0078, ADR-0151).
        new(ProcessoSeletivoErrorCodes.SemPermissaoNaUnidade, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.auth.forbidden", "Acesso negado")),
        // Tem a permissão, mas está inscrita no processo (Lei nº 9.784/1999, art. 18, I): atua nele só como candidata.
        new(ProcessoSeletivoErrorCodes.ImpedidoPorInscricaoNoProcesso, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.auth.impedido_por_inscricao_no_processo", "Impedido de atuar no processo seletivo")),
        // O token não traz o que identifica o solicitante: não houve decisão de acesso a tomar, e a borda responde 401.
        new(ProcessoSeletivoErrorCodes.IdentidadeIncompleta, new DomainErrorMapping(StatusCodes.Status401Unauthorized, "uniplus.selecao.processo_seletivo.identidade_incompleta", "Identidade do solicitante incompleta")),
        new("ProcessoSeletivo.NaoEncontrado", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.processo_seletivo.nao_encontrado", "Processo Seletivo não encontrado")),
        new("ProcessoSeletivo.OcorrenciaSimuladaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ocorrencia_simulada_invalida", "Ocorrência simulada sem identidade própria no grupo")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> ContagemDosPrazos() =>
    [
        new("ProcessoSeletivo.LocalidadeAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.localidade_ausente", "Localidade que rege a contagem dos prazos não declarada")),
        new("ProcessoSeletivo.FusoInstitucionalNaoReconhecido", new DomainErrorMapping(StatusCodes.Status500InternalServerError, "uniplus.selecao.processo_seletivo.fuso_institucional_nao_reconhecido", "O fuso institucional aplicado não é uma zona reconhecida")),
        new("ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.algoritmo_contagem_prazo_nao_declarado", "Convenção de contagem dos prazos não declarada")),
        new("ProcessoSeletivo.CalendarioVigenteAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.calendario_vigente_ausente", "Não há calendário de dias úteis vigente cadastrado")),
        new(DiaNaoUtilCongeladoErrorCodes.DataAusente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.dia_nao_util_congelado.data_ausente", "A data do dia não útil congelado não pode ser o valor default")),
        new(DiaNaoUtilCongeladoErrorCodes.AbrangenciaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.dia_nao_util_congelado.abrangencia_invalida", "Abrangência do dia não útil fora do vocabulário do calendário")),
        new(DiaNaoUtilCongeladoErrorCodes.MunicipioEmDiaEstadual, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.dia_nao_util_congelado.municipio_em_dia_estadual", "Dia de abrangência estadual não carrega município")),
        new(DiaNaoUtilCongeladoErrorCodes.UfAusenteEmDiaEstadual, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.dia_nao_util_congelado.uf_ausente_em_dia_estadual", "Dia de abrangência estadual exige a UF")),
        new(DiaNaoUtilCongeladoErrorCodes.UfInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.dia_nao_util_congelado.uf_invalida", "UF do dia não útil não é uma unidade federativa reconhecida")),
        new(DiaNaoUtilCongeladoErrorCodes.TerritorioEmDiaSemRecorte, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.dia_nao_util_congelado.territorio_em_dia_sem_recorte", "Dia sem recorte territorial não carrega município nem UF")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> CalendarioDeDiasUteisCongelado() =>
    [
        new(CalendarioDiasUteisCongeladoErrorCodes.OrigemObrigatoria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.calendario_dias_uteis_congelado.origem_obrigatoria", "Identificador do calendário de origem é obrigatório")),
        new(CalendarioDiasUteisCongeladoErrorCodes.VersaoDatasetObrigatoria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.calendario_dias_uteis_congelado.versao_dataset_obrigatoria", "Versão do dataset do calendário é obrigatória")),
        new(CalendarioDiasUteisCongeladoErrorCodes.VersaoDatasetTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.calendario_dias_uteis_congelado.versao_dataset_tamanho", "Versão do dataset do calendário excede o tamanho permitido")),
        new(CalendarioDiasUteisCongeladoErrorCodes.DiasObrigatorios, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.calendario_dias_uteis_congelado.dias_obrigatorios", "A lista de dias não úteis congelados é obrigatória")),
        new(CalendarioDiasUteisCongeladoErrorCodes.SemDiaNaoUtil, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.calendario_dias_uteis_congelado.sem_dia_nao_util", "O calendário congelado precisa ter ao menos um dia não útil")),
        new(CalendarioDiasUteisCongeladoErrorCodes.DiaDuplicado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.calendario_dias_uteis_congelado.dia_duplicado", "O calendário congelado traz o mesmo dia não útil mais de uma vez")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> TiposCongelados() =>
    [
        new("TipoProcessoSnapshot.OrigemIdObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.tipo_processo_snapshot.origem_id_obrigatorio", "Origem do tipo de processo seletivo é obrigatória")),
        new("TipoProcessoSnapshot.CodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.tipo_processo_snapshot.codigo_obrigatorio", "Código do tipo de processo seletivo é obrigatório")),
        new("TipoProcessoSnapshot.NomeObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.tipo_processo_snapshot.nome_obrigatorio", "Nome do tipo de processo seletivo é obrigatório")),
        new("TipoProcessoSnapshot.TamanhoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.tipo_processo_snapshot.tamanho_invalido", "Snapshot do tipo de processo seletivo excede o tamanho permitido")),
        new("TipoEtapaSnapshot.OrigemIdObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.tipo_etapa_snapshot.origem_id_obrigatorio", "Origem do tipo de etapa é obrigatória")),
        new("TipoEtapaSnapshot.CodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.tipo_etapa_snapshot.codigo_obrigatorio", "Código do tipo de etapa é obrigatório")),
        new("TipoEtapaSnapshot.NomeObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.tipo_etapa_snapshot.nome_obrigatorio", "Nome do tipo de etapa é obrigatório")),
        new("TipoEtapaSnapshot.SemCaraterAdmitido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.tipo_etapa_snapshot.sem_carater_admitido", "Snapshot do tipo de etapa não admite caráter nenhum")),
        new("TipoEtapaSnapshot.NotaDeOrigemNoEnemSemPontuacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.tipo_etapa_snapshot.nota_de_origem_no_enem_sem_pontuacao", "Snapshot do tipo de etapa com nota de origem no ENEM deve admitir compor a nota final")),
        new("TipoEtapaSnapshot.CaractereNulo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.tipo_etapa_snapshot.caractere_nulo", "Snapshot do tipo de etapa contém caractere nulo ou que não é texto")),
        new("TipoEtapaSnapshot.TamanhoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.tipo_etapa_snapshot.tamanho_invalido", "Snapshot do tipo de etapa excede o tamanho permitido")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> CatalogosDeApoio() =>
    [
        new("GrupoAreaEnemSnapshot.CodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_area_enem_snapshot.codigo_obrigatorio", "Código do grupo de área do ENEM é obrigatório")),
        new("GrupoAreaEnemSnapshot.RotuloObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_area_enem_snapshot.rotulo_obrigatorio", "Rótulo do grupo de área do ENEM é obrigatório")),
        new("GrupoAreaEnemSnapshot.CaractereNulo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_area_enem_snapshot.caractere_nulo", "Grupo de área do ENEM contém caractere nulo ou que não é texto")),
        new("OfertaCursoSnapshot.Invalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_curso_snapshot.invalida", "A oferta de curso não tem curso, grau, unidade ofertante e turno válidos")),
        new("GrupoAreaEnemSnapshot.TamanhoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_area_enem_snapshot.tamanho_invalido", "Grupo de área do ENEM excede o tamanho permitido")),
        new("CalendarioVigente.FormaTerritorialInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.calendario_vigente.forma_territorial_invalida", "O calendário vigente traz dia com recorte territorial incoerente com a abrangência")),
        new("ProcessoSeletivo.AlgoritmoContagemPrazoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.algoritmo_contagem_prazo_nao_encontrado", "Algoritmo de contagem não encontrado no catálogo de regras")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> IdentificacaoDoCertame() =>
    [
        new(ProcessoSeletivoErrorCodes.IdentificadorLegivelTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.identificador_legivel_tamanho", "O identificador legível deve ter entre 3 e 64 caracteres")),
        new(ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.identificador_legivel_formato_invalido", "O identificador legível deve estar em kebab-case")),
        new(ProcessoSeletivoErrorCodes.IdentificadorLegivelComFormatoDeGuid, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.identificador_legivel_com_formato_de_guid", "O identificador legível não pode ter a forma de um identificador técnico")),
        new(ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.identificador_legivel_ausente", "O identificador legível é obrigatório")),
        new(ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.identificador_legivel_imutavel", "O identificador legível já consta em versão publicada e não pode ser alterado")),
        new(ProcessoSeletivoErrorCodes.OpcoesDeCursoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.opcoes_de_curso_invalido", "O número de opções de curso deve ser 1 ou 2")),
        new(ProcessoSeletivoErrorCodes.ConflitoDeConcorrencia, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.processo_seletivo.conflito_de_concorrencia", "O processo foi alterado concorrentemente; tente novamente", RetryableConflict: true)),
        new(ProcessoSeletivoErrorCodes.OpcoesDeCursoAcimaDasOfertas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.opcoes_de_curso_acima_das_ofertas", "Com 2 opções de curso, cadastre pelo menos dois cursos")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> BonusRegionalDoProcesso() =>
    [
        new(ProcessoSeletivoErrorCodes.BonusRegionalAplicaSemConfiguracao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.bonus_regional_aplica_sem_configuracao", "O processo que aplica o bônus regional precisa da configuração do bônus")),
        new(ProcessoSeletivoErrorCodes.BonusRegionalNaoAplicaComConfiguracao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.bonus_regional_nao_aplica_com_configuracao", "O processo que não aplica o bônus regional não pode ter a configuração do bônus")),
        new(ProcessoSeletivoErrorCodes.FatoElegibilidadeAusente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_elegibilidade_ausente", "O processo com inscrição própria que aplica o acréscimo precisa citar o fato que decide quem o recebe")),
        new(ProcessoSeletivoErrorCodes.FatoElegibilidadeForaDoProcesso, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_elegibilidade_fora_do_processo", "O fato que decide quem recebe o acréscimo não é coletado na inscrição nem derivado pelo processo")),
        new(ProcessoSeletivoErrorCodes.FatoElegibilidadeDeMembroDeGrupo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_elegibilidade_de_membro_de_grupo", "O fato que decide quem recebe o acréscimo é campo de membro de grupo, e o acréscimo é decidido por um fato do candidato")),
        new(ProcessoSeletivoErrorCodes.FatoElegibilidadeNaoBooleano, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_elegibilidade_nao_booleano", "O fato que decide quem recebe o acréscimo precisa ser booleano")),
        new(ProcessoSeletivoErrorCodes.FatoElegibilidadeConhecidoDepoisDaInscricao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_elegibilidade_conhecido_depois_da_inscricao", "O fato que decide quem recebe o acréscimo só é conhecido depois da inscrição")),
        new(ProcessoSeletivoErrorCodes.FatoElegibilidadeDaIsencao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_elegibilidade_da_isencao", "O fato que decide quem recebe o acréscimo depende do que só a isenção coleta")),
        new(ProcessoSeletivoErrorCodes.IdentificadorLegivelEmUso, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.processo_seletivo.identificador_legivel_em_uso", "O identificador legível já é usado por outro processo seletivo")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> AtendimentoEspecializado() =>
    [
        new("AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.atendimento_especializado.tipo_deficiencia_sem_condicao_pcd", "Tipo de deficiência só pode ser ofertado sob a condição PcD")),
        new("AtendimentoEspecializado.CondicaoNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.atendimento_especializado.condicao_nao_encontrada", "Condição de atendimento não encontrada ou não está mais viva")),
        new("AtendimentoEspecializado.RecursoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.atendimento_especializado.recurso_nao_encontrado", "Recurso de acessibilidade não encontrado ou não está mais vivo")),
        new("AtendimentoEspecializado.TipoDeficienciaNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.atendimento_especializado.tipo_deficiencia_nao_encontrado", "Tipo de deficiência não encontrado ou não está mais vivo")),
        new("AtendimentoEspecializado.CondicaoDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.atendimento_especializado.condicao_duplicada", "Condição de atendimento duplicada no atendimento especializado")),
        new("AtendimentoEspecializado.RecursoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.atendimento_especializado.recurso_duplicado", "Recurso de acessibilidade duplicado no atendimento especializado")),
        new("AtendimentoEspecializado.TipoDeficienciaDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.atendimento_especializado.tipo_deficiencia_duplicado", "Tipo de deficiência duplicado no atendimento especializado")),
        new("ProcessoSeletivo.CondicaoAtendimentoReferenciadaPorExigenciaViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.condicao_atendimento_referenciada_por_exigencia_viva", "Existe condição de gatilho documental referenciando um código de condição de atendimento que deixaria de ser ofertado")),
        new(ProcessoSeletivoErrorCodes.RecursoAcessibilidadeReferenciadoPorCondicaoViva, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.recurso_acessibilidade_referenciado_por_condicao_viva", "Existe condição referenciando um código de recurso de acessibilidade que deixaria de ser ofertado")),
    ];
}
