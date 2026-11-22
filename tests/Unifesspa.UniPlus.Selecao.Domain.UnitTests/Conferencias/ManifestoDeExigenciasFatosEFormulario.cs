namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Conferencias;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As entradas do manifesto das exigências, dos fatos coletados, do formulário e da derivação de fatos.</summary>
internal static class ManifestoDeExigenciasFatosEFormulario
{
    internal static readonly Dictionary<Type, (string[] Congeladas, (string Propriedade, string Motivo)[] Excluidas)> Entradas = new()
    {
        [typeof(ExigenciasDeclaradas)] = (["DocumentosExigidos", "RegrasDeComprovacao"], []),
        [typeof(ColetaDeclarada)] = (
            ["Campos", "GruposColetados", "Formularios", "TermosExigidos", "OpcoesDeclaradas", "RegrasDerivacao", "ReferenciaTemporalFatos"],
            [("Itens", "Projeção de Campos que exclui os campos de grupos repetíveis.")]),

        // Documentos exigidos (Story #554, PR #903) — exigencias[] real, CA-09 (exigenciaId
        // estável). Aplicabilidade entra porque é o que o resolvedor (PR #903, fora deste
        // bloco) consome para correlacionar apresentação↔exigência.
        [typeof(DocumentoExigido)] = (
            [
                "ExigidoNaFaseId", "ExigidoNaEtapaId", "Finalidade", "TipoDocumentoOrigemId", "TipoDocumentoCodigo", "TipoDocumentoNome",
                "TipoDocumentoCategoria", "ClassificacaoProtecao", "FinalidadeTratamento", "HipoteseLegal", "FatosComprovados",
                "Aplicabilidade", "Obrigatorio", "ConsequenciaIndeferimento",
                "Condicoes", "BasesLegais", "IdadeMaximaEmissao", "FormatosPermitidos", "Modelo",
                "TamanhoMaximoBytes",
            ],
            [("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2).")]),

        [typeof(CondicaoGatilho)] = (
            ["Clausula", "Fato", "Operador", "Valor"],
            [("DocumentoExigidoId", "FK interna.")]),

        [typeof(DocumentoExigidoBaseLegal)] = (
            ["Referencia", "Abrangencia", "Situacao", "Observacao"],
            [("DocumentoExigidoId", "FK interna.")]),

        // A regra de documento referencia a exigência já presente no envelope.
        [typeof(RegraDeComprovacao)] = (
            [
                "RegraQueContemId", "Ordem", "Forma", "DocumentoExigidoId", "QuantidadeMinima", "Consequencia", "Itens",
                "BasesLegais", "Distincao", "GrupoDoFormularioParaRepeticao",
            ],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("DocumentoExigido", "Navegação da regra de documento — o envelope congela DocumentoExigidoId (como `exigenciaId`, referenciando documentosExigidos.exigencias[]), não o objeto/conteúdo duplicado."),
            ]),

        [typeof(DistincaoDasApresentacoes)] = (
            ["Criterio", "DataReferencia", "OcorrenciasEsperadas"],
            [("ConferivelPelaContagem", "Derivada da chave e da ausência de ocorrências esperadas; não acrescenta conteúdo ao envelope.")]),

        [typeof(BaseLegalDaComprovacao)] = (
            ["Referencia", "Abrangencia", "Situacao", "Observacao"],
            [("RegraDeComprovacaoId", "FK interna.")]),

        [typeof(IdadeMaximaEmissao)] = (
            ["Valor", "Unidade", "ReferenciaTipo", "Data", "ReferenciaFaseId"],
            []),

        // Formatos permitidos (Story #918) — substitui o campo singular FormatoPermitido?
        // (enum, fora do alcance deste manifesto — EDoDominio exclui enums).
        [typeof(FormatosPermitidos)] = (["Qualquer", "Lista"], []),
        [typeof(ModeloDaExigencia)] = (["ModeloId", "NomeArquivo", "Formato", "HashSha256"], []),
        [typeof(FormatoPermitidoEntry)] = (["Formato", "TamanhoMaximoBytesMax"], []),

        // A POLÍTICA crua (B-03) — insumo de ProcessoSeletivo.ResolverDataReferenciaFatos.
        // A DateOnly resolvida (dataReferenciaFatos) é output derivado, não uma propriedade
        // de entidade — não passa por este manifesto (é lida diretamente do método).
        [typeof(ReferenciaTemporalFatos)] = (
            ["Tipo", "Data", "FaseId"],
            []),

