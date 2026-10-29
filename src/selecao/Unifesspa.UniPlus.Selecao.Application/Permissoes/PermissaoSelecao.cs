namespace Unifesspa.UniPlus.Selecao.Application.Permissoes;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Permissões do módulo Seleção (ADR-0078, ADR-0151). Acrescentar uma permissão é
/// acrescentar um membro com o seu <see cref="PermissaoAttribute"/>; nada mais é mantido
/// em paralelo.
/// </summary>
/// <remarks>
/// O valor numérico do membro não sai do processo: o que circula em registro de decisão,
/// resposta da API e frontend é sempre o código.
/// </remarks>
[ModuloDePermissoes("selecao")]
public enum PermissaoSelecao
{
    /// <summary>Manter os motivos de decisão de isenção.</summary>
    [Permissao(
        "selecao:motivos-decisao-isencao:manter",
        "Manter motivos de decisão de isenção",
        "Cria, edita, ativa e desativa os motivos institucionais de decisão de isenção.",
        ClassificacaoDeProtecao.Interno)]
    MotivosDecisaoIsencaoManter = 1,

    /// <summary>Configurar o processo seletivo em rascunho.</summary>
    [Permissao(
        "selecao:processos:configurar",
        "Configurar processo seletivo",
        "Monta e altera a configuração do processo seletivo em rascunho, nos processos da unidade proprietária.",
        ClassificacaoDeProtecao.Interno,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    ProcessosConfigurar = 2,

    /// <summary>Publicar e retificar o processo seletivo.</summary>
    [Permissao(
        "selecao:processos:publicar",
        "Publicar e retificar processo seletivo",
        "Publica o processo seletivo e conduz a sua retificação, nos processos da unidade proprietária.",
        ClassificacaoDeProtecao.Interno,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    ProcessosPublicar = 3,

    /// <summary>Consultar o acompanhamento administrativo do processo seletivo.</summary>
    [Permissao(
        "selecao:processos:acompanhar",
        "Acompanhar processo seletivo",
        "Consulta o acompanhamento administrativo dos processos seletivos da unidade proprietária.",
        ClassificacaoDeProtecao.Interno,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    ProcessosAcompanhar = 4,

    /// <summary>Consultar as inscrições do processo seletivo.</summary>
    [Permissao(
        "selecao:inscricoes:consultar",
        "Consultar inscrições do processo",
        "Consulta as inscrições dos processos seletivos da unidade proprietária, sem os documentos e com o CPF mascarado.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    InscricoesConsultar = 5,

    /// <summary>Consultar os documentos pessoais das inscrições.</summary>
    [Permissao(
        "selecao:documentos-pessoais:consultar",
        "Consultar documentos pessoais das inscrições",
        "Abre os documentos pessoais enviados nas inscrições dos processos seletivos da unidade proprietária.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DocumentosPessoaisConsultar = 6,

    /// <summary>Consultar os documentos sensíveis das inscrições.</summary>
    [Permissao(
        "selecao:documentos-sensiveis:consultar",
        "Consultar documentos sensíveis das inscrições",
        "Abre os documentos sensíveis, como laudos médicos, enviados nas inscrições dos processos seletivos da unidade proprietária.",
        ClassificacaoDeProtecao.Sensivel,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 11, II, a",
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DocumentosSensiveisConsultar = 7,

    /// <summary>Revelar o CPF de uma inscrição.</summary>
    [Permissao(
        "selecao:inscricoes:revelar-cpf",
        "Revelar CPF da inscrição",
        "Revela o CPF completo de uma inscrição de processo seletivo da unidade proprietária.",
        ClassificacaoDeProtecao.Identificador,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    InscricoesRevelarCpf = 8,

    /// <summary>Manter as obrigatoriedades legais.</summary>
    [Permissao(
        "selecao:obrigatoriedades-legais:manter",
        "Manter obrigatoriedades legais",
        "Cria, edita e desativa as obrigatoriedades legais institucionais que o processo seletivo precisa cumprir.",
        ClassificacaoDeProtecao.Interno)]
    ObrigatoriedadesLegaisManter = 9,

    /// <summary>Consultar o catálogo de regras.</summary>
    [Permissao(
        "selecao:regras-catalogo:consultar",
        "Consultar catálogo de regras",
        "Consulta o catálogo institucional de regras e o esquema de argumentos de cada uma, usado para montar a configuração do processo.",
        ClassificacaoDeProtecao.Interno)]
    RegrasCatalogoConsultar = 10,

    /// <summary>Consultar os leiautes de arquivo do INEP.</summary>
    [Permissao(
        "selecao:leiautes-inep:consultar",
        "Consultar leiautes de arquivo do INEP",
        "Consulta os leiautes de arquivo do INEP, para escolher na geração do envio e na importação do retorno das notas.",
        ClassificacaoDeProtecao.Interno)]
    LeiautesInepConsultar = 11,

    /// <summary>Gerar o arquivo de envio dos CPFs ao INEP.</summary>
    [Permissao(
        "selecao:envios-inep:gerar",
        "Gerar envio de CPFs ao INEP",
        "Gera o arquivo com os CPFs completos das inscrições enviadas de uma edição do ENEM, nos processos da unidade proprietária, e consulta os envios gerados.",
        ClassificacaoDeProtecao.Identificador,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    EnviosInepGerar = 12,

    /// <summary>Importar e consultar as notas do ENEM.</summary>
    [Permissao(
        "selecao:notas-enem:importar",
        "Importar notas do ENEM",
        "Importa o arquivo de retorno do INEP com as notas do ENEM e consulta os lotes importados, nos processos da unidade proprietária.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    NotasEnemImportar = 13,
}
