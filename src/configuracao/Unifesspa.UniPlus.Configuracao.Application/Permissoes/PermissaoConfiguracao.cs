namespace Unifesspa.UniPlus.Configuracao.Application.Permissoes;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Permissões do módulo Configuração (ADR-0078, ADR-0151). Cada cadastro tem a sua permissão de
/// manutenção, e a de consulta só existe onde a leitura administrativa é restrita; a leitura
/// pública dos cadastros não tem permissão.
/// </summary>
/// <remarks>
/// O valor numérico do membro não sai do processo: o que circula em registro de decisão,
/// resposta da API e frontend é sempre o código.
/// </remarks>
[ModuloDePermissoes("configuracao")]
public enum PermissaoConfiguracao
{
    /// <summary>Manter cadastro da base legal do bônus regional.</summary>
    [Permissao(
        "configuracao:base-legal-bonus-regional:manter",
        "Manter cadastro da base legal do bônus regional",
        "Cria, altera e remove a base legal do bônus regional.",
        ClassificacaoDeProtecao.Interno)]
    BaseLegalBonusRegionalManter = 1,

    /// <summary>Manter cadastro dos calendários de dias úteis.</summary>
    [Permissao(
        "configuracao:calendarios-dias-uteis:manter",
        "Manter cadastro dos calendários de dias úteis",
        "Cria, altera e remove os calendários de dias úteis e os seus dias não úteis.",
        ClassificacaoDeProtecao.Interno)]
    CalendariosDiasUteisManter = 2,

    /// <summary>Manter cadastro dos campi.</summary>
    [Permissao(
        "configuracao:campi:manter",
        "Manter cadastro dos campi",
        "Cria, altera e remove os campi da instituição.",
        ClassificacaoDeProtecao.Interno)]
    CampiManter = 3,

    /// <summary>Manter cadastro das categorias de documento.</summary>
    [Permissao(
        "configuracao:categorias-documento:manter",
        "Manter cadastro das categorias de documento",
        "Cria, altera e remove as categorias de documento.",
        ClassificacaoDeProtecao.Interno)]
    CategoriasDocumentoManter = 4,

    /// <summary>Manter cadastro das condições de atendimento.</summary>
    [Permissao(
        "configuracao:condicoes-atendimento:manter",
        "Manter cadastro das condições de atendimento",
        "Cria, altera e remove as condições de atendimento.",
        ClassificacaoDeProtecao.Interno)]
    CondicoesAtendimentoManter = 5,

    /// <summary>Manter cadastro dos cursos.</summary>
    [Permissao(
        "configuracao:cursos:manter",
        "Manter cadastro dos cursos",
        "Cria, altera e remove os cursos.",
        ClassificacaoDeProtecao.Interno)]
    CursosManter = 6,

    /// <summary>Manter cadastro das fases canônicas.</summary>
    [Permissao(
        "configuracao:fases-canonicas:manter",
        "Manter cadastro das fases canônicas",
        "Cria, altera e remove as fases canônicas dos processos seletivos.",
        ClassificacaoDeProtecao.Interno)]
    FasesCanonicasManter = 7,

    /// <summary>Manter cadastro dos fatos do candidato.</summary>
    [Permissao(
        "configuracao:fatos-candidato:manter",
        "Manter cadastro dos fatos do candidato",
        "Cria, altera e remove o catálogo de fatos do candidato, as suas regras padrão e os seus valores.",
        ClassificacaoDeProtecao.Interno)]
    FatosCandidatoManter = 8,

    /// <summary>Consultar cadastro dos fatos do candidato.</summary>
    [Permissao(
        "configuracao:fatos-candidato:consultar",
        "Consultar cadastro dos fatos do candidato",
        "Consulta na área administrativa o catálogo de fatos do candidato, as suas regras padrão e os seus valores, inclusive o que não está ativo ou publicado.",
        ClassificacaoDeProtecao.Interno)]
    FatosCandidatoConsultar = 9,

    /// <summary>Manter cadastro dos locais de oferta.</summary>
    [Permissao(
        "configuracao:locais-oferta:manter",
        "Manter cadastro dos locais de oferta",
        "Cria, altera e remove os locais de oferta.",
        ClassificacaoDeProtecao.Interno)]
    LocaisOfertaManter = 10,

    /// <summary>Manter cadastro das modalidades de concorrência.</summary>
    [Permissao(
        "configuracao:modalidades:manter",
        "Manter cadastro das modalidades de concorrência",
        "Cria, altera e remove as modalidades de concorrência.",
        ClassificacaoDeProtecao.Interno)]
    ModalidadesManter = 11,

    /// <summary>Manter cadastro dos modelos de formulário.</summary>
    [Permissao(
        "configuracao:modelos-formulario:manter",
        "Manter cadastro dos modelos de formulário",
        "Cria, altera e remove os modelos de formulário de inscrição.",
        ClassificacaoDeProtecao.Interno)]
    ModelosFormularioManter = 12,