        // Coleta de fatos + derivação (Story #928, §7.4) — congeladas nos blocos de topo
        // `fatosColetados`/`regrasDerivacao` e testemunhadas no grafo conjunto (`grafoDependencia`).
        // Rotulo/TipoRenderizacao/Obrigatorio (Story #559) — a apresentação do campo no
        // formulário de inscrição, congelada junto do fato no mesmo bloco `fatosColetados`.
        [typeof(FatoColetado)] = (
            ["FatoCodigo", "Finalidade", "EtapaCodigo", "Ordem", "Rotulo", "TipoRenderizacao", "Obrigatoriedade", "Ajuda", "PedirConfirmacao", "Restricoes", "Impedimento", "OrigemValores", "Formato", "ClassificacaoProtecao", "HipoteseLegal", "FinalidadeTratamento", "Descricao", "ComponenteExibicao", "Precondicoes"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("GrupoColetadoId", "FK interna do campo de grupo — o campo é congelado dentro do grupo, em `gruposColetados`."),
                ("SemPrecondicao", "Derivada de Precondicoes — congelá-la duplicaria a fonte de verdade."),
                ("FatosCitados", "Derivada das Precondicoes, da Obrigatoriedade e das Restricoes — a lista de citações é recomputada, nunca persistida."),
                ("Condicoes", "Derivada das Precondicoes, da Obrigatoriedade e das Restricoes — recomputada, nunca persistida."),
                ("OpcoesDoProcesso", "Derivada de OrigemValores — congelá-la duplicaria a fonte de verdade."),
                ("Exibicao", "Derivada das Precondicoes — o predicado é recomputado, nunca persistido."),
                ("Apresentacao", "Agrupa Rotulo, TipoRenderizacao, Ajuda e PedirConfirmacao, já congelados um a um — congelar o grupo duplicaria a fonte de verdade."),
                ("Regras", "Agrupa Precondicoes, Obrigatoriedade, Restricoes e Impedimento, já congelados um a um — congelar o grupo duplicaria a fonte de verdade."),
            ]),

        // Grupos repetíveis (UNI-REQ-0146) — congelados no bloco de topo `gruposColetados`, com os
        // campos de cada ocorrência na mesma forma dos itens de `fatosColetados`.
        [typeof(GrupoColetado)] = (
            ["Codigo", "Finalidade", "EtapaCodigo", "Ordem", "Rotulo", "Minimo", "Maximo", "IncluiCandidato", "Exibicao", "Obrigatoriedade", "Subitens"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("FatosCitados", "Derivada da exibição e da obrigatoriedade — recomputada, nunca persistida."),
                ("Condicoes", "Derivada das regras do grupo e dos campos — recomputada, nunca persistida."),
                ("Ocorrencias", "Agrupa Minimo, Maximo e IncluiCandidato, já congelados um a um — congelar o grupo duplicaria a fonte de verdade."),
                ("Regras", "Agrupa Exibicao e Obrigatoriedade, já congeladas uma a uma — congelar o grupo duplicaria a fonte de verdade."),
            ]),

        // Opções declaradas pelo processo (issue #1619) — congeladas nos valores selecionáveis do
        // fato coletado de fonte do processo, e reconstruídas deles pelo decoder.
        [typeof(OpcaoDeclaradaFato)] = (
            ["FatoCodigo", "Codigo", "Rotulo", "Ordem"],
            [("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2).")]),

        [typeof(FormularioProcesso)] = (
            ["Finalidade", "FaseId", "Titulo", "ModeloOrigemId", "ModeloOrigemCodigo", "Etapas"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("Estrutura", "Derivada das Etapas — a forma que a estrutura do formulário confere, recomputada."),
            ]),

        [typeof(EtapaFormulario)] = (
            ["Codigo", "Ordem", "Tipo", "Bloco", "Titulo", "Descricao", "Aviso", "Exibicao"],
            [
                ("FormularioProcessoId", "FK interna."),
                ("Estrutura", "Derivada dos campos da etapa — recomputada, nunca persistida."),
                ("FatosCitados", "Derivada da Exibicao — recomputada, nunca persistida."),
                ("Condicoes", "Derivada da Exibicao — recomputada, nunca persistida."),
            ]),

        [typeof(TermoExigidoFormulario)] = (
            ["Codigo", "Finalidade", "Ordem", "TermoId", "VersaoId", "TermoCodigo", "VersaoNumero", "Nome", "Texto", "BaseLegal", "FormaAceite", "HashVersao", "Exibicao", "Obrigatoriedade"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("FatosCitados", "Derivada da exibição e da obrigatoriedade — recomputada, nunca persistida."),
                ("Condicoes", "Derivada da exibição e da obrigatoriedade — recomputada, nunca persistida."),
                ("SemFormaDeAceite", "Derivada de FormaAceite — congelá-la duplicaria a fonte de verdade."),
            ]),

        [typeof(CondicaoPrecondicaoFato)] = (
            ["Clausula", "Fato", "Operador", "Valor"],
            [("FatoColetadoId", "FK interna.")]),

        [typeof(ConfiguracaoDerivacaoFato)] = (
            ["CodigoFato", "Regras"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("FatosCitados", "Derivada das condições das regras — recomputada, nunca persistida."),
                ("Booleano", "Derivada das contribuições das regras — recomputada, nunca persistida."),
                ("CodigosContribuidos", "Derivada das contribuições das regras — recomputada, nunca persistida."),
            ]),

        [typeof(RegraDerivacaoConfigurada)] = (
            ["Ordem", "Contribui", "Condicoes"],
            [("ConfiguracaoDerivacaoFatoId", "FK interna.")]),

        [typeof(CondicaoRegraDerivacao)] = (
            ["Clausula", "Fato", "Operador", "Valor"],
            [("RegraDerivacaoConfiguradaId", "FK interna.")]),
    };
}
