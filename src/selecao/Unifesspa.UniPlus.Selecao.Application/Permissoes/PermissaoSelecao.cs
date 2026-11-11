namespace Unifesspa.UniPlus.Selecao.Application.Permissoes;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Projecao;
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
    /// <summary>Manter o cadastro único de motivos de decisão, de todos os tipos.</summary>
    [Permissao(
        "selecao:motivos-de-decisao:manter",
        "Manter motivos de decisão",
        "Cria, edita, ativa e desativa os motivos institucionais de decisão de todos os tipos, da isenção da taxa à classificação.",
        ClassificacaoDeProtecao.Interno)]
    MotivosDeDecisaoManter = 1,

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
        "Alcança as inscrições dos processos seletivos da unidade proprietária, sem liberar item algum: cada dado e cada documento sai pela permissão da sua classe.",
        ClassificacaoDeProtecao.Interno,
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
        Natureza = NaturezaDoItem.Arquivo,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DocumentosPessoaisConsultar = 6,

    /// <summary>Consultar os documentos sensíveis das inscrições.</summary>
    [Permissao(
        "selecao:documentos-sensiveis:consultar",
        "Consultar documentos sensíveis das inscrições",
        "Abre os documentos sensíveis, como laudos médicos, enviados nas inscrições dos processos seletivos da unidade proprietária.",
        ClassificacaoDeProtecao.Sensivel,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 11, II, a",
        Natureza = NaturezaDoItem.Arquivo,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DocumentosSensiveisConsultar = 7,

    /// <summary>Revelar o valor completo de um dado identificador de uma inscrição.</summary>
    [Permissao(
        "selecao:dados-identificadores:revelar",
        "Revelar documento de identificação da inscrição",
        "Ver o número completo de um documento de identificação, um candidato por vez, a pedido, com registro do acesso. Exige também a permissão de ver o número mascarado.",
        ClassificacaoDeProtecao.Identificador,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DadosIdentificadoresRevelar = 8,

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

    /// <summary>Consultar os documentos de identificação das inscrições.</summary>
    [Permissao(
        "selecao:documentos-identificadores:consultar",
        "Consultar documentos de identificação das inscrições",
        "Abre os documentos com número que identifica diretamente o titular, como RG, CNH e passaporte, enviados nas inscrições dos processos seletivos da unidade proprietária.",
        ClassificacaoDeProtecao.Identificador,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        Natureza = NaturezaDoItem.Arquivo,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DocumentosIdentificadoresConsultar = 14,

    /// <summary>Consultar os documentos internos das inscrições.</summary>
    [Permissao(
        "selecao:documentos-internos:consultar",
        "Consultar documentos internos das inscrições",
        "Abre os documentos de uso interno enviados nas inscrições dos processos seletivos da unidade proprietária.",
        ClassificacaoDeProtecao.Interno,
        Natureza = NaturezaDoItem.Arquivo,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DocumentosInternosConsultar = 15,

    /// <summary>Consultar os documentos públicos das inscrições.</summary>
    [Permissao(
        "selecao:documentos-publicos:consultar",
        "Consultar documentos públicos das inscrições",
        "Abre os documentos públicos, como artigos publicados, enviados nas inscrições dos processos seletivos da unidade proprietária.",
        ClassificacaoDeProtecao.Publico,
        Natureza = NaturezaDoItem.Arquivo,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DocumentosPublicosConsultar = 16,

    /// <summary>Consultar os dados públicos das inscrições.</summary>
    [Permissao(
        "selecao:dados-publicos:consultar",
        "Consultar dados públicos das inscrições",
        "Consulta os dados públicos das inscrições dos processos seletivos da unidade proprietária, como o curso e a situação.",
        ClassificacaoDeProtecao.Publico,
        Natureza = NaturezaDoItem.Dado,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DadosPublicosConsultar = 17,

    /// <summary>Consultar os dados internos das inscrições.</summary>
    [Permissao(
        "selecao:dados-internos:consultar",
        "Consultar dados internos das inscrições",
        "Consulta os dados de uso interno das inscrições dos processos seletivos da unidade proprietária, como o número de inscrição.",
        ClassificacaoDeProtecao.Interno,
        Natureza = NaturezaDoItem.Dado,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DadosInternosConsultar = 18,

    /// <summary>Consultar os dados pessoais das inscrições.</summary>
    [Permissao(
        "selecao:dados-pessoais:consultar",
        "Consultar dados pessoais das inscrições",
        "Consulta os dados pessoais das inscrições dos processos seletivos da unidade proprietária, como o nome.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        Natureza = NaturezaDoItem.Dado,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DadosPessoaisConsultar = 19,

    /// <summary>Consultar os dados sensíveis das inscrições.</summary>
    [Permissao(
        "selecao:dados-sensiveis:consultar",
        "Consultar dados sensíveis das inscrições",
        "Consulta os dados sensíveis, como o tipo de deficiência, declarados nas inscrições dos processos seletivos da unidade proprietária.",
        ClassificacaoDeProtecao.Sensivel,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 11, II, a",
        Natureza = NaturezaDoItem.Dado,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DadosSensiveisConsultar = 20,

    /// <summary>Consultar os dados identificadores mascarados das inscrições.</summary>
    [Permissao(
        "selecao:dados-identificadores:consultar",
        "Consultar documentos de identificação mascarados das inscrições",
        "Ver os números dos documentos de identificação do candidato (CPF, RG, passaporte) mascarados.",
        ClassificacaoDeProtecao.Identificador,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        Natureza = NaturezaDoItem.Dado,
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    DadosIdentificadoresConsultar = 21,

    /// <summary>Emitir o comprovante de inscrição de um candidato.</summary>
    [Permissao(
        "selecao:inscricoes:emitir-comprovante",
        "Emitir comprovante de inscrição",
        "Emite o comprovante de uma inscrição dos processos seletivos da unidade proprietária, inclusive o da inscrição cancelada, com os dados do candidato que o documento reúne. Exige também consultar as inscrições do processo.",
        ClassificacaoDeProtecao.Sensivel,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 11, II, a",
        EscopoContextoObrigatorio = [CamposDeContexto.UnidadeProprietariaId])]
    InscricoesEmitirComprovante = 22,
}