    /// <summary>Consultar cadastro dos modelos de formulário.</summary>
    [Permissao(
        "configuracao:modelos-formulario:consultar",
        "Consultar cadastro dos modelos de formulário",
        "Consulta na área administrativa os modelos de formulário de inscrição, inclusive o que não está ativo ou publicado.",
        ClassificacaoDeProtecao.Interno)]
    ModelosFormularioConsultar = 13,

    /// <summary>Manter cadastro das ofertas de curso.</summary>
    [Permissao(
        "configuracao:ofertas-curso:manter",
        "Manter cadastro das ofertas de curso",
        "Cria, altera e remove as ofertas de curso.",
        ClassificacaoDeProtecao.Interno)]
    OfertasCursoManter = 14,

    /// <summary>Manter cadastro dos pesos por área do ENEM.</summary>
    [Permissao(
        "configuracao:pesos-area-enem:manter",
        "Manter cadastro dos pesos por área do ENEM",
        "Cria, altera e remove os pesos por área do ENEM.",
        ClassificacaoDeProtecao.Interno)]
    PesosAreaEnemManter = 15,

    /// <summary>Manter cadastro das precedências entre fases.</summary>
    [Permissao(
        "configuracao:precedencias-fase:manter",
        "Manter cadastro das precedências entre fases",
        "Cria, altera e remove as precedências entre as fases.",
        ClassificacaoDeProtecao.Interno)]
    PrecedenciasFaseManter = 16,

    /// <summary>Manter cadastro dos recursos de acessibilidade.</summary>
    [Permissao(
        "configuracao:recursos-acessibilidade:manter",
        "Manter cadastro dos recursos de acessibilidade",
        "Cria, altera e remove os recursos de acessibilidade.",
        ClassificacaoDeProtecao.Interno)]
    RecursosAcessibilidadeManter = 17,

    /// <summary>Manter cadastro das referências de reserva demográfica.</summary>
    [Permissao(
        "configuracao:referencias-reserva-demografica:manter",
        "Manter cadastro das referências de reserva demográfica",
        "Cria, altera e remove as referências de reserva demográfica.",
        ClassificacaoDeProtecao.Interno)]
    ReferenciasReservaDemograficaManter = 18,

    /// <summary>Manter cadastro dos termos de consentimento.</summary>
    [Permissao(
        "configuracao:termos-consentimento:manter",
        "Manter cadastro dos termos de consentimento",
        "Cria, altera e remove os termos de consentimento, da redação à promoção da versão vigente.",
        ClassificacaoDeProtecao.Interno)]
    TermosConsentimentoManter = 19,

    /// <summary>Manter cadastro dos tipos de banca.</summary>
    [Permissao(
        "configuracao:tipos-banca:manter",
        "Manter cadastro dos tipos de banca",
        "Cria, altera e remove os tipos de banca.",
        ClassificacaoDeProtecao.Interno)]
    TiposBancaManter = 20,

    /// <summary>Manter cadastro dos tipos de deficiência.</summary>
    [Permissao(
        "configuracao:tipos-deficiencia:manter",
        "Manter cadastro dos tipos de deficiência",
        "Cria, altera e remove os tipos de deficiência.",
        ClassificacaoDeProtecao.Interno)]
    TiposDeficienciaManter = 21,

    /// <summary>Manter cadastro dos tipos de documento.</summary>
    [Permissao(
        "configuracao:tipos-documento:manter",
        "Manter cadastro dos tipos de documento",
        "Cria, altera e remove os tipos de documento.",
        ClassificacaoDeProtecao.Interno)]
    TiposDocumentoManter = 22,

    /// <summary>Manter cadastro dos tipos de etapa.</summary>
    [Permissao(
        "configuracao:tipos-etapa:manter",
        "Manter cadastro dos tipos de etapa",
        "Cria, altera e remove os tipos de etapa.",
        ClassificacaoDeProtecao.Interno)]
    TiposEtapaManter = 23,

    /// <summary>Manter cadastro dos tipos de processo seletivo.</summary>
    [Permissao(
        "configuracao:tipos-processo:manter",
        "Manter cadastro dos tipos de processo seletivo",
        "Cria, altera e remove os tipos de processo seletivo.",
        ClassificacaoDeProtecao.Interno)]
    TiposProcessoManter = 24,

    /// <summary>Consultar cadastro dos tipos de processo seletivo.</summary>
    [Permissao(
        "configuracao:tipos-processo:consultar",
        "Consultar cadastro dos tipos de processo seletivo",
        "Consulta na área administrativa os tipos de processo seletivo, inclusive o que não está ativo ou publicado.",
        ClassificacaoDeProtecao.Interno)]
    TiposProcessoConsultar = 25,
}
