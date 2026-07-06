namespace Unifesspa.UniPlus.Selecao.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, SelecaoDomainErrorRegistration>().")]
internal sealed class SelecaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new("Edital.NaoEncontrado", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.edital.nao_encontrado", "Edital não encontrado")),
        new("Edital.JaPublicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.edital.ja_publicado", "Edital já publicado")),
        new("NumeroEdital.Invalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.numero_edital.invalido", "Número de edital inválido")),
        new("NumeroEdital.AnoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.numero_edital.ano_invalido", "Ano do edital inválido")),
        new("PeriodoInscricao.Invalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.periodo_inscricao.invalido", "Período de inscrição inválido")),
        new("FormulaCalculo.FatorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formula_calculo.fator_invalido", "Fator de divisão inválido")),
        new("FormulaCalculo.BonusInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formula_calculo.bonus_invalido", "Bônus regional inválido")),
        new("Inscricao.StatusInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.status_invalido", "Status de inscrição inválido")),
        // ObrigatoriedadeLegal forma plena (Story #460, ADR-0058). Códigos do
        // placeholder #459 preservados; novos códigos refletem invariantes da
        // forma plena (vigência, governance, hash UNIQUE, regra duplicada).
        new("ObrigatoriedadeLegal.RegraCodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.regra_codigo_obrigatorio", "RegraCodigo obrigatório")),
        new("ObrigatoriedadeLegal.RegraCodigoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.regra_codigo_invalido", "RegraCodigo inválido")),
        new("ObrigatoriedadeLegal.RegraCodigoDuplicada", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.obrigatoriedade_legal.regra_codigo_duplicada", "Já existe regra ativa com este RegraCodigo")),
        new("ObrigatoriedadeLegal.PredicadoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.predicado_obrigatorio", "Predicado obrigatório")),
        new("ObrigatoriedadeLegal.BaseLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.base_legal_obrigatoria", "BaseLegal obrigatória")),
        new("ObrigatoriedadeLegal.BaseLegalInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.base_legal_invalida", "BaseLegal inválida")),
        new("ObrigatoriedadeLegal.DescricaoHumanaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.descricao_humana_obrigatoria", "DescricaoHumana obrigatória")),
        new("ObrigatoriedadeLegal.DescricaoHumanaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.descricao_humana_invalida", "DescricaoHumana inválida")),
        new("ObrigatoriedadeLegal.TipoEditalCodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_edital_codigo_obrigatorio", "TipoEditalCodigo obrigatório")),
        new("ObrigatoriedadeLegal.TipoEditalCodigoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_edital_codigo_invalido", "TipoEditalCodigo inválido")),
        new("ObrigatoriedadeLegal.CategoriaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.categoria_invalida", "Categoria inválida")),
        new("ObrigatoriedadeLegal.AtoNormativoUrlInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.ato_normativo_url_invalido", "AtoNormativoUrl inválido")),
        new("ObrigatoriedadeLegal.PortariaInternaCodigoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.portaria_interna_codigo_invalido", "PortariaInternaCodigo inválido")),
        new("ObrigatoriedadeLegal.VigenciaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.vigencia_invalida", "Vigência inválida")),
        new("ObrigatoriedadeLegal.HashColisao", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.obrigatoriedade_legal.hash_colisao", "Colisão de hash de regra ativa")),
        new("ObrigatoriedadeLegal.NaoEncontrada", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.obrigatoriedade_legal.nao_encontrada", "ObrigatoriedadeLegal não encontrada")),
        // Conformidade (Story #461). Snapshot ausente em edital não publicado.
        new("Conformidade.SnapshotNaoDisponivel", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.conformidade.snapshot_nao_disponivel", "Snapshot de conformidade indisponível — edital não publicado")),
        // Configuração do Processo Seletivo (Story #758, UNI-REQ-0014/0015) — F0.
        // Invariantes do agregado-raiz nesta fatia: etapas e atendimento
        // especializado (ADR-0067). Vagas/bônus/desempate/classificação entram
        // nas fatias F2–F4 sobre o rol_de_regras, com seus próprios códigos.
        new("ProcessoSeletivo.NaoEncontrado", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.processo_seletivo.nao_encontrado", "Processo Seletivo não encontrado")),
        new("ProcessoSeletivo.EtapasVazias", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapas_vazias", "O processo deve ter ao menos uma etapa pontuada")),
        new("ProcessoSeletivo.OrdemEtapaDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ordem_etapa_duplicada", "Ordem de etapa duplicada no processo")),
        new("ProcessoSeletivo.NenhumaEtapaComponeNota", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.nenhuma_etapa_compoe_nota", "Ao menos uma etapa deve compor a nota final")),
        new("OfertaAtendimento.TipoDeficienciaSemCondicaoPcd", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.tipo_deficiencia_sem_condicao_pcd", "Tipo de deficiência só pode ser ofertado sob a condição PcD")),
        new("OfertaAtendimento.CondicaoNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.condicao_nao_encontrada", "Condição de atendimento não encontrada ou não está mais viva")),
        new("OfertaAtendimento.RecursoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.recurso_nao_encontrado", "Recurso de acessibilidade não encontrado ou não está mais vivo")),
        new("OfertaAtendimento.TipoDeficienciaNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.tipo_deficiencia_nao_encontrado", "Tipo de deficiência não encontrado ou não está mais vivo")),
        new("OfertaAtendimento.CondicaoDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.condicao_duplicada", "Condição de atendimento duplicada na oferta")),
        new("OfertaAtendimento.RecursoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.recurso_duplicado", "Recurso de acessibilidade duplicado na oferta")),
        new("OfertaAtendimento.TipoDeficienciaDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.tipo_deficiencia_duplicado", "Tipo de deficiência duplicado na oferta")),
        // Cursor.* codes vivem em Infrastructure.Core/Pagination/PaginationDomainErrorRegistration —
        // capability cross-module, registrada uma única vez via AddCursorPagination().
    ];
}
